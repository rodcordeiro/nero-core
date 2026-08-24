namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminBatchStageToolResult
{
    public required string Stage { get; init; }

    public required string Status { get; init; }

    public required string Detail { get; init; }
}

public sealed record NeroAdminFinalizeBatchToolResult
{
    public required bool Success { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required IReadOnlyList<string> ExpectedPaths { get; init; }

    public required IReadOnlyList<string> FoundMarkdownPaths { get; init; }

    public required IReadOnlyList<string> MissingMarkdownPaths { get; init; }

    public required IReadOnlyList<string> IndexedPaths { get; init; }

    public required IReadOnlyList<string> MissingIndexedPaths { get; init; }

    public bool? IsCompliant { get; init; }

    public int? ActiveBlockingHitCount { get; init; }

    public int? WarningHitCount { get; init; }

    public IReadOnlyList<NeroAdminComplianceScanIssueToolResult> ActiveComplianceHits { get; init; } = [];

    public int? IndexedNodes { get; init; }

    public bool? IsValid { get; init; }

    public int? NodeCount { get; init; }

    public int? EdgeCount { get; init; }

    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    public IReadOnlyList<string> ComplianceGaps { get; init; } = [];

    public required IReadOnlyList<NeroAdminBatchStageToolResult> Stages { get; init; }

    public string? FailedStage { get; init; }

    public required string Recommendation { get; init; }
}
