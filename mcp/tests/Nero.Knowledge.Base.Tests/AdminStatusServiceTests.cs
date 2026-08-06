using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class AdminStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsCleanRepositoryStatusWithExistingIndex()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = Path.Combine(repositoryRoot, "knowledge");
        Directory.CreateDirectory(knowledgeRoot);
        await WriteMarkdownAsync(knowledgeRoot, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var databasePath = Path.Combine(repositoryRoot, "mcp", "data", "knowledge.db");
        var connectionFactory = new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions
        {
            Path = databasePath
        });
        await using (var connection = connectionFactory.CreateConnection())
        {
            await new KnowledgeIndexer().ReindexAsync(connection, knowledgeRoot);
        }

        var service = new AdminStatusService(
            new KnowledgeDatabaseOptions { Path = databasePath },
            new KnowledgeRootOptions { Path = knowledgeRoot },
            new KnowledgeWriteOptions { Mode = "read_only" },
            new FakeGitCommandRunner("main", ""));

        var result = await service.GetStatusAsync();

        Assert.Equal("nero-knowledge-base", result.Server);
        Assert.Equal(repositoryRoot, result.RepositoryRoot);
        Assert.Equal("main", result.Branch);
        Assert.False(result.HasModifiedFiles);
        Assert.Empty(result.ModifiedFiles);
        Assert.True(result.IndexDatabaseExists);
        Assert.Equal(databasePath, result.IndexDatabasePath);
        Assert.NotNull(result.LastIndexedUtc);
        Assert.Equal("read_only", result.WriteMode);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsModifiedFilesAndMissingIndex()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var knowledgeRoot = Path.Combine(repositoryRoot, "knowledge");
        Directory.CreateDirectory(knowledgeRoot);
        var databasePath = Path.Combine(repositoryRoot, "mcp", "data", "missing.db");

        var service = new AdminStatusService(
            new KnowledgeDatabaseOptions { Path = databasePath },
            new KnowledgeRootOptions { Path = knowledgeRoot },
            new KnowledgeWriteOptions { Mode = "draft" },
            new FakeGitCommandRunner(
                "feature/admin-status",
                """
                 M docs/mcp-backlog.md
                """));

        var result = await service.GetStatusAsync();

        Assert.Equal("feature/admin-status", result.Branch);
        Assert.True(result.HasModifiedFiles);
        Assert.Equal(
            ["docs/mcp-backlog.md"],
            result.ModifiedFiles);
        Assert.False(result.IndexDatabaseExists);
        Assert.Null(result.LastIndexedUtc);
        Assert.Equal("draft", result.WriteMode);
    }

    private static string CreateTempRepositoryRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return path;
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private sealed class FakeGitCommandRunner(string branchOutput, string statusOutput) : IGitCommandRunner
    {
        public Task<GitCommandResult> RunAsync(
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null)
        {
            var output = arguments.SequenceEqual(["branch", "--show-current"])
                ? branchOutput
                : statusOutput;

            return Task.FromResult(new GitCommandResult(0, output, ""));
        }
    }
}
