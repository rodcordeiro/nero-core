using System.Security.Cryptography;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class AdminBatchFinalizationServiceTests
{
    [Fact]
    public async Task FinalizeAsync_ValidBatch_ReturnsIndependentEvidence()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.FinalizeAsync(["global/index.md"]);

        Assert.True(result.Success);
        Assert.Equal(["global/index.md"], result.FoundMarkdownPaths);
        Assert.Equal(["global/index.md"], result.IndexedPaths);
        Assert.True(result.Compliance!.IsCompliant);
        Assert.True(result.Validation!.IsValid);
        Assert.True(result.Validation.IsCompliant);
        Assert.Equal("Succeeded", result.Stages.Single(stage => stage.Stage == "IndexEvidence").Status);
        Assert.True(File.Exists(fixture.DatabasePath));
    }

    [Fact]
    public async Task FinalizeAsync_RegisteredSnapshotAndIndex_CompletesOneBatchWithoutChangingMarkdown()
    {
        var fixture = await CreateFixtureAsync();
        var write = await new SnapshotWriterService().WriteAsync(
            fixture.Root,
            new RegisterSnapshotRequest
            {
                Title = "Batch evidence fixture",
                Scope = KnowledgeScope.Global,
                Context = "Contexto ficticio do lote.",
                Evidence = "Evidencia ficticia independente.",
                Origin = "AdminBatchFinalizationServiceTests",
                RelatesTo = ["global/index"]
            });
        var before = CreateMarkdownManifest(fixture.Root);
        Assert.False(File.Exists(fixture.DatabasePath));

        var result = await fixture.Service.FinalizeAsync(["global/index.md", write.RelativePath]);

        Assert.True(result.Success, result.Recommendation);
        Assert.Equal(2, result.IndexedPaths.Count);
        Assert.Equal(before, CreateMarkdownManifest(fixture.Root));
        Assert.Equal(5, result.Stages.Count);
        Assert.All(result.Stages, stage => Assert.Equal("Succeeded", stage.Status));
    }

    [Fact]
    public async Task FinalizeAsync_MissingExpectedPath_StopsBeforeCreatingIndex()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.FinalizeAsync(["projects/Acme.Api/missing.md"]);

        Assert.False(result.Success);
        Assert.Equal("Files", result.FailedStage);
        Assert.Equal(["projects/Acme.Api/missing.md"], result.MissingMarkdownPaths);
        Assert.All(result.Stages.Where(stage => stage.Stage != "Files"), stage => Assert.Equal("Skipped", stage.Status));
        Assert.False(File.Exists(fixture.DatabasePath));
    }

    [Fact]
    public async Task FinalizeAsync_BlockingComplianceHit_StopsBeforeReindex()
    {
        var fixture = await CreateFixtureAsync();
        var secret = $"ghp_{new string('a', 36)}";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "global", "secret.md"), $"# Secret\n\n{secret}");

        var result = await fixture.Service.FinalizeAsync(["global/secret.md"]);

        Assert.False(result.Success);
        Assert.Equal("Compliance", result.FailedStage);
        Assert.False(result.Compliance!.IsCompliant);
        Assert.DoesNotContain(secret, string.Join(' ', result.Compliance.ActiveHits.Select(hit => hit.MaskedExcerpt)));
        Assert.False(File.Exists(fixture.DatabasePath));
    }

    [Fact]
    public async Task FinalizeAsync_QuarantinedExpectedFile_ReportsMissingIndexEvidence()
    {
        var fixture = await CreateFixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "global", "quarantined.md"), """
            ---
            type: snapshot
            scope: global
            origin: "Fixture"
            compliance_status: quarantined
            compliance_reason: "Synthetic fixture"
            links:
              - type: documents
                target: global/index
            ---
            # Quarantined
            """);

        var result = await fixture.Service.FinalizeAsync(["global/quarantined.md"]);

        Assert.False(result.Success);
        Assert.Equal("IndexEvidence", result.FailedStage);
        Assert.True(result.Compliance!.IsCompliant);
        Assert.Equal(["global/quarantined.md"], result.MissingIndexedPaths);
        Assert.True(result.Validation!.IsValid);
    }

    [Fact]
    public async Task FinalizeAsync_InvalidMarkdown_ReportsValidationAfterReindex()
    {
        var fixture = await CreateFixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "global", "orphan.md"), """
            ---
            type: snapshot
            scope: global
            origin: "Fixture"
            ---
            # Orphan
            """);

        var result = await fixture.Service.FinalizeAsync(["global/orphan.md"]);

        Assert.False(result.Success);
        Assert.Equal("Validation", result.FailedStage);
        Assert.NotNull(result.Reindex);
        Assert.False(result.Validation!.IsValid);
        Assert.Contains(result.Validation.Errors, error => error.Contains("orphan", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["global/orphan.md"], result.IndexedPaths);
    }

    [Theory]
    [InlineData("Reindex")]
    [InlineData("Validation")]
    public async Task FinalizeAsync_OperationalFailure_ReturnsCompletedStageEvidence(string failingStage)
    {
        var fixture = await CreateFixtureAsync();
        var operations = new FakeBatchOperations(failingStage);
        var service = new AdminBatchFinalizationService(
            new KnowledgeRootOptions { Path = fixture.Root },
            new KnowledgeMarkdownReader(),
            operations);

        var result = await service.FinalizeAsync(["global/index.md"]);

        Assert.False(result.Success);
        Assert.Equal(failingStage, result.FailedStage);
        Assert.Equal("Succeeded", result.Stages.Single(stage => stage.Stage == "Compliance").Status);
        Assert.Equal("Failed", result.Stages.Single(stage => stage.Stage == failingStage).Status);
        Assert.Contains("retry", result.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinalizeAsync_CancellationBetweenStages_PropagatesWithoutStartingNextStage()
    {
        var fixture = await CreateFixtureAsync();
        using var cancellation = new CancellationTokenSource();
        var operations = new FakeBatchOperations(cancelAfterCompliance: cancellation);
        var service = new AdminBatchFinalizationService(
            new KnowledgeRootOptions { Path = fixture.Root },
            new KnowledgeMarkdownReader(),
            operations);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.FinalizeAsync(["global/index.md"], cancellation.Token));

        Assert.Equal(1, operations.ComplianceCalls);
        Assert.Equal(0, operations.ReindexCalls);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("knowledge/global/index.md")]
    [InlineData("global/index.txt")]
    [InlineData("other/note.md")]
    public async Task FinalizeAsync_UnsafeOrUnsupportedPath_IsRejected(string path)
    {
        var fixture = await CreateFixtureAsync();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => fixture.Service.FinalizeAsync([path]));

        Assert.False(File.Exists(fixture.DatabasePath));
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        await File.WriteAllTextAsync(Path.Combine(root, "global", "index.md"), "# Global");
        var databasePath = Path.Combine(Path.GetDirectoryName(root)!, "knowledge.db");
        var databaseOptions = new KnowledgeDatabaseOptions { Path = databasePath };
        var rootOptions = new KnowledgeRootOptions { Path = root };
        var factory = new KnowledgeDatabaseConnectionFactory(databaseOptions);
        var reader = new KnowledgeMarkdownReader();
        var maintenance = new AdminKnowledgeMaintenanceService(
            factory,
            databaseOptions,
            rootOptions,
            new KnowledgeIndexer(),
            reader,
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());
        return new Fixture(
            root,
            databasePath,
            new AdminBatchFinalizationService(
                rootOptions,
                reader,
                new AdminBatchOperations(maintenance, new KnowledgeIndexedPathReader(factory))));
    }

    private static string[] CreateMarkdownManifest(string root) => Directory
        .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
        .Order(StringComparer.OrdinalIgnoreCase)
        .Select(path => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}:{File.GetLastWriteTimeUtc(path).Ticks}")
        .ToArray();

    private sealed record Fixture(string Root, string DatabasePath, AdminBatchFinalizationService Service);

    private sealed class FakeBatchOperations(
        string? failingStage = null,
        CancellationTokenSource? cancelAfterCompliance = null) : IAdminBatchOperations
    {
        public int ComplianceCalls { get; private set; }

        public int ReindexCalls { get; private set; }

        public Task<AdminComplianceScanResult> ScanComplianceAsync(CancellationToken cancellationToken)
        {
            ComplianceCalls++;
            cancelAfterCompliance?.Cancel();
            return Task.FromResult(new AdminComplianceScanResult
            {
                IsCompliant = true,
                TaxonomyVersion = "fixture",
                ScannedFileCount = 1,
                ActiveBlockingHitCount = 0,
                QuarantinedBlockingHitCount = 0,
                WarningHitCount = 0,
                ActiveHits = [],
                QuarantinedHits = [],
                Warnings = []
            });
        }

        public Task<AdminReindexResult> ReindexAsync(CancellationToken cancellationToken)
        {
            ReindexCalls++;
            if (failingStage == "Reindex")
            {
                throw new IOException("Synthetic reindex failure.");
            }

            return Task.FromResult(new AdminReindexResult
            {
                IndexedNodes = 1,
                KnowledgeRootPath = "fixture",
                IndexDatabasePath = "fixture.db"
            });
        }

        public Task<AdminValidationResult> ValidateAsync(CancellationToken cancellationToken)
        {
            if (failingStage == "Validation")
            {
                throw new IOException("Synthetic validation failure.");
            }

            return Task.FromResult(new AdminValidationResult
            {
                IsValid = true,
                IsCompliant = true,
                NodeCount = 1,
                EdgeCount = 0,
                Errors = [],
                ComplianceGaps = []
            });
        }

        public Task<IReadOnlySet<string>> ReadIndexedPathsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["global/index.md"], StringComparer.OrdinalIgnoreCase));
    }
}
