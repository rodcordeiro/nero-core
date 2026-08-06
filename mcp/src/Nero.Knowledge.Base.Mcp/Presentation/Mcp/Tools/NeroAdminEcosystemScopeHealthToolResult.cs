namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminEcosystemScopeHealthToolResult
{
    public required string Name { get; init; }

    public IReadOnlyList<NeroAdminProjectHealthIssueToolResult> Issues { get; init; } = [];
}
