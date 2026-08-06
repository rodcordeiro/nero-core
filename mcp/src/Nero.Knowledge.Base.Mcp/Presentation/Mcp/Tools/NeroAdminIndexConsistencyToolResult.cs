namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminIndexConsistencyToolResult
{
    public required bool IsConsistent { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required string IndexDatabasePath { get; init; }

    public required int IndexedNodeCount { get; init; }

    public required int MarkdownFileCount { get; init; }

    public required long ElapsedMilliseconds { get; init; }

    public required int ThresholdMilliseconds { get; init; }

    public required bool ExceededThreshold { get; init; }

    public required IReadOnlyList<NeroAdminIndexConsistencyIssueToolResult> Issues { get; init; }
}
