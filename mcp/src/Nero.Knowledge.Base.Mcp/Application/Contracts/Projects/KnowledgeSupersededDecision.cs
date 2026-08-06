namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed record KnowledgeSupersededDecision
{
    public required KnowledgeProjectContextSection Decision { get; init; }

    public required IReadOnlyList<KnowledgeProjectContextSection> SupersededBy { get; init; }
}
