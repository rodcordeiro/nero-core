using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

/// <summary>
/// Baseline wall-time harness for index consistency against the bundled knowledge scaffold.
/// </summary>
public class CheckIndexConsistencyBenchmarkTests
{
    private const int RunCount = 5;
    private const double MaxAllowedMilliseconds = 30_000;

    [Fact]
    public async Task CheckIndexConsistencyAsync_ScaffoldTree_ReportsWallTimes()
    {
        var repoRoot = ResolveRepoRoot();
        var knowledgeRoot = Path.Combine(repoRoot, "examples", "knowledge-scaffold");
        Assert.True(Directory.Exists(knowledgeRoot), $"Knowledge scaffold missing: {knowledgeRoot}");

        var databasePath = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "nero-knowledge.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var databaseOptions = new KnowledgeDatabaseOptions { Path = databasePath };
        var knowledgeRootOptions = new KnowledgeRootOptions { Path = knowledgeRoot };
        var indexer = new KnowledgeIndexer();
        var service = new AdminKnowledgeMaintenanceService(
            new KnowledgeDatabaseConnectionFactory(databaseOptions),
            databaseOptions,
            knowledgeRootOptions,
            indexer,
            new KnowledgeMarkdownReader(),
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());

        await service.ReindexAsync();

        var markdownOnDisk = Directory
            .EnumerateFiles(knowledgeRoot, "*.md", SearchOption.AllDirectories)
            .Count();

        _ = await service.CheckIndexConsistencyAsync();

        var timingsMs = new List<double>(RunCount);
        var lastMarkdownCount = 0;
        var lastIndexedCount = 0;
        var lastIssueCount = 0;
        var lastConsistent = false;
        var lastElapsedMs = 0L;
        var lastThresholdMs = 0;
        var lastExceeded = false;

        for (var i = 0; i < RunCount; i++)
        {
            var result = await service.CheckIndexConsistencyAsync();
            timingsMs.Add(result.ElapsedMilliseconds);
            lastMarkdownCount = result.MarkdownFileCount;
            lastIndexedCount = result.IndexedNodeCount;
            lastIssueCount = result.Issues.Count;
            lastConsistent = result.IsConsistent;
            lastElapsedMs = result.ElapsedMilliseconds;
            lastThresholdMs = result.ThresholdMilliseconds;
            lastExceeded = result.ExceededThreshold;
        }

        timingsMs.Sort();
        var min = timingsMs[0];
        var max = timingsMs[^1];
        var median = timingsMs[timingsMs.Count / 2];

        Assert.True(lastMarkdownCount > 0);
        Assert.True(lastIndexedCount > 0);
        Assert.Equal(markdownOnDisk, lastMarkdownCount);
        Assert.Equal(AdminIndexConsistencyOptions.DefaultThresholdMilliseconds, lastThresholdMs);
        Assert.True(lastElapsedMs >= 0);
        Assert.Equal(lastElapsedMs > lastThresholdMs, lastExceeded);
        Assert.True(median < MaxAllowedMilliseconds, $"Expected median under {MaxAllowedMilliseconds}ms, got {median:F1}ms.");
        Assert.True(max < MaxAllowedMilliseconds, $"Expected max under {MaxAllowedMilliseconds}ms, got {max:F1}ms.");
        _ = lastIssueCount;
        _ = lastConsistent;
        _ = min;
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "examples", "knowledge-scaffold");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repo root containing examples/knowledge-scaffold from test BaseDirectory.");
    }
}
