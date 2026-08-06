namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed class UpdateDomainRequest
{
    public required string Domain { get; init; }

    public required string Purpose { get; init; }

    public required IReadOnlyList<string> Arquivos { get; init; }

    public string? Titulo { get; init; }

    public string? FonteConsolidada { get; init; }

    public string? RegrasLeitura { get; init; }

    public string? Origin { get; init; }

    /// <summary>
    /// Project names or paths emitted as source_for links.
    /// Null/omitted on update preserves existing source_for from the current index.
    /// Non-null list replaces the set (empty list clears all source_for intentionally).
    /// </summary>
    public IReadOnlyList<string>? SourceFor { get; init; }

    public bool Reativar { get; init; }
}
