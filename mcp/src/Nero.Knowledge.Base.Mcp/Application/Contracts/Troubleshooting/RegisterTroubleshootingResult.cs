namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Troubleshooting;

public sealed record RegisterTroubleshootingResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
