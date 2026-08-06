namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroSearchKnowledgeToolResult
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required string Scope { get; init; }

    public required string Type { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Snippet { get; init; }
}
