namespace Nero.Knowledge.Base.Mcp.Application.Services.Security;

/// <summary>
/// Fail-closed path boundary checks for symlink/junction/reparse points (Marco 24 P1).
/// </summary>
public static class KnowledgePathSecurity
{
    public const string CategoryName = "Security";
    public const string CategoryDataKey = "NeroKnowledgeFailureCategory";

    /// <summary>
    /// Ensures every path segment from root to target has no reparse point / symlink / junction.
    /// Also ensures the final path stays under the knowledge root.
    /// </summary>
    public static void EnsureNoReparsePointsUnderRoot(string knowledgeRootPath, string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        var resolvedRoot = Path.GetFullPath(knowledgeRootPath);
        var resolvedPath = Path.GetFullPath(fullPath);
        var rootPrefix = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedPath, resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateSecurityException("Resolved knowledge path escapes the knowledge root.");
        }

        // Walk from root down to the leaf so intermediate junctions are caught.
        var relative = Path.GetRelativePath(resolvedRoot, resolvedPath);
        if (relative is "." or "")
        {
            EnsureEntryIsNotReparsePoint(resolvedRoot);
            return;
        }

        var current = resolvedRoot;
        EnsureEntryIsNotReparsePoint(current);
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Path.Exists(current))
            {
                // Parent chain for create: only existing prefixes must be clean.
                continue;
            }

            EnsureEntryIsNotReparsePoint(current);
        }
    }

    public static bool IsReparsePoint(string path)
    {
        if (!Path.Exists(path))
        {
            return false;
        }

        try
        {
            var attrs = File.GetAttributes(path);
            return attrs.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            // Fail closed: unreadable attribute is treated as unsafe boundary.
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void EnsureEntryIsNotReparsePoint(string path)
    {
        if (IsReparsePoint(path))
        {
            throw CreateSecurityException(
                "Knowledge path contains a symlink, junction or reparse point. Use a real path under the knowledge root.");
        }
    }

    public static InvalidOperationException CreateSecurityException(string message)
    {
        var exception = new InvalidOperationException(message);
        exception.Data[CategoryDataKey] = CategoryName;
        return exception;
    }
}
