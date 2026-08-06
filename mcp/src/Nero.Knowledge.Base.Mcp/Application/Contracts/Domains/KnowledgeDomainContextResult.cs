namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed record KnowledgeDomainContextResult
{
    public required string Domain { get; init; }

    public bool Exists { get; init; }

    public KnowledgeDomainContextSection? Index { get; init; }

    public KnowledgeDomainContextSection? Patterns { get; init; }

    public KnowledgeDomainContextSection? BusinessRules { get; init; }

    public KnowledgeDomainContextSection? ValidationAndTests { get; init; }

    public IReadOnlyList<KnowledgeDomainProjectSummary> Projects { get; init; } = [];
}
