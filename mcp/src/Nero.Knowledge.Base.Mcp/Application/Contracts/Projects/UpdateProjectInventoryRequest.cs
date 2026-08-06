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

    /// <summary>
    /// Non-minimal preferred links (<c>uses_backend</c>, <c>depends_on</c>, …).
    /// Null/omitted preserves existing links in the Markdown; explicit list replaces; empty clears.
    /// Minimal <c>documents</c> / <c>belongs_to_domain</c> are always derived from projeto/dominio.
    /// </summary>
    public IReadOnlyList<ProjectSemanticLink>? SemanticLinks { get; init; }
}
