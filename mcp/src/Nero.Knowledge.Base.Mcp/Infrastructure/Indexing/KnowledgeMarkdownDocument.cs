using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed record KnowledgeMarkdownDocument
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required string Content { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public required KnowledgeNodeType Type { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public IReadOnlyDictionary<string, string> Frontmatter { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<KnowledgeMarkdownLink> Links { get; init; } = [];
}
