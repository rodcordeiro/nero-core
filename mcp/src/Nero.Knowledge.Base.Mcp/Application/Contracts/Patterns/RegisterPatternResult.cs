namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Patterns;

public sealed record RegisterPatternResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
