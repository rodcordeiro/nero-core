using System.Text.RegularExpressions;
using Nero.Knowledge.Base.Mcp.Application.Services.Security;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

/// <summary>
/// Hard gates for controlled Git sync (Marco 25): allowlist, forbidden args, read_only, push phrase, URL sanitization.
/// </summary>
public static partial class GitSyncSecurity
{
    /// <summary>
    /// Paths relative to the Knowledge Repo root (external to Nero Core).
    /// </summary>
    public static readonly string[] AllowedPathPrefixes =
    [
        "global/",
        "domains/",
        "projects/",
        "data/"
    ];

    private static readonly HashSet<string> ForbiddenArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "--force",
        "-f",
        "--force-with-lease",
        "--no-verify",
        "--no-gpg-sign",
        "--amend",
        "rebase",
        "--rebase"
    };

    private static readonly HashSet<string> ForbiddenSecretParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "password",
        "pat",
        "accesstoken",
        "access_token",
        "authorization",
        "passwd",
        "secret",
        "apikey",
        "api_key"
    };

    public static void EnsureNotReadOnly(KnowledgeWriteOptions writeOptions)
    {
        ArgumentNullException.ThrowIfNull(writeOptions);
        if (IsReadOnly(writeOptions.Mode))
        {
            throw new InvalidOperationException(
                "Git pull/commit/push is blocked because KnowledgeWrite__Mode is read_only. Only status/fetch remain available.");
        }
    }

    public static bool IsReadOnly(string? mode)
    {
        var normalized = (mode ?? "direct").Replace("-", "_", StringComparison.Ordinal).Trim();
        return normalized.Equals("read_only", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("readonly", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> NormalizeAndValidateAllowlistedPaths(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            throw new ArgumentException("paths is required and must contain at least one path.", nameof(paths));
        }

        var normalized = new List<string>(paths.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw CreateSecurityException("Git sync path cannot be empty.");
            }

            var path = raw.Trim().Replace('\\', '/');
            while (path.StartsWith("./", StringComparison.Ordinal))
            {
                path = path[2..];
            }

            if (path.Length == 0)
            {
                throw CreateSecurityException("Git sync path cannot be empty.");
            }

            if (IsAbsoluteOrDrivePath(path) || IsAbsoluteOrDrivePath(raw.Trim()))
            {
                throw CreateSecurityException("Git sync rejects absolute paths; use repo-relative paths only.");
            }

            var segments = path.Split('/', StringSplitOptions.None);
            if (segments.Any(segment => segment is "" or "." or ".."))
            {
                throw CreateSecurityException("Git sync rejects path traversal and empty path segments.");
            }

            if (!IsAllowlisted(path))
            {
                throw CreateSecurityException(
                    $"Git sync path is outside the allowlist ({string.Join(", ", AllowedPathPrefixes)}).");
            }

            if (seen.Add(path))
            {
                normalized.Add(path);
            }
        }

        return normalized;
    }

    public static bool IsAllowlisted(string normalizedForwardSlashPath)
    {
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (normalizedForwardSlashPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefixWithoutSlash = prefix.TrimEnd('/');
            if (normalizedForwardSlashPath.Equals(prefixWithoutSlash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void EnsureSafeGitArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (string.IsNullOrEmpty(argument))
            {
                continue;
            }

            if (ForbiddenArguments.Contains(argument)
                || argument.StartsWith("--force", StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith("--rebase", StringComparison.OrdinalIgnoreCase))
            {
                throw CreateSecurityException("Forbidden git argument rejected (force/rebase/hooks-bypass/amend are hard-denied).");
            }

            if (argument.Equals("rebase", StringComparison.OrdinalIgnoreCase)
                || (i > 0
                    && arguments[i - 1].Equals("commit", StringComparison.OrdinalIgnoreCase)
                    && argument.Equals("--amend", StringComparison.OrdinalIgnoreCase)))
            {
                throw CreateSecurityException("Forbidden git argument rejected (force/rebase/hooks-bypass/amend are hard-denied).");
            }
        }
    }

    public static void RejectSecretParamNames(IReadOnlyDictionary<string, string?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return;
        }

        foreach (var key in parameters.Keys)
        {
            if (ForbiddenSecretParamNames.Contains(key))
            {
                throw CreateSecurityException(
                    "Git sync rejects credential parameters; use environment/SSH credentials only.");
            }
        }
    }

    public static void EnsurePushConfirmation(bool confirm, string? confirmPhrase, string remote, string branch)
    {
        if (!confirm)
        {
            throw CreateSecurityException(
                "Git push requires confirm: true and confirmPhrase matching the resolved target.");
        }

        var expected = BuildPushConfirmPhrase(remote, branch);
        if (!string.Equals(confirmPhrase, expected, StringComparison.Ordinal))
        {
            throw CreateSecurityException(
                $"Git push confirmPhrase must be exactly '{expected}' (uppercase PUSH and resolved remote/branch).");
        }
    }

    public static string BuildPushConfirmPhrase(string remote, string branch) =>
        $"PUSH {remote} {branch}";

    public static void EnsureSafeRefName(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        var trimmed = value.Trim();
        // Deny force-refspec (+branch), retarget (src:dst), and other meta chars.
        // Allow only simple remote/branch names: letters, digits, ._/- (no leading - or +).
        if (!SafeRefNameRegex().IsMatch(trimmed)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || ForbiddenArguments.Contains(trimmed))
        {
            throw CreateSecurityException(
                $"Invalid git {fieldName} rejected (force-refspec, retarget, or unsafe characters are hard-denied).");
        }
    }

    public static string? SanitizeGitText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Match userinfo with or without password: scheme://user:pass@host or scheme://TOKEN@host
        return UrlUserInfoRegex().Replace(text, "${scheme}://[REDACTED]@${host}");
    }

    public static bool PathSetsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftSet = left.Select(NormalizeComparePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var rightSet = right.Select(NormalizeComparePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var i = 0; i < leftSet.Length; i++)
        {
            if (!string.Equals(leftSet[i], rightSet[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static InvalidOperationException CreateSecurityException(string message)
    {
        var exception = new InvalidOperationException(message);
        exception.Data[KnowledgePathSecurity.CategoryDataKey] = KnowledgePathSecurity.CategoryName;
        return exception;
    }

    private static string NormalizeComparePath(string path) =>
        path.Trim().Replace('\\', '/');

    private static bool IsAbsoluteOrDrivePath(string path)
    {
        if (path.StartsWith('/') || path.StartsWith('\\'))
        {
            return true;
        }

        return path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
    }

    [GeneratedRegex(
        @"^[A-Za-z0-9][A-Za-z0-9._/-]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SafeRefNameRegex();

    [GeneratedRegex(
        @"(?<scheme>[a-zA-Z][a-zA-Z0-9+.-]*)://[^/\s@]+@(?<host>[^/\s]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UrlUserInfoRegex();
}
