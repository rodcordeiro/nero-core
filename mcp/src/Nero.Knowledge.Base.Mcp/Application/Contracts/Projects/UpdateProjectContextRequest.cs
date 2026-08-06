namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed class UpdateProjectContextRequest
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string Purpose { get; init; }

    public required string Stack { get; init; }

    public required string Superficie { get; init; }

    public required string ResumoOperacional { get; init; }

    public string? SkillOperacional { get; init; }

    public string? Origin { get; init; }
}
