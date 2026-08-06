using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;

public sealed record RegisterSnapshotRequest
{
    public required string Title { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Context { get; init; }

    public required string Evidence { get; init; }

    public required string Origin { get; init; }

    public IReadOnlyList<string>? RelatesTo { get; init; }

    public IReadOnlyList<string>? Evidences { get; init; }
}
