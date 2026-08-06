namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroLinkKnowledgeToolResult
{
    public required string EdgeId { get; init; }

    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public required string Relation { get; init; }

    public required bool Created { get; init; }
}
