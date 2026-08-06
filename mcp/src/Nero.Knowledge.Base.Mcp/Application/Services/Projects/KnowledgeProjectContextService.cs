using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Projects;

public sealed class KnowledgeProjectContextService
{
    public async Task<KnowledgeProjectContextResult> GetProjectContextAsync(
        SqliteConnection connection,
        string project,
        bool includeDecisions = true,
        bool includeTroubleshooting = true,
        int recentLimit = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        if (recentLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentLimit), recentLimit, "Recent limit must be greater than zero.");
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        var index = await GetSectionByIdAsync(connection, $"projects/{project}/index", cancellationToken);
        var context = await GetSectionByIdAsync(connection, $"projects/{project}/context", cancellationToken);
        var patterns = await GetSectionByIdAsync(connection, $"projects/{project}/patterns", cancellationToken);
        var businessRules = await GetSectionByIdAsync(connection, $"projects/{project}/business-rules", cancellationToken);
        var allDecisions = includeDecisions
            ? await GetSectionsByTypeAsync(connection, project, KnowledgeNodeType.Decision, null, cancellationToken)
            : [];
        var supersededDecisionIds = includeDecisions
            ? await GetSupersededDecisionIdsAsync(connection, project, cancellationToken)
            : [];
        var supersededByLookup = includeDecisions
            ? await GetSupersededByLookupAsync(connection, project, cancellationToken)
            : new Dictionary<string, IReadOnlyList<KnowledgeProjectContextSection>>(StringComparer.Ordinal);
        var activeDecisions = allDecisions
            .Where(decision => !supersededDecisionIds.Contains(decision.Id))
            .Take(recentLimit)
            .ToList();
        var decisions = activeDecisions;
        var supersededDecisions = allDecisions
            .Where(decision => supersededDecisionIds.Contains(decision.Id))
            .Take(recentLimit)
            .Select(decision => new KnowledgeSupersededDecision
            {
                Decision = decision,
                SupersededBy = supersededByLookup.TryGetValue(decision.Id, out var replacingDecisions)
                    ? replacingDecisions
                    : []
            })
            .ToList();
        var troubleshooting = includeTroubleshooting
            ? await GetSectionsByTypeAsync(connection, project, KnowledgeNodeType.Troubleshooting, recentLimit, cancellationToken)
            : [];

        return new KnowledgeProjectContextResult
        {
            Project = project,
            Exists = index is not null
                || context is not null
                || patterns is not null
                || businessRules is not null
                || decisions.Count > 0
                || troubleshooting.Count > 0,
            Index = index,
            Context = context,
            Patterns = patterns,
            BusinessRules = businessRules,
            Decisions = decisions,
            ActiveDecisions = activeDecisions,
            SupersededDecisions = supersededDecisions,
            HasSupersededDecisions = supersededDecisions.Count > 0,
            Troubleshooting = troubleshooting
        };
    }

    private static async Task<KnowledgeProjectContextSection?> GetSectionByIdAsync(
        SqliteConnection connection,
        string id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, path, type, content
            FROM knowledge_nodes
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSection(reader)
            : null;
    }

    private static async Task<IReadOnlyList<KnowledgeProjectContextSection>> GetSectionsByTypeAsync(
        SqliteConnection connection,
        string project,
        KnowledgeNodeType type,
        int? limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = limit.HasValue
            ? """
            SELECT id, title, path, type, content
            FROM knowledge_nodes
            WHERE project = $project
              AND type = $type
            ORDER BY path DESC
            LIMIT $limit;
            """
            : """
            SELECT id, title, path, type, content
            FROM knowledge_nodes
            WHERE project = $project
              AND type = $type
            ORDER BY path DESC;
            """;
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$type", type.ToString());
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("$limit", limit.Value);
        }

        var sections = new List<KnowledgeProjectContextSection>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sections.Add(ReadSection(reader));
        }

        return sections;
    }

    private static async Task<HashSet<string>> GetSupersededDecisionIdsAsync(
        SqliteConnection connection,
        string project,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT edge.target_node_id
            FROM knowledge_edges edge
            INNER JOIN knowledge_nodes source ON source.id = edge.source_node_id
            INNER JOIN knowledge_nodes target ON target.id = edge.target_node_id
            WHERE edge.relation = 'Supersedes'
              AND target.project = $project
              AND source.type = 'Decision'
              AND target.type = 'Decision';
            """;
        command.Parameters.AddWithValue("$project", project);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static async Task<Dictionary<string, IReadOnlyList<KnowledgeProjectContextSection>>> GetSupersededByLookupAsync(
        SqliteConnection connection,
        string project,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT edge.target_node_id, source.id, source.title, source.path, source.type, source.content
            FROM knowledge_edges edge
            INNER JOIN knowledge_nodes source ON source.id = edge.source_node_id
            INNER JOIN knowledge_nodes target ON target.id = edge.target_node_id
            WHERE edge.relation = 'Supersedes'
              AND target.project = $project
              AND source.type = 'Decision'
              AND target.type = 'Decision'
            ORDER BY source.path DESC;
            """;
        command.Parameters.AddWithValue("$project", project);

        var lookup = new Dictionary<string, List<KnowledgeProjectContextSection>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var supersededId = reader.GetString(0);
            var replacingDecision = ReadSection(reader, 1);
            if (!lookup.TryGetValue(supersededId, out var replacingDecisions))
            {
                replacingDecisions = [];
                lookup[supersededId] = replacingDecisions;
            }

            replacingDecisions.Add(replacingDecision);
        }

        return lookup.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<KnowledgeProjectContextSection>)item.Value,
            StringComparer.Ordinal);
    }

    private static KnowledgeProjectContextSection ReadSection(SqliteDataReader reader, int offset = 0)
    {
        return new KnowledgeProjectContextSection
        {
            Id = reader.GetString(offset),
            Title = reader.GetString(offset + 1),
            Path = reader.GetString(offset + 2),
            Type = Enum.Parse<KnowledgeNodeType>(reader.GetString(offset + 3)),
            Content = reader.GetString(offset + 4)
        };
    }
}
