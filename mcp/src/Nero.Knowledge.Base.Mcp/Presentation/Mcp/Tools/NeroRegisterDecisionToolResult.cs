namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroRegisterDecisionToolResult
{
    public required string Title { get; init; }

    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public string Recommendation { get; init; } = KnowledgeBatchHints.WriteRecommendation;
}
