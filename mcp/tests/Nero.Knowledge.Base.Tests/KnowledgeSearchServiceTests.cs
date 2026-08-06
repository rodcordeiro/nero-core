using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Search;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Search;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsResultsOrderedByFtsRank()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/mobile/patterns.md", "# Padroes Mobile\n\noffline offline sqlite");
        await WriteMarkdownAsync(root, "global/patterns.md", "# Padroes Globais\n\noffline");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new KnowledgeSearchService().SearchAsync(connection, "offline");

        Assert.Equal(2, results.Count);
        Assert.Equal("domains/mobile/patterns", results[0].Id);
        Assert.True(results[0].Rank <= results[1].Rank);
        Assert.Contains("offline", results[0].Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDomain()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/mobile/patterns.md", "# Mobile\n\nFluxo offline.");
        await WriteMarkdownAsync(root, "domains/api/patterns.md", "# API\n\nFluxo offline.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new KnowledgeSearchService().SearchAsync(connection, "offline", domain: "api");

        var result = Assert.Single(results);
        Assert.Equal("domains/api/patterns", result.Id);
        Assert.Equal("api", result.Domain);
    }

    [Fact]
    public async Task SearchAsync_FiltersByProject()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Inventory\n\nWebhook offline.");
        await WriteMarkdownAsync(root, "projects/Acme.Receiving.Api/context.md", "# Recebimento\n\nWebhook offline.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new KnowledgeSearchService().SearchAsync(
            connection,
            "webhook",
            project: "Acme.Receiving.Api");

        var result = Assert.Single(results);
        Assert.Equal("projects/Acme.Receiving.Api/context", result.Id);
        Assert.Equal("Acme.Receiving.Api", result.Project);
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "global/first.md", "# Primeiro\n\nBusca limite.");
        await WriteMarkdownAsync(root, "global/second.md", "# Segundo\n\nBusca limite.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new KnowledgeSearchService().SearchAsync(connection, "limite", limit: 1);

        Assert.Single(results);
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
