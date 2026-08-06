namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed class UpdateProjectFileResult
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string FileKind { get; init; }

    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required bool Created { get; init; }
}
