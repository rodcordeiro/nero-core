namespace Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

public sealed record AdminProjectHealthResult
{
    public required string Project { get; init; }

    public string? PrimaryDomain { get; init; }

    public required string KnowledgeRootPath { get; init; }

    public required string ProjectDirectoryPath { get; init; }

    public bool ExistsInFilesystem { get; init; }

    public bool ExistsInIndex { get; init; }

    public bool HasIndex { get; init; }

    public bool HasContext { get; init; }

    public bool HasBaseStructure { get; init; }

    public bool HasNotesWithoutBaseStructure { get; init; }

    public bool HasBelongsToDomain { get; init; }

    public string? LastIndexedUtc { get; init; }

    public int RecentSnapshotDays { get; init; }

    public bool HasRecentSnapshot { get; init; }

    public string? LatestSnapshotPath { get; init; }

    public string? LatestSnapshotDate { get; init; }

    public int? LatestSnapshotAgeDays { get; init; }

    public string? LatestSnapshotOrigin { get; init; }

    public required string Recommendation { get; init; }

    public IReadOnlyList<AdminProjectHealthIssue> Issues { get; init; } = [];
}
