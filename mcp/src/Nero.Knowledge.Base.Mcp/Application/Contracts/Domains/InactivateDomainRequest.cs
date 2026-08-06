namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;

public sealed class InactivateDomainRequest
{
    public required string Domain { get; init; }

    public required string Motivo { get; init; }

    public required string Origin { get; init; }

    public string? Confirmacao { get; init; }

    public string? Evidencia { get; init; }
}
