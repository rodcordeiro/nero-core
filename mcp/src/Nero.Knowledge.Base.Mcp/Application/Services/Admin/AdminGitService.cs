using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed class AdminGitService(
    KnowledgeRootOptions knowledgeRootOptions,
    IGitCommandRunner gitCommandRunner,
    KnowledgeWriteOptions writeOptions)
{
    public async Task<AdminGitStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var remote = await GetPreferredRemoteAsync(repositoryRoot, cancellationToken);
        var upstream = await GetOptionalOutputAsync(repositoryRoot, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], cancellationToken);
        var aheadBehind = upstream is null
            ? (Ahead: (int?)null, Behind: (int?)null)
            : await GetAheadBehindAsync(repositoryRoot, cancellationToken);
        var modifiedFiles = await GetModifiedFilesAsync(repositoryRoot, cancellationToken);

        return new AdminGitStatusResult
        {
            RepositoryRoot = repositoryRoot,
            Branch = await GetOptionalOutputAsync(repositoryRoot, ["branch", "--show-current"], cancellationToken),
            HasRemote = remote is not null,
            Remote = remote,
            Upstream = upstream,
            Ahead = aheadBehind.Ahead,
            Behind = aheadBehind.Behind,
            LocalHead = await GetOptionalOutputAsync(repositoryRoot, ["rev-parse", "HEAD"], cancellationToken),
            RemoteHead = upstream is null
                ? null
                : await GetOptionalOutputAsync(repositoryRoot, ["rev-parse", "@{u}"], cancellationToken),
            HasModifiedFiles = modifiedFiles.Count > 0,
            ModifiedFiles = modifiedFiles
        };
    }

    public async Task<AdminGitFetchResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var remote = await GetPreferredRemoteAsync(repositoryRoot, cancellationToken);
        if (remote is null)
        {
            return new AdminGitFetchResult
            {
                Success = false,
                RepositoryRoot = repositoryRoot,
                Remote = null,
                Message = "Git fetch blocked because no remote is configured."
            };
        }

        var result = await RunSafeAsync(
            repositoryRoot,
            ["fetch", "--prune", remote],
            cancellationToken,
            timeout: TimeSpan.FromSeconds(25));

        return new AdminGitFetchResult
        {
            Success = result.ExitCode == 0,
            RepositoryRoot = repositoryRoot,
            Remote = remote,
            Message = result.ExitCode == 0
                ? "Git fetch completed without merge."
                : "Git fetch failed.",
            Output = SanitizeOptional(result.Output),
            Error = SanitizeOptional(result.Error)
        };
    }

    public async Task<AdminGitPullResult> PullAsync(
        string? remote = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        GitSyncSecurity.EnsureNotReadOnly(writeOptions);
        GitSyncSecurity.RejectSecretParamNames(BuildOptionalParamBag(remote: remote, branch: branch));
        var repositoryRoot = ResolveRepositoryRoot();

        var dirty = await GetPorcelainStatusAsync(repositoryRoot, includeUntracked: true, cancellationToken);
        if (dirty.Count > 0)
        {
            throw GitSyncSecurity.CreateSecurityException(
                "Git pull blocked because the worktree has local modifications or untracked files. Commit, stash or clean before pull.");
        }

        var resolvedRemote = string.IsNullOrWhiteSpace(remote)
            ? await GetPreferredRemoteAsync(repositoryRoot, cancellationToken)
            : remote.Trim();
        if (string.IsNullOrWhiteSpace(resolvedRemote))
        {
            return new AdminGitPullResult
            {
                Success = false,
                RepositoryRoot = repositoryRoot,
                Remote = null,
                Branch = null,
                Message = "Git pull blocked because no remote is configured."
            };
        }

        GitSyncSecurity.EnsureSafeRefName(resolvedRemote, "remote");

        var resolvedBranch = string.IsNullOrWhiteSpace(branch)
            ? await GetOptionalOutputAsync(repositoryRoot, ["branch", "--show-current"], cancellationToken)
            : branch.Trim();
        if (string.IsNullOrWhiteSpace(resolvedBranch))
        {
            return new AdminGitPullResult
            {
                Success = false,
                RepositoryRoot = repositoryRoot,
                Remote = resolvedRemote,
                Branch = null,
                Message = "Git pull blocked because the current branch could not be resolved."
            };
        }

        GitSyncSecurity.EnsureSafeRefName(resolvedBranch, "branch");

        var result = await RunSafeAsync(
            repositoryRoot,
            ["pull", "--ff-only", resolvedRemote, resolvedBranch],
            cancellationToken,
            timeout: TimeSpan.FromSeconds(60));

        var diverged = result.ExitCode != 0
            && (ContainsIgnoreCase(result.Error, "Not possible to fast-forward")
                || ContainsIgnoreCase(result.Error, "diverged")
                || ContainsIgnoreCase(result.Output, "diverged")
                || ContainsIgnoreCase(result.Error, "fatal: Not possible to fast-forward"));

        return new AdminGitPullResult
        {
            Success = result.ExitCode == 0,
            RepositoryRoot = repositoryRoot,
            Remote = resolvedRemote,
            Branch = resolvedBranch,
            Message = result.ExitCode == 0
                ? "Git pull completed with fast-forward only."
                : diverged
                    ? "Git pull failed because local and remote have diverged; MCP does not merge or rebase."
                    : "Git pull failed.",
            Output = SanitizeOptional(result.Output),
            Error = SanitizeOptional(result.Error)
        };
    }

    public async Task<AdminGitCreateCommitResult> CreateCommitAsync(
        string message,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        GitSyncSecurity.EnsureNotReadOnly(writeOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        GitSyncSecurity.RejectSecretParamNames(BuildOptionalParamBag(message: message));

        var normalizedPaths = GitSyncSecurity.NormalizeAndValidateAllowlistedPaths(paths);
        var repositoryRoot = ResolveRepositoryRoot();

        var stagedBefore = await GetCachedNameOnlyAsync(repositoryRoot, cancellationToken);
        if (stagedBefore.Count > 0)
        {
            throw GitSyncSecurity.CreateSecurityException(
                "Git commit blocked because the index already has staged files. Unstage them first so MCP can stage exactly paths[].");
        }

        var stagedByUs = false;
        try
        {
            var addArguments = new List<string> { "add", "--" };
            addArguments.AddRange(normalizedPaths);
            var addResult = await RunSafeAsync(repositoryRoot, addArguments, cancellationToken);
            if (addResult.ExitCode != 0)
            {
                throw GitSyncSecurity.CreateSecurityException(
                    "Git add failed while staging allowlisted paths for commit.");
            }

            stagedByUs = true;

            var stagedAfter = await GetCachedNameOnlyAsync(repositoryRoot, cancellationToken);
            if (!GitSyncSecurity.PathSetsEqual(stagedAfter, normalizedPaths))
            {
                throw GitSyncSecurity.CreateSecurityException(
                    "Git commit aborted because the staged set did not exactly match paths[].");
            }

            var diffResult = await RunSafeAsync(
                repositoryRoot,
                ["diff", "--cached"],
                cancellationToken);
            if (diffResult.ExitCode != 0)
            {
                throw GitSyncSecurity.CreateSecurityException("Git diff --cached failed before commit.");
            }

            var hits = ComplianceScanner.Scan(diffResult.Output, "stagedDiff");
            if (hits.Count > 0)
            {
                throw new ComplianceViolationException("stagedDiff", hits[0].RuleId);
            }

            var commitResult = await RunSafeAsync(
                repositoryRoot,
                ["commit", "-m", message],
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30));
            if (commitResult.ExitCode != 0)
            {
                throw GitSyncSecurity.CreateSecurityException("Git commit failed.");
            }

            stagedByUs = false;
            var commitSha = await GetOptionalOutputAsync(repositoryRoot, ["rev-parse", "HEAD"], cancellationToken);

            return new AdminGitCreateCommitResult
            {
                Success = true,
                RepositoryRoot = repositoryRoot,
                CommitSha = commitSha,
                Paths = normalizedPaths,
                Message = "Git commit created for allowlisted paths.",
                Output = SanitizeOptional(commitResult.Output),
                Error = SanitizeOptional(commitResult.Error)
            };
        }
        catch
        {
            if (stagedByUs)
            {
                await TryUnstageAsync(repositoryRoot, normalizedPaths, cancellationToken);
            }

            throw;
        }
    }

    public async Task<AdminGitPushResult> PushAsync(
        bool confirm,
        string? confirmPhrase,
        string? remote = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        GitSyncSecurity.EnsureNotReadOnly(writeOptions);
        GitSyncSecurity.RejectSecretParamNames(
            BuildOptionalParamBag(remote: remote, branch: branch, confirmPhrase: confirmPhrase));
        var repositoryRoot = ResolveRepositoryRoot();

        var resolvedRemote = string.IsNullOrWhiteSpace(remote)
            ? await GetPreferredRemoteAsync(repositoryRoot, cancellationToken)
            : remote.Trim();
        if (string.IsNullOrWhiteSpace(resolvedRemote))
        {
            return new AdminGitPushResult
            {
                Success = false,
                RepositoryRoot = repositoryRoot,
                Remote = null,
                Branch = null,
                Message = "Git push blocked because no remote is configured."
            };
        }

        GitSyncSecurity.EnsureSafeRefName(resolvedRemote, "remote");

        var resolvedBranch = string.IsNullOrWhiteSpace(branch)
            ? await GetOptionalOutputAsync(repositoryRoot, ["branch", "--show-current"], cancellationToken)
            : branch.Trim();
        if (string.IsNullOrWhiteSpace(resolvedBranch))
        {
            return new AdminGitPushResult
            {
                Success = false,
                RepositoryRoot = repositoryRoot,
                Remote = resolvedRemote,
                Branch = null,
                Message = "Git push blocked because the current branch could not be resolved."
            };
        }

        GitSyncSecurity.EnsureSafeRefName(resolvedBranch, "branch");
        GitSyncSecurity.EnsurePushConfirmation(confirm, confirmPhrase, resolvedRemote, resolvedBranch);

        var result = await RunSafeAsync(
            repositoryRoot,
            ["push", resolvedRemote, resolvedBranch],
            cancellationToken,
            timeout: TimeSpan.FromSeconds(60));

        return new AdminGitPushResult
        {
            Success = result.ExitCode == 0,
            RepositoryRoot = repositoryRoot,
            Remote = resolvedRemote,
            Branch = resolvedBranch,
            Message = result.ExitCode == 0
                ? "Git push completed without force."
                : "Git push failed.",
            Output = SanitizeOptional(result.Output),
            Error = SanitizeOptional(result.Error)
        };
    }

    private async Task<GitCommandResult> RunSafeAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        GitSyncSecurity.EnsureSafeGitArguments(arguments);
        return await gitCommandRunner.RunAsync(repositoryRoot, arguments, cancellationToken, timeout);
    }

    private async Task TryUnstageAsync(
        string repositoryRoot,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        try
        {
            var resetArguments = new List<string> { "reset", "--" };
            resetArguments.AddRange(paths);
            await RunSafeAsync(repositoryRoot, resetArguments, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup after a failed commit attempt.
        }
    }

    private async Task<IReadOnlyList<string>> GetCachedNameOnlyAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await RunSafeAsync(
            repositoryRoot,
            ["diff", "--cached", "--name-only"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw GitSyncSecurity.CreateSecurityException(
                "Git diff --cached --name-only failed while validating the index.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Replace('\\', '/'))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetPorcelainStatusAsync(
        string repositoryRoot,
        bool includeUntracked,
        CancellationToken cancellationToken)
    {
        var arguments = includeUntracked
            ? new[] { "status", "--porcelain" }
            : new[] { "status", "--porcelain", "--untracked-files=no" };
        var result = await RunSafeAsync(repositoryRoot, arguments, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim().Replace('\\', '/') : line.Trim())
            .ToList();
    }

    private async Task<string?> GetPreferredRemoteAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var result = await RunSafeAsync(repositoryRoot, ["remote"], cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var remotes = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return remotes.Contains("origin", StringComparer.OrdinalIgnoreCase)
            ? "origin"
            : remotes.FirstOrDefault();
    }

    private async Task<(int? Ahead, int? Behind)> GetAheadBehindAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await RunSafeAsync(
            repositoryRoot,
            ["rev-list", "--left-right", "--count", "HEAD...@{u}"],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            return (null, null);
        }

        var parts = result.Output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && int.TryParse(parts[0], out var ahead)
            && int.TryParse(parts[1], out var behind)
            ? (ahead, behind)
            : (null, null);
    }

    private async Task<IReadOnlyList<string>> GetModifiedFilesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        return await GetPorcelainStatusAsync(repositoryRoot, includeUntracked: false, cancellationToken);
    }

    private async Task<string?> GetOptionalOutputAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunSafeAsync(repositoryRoot, arguments, cancellationToken);
        return result.ExitCode == 0
            ? NullIfWhiteSpace(result.Output.Trim())
            : null;
    }

    private string ResolveRepositoryRoot()
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        return FindRepositoryRoot(knowledgeRootPath) ?? FindRepositoryRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : Directory.GetParent(startPath);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? SanitizeOptional(string? value)
    {
        return NullIfWhiteSpace(GitSyncSecurity.SanitizeGitText(value));
    }

    private static IReadOnlyDictionary<string, string?> BuildOptionalParamBag(
        string? remote = null,
        string? branch = null,
        string? confirmPhrase = null,
        string? message = null)
    {
        var bag = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (remote is not null) bag["remote"] = remote;
        if (branch is not null) bag["branch"] = branch;
        if (confirmPhrase is not null) bag["confirmPhrase"] = confirmPhrase;
        if (message is not null) bag["message"] = message;
        return bag;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ContainsIgnoreCase(string? text, string value) =>
        !string.IsNullOrEmpty(text)
        && text.Contains(value, StringComparison.OrdinalIgnoreCase);
}
