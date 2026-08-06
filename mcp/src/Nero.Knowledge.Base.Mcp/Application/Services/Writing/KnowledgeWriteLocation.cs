namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

public sealed record KnowledgeWriteLocation
{
    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }
}
