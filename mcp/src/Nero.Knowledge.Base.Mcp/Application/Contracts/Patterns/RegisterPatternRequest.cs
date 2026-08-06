using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Patterns;

public sealed record RegisterPatternRequest
{
    public required string Title { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Context { get; init; }

    public required string Pattern { get; init; }

    public required string WhenToApply { get; init; }

    public required string WhenNotToApply { get; init; }

    public string? Exceptions { get; init; }

    public IReadOnlyList<string>? Examples { get; init; }

    public required string Origin { get; init; }

    public IReadOnlyList<string>? UsedBy { get; init; }

    public IReadOnlyList<string>? CandidateForReuse { get; init; }
}
