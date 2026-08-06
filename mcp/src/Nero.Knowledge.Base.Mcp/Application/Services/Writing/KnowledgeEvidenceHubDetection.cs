namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

/// <summary>
/// Shared G4 hub heuristics for <c>evidences</c> targets: writers reject hubs at register time;
/// <c>nero_admin_validate</c> rejects hubs already present in Markdown.
/// </summary>
/// <remarks>
/// Path-only checks (no document graph). Prefer false negatives when classification is ambiguous.
/// </remarks>
internal static class KnowledgeEvidenceHubDetection
{
    /// <summary>
    /// Directory-style hubs / category folders — not concrete note ids.
    /// </summary>
    public static readonly IReadOnlySet<string> HubFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "patterns",
        "decisions",
        "snapshots",
        "business-rules",
        "troubleshooting",
        "validation-and-tests",
        "index",
        "context"
    };

    /// <summary>
    /// Returns true when <paramref name="target"/> looks like a category/directory hub, not a concrete note slug.
    /// </summary>
    public static bool IsHubTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var normalized = NormalizeKnowledgePath(target);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (HubFolderNames.Contains(segments[^1]))
        {
            return true;
        }

        return IsTooShallowForConcreteNote(segments);
    }

    /// <summary>Message used by validate (includes source note id).</summary>
    public static string FormatValidateError(string sourceId, string target) =>
        $"Relation 'evidences' in '{sourceId}' → '{target}' targets a directory hub or generic folder, " +
        "not a concrete note. Point evidences at a note slug " +
        "(e.g. domains/api/patterns/some-pattern or projects/X/decisions/yyyy-mm-dd-…).";

    /// <summary>Message used by writers (parameter-focused).</summary>
    public static string FormatWriterError(string target) =>
        $"Evidences target '{target}' is a directory hub or generic folder, not a concrete note. " +
        "Point evidenciaDe at a note slug " +
        "(e.g. domains/api/patterns/some-pattern or projects/X/decisions/yyyy-mm-dd-…).";

    private static bool IsTooShallowForConcreteNote(string[] segments)
    {
        // domains/{domain}/… needs category + note → ≥ 4 segments.
        if (segments.Length >= 1
            && segments[0].Equals("domains", StringComparison.OrdinalIgnoreCase)
            && segments.Length < 4)
        {
            return true;
        }

        // projects/{project}/… needs category + note → ≥ 4 segments.
        if (segments.Length >= 1
            && segments[0].Equals("projects", StringComparison.OrdinalIgnoreCase)
            && segments.Length < 4)
        {
            return true;
        }

        // global/… needs category + note → ≥ 3 segments (global/patterns/slug).
        if (segments.Length >= 1
            && segments[0].Equals("global", StringComparison.OrdinalIgnoreCase)
            && segments.Length < 3)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeKnowledgePath(string pathOrId) =>
        pathOrId.Trim().Replace('\\', '/').Trim('/');
}
