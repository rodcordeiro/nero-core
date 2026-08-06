namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Decisions;

public sealed record RegisterDecisionResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
