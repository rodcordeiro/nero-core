using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Search;

public sealed record KnowledgeSearchResult
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public required KnowledgeNodeType Type { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Snippet { get; init; }

    public double Rank { get; init; }
}
