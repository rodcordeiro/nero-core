namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

public enum ComplianceSeverity
{
    Blocking = 0,
    Warning = 1
}

public sealed record ComplianceScanHit
{
    public required string RuleId { get; init; }

    public required ComplianceSeverity Severity { get; init; }

    public required int Line { get; init; }

    public required int Column { get; init; }

    /// <summary>0-based absolute start index of the match in the scanned text.</summary>
    public required int MatchIndex { get; init; }

    /// <summary>Length of the matched span in the scanned text.</summary>
    public required int MatchLength { get; init; }

    /// <summary>Masked excerpt safe for agent/admin output. Never contains the raw secret/PII value.</summary>
    public required string MaskedExcerpt { get; init; }

    public string? Field { get; init; }
}
