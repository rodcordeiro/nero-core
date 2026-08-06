namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroUpdateProjectFileToolResult
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string FileKind { get; init; }

    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required bool Created { get; init; }

    public string Recommendation { get; init; } = KnowledgeBatchHints.WriteRecommendation;
}
