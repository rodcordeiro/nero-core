using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;

public sealed record RegisterBusinessRuleRequest
{
    public required string Title { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Rule { get; init; }

    public required string Evidence { get; init; }

    public required string Origin { get; init; }
}
