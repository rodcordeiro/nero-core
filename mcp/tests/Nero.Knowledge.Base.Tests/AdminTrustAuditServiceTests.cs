using System.Security.Cryptography;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Tests;

public class AdminTrustAuditServiceTests
{
    [Fact]
    public async Task AuditAsync_ClassifiesTrustGapsDeterministicallyWithoutWriting()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteAsync(root, "global/index.md", "# Global");
        await WriteAsync(root, "projects/Acme.Api/decisions/decision.md", Note("decision", "", "never_verified"));
        await WriteAsync(root, "projects/Acme.Api/patterns/unverifiable.md", Note("pattern", "Research", "unverifiable"));
        await WriteAsync(root, "projects/Acme.Api/patterns/sourced.md", """
            ---
            type: pattern
            scope: project
            project: Acme.Api
            sources:
              - docs/ticket-123
            verification_status: verified
            ---
            # Sourced fixture
            """);
        await WriteAsync(root, "projects/Acme.Api/snapshots/2025-01-01-old.md", Note("snapshot", "Repository review", "verified"));
        await WriteAsync(root, "projects/Acme.Api/snapshots/2026-08-01-recent.md", Note("snapshot", "Repository review", "verified"));
        var before = CreateManifest(root);
        var service = CreateService(root);

        var first = await service.AuditAsync(new DateOnly(2026, 8, 24));
        var second = await service.AuditAsync(new DateOnly(2026, 8, 24));

        Assert.Equal(first.KnowledgeRootPath, second.KnowledgeRootPath);
        Assert.Equal(first.AsOfDate, second.AsOfDate);
        Assert.Equal(first.ScannedFileCount, second.ScannedFileCount);
        Assert.Equal(first.Issues, second.Issues);
        Assert.Equal(6, first.ScannedFileCount);
        Assert.Contains(first.Issues, issue => issue.Type == "MissingSource" && issue.Path.EndsWith("decision.md"));
        Assert.Contains(first.Issues, issue => issue.Type == "NeverVerified" && issue.Path.EndsWith("decision.md"));
        Assert.Contains(first.Issues, issue => issue.Type == "UnverifiableClaim" && issue.Path.EndsWith("unverifiable.md"));
        Assert.Contains(first.Issues, issue => issue.Type == "StaleSnapshot" && issue.Path.EndsWith("2025-01-01-old.md"));
        Assert.Contains(first.Issues, issue => issue.Type == "ArchiveCandidate" && issue.Path.EndsWith("2025-01-01-old.md"));
        Assert.DoesNotContain(first.Issues, issue => issue.Path.EndsWith("2026-08-01-recent.md"));
        Assert.DoesNotContain(first.Issues, issue => issue.Path.EndsWith("sourced.md"));
        Assert.Equal(before, CreateManifest(root));
        Assert.False(Directory.Exists(Path.Combine(root, ".nero")));
    }

    [Fact]
    public async Task AuditAsync_UsesExclusiveFreshnessBoundary()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteAsync(root, "projects/Acme.Api/snapshots/2026-05-26-boundary.md", Note("snapshot", "Review", "verified"));
        await WriteAsync(root, "projects/Acme.Api/snapshots/2026-05-25-stale.md", Note("snapshot", "Review", "verified"));

        var result = await CreateService(root).AuditAsync(new DateOnly(2026, 8, 24));

        Assert.DoesNotContain(result.Issues, issue => issue.Type == "StaleSnapshot" && issue.Path.EndsWith("boundary.md"));
        Assert.Contains(result.Issues, issue => issue.Type == "StaleSnapshot" && issue.Path.EndsWith("stale.md"));
    }

    private static AdminTrustAuditService CreateService(string root) => new(
        new KnowledgeRootOptions { Path = root },
        new KnowledgeMarkdownReader(),
        new AdminProjectFreshnessOptions { RecentSnapshotDays = 90 });

    private static string Note(string type, string origin, string verificationStatus) => $$"""
        ---
        type: {{type}}
        scope: project
        project: Acme.Api
        origin: "{{origin}}"
        verification_status: {{verificationStatus}}
        ---
        # Fixture

        Conteudo ficticio para teste.
        """;

    private static async Task WriteAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static string[] CreateManifest(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.OrdinalIgnoreCase)
        .Select(path => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}:{File.GetLastWriteTimeUtc(path).Ticks}")
        .ToArray();

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
