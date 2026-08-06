using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Operations;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeCliCommandRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_ReindexIndexesFixtureKnowledge()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteLinkedFixtureAsync(root);
        var runner = CreateRunner(root, out _);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["reindex"], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Reindexed 2 knowledge nodes.", output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ValidateReportsNodesAndEdges()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteLinkedFixtureAsync(root);
        var runner = CreateRunner(root, out _);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["validate"], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Validated 2 nodes and 1 edges.", output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ValidateFailsOnLegacyRelatesTo()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: domain_index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            type: project_index
            scope: project
            project: Acme.Api
            links:
              - type: belongs_to_domain
                target: domains/api/index
              - type: relates_to
                target: domains/api/index
            ---
            # Inventory API
            """);
        var runner = CreateRunner(root, out _);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["validate"], output);

        Assert.Equal(1, exitCode);
        Assert.Contains("Legacy or non-preferred relation type 'relates_to'", output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_DumpGraphPrintsEdgesFromIndex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteLinkedFixtureAsync(root);
        var runner = CreateRunner(root, out _);
        using var reindexOutput = new StringWriter();
        await runner.ExecuteAsync(["reindex"], reindexOutput);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["dump-graph"], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("projects/Acme.Api/index --BelongsToDomain--> domains/api/index", output.ToString());
        Assert.Contains("Dumped 1 knowledge edges.", output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_CheckOrphansPrintsUnlinkedNodes()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        var runner = CreateRunner(root, out _);
        using var reindexOutput = new StringWriter();
        await runner.ExecuteAsync(["reindex"], reindexOutput);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["check-orphans"], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("domains/api/index | API", output.ToString());
        Assert.Contains("projects/Acme.Api/index | Inventory API", output.ToString());
        Assert.Contains("Found 2 orphan knowledge nodes.", output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommandReturnsError()
    {
        var root = CreateTempKnowledgeRoot();
        var runner = CreateRunner(root, out _);
        using var output = new StringWriter();

        var exitCode = await runner.ExecuteAsync(["unknown"], output);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown command 'unknown'", output.ToString());
    }

    private static KnowledgeCliCommandRunner CreateRunner(string root, out string databasePath)
    {
        databasePath = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge.db");
        var databaseOptions = new KnowledgeDatabaseOptions
        {
            Path = databasePath
        };
        var connectionFactory = new KnowledgeDatabaseConnectionFactory(databaseOptions);
        var knowledgeRootOptions = new KnowledgeRootOptions { Path = root };
        var indexer = new KnowledgeIndexer();
        var markdownReader = new KnowledgeMarkdownReader();
        var admin = new AdminKnowledgeMaintenanceService(
            connectionFactory,
            databaseOptions,
            knowledgeRootOptions,
            indexer,
            markdownReader,
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());

        return new KnowledgeCliCommandRunner(
            connectionFactory,
            knowledgeRootOptions,
            indexer,
            admin);
    }

    private static async Task WriteLinkedFixtureAsync(string root)
    {
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: domain_index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            type: project_index
            scope: project
            project: Acme.Api
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Inventory API
            """);
    }

    private static Task WriteRequiredStructureAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        return Task.CompletedTask;
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
