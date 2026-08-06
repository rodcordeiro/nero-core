using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Search;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Search;

public sealed class KnowledgeSearchService
{
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        SqliteConnection connection,
        string query,
        string? domain = null,
        string? project = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                n.id,
                n.title,
                n.path,
                n.scope,
                n.type,
                n.domain,
                n.project,
                snippet(knowledge_nodes_fts, 2, '', '', '...', 16) AS snippet,
                bm25(knowledge_nodes_fts) AS rank
            FROM knowledge_nodes_fts
            INNER JOIN knowledge_nodes n ON n.id = knowledge_nodes_fts.node_id
            WHERE knowledge_nodes_fts MATCH $query
              AND ($domain IS NULL OR n.domain = $domain)
              AND ($project IS NULL OR n.project = $project)
            ORDER BY rank ASC, n.title ASC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$domain", string.IsNullOrWhiteSpace(domain) ? DBNull.Value : domain);
        command.Parameters.AddWithValue("$project", string.IsNullOrWhiteSpace(project) ? DBNull.Value : project);
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<KnowledgeSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new KnowledgeSearchResult
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Path = reader.GetString(2),
                Scope = Enum.Parse<KnowledgeScope>(reader.GetString(3)),
                Type = Enum.Parse<KnowledgeNodeType>(reader.GetString(4)),
                Domain = reader.IsDBNull(5) ? null : reader.GetString(5),
                Project = reader.IsDBNull(6) ? null : reader.GetString(6),
                Snippet = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Rank = reader.GetDouble(8)
            });
        }

        return results;
    }
}
