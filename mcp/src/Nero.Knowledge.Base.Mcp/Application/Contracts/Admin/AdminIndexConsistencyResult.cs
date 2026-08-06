namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminIndexConsistencyResult
{
    public required bool IsConsistent { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required string IndexDatabasePath { get; init; }

    public required int IndexedNodeCount { get; init; }

    public required int MarkdownFileCount { get; init; }

    public required long ElapsedMilliseconds { get; init; }

    public required int ThresholdMilliseconds { get; init; }

    public required bool ExceededThreshold { get; init; }

    public required IReadOnlyList<AdminIndexConsistencyIssue> Issues { get; init; }
}
