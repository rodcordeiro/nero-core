using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Security;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Tests;

public class AdminGitServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsRemotePendingCountsWithoutFetching()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["remote"] = new(0, "origin\n", ""),
            ["rev-parse --abbrev-ref --symbolic-full-name @{u}"] = new(0, "origin/main\n", ""),
            ["rev-list --left-right --count HEAD...@{u}"] = new(0, "2\t3\n", ""),
            ["status --porcelain --untracked-files=no"] = new(0, " M global/index.md\n", ""),
            ["branch --show-current"] = new(0, "main\n", ""),
            ["rev-parse HEAD"] = new(0, "local-sha\n", ""),
            ["rev-parse @{u}"] = new(0, "remote-sha\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.GetStatusAsync();

        Assert.True(result.HasRemote);
        Assert.Equal("origin", result.Remote);
        Assert.Equal("origin/main", result.Upstream);
        Assert.Equal(2, result.Ahead);
        Assert.Equal(3, result.Behind);
        Assert.Equal("local-sha", result.LocalHead);
        Assert.Equal("remote-sha", result.RemoteHead);
        Assert.True(result.HasModifiedFiles);
        Assert.Equal(["global/index.md"], result.ModifiedFiles);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("fetch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchAsync_BlocksWhenRepositoryHasNoRemote()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["remote"] = new(0, "", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.FetchAsync();

        Assert.False(result.Success);
        Assert.Null(result.Remote);
        Assert.Equal("Git fetch blocked because no remote is configured.", result.Message);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("fetch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchAsync_RunsFetchPruneForConfiguredRemote()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["remote"] = new(0, "origin\nbackup\n", ""),
            ["fetch --prune origin"] = new(0, "fetch output\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.FetchAsync();

        Assert.True(result.Success);
        Assert.Equal("origin", result.Remote);
        Assert.Equal("Git fetch completed without merge.", result.Message);
        Assert.Equal("fetch output\n", result.Output);
        Assert.Contains("fetch --prune origin", runner.Commands);
    }

    [Fact]
    public async Task PullAsync_UsesFfOnlyAndBlocksOnDirtyWorktree()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["status --porcelain"] = new(0, "?? secrets.env\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PullAsync());

        Assert.Equal(KnowledgePathSecurity.CategoryName, exception.Data[KnowledgePathSecurity.CategoryDataKey]);
        Assert.Contains("local modifications", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAsync_RunsPullFfOnlyForResolvedRemoteAndBranch()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["status --porcelain"] = new(0, "", ""),
            ["remote"] = new(0, "origin\n", ""),
            ["branch --show-current"] = new(0, "main\n", ""),
            ["pull --ff-only origin main"] = new(0, "Already up to date.\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.PullAsync();

        Assert.True(result.Success);
        Assert.Equal("origin", result.Remote);
        Assert.Equal("main", result.Branch);
        Assert.Contains("pull --ff-only origin main", runner.Commands);
        Assert.DoesNotContain(runner.Commands, command => command.Contains("--force", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Commands, command => command.Contains("rebase", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(runner.Commands, command => command.Contains("merge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PullAsync_BlocksWhenReadOnly()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>());
        var service = CreateService(knowledgeRoot, runner, new KnowledgeWriteOptions { Mode = "read_only" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PullAsync());

        Assert.Contains("read_only", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task CreateCommitAsync_RejectsPathOutsideAllowlist()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>());
        var service = CreateService(knowledgeRoot, runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCommitAsync("msg", ["mcp/src/Program.cs"]));

        Assert.Equal(KnowledgePathSecurity.CategoryName, exception.Data[KnowledgePathSecurity.CategoryDataKey]);
        Assert.Contains("allowlist", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task CreateCommitAsync_RejectsDirtyIndex()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["diff --cached --name-only"] = new(0, "domains/other.md\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCommitAsync("msg", ["global/index.md"]));

        Assert.Contains("index already has staged files", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("add ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCommitAsync_RejectsComplianceHitOnStagedDiffAndUnstages()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var path = "global/index.md";
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["diff --cached --name-only"] = new(0, "", ""),
            [$"add -- {path}"] = new(0, "", ""),
            ["diff --cached"] = new(0, "+Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.aaa.bbb\n", ""),
            [$"reset -- {path}"] = new(0, "", "")
        });
        // After add, name-only must return the staged path once.
        runner.SetSequence("diff --cached --name-only",
            new GitCommandResult(0, "", ""),
            new GitCommandResult(0, $"{path}\n", ""));
        var service = CreateService(knowledgeRoot, runner);

        var exception = await Assert.ThrowsAsync<ComplianceViolationException>(() =>
            service.CreateCommitAsync("msg", [path]));

        Assert.Equal("stagedDiff", exception.ParamName);
        Assert.Equal(ComplianceViolationException.CategoryName, exception.Data[ComplianceViolationException.CategoryDataKey]);
        Assert.Contains($"reset -- {path}", runner.Commands);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("commit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCommitAsync_CommitsAllowlistedPathsWhenClean()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var path = "global/index.md";
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            [$"add -- {path}"] = new(0, "", ""),
            ["diff --cached"] = new(0, "+# Global note without secrets\n", ""),
            ["commit -m safe commit"] = new(0, "[main abc123] safe commit\n", ""),
            ["rev-parse HEAD"] = new(0, "abc123\n", "")
        });
        runner.SetSequence("diff --cached --name-only",
            new GitCommandResult(0, "", ""),
            new GitCommandResult(0, $"{path}\n", ""));
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.CreateCommitAsync("safe commit", [path]);

        Assert.True(result.Success);
        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal([path], result.Paths);
        Assert.Contains("commit -m safe commit", runner.Commands);
        Assert.DoesNotContain(runner.Commands, command => command.Contains("--no-verify", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Commands, command => command.Contains("--amend", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Commands, command => command.Contains("--force", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCommitAsync_BlocksWhenReadOnly()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>());
        var service = CreateService(knowledgeRoot, runner, new KnowledgeWriteOptions { Mode = "read_only" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCommitAsync("msg", ["docs/a.md"]));

        Assert.Contains("read_only", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task PushAsync_RejectsWithoutConfirmOrWrongPhrase()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["remote"] = new(0, "origin\n", ""),
            ["branch --show-current"] = new(0, "main\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var missingConfirm = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PushAsync(confirm: false, confirmPhrase: "PUSH origin main"));
        Assert.Contains("confirm: true", missingConfirm.Message, StringComparison.OrdinalIgnoreCase);

        var wrongPhrase = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PushAsync(confirm: true, confirmPhrase: "push origin main"));
        Assert.Contains("confirmPhrase", wrongPhrase.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Commands, command => command.StartsWith("push", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PushAsync_PushesWithoutForceWhenPhraseMatches()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>
        {
            ["remote"] = new(0, "origin\n", ""),
            ["branch --show-current"] = new(0, "main\n", ""),
            ["push origin main"] = new(0, "ok\n", "")
        });
        var service = CreateService(knowledgeRoot, runner);

        var result = await service.PushAsync(confirm: true, confirmPhrase: "PUSH origin main");

        Assert.True(result.Success);
        Assert.Equal("origin", result.Remote);
        Assert.Equal("main", result.Branch);
        Assert.Contains("push origin main", runner.Commands);
        Assert.DoesNotContain(runner.Commands, command => command.Contains("--force", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Commands, command => command.Contains("-f", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PushAsync_BlocksWhenReadOnly()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = CreateKnowledgeRoot(repositoryRoot);
        var runner = new FakeGitCommandRunner(new Dictionary<string, GitCommandResult>());
        var service = CreateService(knowledgeRoot, runner, new KnowledgeWriteOptions { Mode = "read_only" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PushAsync(true, "PUSH origin main"));

        Assert.Contains("read_only", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runner.Commands);
    }

    private static AdminGitService CreateService(
        string knowledgeRoot,
        FakeGitCommandRunner runner,
        KnowledgeWriteOptions? writeOptions = null)
    {
        return new AdminGitService(
            new KnowledgeRootOptions { Path = knowledgeRoot },
            runner,
            writeOptions ?? new KnowledgeWriteOptions { Mode = "direct" });
    }

    private static string CreateTempRepositoryRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return path;
    }

    private static string CreateKnowledgeRoot(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeGitCommandRunner(IReadOnlyDictionary<string, GitCommandResult> results) : IGitCommandRunner
    {
        private readonly Dictionary<string, Queue<GitCommandResult>> sequences = new(StringComparer.Ordinal);

        public List<string> Commands { get; } = [];

        public void SetSequence(string command, params GitCommandResult[] sequence)
        {
            sequences[command] = new Queue<GitCommandResult>(sequence);
        }

        public Task<GitCommandResult> RunAsync(
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null)
        {
            var command = string.Join(' ', arguments);
            Commands.Add(command);

            if (sequences.TryGetValue(command, out var queue) && queue.Count > 0)
            {
                return Task.FromResult(queue.Dequeue());
            }

            return Task.FromResult(results.TryGetValue(command, out var result)
                ? result
                : new GitCommandResult(1, "", $"Missing fake result for {command}"));
        }
    }
}
