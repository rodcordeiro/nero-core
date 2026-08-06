namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed class UpdateProjectIndexRequest
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string Purpose { get; init; }

    public required IReadOnlyList<string> Arquivos { get; init; }

    public string? Origin { get; init; }
}
