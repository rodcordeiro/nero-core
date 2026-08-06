using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

/// <summary>
/// Semantic guardrails for <c>nero_admin_validate</c>: preferred relation vocabulary and required links.
/// </summary>
/// <remarks>
/// G3 (direction): validate <c>depends_on</c> / <c>uses_backend</c> orientation (API/lib must not point at consumers).
/// G4 (evidences hubs): reject <c>evidences</c> targets that are directories or generic hubs (e.g. <c>domains/*/patterns</c>).
/// Prefer false negatives over blocking legitimate edges when role/hub classification is ambiguous.
/// </remarks>
internal static class KnowledgeSemanticValidation
{
    /// <summary>Preferred snake_case relation vocabulary emitted by register tools.</summary>
    public static readonly IReadOnlySet<string> PreferredRelationTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "belongs_to_domain",
        "documents",
        "evidences",
        "updates",
        "depends_on",
        "uses_backend",
        "related_decision",
        "related_pattern",
        "source_for"
    };

    private static readonly HashSet<string> PreferredNormalized = new(
        PreferredRelationTypes.Select(NormalizeRelation),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ContentPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "decisions",
        "patterns",
        "business-rules",
        "troubleshooting",
        "snapshots",
        "validation-and-tests"
    };

    /// <summary>
    /// Validates preferred relation vocabulary and required <c>links:</c> on content notes.
    /// </summary>
    public static IReadOnlyList<string> Validate(IReadOnlyCollection<KnowledgeMarkdownDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var errors = new List<string>();
        var nodesById = documents.ToDictionary(document => document.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents.OrderBy(document => document.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (RequiresLinks(document) && document.Links.Count == 0)
            {
                errors.Add(
                    $"Note '{document.Id}' (path '{document.Path}') is missing a non-empty links: block.");
            }

            foreach (var link in document.Links)
            {
                errors.AddRange(ValidateLink(document, link, nodesById));
            }
        }

        return errors;
    }

    private static IEnumerable<string> ValidateLink(
        KnowledgeMarkdownDocument source,
        KnowledgeMarkdownLink link,
        IReadOnlyDictionary<string, KnowledgeMarkdownDocument> nodesById)
    {
        var relation = link.Type.Trim();
        var normalized = NormalizeRelation(relation);

        // G3: direction checks on preferred depends_on / uses_backend (before generic preferred pass-through).
        if (normalized.Equals("dependson", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("usesbackend", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var error in ValidateDependencyDirection(source, link, relation))
            {
                yield return error;
            }

            yield break;
        }

        // G4: evidences must point at concrete notes, not directory hubs.
        if (normalized.Equals("evidences", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var error in ValidateEvidencesTarget(source, link, nodesById))
            {
                yield return error;
            }

            yield break;
        }

        if (PreferredNormalized.Contains(normalized))
        {
            yield break;
        }

        if (normalized.Equals("supersedes", StringComparison.OrdinalIgnoreCase))
        {
            if (IsDecisionLike(source) && IsDecisionLikeTarget(link.Target, nodesById))
            {
                yield break;
            }

            yield return
                $"Relation 'supersedes' in '{source.Id}' → '{link.Target}' is only allowed decision→decision " +
                "(source and target path/id must contain '/decisions/' or be type decision).";
            yield break;
        }

        yield return
            $"Legacy or non-preferred relation type '{relation}' in '{source.Id}' → '{link.Target}'. " +
            $"Use preferred vocabulary: {string.Join(", ", PreferredRelationTypes)} " +
            "(special allow: supersedes only for decision→decision).";
    }

    /// <summary>
    /// G3 — reject inverted dependency edges when both ends have clear roles.
    /// Heuristic (pragmatic, prefer false negatives):
    /// - Consumer: Front/Mobile/.Web project tokens, domains/front, domains/mobile.
    /// - Backend: .API/.Api/.Lib project tokens, domains/api, packages/.
    /// - Ambiguous (mixed or no signal): do not reject.
    /// Inverted = Backend source → Consumer target for depends_on or uses_backend.
    /// Consumer → backend (uses_backend / depends_on toward API) is allowed.
    /// </summary>
    private static IEnumerable<string> ValidateDependencyDirection(
        KnowledgeMarkdownDocument source,
        KnowledgeMarkdownLink link,
        string relation)
    {
        var sourceRole = ClassifyNodeRole(source.Id, source.Path);
        var targetRole = ClassifyNodeRole(link.Target, link.Target);

        if (sourceRole == NodeRole.Backend && targetRole == NodeRole.Consumer)
        {
            yield return
                $"Relation '{relation}' in '{source.Id}' → '{link.Target}' looks inverted " +
                "(API/backend/lib must not depend_on or uses_backend a Front/Mobile consumer). " +
                "Prefer consumer → backend orientation.";
        }
    }

    /// <summary>
    /// G4 — reject evidences whose target is a category/directory hub, not a concrete note.
    /// Path heuristics live in <see cref="KnowledgeEvidenceHubDetection"/> (shared with writers).
    /// When a target resolves to an index-typed document, treat as hub.
    /// </summary>
    private static IEnumerable<string> ValidateEvidencesTarget(
        KnowledgeMarkdownDocument source,
        KnowledgeMarkdownLink link,
        IReadOnlyDictionary<string, KnowledgeMarkdownDocument> nodesById)
    {
        foreach (var candidate in ExpandTargetCandidates(link.Target))
        {
            if (nodesById.TryGetValue(candidate, out var resolved)
                && IsDirectoryStyleHubDocument(resolved))
            {
                yield return KnowledgeEvidenceHubDetection.FormatValidateError(source.Id, link.Target);
                yield break;
            }
        }

        if (KnowledgeEvidenceHubDetection.IsHubTarget(link.Target))
        {
            yield return KnowledgeEvidenceHubDetection.FormatValidateError(source.Id, link.Target);
        }
    }

    private static bool IsDirectoryStyleHubDocument(KnowledgeMarkdownDocument document)
    {
        // Only strong hub types — KnowledgeNodeType.Context is the parser fallback for unknowns.
        if (document.Type is KnowledgeNodeType.Index or KnowledgeNodeType.ProjectContext)
        {
            return true;
        }

        var idSegments = NormalizeKnowledgePath(document.Id)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return idSegments.Length > 0
            && KnowledgeEvidenceHubDetection.HubFolderNames.Contains(idSegments[^1]);
    }

    private enum NodeRole
    {
        Ambiguous,
        Consumer,
        Backend
    }

    private static NodeRole ClassifyNodeRole(string primary, string secondary)
    {
        var consumer = LooksLikeConsumer(primary) || LooksLikeConsumer(secondary);
        var backend = LooksLikeBackend(primary) || LooksLikeBackend(secondary);

        // Mixed signals (e.g. odd project names) → ambiguous → do not reject.
        if (consumer && backend)
        {
            return NodeRole.Ambiguous;
        }

        if (consumer)
        {
            return NodeRole.Consumer;
        }

        if (backend)
        {
            return NodeRole.Backend;
        }

        return NodeRole.Ambiguous;
    }

    private static bool LooksLikeConsumer(string pathOrId)
    {
        if (string.IsNullOrWhiteSpace(pathOrId))
        {
            return false;
        }

        var normalized = NormalizeKnowledgePath(pathOrId);

        // Domain folders for front/mobile consumers (whole path prefixes only).
        if (HasPathPrefix(normalized, "domains/front")
            || HasPathPrefix(normalized, "domains/mobile"))
        {
            return true;
        }

        return ProjectTokenLooksLikeConsumer(normalized);
    }

    private static bool LooksLikeBackend(string pathOrId)
    {
        if (string.IsNullOrWhiteSpace(pathOrId))
        {
            return false;
        }

        var normalized = NormalizeKnowledgePath(pathOrId);

        if (HasPathPrefix(normalized, "domains/api")
            || HasPathPrefix(normalized, "packages/")
            || HasPathSegment(normalized, "packages"))
        {
            return true;
        }

        return ProjectTokenLooksLikeBackend(normalized);
    }

    /// <summary>
    /// Inspects project folder tokens (e.g. Acme.Auth.Api, Acme.App.Front) in the path/id.
    /// </summary>
    private static bool ProjectTokenLooksLikeConsumer(string normalizedPath)
    {
        foreach (var token in EnumerateProjectTokens(normalizedPath))
        {
            if (TokenHasSuffix(token, ".Front")
                || TokenHasSuffix(token, ".Mobile")
                || TokenHasSuffix(token, ".Web")
                || token.Equals("Front", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Mobile", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProjectTokenLooksLikeBackend(string normalizedPath)
    {
        foreach (var token in EnumerateProjectTokens(normalizedPath))
        {
            if (TokenHasSuffix(token, ".API")
                || TokenHasSuffix(token, ".Api")
                || TokenHasSuffix(token, ".Lib")
                || TokenHasSuffix(token, ".Backend")
                || token.Equals("API", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Api", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateProjectTokens(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            yield return segments[i];

            // projects/{ProjectName}/...
            if (i > 0 && segments[i - 1].Equals("projects", StringComparison.OrdinalIgnoreCase))
            {
                yield return segments[i];
            }
        }
    }

    private static bool TokenHasSuffix(string token, string suffix) =>
        token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool HasPathPrefix(string normalizedPath, string prefix) =>
        normalizedPath.Equals(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
        || normalizedPath.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresLinks(KnowledgeMarkdownDocument document)
    {
        if (document.Type is KnowledgeNodeType.Decision
            or KnowledgeNodeType.Pattern
            or KnowledgeNodeType.BusinessRule
            or KnowledgeNodeType.Troubleshooting
            or KnowledgeNodeType.Snapshot
            or KnowledgeNodeType.ValidationRule)
        {
            return true;
        }

        return HasContentPathSegment(document.Id) || HasContentPathSegment(document.Path);
    }

    private static bool IsDecisionLike(KnowledgeMarkdownDocument document)
    {
        return document.Type == KnowledgeNodeType.Decision
            || HasPathSegment(document.Id, "decisions")
            || HasPathSegment(document.Path, "decisions");
    }

    private static bool IsDecisionLikeTarget(
        string target,
        IReadOnlyDictionary<string, KnowledgeMarkdownDocument> nodesById)
    {
        if (HasPathSegment(target, "decisions"))
        {
            return true;
        }

        foreach (var candidate in ExpandTargetCandidates(target))
        {
            if (nodesById.TryGetValue(candidate, out var document) && IsDecisionLike(document))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExpandTargetCandidates(string target)
    {
        var normalized = NormalizeKnowledgePath(target);
        yield return normalized;

        if (normalized.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized["knowledge/".Length..];
        }

        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized[..^3];
            if (normalized.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase))
            {
                yield return normalized["knowledge/".Length..^3];
            }
        }
    }

    private static string NormalizeKnowledgePath(string pathOrId) =>
        pathOrId.Trim().Replace('\\', '/').Trim('/');

    private static bool HasContentPathSegment(string pathOrId)
    {
        foreach (var segment in pathOrId.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ContentPathSegments.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPathSegment(string pathOrId, string segment)
    {
        foreach (var part in pathOrId.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeRelation(string relation)
    {
        return relation
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }
}
