namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminProjectHealthIssue
{
    public required string Type { get; init; }

    public string? Path { get; init; }

    public required string Recommendation { get; init; }
}
