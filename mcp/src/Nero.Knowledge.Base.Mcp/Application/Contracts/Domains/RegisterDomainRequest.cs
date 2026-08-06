namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed class RegisterDomainRequest
{
    public required string Domain { get; init; }

    public required string Purpose { get; init; }

    public required string Origin { get; init; }

    public string? Titulo { get; init; }

    public string? FonteConsolidada { get; init; }

    public IReadOnlyList<string>? Arquivos { get; init; }

    public string? RegrasLeitura { get; init; }

    /// <summary>
    /// Project names or paths (Acme.X.API or projects/Acme.X.API) emitted as source_for links.
    /// </summary>
    public IReadOnlyList<string>? SourceFor { get; init; }
}
