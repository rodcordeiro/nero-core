using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeDatabaseMigratorTests
{
    [Fact]
    public async Task MigrateAsync_CreatesSqliteSchemaInMemory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await KnowledgeDatabaseMigrator.MigrateAsync(connection);

        Assert.True(await ObjectExistsAsync(connection, "table", "knowledge_nodes"));
        Assert.True(await ObjectExistsAsync(connection, "table", "knowledge_edges"));
        Assert.True(await ObjectExistsAsync(connection, "table", "knowledge_nodes_fts"));
        Assert.True(await ObjectExistsAsync(connection, "table", "knowledge_nodes_fts_data"));
    }

    [Fact]
    public async Task MigrateAsync_CanRunMoreThanOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");

        await KnowledgeDatabaseMigrator.MigrateAsync(connection);
        var exception = await Record.ExceptionAsync(() => KnowledgeDatabaseMigrator.MigrateAsync(connection));

        Assert.Null(exception);
    }

    [Fact]
    public async Task MigrateAsync_UpgradesKnowledgeEdgesToSupportSupersedes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE knowledge_nodes (
                id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                path TEXT NOT NULL,
                scope TEXT NOT NULL,
                type TEXT NOT NULL,
                domain TEXT NULL,
                project TEXT NULL,
                content TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK (scope IN ('Global', 'Domain', 'Project')),
                CHECK (type IN ('Index', 'BusinessRule', 'Context', 'Decision', 'Pattern', 'ProjectContext', 'Snapshot', 'Troubleshooting', 'ValidationRule'))
            );
            """);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE knowledge_edges (
                id TEXT PRIMARY KEY NOT NULL,
                source_node_id TEXT NOT NULL,
                target_node_id TEXT NOT NULL,
                relation TEXT NOT NULL,
                confidence REAL NOT NULL DEFAULT 1,
                evidence TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK (relation IN ('BelongsToDomain', 'DependsOn', 'Documents', 'Evidences', 'RelatedDecision', 'RelatedPattern', 'SourceFor', 'Updates', 'UsesBackend')),
                CHECK (confidence >= 0 AND confidence <= 1),
                FOREIGN KEY (source_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE,
                FOREIGN KEY (target_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE
            );
            """);

        await KnowledgeDatabaseMigrator.MigrateAsync(connection);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO knowledge_nodes (id, title, path, scope, type, content)
            VALUES
                ('decisions/new', 'Nova', 'knowledge/decisions/new.md', 'Global', 'Decision', 'Nova'),
                ('decisions/old', 'Antiga', 'knowledge/decisions/old.md', 'Global', 'Decision', 'Antiga');
            """);

        var exception = await Record.ExceptionAsync(() => ExecuteAsync(
            connection,
            """
            INSERT INTO knowledge_edges (
                id,
                source_node_id,
                target_node_id,
                relation,
                confidence,
                evidence
            )
            VALUES (
                'decisions/new|Supersedes|decisions/old',
                'decisions/new',
                'decisions/old',
                'Supersedes',
                1,
                'test'
            );
            """));

        Assert.Null(exception);
    }

    [Fact]
    public async Task MigrateAsync_UpgradesKnowledgeNodesToSupportSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE knowledge_nodes (
                id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                path TEXT NOT NULL,
                scope TEXT NOT NULL,
                type TEXT NOT NULL,
                domain TEXT NULL,
                project TEXT NULL,
                content TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK (scope IN ('Global', 'Domain', 'Project')),
                CHECK (type IN ('Index', 'BusinessRule', 'Context', 'Decision', 'Pattern', 'ProjectContext', 'Troubleshooting', 'ValidationRule'))
            );
            """);

        await KnowledgeDatabaseMigrator.MigrateAsync(connection);

        var exception = await Record.ExceptionAsync(() => ExecuteAsync(
            connection,
            """
            INSERT INTO knowledge_nodes (id, title, path, scope, type, content)
            VALUES ('snapshots/rotas', 'Snapshot de rotas', 'knowledge/snapshots/rotas.md', 'Global', 'Snapshot', 'Rotas.');
            """));

        Assert.Null(exception);
    }

    private static async Task<bool> ObjectExistsAsync(SqliteConnection connection, string type, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = $type AND name = $name;
            """;
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name);

        var result = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return result > 0;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
