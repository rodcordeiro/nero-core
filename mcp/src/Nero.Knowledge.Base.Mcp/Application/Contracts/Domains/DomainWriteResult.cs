namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed class DomainWriteResult
{
    public required string Domain { get; init; }

    public required string Status { get; init; }

    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required string Action { get; init; }

    public required bool Created { get; init; }

    public IReadOnlyList<string> LinkedProjects { get; init; } = [];
}
