namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Links;

public sealed record RegisterKnowledgeLinkRequest
{
    public required string Source { get; init; }

    public required string Target { get; init; }

    public required string Relation { get; init; }

    public decimal Confidence { get; init; } = 1m;

    public string Evidence { get; init; } = string.Empty;
}
