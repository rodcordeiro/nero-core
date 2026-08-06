namespace Nero.Knowledge.Base.Mcp.Application.Contracts.ValidationRules;

public sealed record RegisterValidationRuleResult
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Title { get; init; }
}
