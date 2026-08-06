namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroRegisterProjectToolResult
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required bool Created { get; init; }

    public required string ProjectDirectoryPath { get; init; }

    public required string ProjectRelativePath { get; init; }

    public required string IndexPath { get; init; }

    public required string ContextPath { get; init; }

    public required IReadOnlyList<string> CreatedFiles { get; init; }

    public string Recommendation { get; init; } = KnowledgeBatchHints.WriteRecommendation;
}
