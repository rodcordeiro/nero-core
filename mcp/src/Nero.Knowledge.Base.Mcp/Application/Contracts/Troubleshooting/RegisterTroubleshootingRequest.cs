using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Troubleshooting;

public sealed record RegisterTroubleshootingRequest
{
    public required string Title { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public required string Symptom { get; init; }

    public required string Cause { get; init; }

    public required string Action { get; init; }

    public required string Evidence { get; init; }

    public required string Impact { get; init; }

    public string? Solution { get; init; }

    public string? Prevention { get; init; }

    public required string Origin { get; init; }

    public IReadOnlyList<string>? CausedBy { get; init; }

    public IReadOnlyList<string>? RelatesTo { get; init; }
}
