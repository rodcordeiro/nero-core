namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

public sealed class UpdateProjectInventoryRequest
{
    public required string Project { get; init; }

    public required string Domain { get; init; }

    public required string ReviewedAt { get; init; }

    public required string Classificacao { get; init; }

    public string? GitBranch { get; init; }

    public string? GitHead { get; init; }

    public string? GitRemote { get; init; }

    public required IReadOnlyList<string> SinaisTecnicos { get; init; }

    public string? Origin { get; init; }
}
