namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

/// <summary>
/// Non-minimal preferred frontmatter link for project index/context/inventory updates.
/// </summary>
public sealed class ProjectSemanticLink
{
    public required string Type { get; init; }

    public required string Target { get; init; }
}
