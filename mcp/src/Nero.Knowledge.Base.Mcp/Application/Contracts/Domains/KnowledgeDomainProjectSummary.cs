namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed record KnowledgeDomainProjectSummary
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required string Project { get; init; }
}
