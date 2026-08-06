using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed class KnowledgeIndexer(
    KnowledgeMarkdownReader? reader = null,
    KnowledgeNodeRepository? repository = null)
{
    private readonly KnowledgeMarkdownReader reader = reader ?? new KnowledgeMarkdownReader();
    private readonly KnowledgeNodeRepository repository = repository ?? new KnowledgeNodeRepository();

    /// <summary>
    /// Rebuilds the derived SQLite index from the canonical Markdown knowledge tree.
    /// </summary>
    public async Task<KnowledgeIndexResult> ReindexAsync(
        SqliteConnection connection,
        string knowledgeRootPath,
        CancellationToken cancellationToken = default)
    {
        var documents = await reader.ReadAsync(knowledgeRootPath, cancellationToken);
        // Quarantined notes stay on disk for admin compliance scan but are excluded from search/context index.
        var indexableDocuments = documents
            .Where(document => !ComplianceFrontmatter.IsQuarantined(document.Frontmatter))
            .ToList();
        var nodes = indexableDocuments.Select(ToKnowledgeNode).ToList();
        var edges = ToKnowledgeEdges(indexableDocuments, nodes);

        foreach (var node in nodes)
        {
            var validation = node.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Invalid knowledge node '{node.Id}': {string.Join(" ", validation.Errors)}");
            }
        }

        foreach (var edge in edges)
        {
            var validation = edge.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Invalid knowledge edge '{edge.Id}': {string.Join(" ", validation.Errors)}");
            }
        }

        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);
        await repository.ReindexAsync(connection, nodes, edges, cancellationToken);

        return new KnowledgeIndexResult(nodes.Count);
    }

    /// <summary>
    /// Converts parsed Markdown metadata into the node model persisted by the SQLite index.
    /// </summary>
    public static KnowledgeNode ToKnowledgeNode(KnowledgeMarkdownDocument document)
    {
        return new KnowledgeNode
        {
            Id = document.Id,
            Title = document.Title,
            Path = document.Path,
            Scope = document.Scope,
            Type = document.Type,
            Domain = document.Domain,
            Project = document.Project,
            Content = document.Content
        };
    }

    public static IReadOnlyList<KnowledgeEdge> ToKnowledgeEdges(
        IReadOnlyCollection<KnowledgeMarkdownDocument> documents,
        IReadOnlyCollection<KnowledgeNode> nodes)
    {
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var nodesByPath = nodes.ToDictionary(node => node.Path, StringComparer.OrdinalIgnoreCase);
        var documentsById = documents.ToDictionary(document => document.Id, StringComparer.OrdinalIgnoreCase);
        var edges = new List<KnowledgeEdge>();
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents.OrderBy(document => document.Id, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var link in document.Links)
            {
                var relation = ParseRelation(link.Type, document.Id);
                var targetNode = ResolveTargetNode(link.Target, nodesById, nodesByPath, document.Id);

                if (relation == KnowledgeRelationType.DependsOn
                    && documentsById.TryGetValue(targetNode.Id, out var targetDocument)
                    && !string.IsNullOrWhiteSpace(document.Project)
                    && string.Equals(document.Project, targetDocument.Project, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Invalid depends_on relation in '{document.Id}': target '{targetNode.Id}' belongs to the same project.");
                }

                var edgeKey = $"{document.Id}\u001f{relation}\u001f{targetNode.Id}";
                if (!edgeKeys.Add(edgeKey))
                {
                    continue;
                }

                edges.Add(new KnowledgeEdge
                {
                    Id = CreateEdgeId(document.Id, relation, targetNode.Id),
                    SourceNodeId = document.Id,
                    TargetNodeId = targetNode.Id,
                    Relation = relation,
                    Evidence = $"frontmatter links in {document.Path}"
                });
            }
        }

        return edges;
    }

    private static KnowledgeRelationType ParseRelation(string relation, string sourceNodeId)
    {
        var normalized = relation.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var candidate in Enum.GetValues<KnowledgeRelationType>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unsupported knowledge relation '{relation}' in '{sourceNodeId}'.");
    }

    private static KnowledgeNode ResolveTargetNode(
        string target,
        IReadOnlyDictionary<string, KnowledgeNode> nodesById,
        IReadOnlyDictionary<string, KnowledgeNode> nodesByPath,
        string sourceNodeId)
    {
        var normalizedTarget = target.Trim().Replace('\\', '/').Trim('/');
        var candidates = new List<string>
        {
            normalizedTarget,
            normalizedTarget.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
                ? normalizedTarget["knowledge/".Length..]
                : $"knowledge/{normalizedTarget}",
        };

        if (!normalizedTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"{normalizedTarget}/index");
            candidates.Add($"knowledge/{normalizedTarget}/index.md");
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (nodesById.TryGetValue(TrimMarkdownExtension(candidate), out var nodeById))
            {
                return nodeById;
            }

            if (nodesByPath.TryGetValue(EnsureKnowledgeMarkdownPath(candidate), out var nodeByPath))
            {
                return nodeByPath;
            }
        }

        throw new InvalidOperationException(
            $"Broken knowledge link in '{sourceNodeId}': target '{target}' does not resolve to an indexed node.");
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

    private static string CreateEdgeId(string sourceNodeId, KnowledgeRelationType relation, string targetNodeId)
    {
        return $"{sourceNodeId}|{relation}|{targetNodeId}";
    }
}
