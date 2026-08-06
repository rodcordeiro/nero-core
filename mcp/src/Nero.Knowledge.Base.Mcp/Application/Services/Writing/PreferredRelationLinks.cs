using System.Text;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

/// <summary>
/// Maps register-tool parameters to the preferred snake_case relation vocabulary for Markdown frontmatter.
/// <see cref="Supersedes"/> is a decision-only special relation (not collapsed into <see cref="Updates"/>).
/// </summary>
internal static class PreferredRelationLinks
{
    public const string BelongsToDomain = "belongs_to_domain";
    public const string Documents = "documents";
    public const string Evidences = "evidences";
    public const string RelatedDecision = "related_decision";
    public const string RelatedPattern = "related_pattern";
    public const string SourceFor = "source_for";
    /// <summary>Decision→decision only; drives active/superseded split in project context.</summary>
    public const string Supersedes = "supersedes";
    public const string Updates = "updates";

    /// <summary>
    /// Infers a preferred related-* type from the target path, defaulting to documents.
    /// </summary>
    public static string InferRelated(string target)
    {
        var normalized = Normalize(target);
        if (ContainsPathSegment(normalized, "patterns"))
        {
            return RelatedPattern;
        }

        if (ContainsPathSegment(normalized, "decisions"))
        {
            return RelatedDecision;
        }

        return Documents;
    }

    /// <summary>
    /// Maps causadoPor targets without emitting caused_by.
    /// </summary>
    public static string InferCausedBy(string target)
    {
        var normalized = Normalize(target);
        if (ContainsPathSegment(normalized, "decisions"))
        {
            return RelatedDecision;
        }

        return Documents;
    }

    /// <summary>
    /// Builds a non-empty <c>links:</c> block from scope context so content notes pass
    /// <c>nero_admin_validate</c> even when optional relation parameters are absent.
    /// </summary>
    public static string RenderMinimalScopeLinks(
        KnowledgeScope scope,
        string? domain,
        string? project,
        Func<string, string> escapeYaml)
    {
        ArgumentNullException.ThrowIfNull(escapeYaml);

        var links = new List<(string Type, string Target)>();
        switch (scope)
        {
            case KnowledgeScope.Global:
                links.Add((Documents, "global"));
                break;
            case KnowledgeScope.Domain:
                {
                    var domainName = domain!.Trim();
                    links.Add((BelongsToDomain, $"domains/{domainName}"));
                    links.Add((Documents, $"domains/{domainName}"));
                    break;
                }
            case KnowledgeScope.Project:
                {
                    var projectName = project!.Trim();
                    links.Add((Documents, $"projects/{projectName}/index"));
                    if (!string.IsNullOrWhiteSpace(domain))
                    {
                        links.Add((BelongsToDomain, $"domains/{domain.Trim()}"));
                    }

                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Scope must be supported.");
        }

        var builder = new StringBuilder("links:\n");
        foreach (var (type, target) in links)
        {
            builder.Append("  - type: ");
            builder.AppendLine(type);
            builder.Append("    target: \"");
            builder.Append(escapeYaml(target));
            builder.AppendLine("\"");
        }

        return builder.ToString();
    }

    private static string Normalize(string target)
    {
        return target.Trim().Replace('\\', '/').Trim('/');
    }

    private static bool ContainsPathSegment(string normalizedPath, string segment)
    {
        foreach (var part in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
