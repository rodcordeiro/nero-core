using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class AdminComplianceScanTests
{
    [Fact]
    public async Task ValidateAsync_IsCompliantFalseWhenActiveSecretPresent()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/snapshots/2026-08-05-leak.md", """
            ---
            type: snapshot
            scope: global
            title: "leak"
            links:
              - type: documents
                target: global
            ---
            # leak

            Authorization: Bearer super-secret-token-value-12345
            """);

        var result = await CreateService(root).ValidateAsync();

        Assert.False(result.IsCompliant);
        Assert.NotEmpty(result.ComplianceGaps);
        Assert.All(result.ComplianceGaps, gap => Assert.DoesNotContain("super-secret-token-value-12345", gap));
    }

    [Fact]
    public async Task ValidateAsync_IsCompliantTrueWhenOnlyQuarantinedHit()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/snapshots/2026-08-05-quarantined.md", """
            ---
            type: snapshot
            scope: global
            title: "quarantined"
            compliance_status: quarantined
            compliance_reason: "legacy fixture pending rewrite"
            links:
              - type: documents
                target: global
            ---
            # quarantined

            Authorization: Bearer super-secret-token-value-12345
            """);

        var validation = await CreateService(root).ValidateAsync();
        var scan = await CreateService(root).ScanComplianceAsync();

        Assert.True(validation.IsCompliant);
        Assert.True(scan.IsCompliant);
        Assert.Empty(scan.ActiveHits);
        Assert.NotEmpty(scan.QuarantinedHits);
        Assert.All(scan.QuarantinedHits, hit => Assert.DoesNotContain("super-secret-token-value-12345", hit.MaskedExcerpt));
    }

    [Fact]
    public async Task Reindex_ExcludesQuarantinedFromIndexedNodes()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/snapshots/2026-08-05-visible.md", """
            ---
            type: snapshot
            scope: global
            title: "visible"
            links:
              - type: documents
                target: global
            ---
            # visible
            """);
        await WriteMarkdownAsync(root, "global/snapshots/2026-08-05-hidden.md", """
            ---
            type: snapshot
            scope: global
            title: "hidden"
            compliance_status: quarantined
            compliance_reason: "secret legacy"
            links:
              - type: documents
                target: global
            ---
            # hidden Bearer super-secret-token-value-12345
            """);

        var dbPath = Path.Combine(Path.GetTempPath(), $"tkb-{Guid.NewGuid():N}.db");
        var service = CreateService(root, dbPath);
        var reindex = await service.ReindexAsync();

        Assert.Equal(2, reindex.IndexedNodes); // global/index + visible snapshot; quarantined excluded
    }

    private static AdminKnowledgeMaintenanceService CreateService(string root, string? dbPath = null)
    {
        dbPath ??= Path.Combine(Path.GetTempPath(), $"tkb-{Guid.NewGuid():N}.db");
        return new AdminKnowledgeMaintenanceService(
            new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions { Path = dbPath }),
            new KnowledgeDatabaseOptions { Path = dbPath },
            new KnowledgeRootOptions { Path = root },
            new KnowledgeIndexer(),
            new KnowledgeMarkdownReader(),
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());
    }

    private static async Task WriteRequiredStructureAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        await File.WriteAllTextAsync(Path.Combine(root, "global", "index.md"), """
            ---
            type: index
            scope: global
            ---
            # Global
            """);
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
