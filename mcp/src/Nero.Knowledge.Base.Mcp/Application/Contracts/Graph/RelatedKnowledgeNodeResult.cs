using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Graph;

public sealed record RelatedKnowledgeNodeResult
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public required KnowledgeNodeType Type { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required KnowledgeRelationType Relation { get; init; }

    public required string Evidence { get; init; }

    public decimal Score { get; init; }
}
