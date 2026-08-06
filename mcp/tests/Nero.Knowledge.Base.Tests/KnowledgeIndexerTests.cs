using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeIndexerTests
{
    [Fact]
    public void ToKnowledgeNode_ConvertsMarkdownDocument()
    {
        var document = new KnowledgeMarkdownDocument
        {
            Id = "domains/mobile/patterns",
            Title = "Padroes Mobile",
            Path = "knowledge/domains/mobile/patterns.md",
            Content = "Conteudo offline",
            Scope = KnowledgeScope.Domain,
            Type = KnowledgeNodeType.Pattern,
            Domain = "mobile"
        };

        var node = KnowledgeIndexer.ToKnowledgeNode(document);

        Assert.Equal(document.Id, node.Id);
        Assert.Equal(document.Title, node.Title);
        Assert.Equal(document.Path, node.Path);
        Assert.Equal(document.Content, node.Content);
        Assert.Equal(document.Scope, node.Scope);
        Assert.Equal(document.Type, node.Type);
        Assert.Equal(document.Domain, node.Domain);
        Assert.Null(node.Project);
    }

    [Fact]
    public async Task ReindexAsync_PersistsNodesAndPopulatesFts()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nConhecimento geral.");
        await WriteMarkdownAsync(root, "domains/mobile/patterns.md", "# Padroes Mobile\n\nFluxo offline.");
        await using var connection = new SqliteConnection("Data Source=:memory:");

        var result = await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(2, result.NodeCount);
        Assert.Equal(2, await CountAsync(connection, "knowledge_nodes"));
        Assert.Equal(2, await CountAsync(connection, "knowledge_nodes_fts"));
        Assert.Equal(1, await CountFtsMatchesAsync(connection, "offline"));
        Assert.Equal("mobile", await ScalarStringAsync(
            connection,
            "SELECT domain FROM knowledge_nodes WHERE id = 'domains/mobile/patterns';"));
    }

    [Fact]
    public async Task ReindexAsync_RemovesOldDerivedDataBeforeRebuilding()
    {
        var root = CreateTempKnowledgeRoot();
        var firstPath = await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nConteudo antigo.");
        await WriteMarkdownAsync(root, "projects/Acme.Mobile/context.md", "# Inventory\n\nContexto atual.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var indexer = new KnowledgeIndexer();

        await indexer.ReindexAsync(connection, root);
        File.Delete(firstPath);
        var result = await indexer.ReindexAsync(connection, root);

        Assert.Equal(1, result.NodeCount);
        Assert.Equal(1, await CountAsync(connection, "knowledge_nodes"));
        Assert.Equal(1, await CountAsync(connection, "knowledge_nodes_fts"));
        Assert.Equal(0, await CountNodeByIdAsync(connection, "global/index"));
        Assert.Equal(1, await CountNodeByIdAsync(connection, "projects/Acme.Mobile/context"));
    }

    [Fact]
    public async Task ReindexAsync_WithMissingKnowledgeRoot_ReturnsActionableError()
    {
        var root = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        await using var connection = new SqliteConnection("Data Source=:memory:");

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new KnowledgeIndexer().ReindexAsync(connection, root));

        Assert.Contains("Knowledge root not found", exception.Message);
        Assert.Contains("KnowledgeRoot__Path", exception.Message);
        Assert.Contains("examples/knowledge-scaffold", exception.Message);
    }

    [Fact]
    public async Task ReindexAsync_WithCanonicalSkillShape_KeepsLogicalKnowledgePaths()
    {
        var skillRoot = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(skillRoot, "knowledge");
        await WriteMarkdownAsync(root, "domains/api/index.md", "# APIs\n\nContexto API.");
        await using var connection = new SqliteConnection("Data Source=:memory:");

        var result = await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(1, result.NodeCount);
        Assert.Equal("knowledge/domains/api/index.md", await ScalarStringAsync(
            connection,
            "SELECT path FROM knowledge_nodes WHERE id = 'domains/api/index';"));
    }

    [Fact]
    public async Task ReindexAsync_PersistsEdgesFromFrontmatterLinks()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Events.API/index.md", "# Events Gateway\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api
              - type: depends_on
                target: projects/Acme.Events.API
            ---
            # Inventory API
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(2, await CountAsync(connection, "knowledge_edges"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "projects/Acme.Api/context",
            "domains/api/index",
            "BelongsToDomain"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "projects/Acme.Api/context",
            "projects/Acme.Events.API/index",
            "DependsOn"));
    }

    [Fact]
    public async Task ReindexAsync_SupportsPatternReuseRelations()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await WriteMarkdownAsync(root, "domains/api/patterns/cache.md", """
            ---
            type: pattern
            links:
              - type: used_by
                target: projects/Acme.Api/index
              - type: candidate_for_reuse
                target: domains/api/index
            ---
            # Cache
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(2, await CountAsync(connection, "knowledge_edges"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "domains/api/patterns/cache",
            "projects/Acme.Api/index",
            "UsedBy"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "domains/api/patterns/cache",
            "domains/api/index",
            "CandidateForReuse"));
    }

    [Fact]
    public async Task ReindexAsync_SupportsTroubleshootingRelations()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/troubleshooting/2026-07-22-timeout.md", """
            ---
            type: troubleshooting
            links:
              - type: caused_by
                target: domains/api/index
              - type: relates_to
                target: projects/Acme.Api/index
            ---
            # Timeout
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(2, await CountAsync(connection, "knowledge_edges"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "projects/Acme.Api/troubleshooting/2026-07-22-timeout",
            "domains/api/index",
            "CausedBy"));
        Assert.Equal(1, await CountEdgesAsync(
            connection,
            "projects/Acme.Api/troubleshooting/2026-07-22-timeout",
            "projects/Acme.Api/index",
            "RelatesTo"));
    }

    [Fact]
    public async Task ReindexAsync_DeduplicatesRepeatedEdges()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api
              - type: belongs_to_domain
                target: knowledge/domains/api/index.md
            ---
            # Inventory API
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await new KnowledgeIndexer().ReindexAsync(connection, root);

        Assert.Equal(1, await CountAsync(connection, "knowledge_edges"));
    }

    [Fact]
    public async Task ReindexAsync_WithBrokenLinkTarget_ReturnsActionableError()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", """
            ---
            links:
              - type: depends_on
                target: projects/Missing.Api
            ---
            # Inventory API
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new KnowledgeIndexer().ReindexAsync(connection, root));

        Assert.Contains("Broken knowledge link", exception.Message);
        Assert.Contains("projects/Missing.Api", exception.Message);
    }

    [Fact]
    public async Task ReindexAsync_WithDependsOnSameProject_ReturnsError()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n");
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", """
            ---
            links:
              - type: depends_on
                target: projects/Acme.Api
            ---
            # Inventory API Context
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new KnowledgeIndexer().ReindexAsync(connection, root));

        Assert.Contains("Invalid depends_on relation", exception.Message);
        Assert.Contains("same project", exception.Message);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<string> WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static async Task<long> CountAsync(SqliteConnection connection, string tableName)
    {
        return await ScalarLongAsync(connection, $"SELECT COUNT(*) FROM {tableName};");
    }

    private static async Task<long> CountFtsMatchesAsync(SqliteConnection connection, string query)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM knowledge_nodes_fts
            WHERE knowledge_nodes_fts MATCH $query;
            """;
        command.Parameters.AddWithValue("$query", query);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountNodeByIdAsync(SqliteConnection connection, string id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM knowledge_nodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountEdgesAsync(
        SqliteConnection connection,
        string sourceNodeId,
        string targetNodeId,
        string relation)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM knowledge_edges
            WHERE source_node_id = $source_node_id
              AND target_node_id = $target_node_id
              AND relation = $relation;
            """;
        command.Parameters.AddWithValue("$source_node_id", sourceNodeId);
        command.Parameters.AddWithValue("$target_node_id", targetNodeId);
        command.Parameters.AddWithValue("$relation", relation);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return (string?)await command.ExecuteScalarAsync();
    }
}
