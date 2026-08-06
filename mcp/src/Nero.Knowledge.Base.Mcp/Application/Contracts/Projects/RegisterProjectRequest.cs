namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed record RegisterProjectRequest
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string Purpose { get; init; }

    public required string Origin { get; init; }
}
