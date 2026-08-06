namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminGitFetchToolResult
{
    public required bool Success { get; init; }

    public required string RepositoryRoot { get; init; }

    public string? Remote { get; init; }

    public required string Message { get; init; }

    public string? Output { get; init; }

    public string? Error { get; init; }
}
