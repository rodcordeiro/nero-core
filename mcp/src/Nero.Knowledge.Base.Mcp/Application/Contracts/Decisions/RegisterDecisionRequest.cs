using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Decisions;

public sealed record RegisterDecisionRequest
{
    public required string Title { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Problem { get; init; }

    public required string Options { get; init; }

    public required string Decision { get; init; }

    public required string Consequences { get; init; }

    public required string Origin { get; init; }

    public IReadOnlyList<string>? Supersedes { get; init; }
}
