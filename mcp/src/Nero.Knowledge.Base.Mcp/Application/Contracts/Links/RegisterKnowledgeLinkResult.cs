namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Links;

public sealed record RegisterKnowledgeLinkResult
{
    public required string EdgeId { get; init; }

    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public required string Relation { get; init; }

    public required bool Created { get; init; }
}
