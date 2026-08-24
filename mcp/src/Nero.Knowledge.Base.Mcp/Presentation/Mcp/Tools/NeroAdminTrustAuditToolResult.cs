namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminTrustAuditIssueToolResult
{
    public required string Type { get; init; }

    public required string Path { get; init; }

    public required string Reason { get; init; }

    public required string Recommendation { get; init; }
}

public sealed record NeroAdminTrustAuditToolResult
{
    public required string KnowledgeRootPath { get; init; }

    public required string AsOfDate { get; init; }

    public required int ScannedFileCount { get; init; }

    public required int IssueCount { get; init; }

    public required IReadOnlyList<NeroAdminTrustAuditIssueToolResult> Issues { get; init; }

    public required string Recommendation { get; init; }
}
