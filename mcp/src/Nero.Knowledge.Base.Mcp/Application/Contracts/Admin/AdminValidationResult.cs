namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminValidationResult
{
    public required bool IsValid { get; init; }

    /// <summary>
    /// Independent of <see cref="IsValid"/>. False when any active (non-quarantined) P0 compliance hit exists in the corpus.
    /// </summary>
    public required bool IsCompliant { get; init; }

    public required int NodeCount { get; init; }

    public required int EdgeCount { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public IReadOnlyList<string> ComplianceGaps { get; init; } = [];
}
