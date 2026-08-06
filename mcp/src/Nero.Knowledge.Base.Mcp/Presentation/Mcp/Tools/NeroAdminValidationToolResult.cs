namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminValidationToolResult
{
    public required bool IsValid { get; init; }

    public required bool IsCompliant { get; init; }

    public required int NodeCount { get; init; }

    public required int EdgeCount { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public IReadOnlyList<string> ComplianceGaps { get; init; } = [];

    public IReadOnlyList<string> ActionableGaps { get; init; } = [];

    public required string Recommendation { get; init; }
}
