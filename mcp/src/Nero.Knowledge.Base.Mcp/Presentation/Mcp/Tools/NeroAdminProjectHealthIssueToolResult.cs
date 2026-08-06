namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminProjectHealthIssueToolResult
{
    public required string Type { get; init; }

    public string? Path { get; init; }

    public required string Recommendation { get; init; }
}
