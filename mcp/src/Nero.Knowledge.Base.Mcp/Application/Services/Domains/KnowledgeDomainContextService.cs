using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Domains;

public sealed class KnowledgeDomainContextService
{
    public async Task<KnowledgeDomainContextResult> GetDomainContextAsync(
        SqliteConnection connection,
        string domain,
        bool includeProjects = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        var index = await GetSectionByIdAsync(connection, $"domains/{domain}/index", cancellationToken);
        var patterns = await GetSectionByIdAsync(connection, $"domains/{domain}/patterns", cancellationToken);
        var businessRules = await GetSectionByIdAsync(connection, $"domains/{domain}/business-rules", cancellationToken);
        var validationAndTests = await GetSectionByIdAsync(connection, $"domains/{domain}/validation-and-tests", cancellationToken);
        var projects = includeProjects
            ? await GetRelatedProjectsAsync(connection, domain, cancellationToken)
            : [];

        return new KnowledgeDomainContextResult
        {
            Domain = domain,
            Exists = index is not null
                || patterns is not null
                || businessRules is not null
                || validationAndTests is not null
                || projects.Count > 0,
            Index = index,
            Patterns = patterns,
            BusinessRules = businessRules,
            ValidationAndTests = validationAndTests,
            Projects = projects
        };
    }

    private static async Task<KnowledgeDomainContextSection?> GetSectionByIdAsync(
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

    private static KnowledgeDomainContextSection ReadSection(SqliteDataReader reader)
    {
        return new KnowledgeDomainContextSection
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Path = reader.GetString(2),
            Type = Enum.Parse<KnowledgeNodeType>(reader.GetString(3)),
            Content = reader.GetString(4)
        };
    }

    private static async Task<IReadOnlyList<KnowledgeDomainProjectSummary>> GetRelatedProjectsAsync(
        SqliteConnection connection,
        string domain,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT
                source.id,
                source.title,
                source.path,
                source.project
            FROM knowledge_edges edge
            INNER JOIN knowledge_nodes source ON source.id = edge.source_node_id
            INNER JOIN knowledge_nodes target ON target.id = edge.target_node_id
            WHERE edge.relation = $relation
              AND target.id = $domainIndexId
              AND source.project IS NOT NULL
            ORDER BY source.project ASC, source.title ASC;
            """;
        command.Parameters.AddWithValue("$relation", KnowledgeRelationType.BelongsToDomain.ToString());
        command.Parameters.AddWithValue("$domainIndexId", $"domains/{domain}/index");

        var projects = new List<KnowledgeDomainProjectSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new KnowledgeDomainProjectSummary
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Path = reader.GetString(2),
                Project = reader.GetString(3)
            });
        }

        return projects;
    }
}
