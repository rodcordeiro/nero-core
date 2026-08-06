namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroAdminEcosystemHealthToolResult
{
    public required string KnowledgeRootPath { get; init; }

    public required string IndexDatabasePath { get; init; }

    public int ProjectCount { get; init; }

    public int DomainCount { get; init; }

    public int HealthyProjectCount { get; init; }

    public int ProjectsWithIssuesCount { get; init; }

    public int HealthyDomainCount { get; init; }

    public int DomainsWithIssuesCount { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public int ThresholdMilliseconds { get; init; }

    public bool ExceededThreshold { get; init; }

    public required string Recommendation { get; init; }

    public IReadOnlyList<NeroAdminEcosystemScopeHealthToolResult> ProjectsWithIssues { get; init; } = [];

    public IReadOnlyList<NeroAdminEcosystemScopeHealthToolResult> DomainsWithIssues { get; init; } = [];
}
