namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;

public sealed record RegisterSnapshotResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
