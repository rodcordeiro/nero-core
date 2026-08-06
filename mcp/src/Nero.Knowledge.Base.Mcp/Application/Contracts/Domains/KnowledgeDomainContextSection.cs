using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed record KnowledgeDomainContextSection
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required KnowledgeNodeType Type { get; init; }

    public required string Content { get; init; }
}
