namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminTrustAuditIssue
{
    public required string Type { get; init; }

    public required string Path { get; init; }

    public required string Reason { get; init; }

    public required string Recommendation { get; init; }
}

public sealed record AdminTrustAuditResult
{
    public required string KnowledgeRootPath { get; init; }

    public required string AsOfDate { get; init; }

    public required int ScannedFileCount { get; init; }

    public required IReadOnlyList<AdminTrustAuditIssue> Issues { get; init; }
}
