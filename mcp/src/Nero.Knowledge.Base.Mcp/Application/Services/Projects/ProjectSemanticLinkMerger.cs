using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Projects;

/// <summary>
/// Resolves non-minimal preferred links for project index/context/inventory updates (Marco 21 P3).
/// Contract mirrors domain <c>sourceFor</c>: omit preserves, list replaces, empty clears.
/// </summary>
internal static class ProjectSemanticLinkMerger
{
    private const int MaxLinkCount = 40;
    private const int MaxTargetLength = 300;

    private static readonly HashSet<string> MinimalRelationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        PreferredRelationLinks.Documents,
        PreferredRelationLinks.BelongsToDomain
    };

    public static IReadOnlyList<ProjectSemanticLink> Resolve(
        string knowledgeRootPath,
        string? existingMarkdownPath,
        string project,
        IReadOnlyList<ProjectSemanticLink>? requested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        IReadOnlyList<ProjectSemanticLink> effective = requested is null
            ? ReadExistingNonMinimal(knowledgeRootPath, existingMarkdownPath)
            : Normalize(requested);

        Validate(project, effective);
        return effective;
    }

    /// <summary>
    /// Parses MCP string items <c>type:target</c> (first colon splits).
    /// Null list means omit (caller should pass null through); empty list clears.
    /// </summary>
    public static IReadOnlyList<ProjectSemanticLink>? ParseToolInput(IReadOnlyList<string>? items)
    {
        if (items is null)
        {
            return null;
        }

        if (items.Count == 0)
        {
            return [];
        }

        var links = new List<ProjectSemanticLink>(items.Count);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                throw new ArgumentException(
                    "linksSemanticos items must be non-empty 'type:target' values.",
                    nameof(items));
            }

            var separator = item.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator >= item.Length - 1)
            {
                throw new ArgumentException(
                    "linksSemanticos items must use 'type:target' (example: uses_backend:projects/Acme.X.Api).",
                    nameof(items));
            }

            links.Add(new ProjectSemanticLink
            {
                Type = item[..separator].Trim(),
                Target = item[(separator + 1)..].Trim()
            });
        }

        return links;
    }

    public static string RenderYamlBlock(IReadOnlyList<ProjectSemanticLink> links, Func<string, string> escapeYaml)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(escapeYaml);
        if (links.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var link in links)
        {
            builder.AppendLine($"  - type: {link.Type}");
            builder.AppendLine($"    target: {escapeYaml(link.Target)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<ProjectSemanticLink> ReadExistingNonMinimal(
        string knowledgeRootPath,
        string? existingMarkdownPath)
    {
        if (string.IsNullOrWhiteSpace(existingMarkdownPath) || !File.Exists(existingMarkdownPath))
        {
            return [];
        }

        var markdown = File.ReadAllText(existingMarkdownPath);
        var document = new KnowledgeMarkdownParser().Parse(
            knowledgeRootPath,
            existingMarkdownPath,
            markdown);

        return document.Links
            .Where(link => !MinimalRelationTypes.Contains(link.Type.Trim()))
            .Select(link => new ProjectSemanticLink
            {
                Type = link.Type.Trim(),
                Target = link.Target.Trim().Trim('"', '\'')
            })
            .GroupBy(
                link => $"{link.Type.ToLowerInvariant()}|{link.Target}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<ProjectSemanticLink> Normalize(IReadOnlyList<ProjectSemanticLink> links)
    {
        return links
            .Select(link => new ProjectSemanticLink
            {
                Type = link.Type.Trim(),
                Target = link.Target.Trim().Trim('`').Trim('"', '\'')
            })
            .Where(link => !string.IsNullOrWhiteSpace(link.Type) && !string.IsNullOrWhiteSpace(link.Target))
            .GroupBy(
                link => $"{link.Type.ToLowerInvariant()}|{link.Target}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void Validate(string project, IReadOnlyList<ProjectSemanticLink> links)
    {
        if (links.Count > MaxLinkCount)
        {
            throw new ArgumentException(
                $"SemanticLinks must contain at most {MaxLinkCount} items.",
                nameof(links));
        }

        var sourcePath = $"projects/{project.Trim()}";
        foreach (var link in links)
        {
            if (link.Type.Length > 64)
            {
                throw new ArgumentException("Semantic link type is too long.", nameof(links));
            }

            if (link.Target.Length > MaxTargetLength)
            {
                throw new ArgumentException(
                    $"Semantic link target must be at most {MaxTargetLength} characters.",
                    nameof(links));
            }

            if (MinimalRelationTypes.Contains(link.Type))
            {
                throw new ArgumentException(
                    $"Do not pass minimal link type '{link.Type}' in linksSemanticos; "
                    + "documents and belongs_to_domain are derived from projeto/dominio.",
                    nameof(links));
            }

            var normalizedType = link.Type
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            if (!KnowledgeSemanticValidation.PreferredRelationTypes.Any(preferred =>
                    preferred.Replace("-", string.Empty, StringComparison.Ordinal)
                        .Replace("_", string.Empty, StringComparison.Ordinal)
                        .Equals(normalizedType, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Legacy or non-preferred relation type '{link.Type}'. "
                    + $"Use: {string.Join(", ", KnowledgeSemanticValidation.PreferredRelationTypes)}.",
                    nameof(links));
            }

            if (link.Target.Contains("..", StringComparison.Ordinal)
                || link.Target.Contains('\\', StringComparison.Ordinal)
                || link.Target.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Semantic link targets must be knowledge-relative paths without traversal or drive prefixes.",
                    nameof(links));
            }

            if (normalizedType.Equals("evidences", StringComparison.OrdinalIgnoreCase)
                && KnowledgeEvidenceHubDetection.IsHubTarget(link.Target))
            {
                throw new ArgumentException(
                    KnowledgeEvidenceHubDetection.FormatWriterError(link.Target),
                    nameof(links));
            }

            var inverted = KnowledgeSemanticValidation.GetInvertedDependencyError(
                sourcePath,
                link.Target,
                link.Type);
            if (inverted is not null)
            {
                throw new ArgumentException(inverted, nameof(links));
            }

            ComplianceScanner.EnsureNoBlockingHits(
                (link.Type, "SemanticLinks.Type"),
                (link.Target, "SemanticLinks.Target"));
        }
    }
}
