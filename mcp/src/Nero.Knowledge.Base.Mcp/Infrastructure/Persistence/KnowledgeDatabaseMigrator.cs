using Microsoft.Data.Sqlite;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public static class KnowledgeDatabaseMigrator
{
    public static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS knowledge_nodes (
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
            """,
            cancellationToken);

        await EnsureKnowledgeNodesSupportsCurrentTypesAsync(connection, cancellationToken);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS knowledge_edges (
                id TEXT PRIMARY KEY NOT NULL,
                source_node_id TEXT NOT NULL,
                target_node_id TEXT NOT NULL,
                relation TEXT NOT NULL,
                confidence REAL NOT NULL DEFAULT 1,
                evidence TEXT NOT NULL DEFAULT '',
                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CHECK (relation IN ('BelongsToDomain', 'CandidateForReuse', 'CausedBy', 'DependsOn', 'Documents', 'Evidences', 'RelatedDecision', 'RelatedPattern', 'RelatesTo', 'SourceFor', 'Supersedes', 'Updates', 'UsedBy', 'UsesBackend')),
                CHECK (confidence >= 0 AND confidence <= 1),
                FOREIGN KEY (source_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE,
                FOREIGN KEY (target_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await EnsureKnowledgeEdgesSupportsCurrentRelationsAsync(connection, cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS knowledge_nodes_fts
            USING fts5(node_id UNINDEXED, title, content);
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_knowledge_nodes_path ON knowledge_nodes(path);",
            cancellationToken);

        await ExecuteAsync(
            connection,
            "CREATE INDEX IF NOT EXISTS ix_knowledge_nodes_scope_domain_project ON knowledge_nodes(scope, domain, project);",
            cancellationToken);

        await ExecuteAsync(
            connection,
            "CREATE INDEX IF NOT EXISTS ix_knowledge_edges_source_relation ON knowledge_edges(source_node_id, relation);",
            cancellationToken);

        await ExecuteAsync(
            connection,
            "CREATE INDEX IF NOT EXISTS ix_knowledge_edges_target_relation ON knowledge_edges(target_node_id, relation);",
            cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureKnowledgeNodesSupportsCurrentTypesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table' AND name = 'knowledge_nodes';
            """;

        var createSql = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (createSql is null || createSql.Contains("'Snapshot'", StringComparison.Ordinal))
        {
            return;
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken);
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS knowledge_nodes_fts;", cancellationToken);
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS knowledge_edges;", cancellationToken);
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS knowledge_nodes;", cancellationToken);
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
            """,
            cancellationToken);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
    }

    private static async Task EnsureKnowledgeEdgesSupportsCurrentRelationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table' AND name = 'knowledge_edges';
            """;

        var createSql = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (createSql is null
            || (createSql.Contains("'Supersedes'", StringComparison.Ordinal)
                && createSql.Contains("'UsedBy'", StringComparison.Ordinal)
                && createSql.Contains("'CandidateForReuse'", StringComparison.Ordinal)
                && createSql.Contains("'CausedBy'", StringComparison.Ordinal)
                && createSql.Contains("'RelatesTo'", StringComparison.Ordinal)))
        {
            return;
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken);
        await ExecuteAsync(connection, "ALTER TABLE knowledge_edges RENAME TO knowledge_edges_old;", cancellationToken);
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
                CHECK (relation IN ('BelongsToDomain', 'CandidateForReuse', 'CausedBy', 'DependsOn', 'Documents', 'Evidences', 'RelatedDecision', 'RelatedPattern', 'RelatesTo', 'SourceFor', 'Supersedes', 'Updates', 'UsedBy', 'UsesBackend')),
                CHECK (confidence >= 0 AND confidence <= 1),
                FOREIGN KEY (source_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE,
                FOREIGN KEY (target_node_id) REFERENCES knowledge_nodes(id) ON DELETE CASCADE
            );
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO knowledge_edges (
                id,
                source_node_id,
                target_node_id,
                relation,
                confidence,
                evidence,
                created_utc
            )
            SELECT
                id,
                source_node_id,
                target_node_id,
                relation,
                confidence,
                evidence,
                created_utc
            FROM knowledge_edges_old;
            """,
            cancellationToken);
        await ExecuteAsync(connection, "DROP TABLE knowledge_edges_old;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
    }
}
