namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminBatchStageResult
{
    public required string Stage { get; init; }

    public required string Status { get; init; }

    public required string Detail { get; init; }
}

public sealed record AdminFinalizeBatchResult
{
    public required bool Success { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required IReadOnlyList<string> ExpectedPaths { get; init; }

    public required IReadOnlyList<string> FoundMarkdownPaths { get; init; }

    public required IReadOnlyList<string> MissingMarkdownPaths { get; init; }

    public required IReadOnlyList<string> IndexedPaths { get; init; }

    public required IReadOnlyList<string> MissingIndexedPaths { get; init; }

    public AdminComplianceScanResult? Compliance { get; init; }

    public AdminReindexResult? Reindex { get; init; }

    public AdminValidationResult? Validation { get; init; }

    public required IReadOnlyList<AdminBatchStageResult> Stages { get; init; }

    public string? FailedStage { get; init; }

    public required string Recommendation { get; init; }
}
