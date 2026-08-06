namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminComplianceScanIssue
{
    public required string Path { get; init; }

    public required string RuleId { get; init; }

    public required string Severity { get; init; }

    public required int Line { get; init; }

    public required string MaskedExcerpt { get; init; }

    public required bool Quarantined { get; init; }

    public string? ComplianceReason { get; init; }
}

public sealed record AdminComplianceScanResult
{
    public required bool IsCompliant { get; init; }

    public required string TaxonomyVersion { get; init; }

    public required int ScannedFileCount { get; init; }

    public required int ActiveBlockingHitCount { get; init; }

    public required int QuarantinedBlockingHitCount { get; init; }

    public required int WarningHitCount { get; init; }

    public required IReadOnlyList<AdminComplianceScanIssue> ActiveHits { get; init; }

    public required IReadOnlyList<AdminComplianceScanIssue> QuarantinedHits { get; init; }

    public required IReadOnlyList<AdminComplianceScanIssue> Warnings { get; init; }
}
