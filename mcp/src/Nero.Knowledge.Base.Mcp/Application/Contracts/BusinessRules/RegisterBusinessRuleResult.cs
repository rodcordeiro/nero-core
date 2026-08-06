namespace Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;

public sealed record RegisterBusinessRuleResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
