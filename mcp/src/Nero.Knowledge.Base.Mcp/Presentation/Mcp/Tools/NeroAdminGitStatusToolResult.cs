namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminGitStatusToolResult
{
    public required string RepositoryRoot { get; init; }

    public string? Branch { get; init; }

    public required bool HasRemote { get; init; }

    public string? Remote { get; init; }

    public string? Upstream { get; init; }

    public int? Ahead { get; init; }

    public int? Behind { get; init; }

    public string? LocalHead { get; init; }

    public string? RemoteHead { get; init; }

    public required bool HasModifiedFiles { get; init; }

    public required IReadOnlyList<string> ModifiedFiles { get; init; }
}
