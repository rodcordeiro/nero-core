namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminGitCreateCommitResult
{
    public required bool Success { get; init; }

    public required string RepositoryRoot { get; init; }

    public string? CommitSha { get; init; }

    public required IReadOnlyList<string> Paths { get; init; }

    public required string Message { get; init; }

    public string? Output { get; init; }

    public string? Error { get; init; }
}
