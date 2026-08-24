using System.Globalization;
using System.Text.RegularExpressions;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed partial class AdminTrustAuditService(
    KnowledgeRootOptions knowledgeRootOptions,
    KnowledgeMarkdownReader markdownReader,
    AdminProjectFreshnessOptions freshnessOptions)
{
    public const int DefaultArchiveCandidateDays = 365;

    /// <summary>
    /// Audits trust signals without changing Markdown or the derived index.
    /// </summary>
    public async Task<AdminTrustAuditResult> AuditAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var documents = await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken);
        var issues = documents
            .SelectMany(document => AuditDocument(document, asOfDate))
            .OrderBy(issue => issue.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Type, StringComparer.Ordinal)
            .ToArray();

        return new AdminTrustAuditResult
        {
            KnowledgeRootPath = knowledgeRootPath,
            AsOfDate = asOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ScannedFileCount = documents.Count,
            Issues = issues
        };
    }

    private IEnumerable<AdminTrustAuditIssue> AuditDocument(
        KnowledgeMarkdownDocument document,
        DateOnly asOfDate)
    {
        if (document.Type == KnowledgeNodeType.Index)
        {
            yield break;
        }

        if (IsClaimBearing(document.Type) && !HasSource(document))
        {
            yield return Issue(
                "MissingSource",
                document.Path,
                "No non-empty origin or sources trust signal was found.",
                "Add an evidence-backed origin or sources value; do not invent a source.");
        }

        var verificationStatus = GetFrontmatter(document, "verification_status");
        if (string.Equals(verificationStatus, "unverified", StringComparison.OrdinalIgnoreCase)
            || string.Equals(verificationStatus, "never_verified", StringComparison.OrdinalIgnoreCase))
        {
            yield return Issue(
                "NeverVerified",
                document.Path,
                $"verification_status is {verificationStatus} and no completed verification is recorded.",
                "Review the note and record opt-in verification metadata when evidence is available.");
        }

        if (string.Equals(verificationStatus, "unverifiable", StringComparison.OrdinalIgnoreCase))
        {
            yield return Issue(
                "UnverifiableClaim",
                document.Path,
                "verification_status is unverifiable.",
                "Keep the limitation explicit and seek an independent source before promotion.");
        }

        if (document.Type != KnowledgeNodeType.Snapshot || !TryGetSnapshotDate(document, out var snapshotDate))
        {
            yield break;
        }

        var ageDays = asOfDate.DayNumber - snapshotDate.DayNumber;
        if (ageDays > freshnessOptions.RecentSnapshotDays)
        {
            yield return Issue(
                "StaleSnapshot",
                document.Path,
                $"Snapshot is {ageDays} days old; freshness threshold is {freshnessOptions.RecentSnapshotDays} days.",
                "Review the source and create a new snapshot if the context is still operationally relevant.");
        }

        if (ageDays >= DefaultArchiveCandidateDays)
        {
            yield return Issue(
                "ArchiveCandidate",
                document.Path,
                $"Snapshot is {ageDays} days old; archive-candidate threshold is {DefaultArchiveCandidateDays} days.",
                "Review manually before archiving; the audit never moves or deletes files.");
        }
    }

    private static bool HasSource(KnowledgeMarkdownDocument document) =>
        !string.IsNullOrWhiteSpace(GetFrontmatter(document, "origin"))
        || !string.IsNullOrWhiteSpace(GetFrontmatter(document, "sources"));

    private static bool IsClaimBearing(KnowledgeNodeType type) => type is
        KnowledgeNodeType.BusinessRule
        or KnowledgeNodeType.Decision
        or KnowledgeNodeType.Pattern
        or KnowledgeNodeType.Snapshot
        or KnowledgeNodeType.Troubleshooting
        or KnowledgeNodeType.ValidationRule;

    private static string? GetFrontmatter(KnowledgeMarkdownDocument document, string key) =>
        document.Frontmatter.TryGetValue(key, out var value) ? value : null;

    private static bool TryGetSnapshotDate(KnowledgeMarkdownDocument document, out DateOnly date)
    {
        date = default;
        var fileName = Path.GetFileNameWithoutExtension(document.Path);
        var match = SnapshotDateRegex().Match(fileName);
        return match.Success
            && DateOnly.TryParseExact(
                match.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
    }

    private static AdminTrustAuditIssue Issue(
        string type,
        string path,
        string reason,
        string recommendation) => new()
        {
            Type = type,
            Path = path,
            Reason = reason,
            Recommendation = recommendation
        };

    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex SnapshotDateRegex();
}
