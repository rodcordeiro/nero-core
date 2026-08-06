namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed record RegisterProjectResult
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string ProjectDirectoryPath { get; init; }

    public required string ProjectRelativePath { get; init; }

    public required string IndexPath { get; init; }

    public required string ContextPath { get; init; }

    public required IReadOnlyList<string> CreatedFiles { get; init; }

    public required bool Created { get; init; }
}
