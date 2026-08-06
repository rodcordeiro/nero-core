using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Links;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Links;

public sealed class KnowledgeLinkService
{
    public async Task<RegisterKnowledgeLinkResult> LinkAsync(
        SqliteConnection connection,
        RegisterKnowledgeLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Target);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Relation);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(request.Evidence, nameof(request.Evidence));
        ComplianceScanner.EnsureNoBlockingHits(
            (request.Source, nameof(request.Source)),
            (request.Target, nameof(request.Target)),
            (request.Relation, nameof(request.Relation)),
            (request.Evidence, nameof(request.Evidence)));

        var relation = ParseRelationType(request.Relation);
        if (request.Confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Confidence), request.Confidence, "Confidence must be between 0 and 1.");
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);
        var sourceNodeId = await ResolveNodeIdAsync(connection, request.Source, nameof(request.Source), cancellationToken);
        var targetNodeId = await ResolveNodeIdAsync(connection, request.Target, nameof(request.Target), cancellationToken);

        var edge = new KnowledgeEdge
        {
            Id = CreateEdgeId(sourceNodeId, relation, targetNodeId),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            Relation = relation,
            Confidence = request.Confidence,
            Evidence = request.Evidence.Trim()
        };

        var validation = edge.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid manual knowledge edge '{edge.Id}': {string.Join(" ", validation.Errors)}");
        }

        var created = await InsertIfMissingAsync(connection, edge, cancellationToken);
        return new RegisterKnowledgeLinkResult
        {
            EdgeId = edge.Id,
            SourceNodeId = edge.SourceNodeId,
            TargetNodeId = edge.TargetNodeId,
            Relation = edge.Relation.ToString(),
            Created = created
        };
    }

    private static KnowledgeRelationType ParseRelationType(string relationType)
    {
        var normalized = relationType.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var candidate in Enum.GetValues<KnowledgeRelationType>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"Unsupported knowledge relation type '{relationType}'.", nameof(relationType));
    }

    private static async Task<string> ResolveNodeIdAsync(
        SqliteConnection connection,
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        var candidates = new List<string>
        {
            normalized,
            TrimMarkdownExtension(normalized),
            normalized.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
                ? normalized["knowledge/".Length..]
                : $"knowledge/{normalized}",
        };

        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"{normalized}/index");
            candidates.Add($"knowledge/{normalized}/index.md");
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var nodeId = await TryResolveNodeIdAsync(connection, candidate, cancellationToken);
            if (nodeId is not null)
            {
                return nodeId;
            }
        }

        throw new ArgumentException($"Knowledge node '{value}' was not found by id or logical path.", parameterName);
    }

    private static async Task<string?> TryResolveNodeIdAsync(
        SqliteConnection connection,
        string candidate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM knowledge_nodes
            WHERE id = $id OR path = $path
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", TrimMarkdownExtension(candidate));
        command.Parameters.AddWithValue("$path", EnsureKnowledgeMarkdownPath(candidate));

        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<bool> InsertIfMissingAsync(
        SqliteConnection connection,
        KnowledgeEdge edge,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO knowledge_edges (
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

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows > 0;
    }

    private static string TrimMarkdownExtension(string value)
    {
        return value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? value[..^3]
            : value;
    }

    private static string EnsureKnowledgeMarkdownPath(string value)
    {
        var path = value.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"knowledge/{value}";

        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path : $"{path}.md";
    }

    private static string CreateEdgeId(
        string sourceNodeId,
        KnowledgeRelationType relation,
        string targetNodeId)
    {
        return $"{sourceNodeId}|{relation}|{targetNodeId}";
    }
}
