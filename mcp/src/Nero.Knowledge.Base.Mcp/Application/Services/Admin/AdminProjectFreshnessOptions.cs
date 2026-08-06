namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed record AdminProjectFreshnessOptions
{
    public const string SectionName = "AdminProjectFreshness";

    public const int DefaultRecentSnapshotDays = 90;

    public int RecentSnapshotDays { get; init; } = DefaultRecentSnapshotDays;
}
