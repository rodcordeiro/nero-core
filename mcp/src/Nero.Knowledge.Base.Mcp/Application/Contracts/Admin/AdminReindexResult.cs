namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminReindexResult
{
    public required int IndexedNodes { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required string IndexDatabasePath { get; init; }
}
