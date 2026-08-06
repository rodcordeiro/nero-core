using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public sealed class KnowledgeNodeRepository
{
    /// <summary>
    /// Replaces the derived node and FTS data with the provided node set.
    /// </summary>
    public async Task ReindexAsync(
        SqliteConnection connection,
        IReadOnlyCollection<KnowledgeNode> nodes,
        IReadOnlyCollection<KnowledgeEdge>? edges = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var transaction = connection.BeginTransaction();
        await ClearDerivedDataAsync(connection, transaction, cancellationToken);

        foreach (var node in nodes)
        {
            await InsertNodeAsync(connection, transaction, node, cancellationToken);
            await InsertFtsAsync(connection, transaction, node, cancellationToken);
        }

        foreach (var edge in edges ?? [])
        {
            await InsertEdgeAsync(connection, transaction, edge, cancellationToken);
        }

        transaction.Commit();
    }

    private static async Task ClearDerivedDataAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, "DELETE FROM knowledge_nodes_fts;", cancellationToken);
        await ExecuteAsync(connection, transaction, "DELETE FROM knowledge_edges;", cancellationToken);
        await ExecuteAsync(connection, transaction, "DELETE FROM knowledge_nodes;", cancellationToken);
    }

    private static async Task InsertNodeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        KnowledgeNode node,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO knowledge_nodes (
                id,
                title,
                path,
                scope,
                type,
                domain,
                project,
                content,
                updated_utc
            )
            VALUES (
                $id,
                $title,
                $path,
                $scope,
                $type,
                $domain,
                $project,
                $content,
                CURRENT_TIMESTAMP
            );
            """;
        command.Parameters.AddWithValue("$id", node.Id);
        command.Parameters.AddWithValue("$title", node.Title);
        command.Parameters.AddWithValue("$path", node.Path);
        command.Parameters.AddWithValue("$scope", node.Scope.ToString());
        command.Parameters.AddWithValue("$type", node.Type.ToString());
        command.Parameters.AddWithValue("$domain", (object?)node.Domain ?? DBNull.Value);
        command.Parameters.AddWithValue("$project", (object?)node.Project ?? DBNull.Value);
        command.Parameters.AddWithValue("$content", node.Content);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertFtsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        KnowledgeNode node,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO knowledge_nodes_fts (node_id, title, content)
            VALUES ($node_id, $title, $content);
            """;
        command.Parameters.AddWithValue("$node_id", node.Id);
        command.Parameters.AddWithValue("$title", node.Title);
        command.Parameters.AddWithValue("$content", node.Content);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEdgeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        KnowledgeEdge edge,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO knowledge_edges (
                id,
                source_node_id,
                target_node_id,
                relation,
                confidence,
                evidence
            )
            VALUES (
                $id,
                $source_node_id,
                $target_node_id,
                $relation,
                $confidence,
                $evidence
            );
            """;
        command.Parameters.AddWithValue("$id", edge.Id);
        command.Parameters.AddWithValue("$source_node_id", edge.SourceNodeId);
        command.Parameters.AddWithValue("$target_node_id", edge.TargetNodeId);
        command.Parameters.AddWithValue("$relation", edge.Relation.ToString());
        command.Parameters.AddWithValue("$confidence", edge.Confidence);
        command.Parameters.AddWithValue("$evidence", edge.Evidence);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
