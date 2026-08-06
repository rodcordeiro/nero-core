namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminIndexConsistencyIssueToolResult
{
    public required string Type { get; init; }

    public required string Id { get; init; }

    public string? Path { get; init; }

    public string? IndexedUpdatedUtc { get; init; }

    public string? FileLastWriteUtc { get; init; }

    public required string Recommendation { get; init; }
}
