using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Microsoft.Extensions.Logging;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed class AdminBatchFinalizationService(
    KnowledgeRootOptions knowledgeRootOptions,
    KnowledgeMarkdownReader markdownReader,
    IAdminBatchOperations operations,
    ILogger<AdminBatchFinalizationService>? logger = null)
{
    public const int MaximumExpectedPaths = 100;

    /// <summary>
    /// Finalizes an explicit Markdown write batch through compliance, reindex, validation and index evidence.
    /// </summary>
    public async Task<AdminFinalizeBatchResult> FinalizeAsync(
        IReadOnlyCollection<string> expectedPaths,
        CancellationToken cancellationToken = default)
    {
        var normalizedPaths = NormalizeExpectedPaths(expectedPaths);
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var documents = await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken);
        var markdownPaths = documents
            .Select(document => NormalizeDocumentPath(document.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foundPaths = normalizedPaths.Where(markdownPaths.Contains).ToArray();
        var missingPaths = normalizedPaths.Where(path => !markdownPaths.Contains(path)).ToArray();
        var stages = new List<AdminBatchStageResult>();

        if (missingPaths.Length > 0)
        {
            stages.Add(Stage("Files", "Failed", $"{missingPaths.Length} expected Markdown file(s) were not found."));
            AddSkipped(stages, "Compliance", "Reindex", "Validation", "IndexEvidence");
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, missingPaths, [], [],
                stages, "Files", "Create or correct every missing expected path, then retry nero_admin_finalize_batch.");
        }

        stages.Add(Stage("Files", "Succeeded", $"Found all {foundPaths.Length} expected Markdown file(s)."));
        cancellationToken.ThrowIfCancellationRequested();

        AdminComplianceScanResult compliance;
        try
        {
            compliance = await operations.ScanComplianceAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Batch finalization failed during compliance.");
            stages.Add(Stage("Compliance", "Failed", "Compliance scan failed operationally; inspect server logs."));
            AddSkipped(stages, "Reindex", "Validation", "IndexEvidence");
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, [], [], [], stages, "Compliance",
                "Resolve the compliance scan failure, then retry nero_admin_finalize_batch.");
        }
        if (!compliance.IsCompliant)
        {
            stages.Add(Stage("Compliance", "Failed", $"Found {compliance.ActiveBlockingHitCount} active blocking compliance hit(s)."));
            AddSkipped(stages, "Reindex", "Validation", "IndexEvidence");
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, [], [], [], stages, "Compliance",
                "Fix or quarantine active P0 compliance hits, then retry nero_admin_finalize_batch.", compliance);
        }

        stages.Add(Stage("Compliance", "Succeeded", "No active blocking compliance hits were found."));
        cancellationToken.ThrowIfCancellationRequested();

        AdminReindexResult reindex;
        try
        {
            reindex = await operations.ReindexAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Batch finalization failed during reindex.");
            stages.Add(Stage("Reindex", "Failed", "Reindex failed operationally; inspect server logs."));
            AddSkipped(stages, "Validation", "IndexEvidence");
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, [], [], [], stages, "Reindex",
                "Resolve the reindex failure, then retry nero_admin_finalize_batch. Markdown was not changed by finalization.",
                compliance);
        }
        stages.Add(Stage("Reindex", "Succeeded", $"Indexed {reindex.IndexedNodes} node(s)."));
        cancellationToken.ThrowIfCancellationRequested();

        AdminValidationResult validation;
        try
        {
            validation = await operations.ValidateAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Batch finalization failed during validation.");
            stages.Add(Stage("Validation", "Failed", "Validation failed operationally; inspect server logs."));
            AddSkipped(stages, "IndexEvidence");
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, [], [], [], stages, "Validation",
                "Resolve the validation failure, then retry nero_admin_finalize_batch. The derived index was already replaced.",
                compliance,
                reindex);
        }
        var validationSucceeded = validation.IsValid && validation.IsCompliant;
        stages.Add(Stage(
            "Validation",
            validationSucceeded ? "Succeeded" : "Failed",
            validationSucceeded
                ? $"Validated {validation.NodeCount} node(s) and {validation.EdgeCount} edge(s)."
                : $"Validation returned {validation.Errors.Count} structural error(s) and {validation.ComplianceGaps.Count} compliance gap(s)."));
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlySet<string> indexedPathSet;
        try
        {
            indexedPathSet = await operations.ReadIndexedPathsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Batch finalization failed while reading index evidence.");
            stages.Add(Stage("IndexEvidence", "Failed", "Index evidence query failed operationally; inspect server logs."));
            return Result(
                false, knowledgeRootPath, normalizedPaths, foundPaths, [], [], normalizedPaths, stages, "IndexEvidence",
                "Resolve the SQLite evidence failure, then retry nero_admin_finalize_batch. The derived index was already replaced.",
                compliance,
                reindex,
                validation);
        }
        var indexedPaths = normalizedPaths.Where(indexedPathSet.Contains).ToArray();
        var missingIndexedPaths = normalizedPaths.Where(path => !indexedPathSet.Contains(path)).ToArray();
        var indexEvidenceSucceeded = missingIndexedPaths.Length == 0;
        stages.Add(Stage(
            "IndexEvidence",
            indexEvidenceSucceeded ? "Succeeded" : "Failed",
            indexEvidenceSucceeded
                ? $"Confirmed all {indexedPaths.Length} expected path(s) in SQLite."
                : $"{missingIndexedPaths.Length} expected path(s) were not found in SQLite."));

        var success = validationSucceeded && indexEvidenceSucceeded;
        var failedStage = !validationSucceeded ? "Validation" : !indexEvidenceSucceeded ? "IndexEvidence" : null;
        return Result(
            success,
            knowledgeRootPath,
            normalizedPaths,
            foundPaths,
            [],
            indexedPaths,
            missingIndexedPaths,
            stages,
            failedStage,
            success
                ? "Batch evidence passed. Review the explicit paths before creating a commit."
                : failedStage == "Validation"
                    ? "Fix validation gaps in Markdown, then retry nero_admin_finalize_batch. The derived index was already replaced."
                    : "Review quarantined or non-indexable expected files, then retry nero_admin_finalize_batch.",
            compliance,
            reindex,
            validation);
    }

    private static IReadOnlyList<string> NormalizeExpectedPaths(IReadOnlyCollection<string> expectedPaths)
    {
        ArgumentNullException.ThrowIfNull(expectedPaths);
        if (expectedPaths.Count is < 1 or > MaximumExpectedPaths)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedPaths),
                $"Expected between 1 and {MaximumExpectedPaths} Markdown paths.");
        }

        var normalized = new List<string>(expectedPaths.Count);
        foreach (var rawPath in expectedPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawPath, nameof(expectedPaths));
            var path = rawPath.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(path)
                || path.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
                || path.Split('/').Any(segment => segment is "." or "..")
                || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || !(path.StartsWith("global/", StringComparison.OrdinalIgnoreCase)
                     || path.StartsWith("domains/", StringComparison.OrdinalIgnoreCase)
                     || path.StartsWith("projects/", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    "Each expected path must be a relative Markdown path under global/, domains/ or projects/, without knowledge/, '.' or '..' segments.",
                    nameof(expectedPaths));
            }

            normalized.Add(path);
        }

        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
        {
            throw new ArgumentException("Expected paths must be unique.", nameof(expectedPaths));
        }

        return normalized.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeDocumentPath(string path) =>
        (path.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
            ? path["knowledge/".Length..]
            : path).Replace('\\', '/');

    private static AdminBatchStageResult Stage(string stage, string status, string detail) => new()
    {
        Stage = stage,
        Status = status,
        Detail = detail
    };

    private static void AddSkipped(ICollection<AdminBatchStageResult> stages, params string[] names)
    {
        foreach (var name in names)
        {
            stages.Add(Stage(name, "Skipped", "Skipped because an earlier stage failed."));
        }
    }

    private static AdminFinalizeBatchResult Result(
        bool success,
        string root,
        IReadOnlyList<string> expected,
        IReadOnlyList<string> found,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> indexed,
        IReadOnlyList<string> missingIndexed,
        IReadOnlyList<AdminBatchStageResult> stages,
        string? failedStage,
        string recommendation,
        AdminComplianceScanResult? compliance = null,
        AdminReindexResult? reindex = null,
        AdminValidationResult? validation = null) => new()
        {
            Success = success,
            KnowledgeRootPath = root,
            ExpectedPaths = expected,
            FoundMarkdownPaths = found,
            MissingMarkdownPaths = missing,
            IndexedPaths = indexed,
            MissingIndexedPaths = missingIndexed,
            Compliance = compliance,
            Reindex = reindex,
            Validation = validation,
            Stages = stages,
            FailedStage = failedStage,
            Recommendation = recommendation
        };
}
