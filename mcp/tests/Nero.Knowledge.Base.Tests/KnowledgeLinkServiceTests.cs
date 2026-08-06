using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Links;
using Nero.Knowledge.Base.Mcp.Application.Services.Links;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeLinkServiceTests
{
    [Fact]
    public async Task LinkAsync_ResolvesNodesByIdOrPathAndCreatesManualEdge()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeLinkService().LinkAsync(
            connection,
            new RegisterKnowledgeLinkRequest
            {
                Source = "knowledge/projects/Acme.Api/index.md",
                Target = "domains/api",
                Relation = "belongs_to_domain",
                Confidence = 0.9m,
                Evidence = "Relacionamento manual confirmado em teste."
            });

        Assert.True(result.Created);
        Assert.Equal("projects/Acme.Api/index", result.SourceNodeId);
        Assert.Equal("domains/api/index", result.TargetNodeId);
        Assert.Equal("BelongsToDomain", result.Relation);
        Assert.Equal(1, await CountEdgesAsync(connection));
    }

    [Fact]
    public async Task LinkAsync_IsIdempotentForDuplicateEdge()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);
        var service = new KnowledgeLinkService();
        var request = new RegisterKnowledgeLinkRequest
        {
            Source = "projects/Acme.Api/index",
            Target = "domains/api/index",
            Relation = "belongs_to_domain",
            Evidence = "Relacionamento manual confirmado em teste."
        };

        var first = await service.LinkAsync(connection, request);
        var second = await service.LinkAsync(connection, request);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.EdgeId, second.EdgeId);
        Assert.Equal(1, await CountEdgesAsync(connection));
    }

    [Fact]
    public async Task LinkAsync_RejectsUnsupportedRelation()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        await Assert.ThrowsAsync<ArgumentException>(() => new KnowledgeLinkService().LinkAsync(
            connection,
            new RegisterKnowledgeLinkRequest
            {
                Source = "projects/Acme.Api/index",
                Target = "domains/api/index",
                Relation = "unsupported_relation"
            }));
    }

    [Fact]
    public async Task LinkAsync_RejectsMissingNode()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        await Assert.ThrowsAsync<ArgumentException>(() => new KnowledgeLinkService().LinkAsync(
            connection,
            new RegisterKnowledgeLinkRequest
            {
                Source = "projects/Missing.Api/index",
                Target = "domains/api/index",
                Relation = "belongs_to_domain"
            }));
    }

    [Fact]
    public async Task LinkAsync_ValidatesConfidence()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new KnowledgeLinkService().LinkAsync(
            connection,
            new RegisterKnowledgeLinkRequest
            {
                Source = "projects/Acme.Api/index",
                Target = "domains/api/index",
                Relation = "belongs_to_domain",
                Confidence = 1.1m
            }));
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

    private static async Task<long> CountEdgesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM knowledge_edges;";

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
