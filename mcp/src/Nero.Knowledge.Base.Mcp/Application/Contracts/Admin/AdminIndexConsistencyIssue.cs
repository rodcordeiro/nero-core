namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminIndexConsistencyIssue
{
    public required string Type { get; init; }

    public required string Id { get; init; }

    public string? Path { get; init; }

    public string? IndexedUpdatedUtc { get; init; }

    public string? FileLastWriteUtc { get; init; }

    public required string Recommendation { get; init; }
}
