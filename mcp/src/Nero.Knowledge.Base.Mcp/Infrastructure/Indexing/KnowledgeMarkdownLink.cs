namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed record KnowledgeMarkdownLink
{
    public required string Type { get; init; }

    public required string Target { get; init; }
}
