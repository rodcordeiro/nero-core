namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed record KnowledgeProjectContextResult
{
    public required string Project { get; init; }

    public bool Exists { get; init; }

    public KnowledgeProjectContextSection? Index { get; init; }

    public KnowledgeProjectContextSection? Context { get; init; }

    public KnowledgeProjectContextSection? Patterns { get; init; }

    public KnowledgeProjectContextSection? BusinessRules { get; init; }

    public IReadOnlyList<KnowledgeProjectContextSection> Decisions { get; init; } = [];

    public IReadOnlyList<KnowledgeProjectContextSection> ActiveDecisions { get; init; } = [];

    public IReadOnlyList<KnowledgeSupersededDecision> SupersededDecisions { get; init; } = [];

    public bool HasSupersededDecisions { get; init; }

    public IReadOnlyList<KnowledgeProjectContextSection> Troubleshooting { get; init; } = [];
}
