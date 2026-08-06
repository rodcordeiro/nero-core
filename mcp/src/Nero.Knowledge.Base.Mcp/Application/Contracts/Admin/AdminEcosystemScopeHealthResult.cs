namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminEcosystemScopeHealthResult
{
    public required string Name { get; init; }

    public IReadOnlyList<AdminProjectHealthIssue> Issues { get; init; } = [];
}
