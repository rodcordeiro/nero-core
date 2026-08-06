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

    /// <summary>
    /// Non-minimal preferred links (<c>uses_backend</c>, <c>depends_on</c>, …).
    /// Null/omitted preserves existing links in the Markdown; explicit list replaces; empty clears.
    /// Minimal <c>documents</c> / <c>belongs_to_domain</c> are always derived from projeto/dominio.
    /// </summary>
    public IReadOnlyList<ProjectSemanticLink>? SemanticLinks { get; init; }
}
