namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminGitFetchResult
{
    public required bool Success { get; init; }

    public required string RepositoryRoot { get; init; }

    public string? Remote { get; init; }

    public required string Message { get; init; }

    public string? Output { get; init; }

    public string? Error { get; init; }
}
