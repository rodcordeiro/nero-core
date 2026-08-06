using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Graph;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Graph;

public sealed class RelatedKnowledgeService
{
    /// <summary>
    /// Cap same-domain BelongsToDomain sibling expansion so inventory flood cannot dominate results.
    /// </summary>
    public const int MaxBelongsToDomainSiblings = 5;

    private static readonly HashSet<string> HighSignalRelations = new(StringComparer.Ordinal)
    {
        nameof(KnowledgeRelationType.Documents),
        nameof(KnowledgeRelationType.Evidences),
        nameof(KnowledgeRelationType.RelatedDecision),
        nameof(KnowledgeRelationType.RelatedPattern),
        nameof(KnowledgeRelationType.DependsOn),
        nameof(KnowledgeRelationType.UsesBackend),
        nameof(KnowledgeRelationType.Supersedes),
        nameof(KnowledgeRelationType.Updates),
        nameof(KnowledgeRelationType.SourceFor)
    };

    public async Task<IReadOnlyList<RelatedKnowledgeNodeResult>> FindRelatedAsync(
        SqliteConnection connection,
        string? project = null,
        string? topic = null,
        IReadOnlyCollection<KnowledgeRelationType>? relationTypes = null,
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project) && string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Project or topic must be provided.");
        }

        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be greater than zero.");
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        var seedNodeIds = await GetSeedNodeIdsAsync(connection, project, topic, cancellationToken);
        var relationFilter = relationTypes is { Count: > 0 }
            ? relationTypes.Select(relation => relation.ToString()).ToHashSet(StringComparer.Ordinal)
            : null;
        var results = new Dictionary<string, RelatedKnowledgeNodeResult>(StringComparer.OrdinalIgnoreCase);
        var frontier = seedNodeIds;
        var visited = new HashSet<string>(seedNodeIds, StringComparer.OrdinalIgnoreCase);

        for (var currentDepth = 1; currentDepth <= depth && frontier.Count > 0; currentDepth++)
        {
            var direct = await GetDirectRelationsAsync(connection, frontier, relationFilter, cancellationToken);
            var nextFrontier = new List<string>();

            foreach (var related in direct)
            {
                AddBest(results, related);
                // Do not expand through BelongsToDomain: domain hubs fan out to every sibling project.
                if (related.Relation == KnowledgeRelationType.BelongsToDomain)
                {
                    continue;
                }

                if (visited.Add(related.Id))
                {
                    nextFrontier.Add(related.Id);
                }
            }

            frontier = nextFrontier;
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            foreach (var related in await GetCommonDomainRelationsAsync(connection, project, cancellationToken))
            {
                AddBest(results, related);
            }

            var siblingCount = 0;
            foreach (var related in await GetSiblingProjectRelationsAsync(connection, project, topic, cancellationToken))
            {
                if (siblingCount >= MaxBelongsToDomainSiblings)
                {
                    break;
                }

                AddBest(results, related);
                siblingCount++;
            }
        }

        return results.Values
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Project ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> GetSeedNodeIdsAsync(
        SqliteConnection connection,
        string? project,
        string? topic,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(topic))
        {
            command.CommandText = """
                SELECT id
                FROM knowledge_nodes
                WHERE project = $project
                ORDER BY type = 'Index' DESC, id ASC;
                """;
        }
        else
        {
            command.CommandText = """
                SELECT n.id
                FROM knowledge_nodes_fts fts
                INNER JOIN knowledge_nodes n ON n.id = fts.node_id
                WHERE knowledge_nodes_fts MATCH $topic
                  AND ($project IS NULL OR n.project = $project)
                ORDER BY bm25(knowledge_nodes_fts) ASC, n.title ASC;
                """;
        }

        command.Parameters.AddWithValue("$project", string.IsNullOrWhiteSpace(project) ? DBNull.Value : project);
        if (!string.IsNullOrWhiteSpace(topic))
        {
            command.Parameters.AddWithValue("$topic", topic);
        }

        var nodeIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodeIds.Add(reader.GetString(0));
        }

        return nodeIds;
    }

    private static async Task<IReadOnlyList<RelatedKnowledgeNodeResult>> GetDirectRelationsAsync(
        SqliteConnection connection,
        IReadOnlyList<string> nodeIds,
        IReadOnlySet<string>? relationFilter,
        CancellationToken cancellationToken)
    {
        var results = new List<RelatedKnowledgeNodeResult>();
        foreach (var nodeId in nodeIds)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    related.id,
                    related.title,
                    related.path,
                    related.scope,
                    related.type,
                    related.domain,
                    related.project,
                    edge.relation,
                    edge.evidence
                FROM knowledge_edges edge
                INNER JOIN knowledge_nodes related
                    ON related.id = CASE
                        WHEN edge.source_node_id = $nodeId THEN edge.target_node_id
                        ELSE edge.source_node_id
                    END
                WHERE (edge.source_node_id = $nodeId OR edge.target_node_id = $nodeId)
                ORDER BY related.title ASC;
                """;
            command.Parameters.AddWithValue("$nodeId", nodeId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var relation = reader.GetString(7);
                if (relationFilter is not null && !relationFilter.Contains(relation))
                {
                    continue;
                }

                // BelongsToDomain is useful project→domain; domain→project reverse walk is inventory flood.
                if (string.Equals(relation, nameof(KnowledgeRelationType.BelongsToDomain), StringComparison.Ordinal)
                    && !string.Equals(reader.GetString(3), nameof(KnowledgeScope.Domain), StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(ReadRelatedNode(
                    reader,
                    relation,
                    reader.GetString(8),
                    ScoreForRelation(relation)));
            }
        }

        return results;
    }

    private static async Task<IReadOnlyList<RelatedKnowledgeNodeResult>> GetCommonDomainRelationsAsync(
        SqliteConnection connection,
        string project,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT
                domain.id,
                domain.title,
                domain.path,
                domain.scope,
                domain.type,
                domain.domain,
                domain.project,
                $relation AS relation,
                'common domain for project ' || $project AS evidence
            FROM knowledge_edges edge
            INNER JOIN knowledge_nodes source ON source.id = edge.source_node_id
            INNER JOIN knowledge_nodes domain ON domain.id = edge.target_node_id
            WHERE edge.relation = $relation
              AND source.project = $project
              AND domain.scope = 'Domain'
            ORDER BY domain.title ASC;
            """;
        command.Parameters.AddWithValue("$relation", KnowledgeRelationType.BelongsToDomain.ToString());
        command.Parameters.AddWithValue("$project", project);

        return await ReadRelatedNodesAsync(command, ScoreForRelation(nameof(KnowledgeRelationType.BelongsToDomain)), cancellationToken);
    }

    private static async Task<IReadOnlyList<RelatedKnowledgeNodeResult>> GetSiblingProjectRelationsAsync(
        SqliteConnection connection,
        string project,
        string? topic,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(topic))
        {
            command.CommandText = """
                SELECT DISTINCT
                    sibling.id,
                    sibling.title,
                    sibling.path,
                    sibling.scope,
                    sibling.type,
                    sibling.domain,
                    sibling.project,
                    $relation AS relation,
                    'sibling project through common domain ' || domain.id AS evidence
                FROM knowledge_edges own_edge
                INNER JOIN knowledge_nodes own ON own.id = own_edge.source_node_id
                INNER JOIN knowledge_nodes domain ON domain.id = own_edge.target_node_id
                INNER JOIN knowledge_edges sibling_edge ON sibling_edge.target_node_id = domain.id
                INNER JOIN knowledge_nodes sibling ON sibling.id = sibling_edge.source_node_id
                WHERE own_edge.relation = $relation
                  AND sibling_edge.relation = $relation
                  AND own.project = $project
                  AND sibling.project IS NOT NULL
                  AND sibling.project <> $project
                  AND sibling.type = 'Index'
                ORDER BY sibling.project ASC, sibling.title ASC;
                """;
        }
        else
        {
            command.CommandText = """
                SELECT DISTINCT
                    sibling.id,
                    sibling.title,
                    sibling.path,
                    sibling.scope,
                    sibling.type,
                    sibling.domain,
                    sibling.project,
                    $relation AS relation,
                    'sibling project through common domain ' || domain.id AS evidence
                FROM knowledge_edges own_edge
                INNER JOIN knowledge_nodes own ON own.id = own_edge.source_node_id
                INNER JOIN knowledge_nodes domain ON domain.id = own_edge.target_node_id
                INNER JOIN knowledge_edges sibling_edge ON sibling_edge.target_node_id = domain.id
                INNER JOIN knowledge_nodes sibling_index ON sibling_index.id = sibling_edge.source_node_id
                INNER JOIN knowledge_nodes sibling ON sibling.project = sibling_index.project
                INNER JOIN knowledge_nodes_fts fts ON fts.node_id = sibling.id
                WHERE own_edge.relation = $relation
                  AND sibling_edge.relation = $relation
                  AND own.project = $project
                  AND sibling_index.project IS NOT NULL
                  AND sibling_index.project <> $project
                  AND knowledge_nodes_fts MATCH $topic
                ORDER BY sibling.project ASC, sibling.title ASC;
                """;
            command.Parameters.AddWithValue("$topic", topic);
        }

        command.Parameters.AddWithValue("$relation", KnowledgeRelationType.BelongsToDomain.ToString());
        command.Parameters.AddWithValue("$project", project);

        // Sibling inventory is lower-signal than the project's own BelongsToDomain domain link.
        return await ReadRelatedNodesAsync(command, 0.35m, cancellationToken);
    }

    private static async Task<IReadOnlyList<RelatedKnowledgeNodeResult>> ReadRelatedNodesAsync(
        SqliteCommand command,
        decimal score,
        CancellationToken cancellationToken)
    {
        var results = new List<RelatedKnowledgeNodeResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRelatedNode(reader, reader.GetString(7), reader.GetString(8), score));
        }

        return results;
    }

    private static RelatedKnowledgeNodeResult ReadRelatedNode(
        SqliteDataReader reader,
        string relation,
        string evidence,
        decimal score)
    {
        return new RelatedKnowledgeNodeResult
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Path = reader.GetString(2),
            Scope = Enum.Parse<KnowledgeScope>(reader.GetString(3)),
            Type = Enum.Parse<KnowledgeNodeType>(reader.GetString(4)),
            Domain = reader.IsDBNull(5) ? null : reader.GetString(5),
            Project = reader.IsDBNull(6) ? null : reader.GetString(6),
            Relation = Enum.Parse<KnowledgeRelationType>(relation),
            Evidence = evidence,
            Score = score
        };
    }

    /// <summary>
    /// Prefer typed edges over BelongsToDomain inventory links in ranking.
    /// </summary>
    private static decimal ScoreForRelation(string relation)
    {
        if (HighSignalRelations.Contains(relation))
        {
            return 1.0m;
        }

        if (string.Equals(relation, nameof(KnowledgeRelationType.BelongsToDomain), StringComparison.Ordinal))
        {
            return 0.45m;
        }

        return 0.55m;
    }

    private static void AddBest(
        Dictionary<string, RelatedKnowledgeNodeResult> results,
        RelatedKnowledgeNodeResult candidate)
    {
        if (!results.TryGetValue(candidate.Id, out var current) || candidate.Score > current.Score)
        {
            results[candidate.Id] = candidate;
        }
    }
}
