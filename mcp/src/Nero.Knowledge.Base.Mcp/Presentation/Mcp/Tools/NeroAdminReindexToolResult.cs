namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminReindexToolResult
{
    public required int IndexedNodes { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required string IndexDatabasePath { get; init; }

    public string Recommendation { get; init; } = KnowledgeBatchHints.ReindexRecommendation;
}
