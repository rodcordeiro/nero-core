namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminStatusResult
{
    public required string Server { get; init; }

    public required string RepositoryRoot { get; init; }

    public string? Branch { get; init; }

    public required bool HasModifiedFiles { get; init; }

    public required IReadOnlyList<string> ModifiedFiles { get; init; }

    public required bool IndexDatabaseExists { get; init; }

    public required string IndexDatabasePath { get; init; }

    public string? LastIndexedUtc { get; init; }

    public required string WriteMode { get; init; }
}
