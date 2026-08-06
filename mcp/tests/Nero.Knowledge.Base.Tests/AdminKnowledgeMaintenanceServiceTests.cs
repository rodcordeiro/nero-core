using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class AdminKnowledgeMaintenanceServiceTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsSuccessForValidKnowledgeRoot()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: domain_index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            type: project_index
            scope: project
            project: Acme.Api
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Inventory API
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.True(result.IsValid);
        Assert.Equal(2, result.NodeCount);
        Assert.Equal(1, result.EdgeCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFrontmatterErrors()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            type: project_index
            scope: project
            ---
            # Inventory API
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            "Markdown 'projects/Acme.Api/index' has project scope but is missing 'project'.",
            result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_RejectsLegacyRelatesTo()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-01-legacy.md", """
            ---
            type: decision
            scope: project
            project: Acme.Api
            links:
              - type: relates_to
                target: domains/api/index
            ---
            # Legacy decision
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("relates_to", StringComparison.OrdinalIgnoreCase)
                && error.Contains("projects/Acme.Api/decisions/2026-07-01-legacy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_RejectsOrphanContentNoteWithoutLinks()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-01-orphan.md", """
            ---
            type: decision
            scope: project
            project: Acme.Api
            ---
            # Orphan decision
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("projects/Acme.Api/decisions/2026-07-01-orphan", StringComparison.Ordinal)
                && error.Contains("missing a non-empty links:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_AllowsSupersedesBetweenDecisions()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-01-original.md", """
            ---
            type: decision
            scope: project
            project: Acme.Api
            links:
              - type: documents
                target: domains/api/index
            ---
            # Original decision
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-02-replacement.md", """
            ---
            type: decision
            scope: project
            project: Acme.Api
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-01-original
              - type: documents
                target: domains/api/index
            ---
            # Replacement decision
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_RejectsSupersedesFromPattern()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/patterns/http-versioning.md", """
            ---
            type: pattern
            scope: project
            project: Acme.Api
            links:
              - type: supersedes
                target: domains/api/index
              - type: documents
                target: domains/api/index
            ---
            # HTTP versioning
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("supersedes", StringComparison.OrdinalIgnoreCase)
                && error.Contains("only allowed decision→decision", StringComparison.Ordinal)
                && error.Contains("projects/Acme.Api/patterns/http-versioning", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_RejectsInvertedDependsOnFromApiToFront()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Checkout.Front/index.md", """
            ---
            type: index
            scope: project
            project: Acme.Checkout.Front
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Checkout Front
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Checkout.Api/decisions/2026-07-01-inverted.md", """
            ---
            type: decision
            scope: project
            project: Acme.Checkout.Api
            links:
              - type: depends_on
                target: projects/Acme.Checkout.Front/index
              - type: documents
                target: domains/api/index
            ---
            # Inverted depends_on
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("inverted", StringComparison.OrdinalIgnoreCase)
                && error.Contains("depends_on", StringComparison.OrdinalIgnoreCase)
                && error.Contains("projects/Acme.Checkout.Api/decisions/2026-07-01-inverted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_AllowsUsesBackendFromFrontToApi()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Checkout.Api/index.md", """
            ---
            type: index
            scope: project
            project: Acme.Checkout.Api
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Checkout API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Checkout.Front/decisions/2026-07-01-uses-backend.md", """
            ---
            type: decision
            scope: project
            project: Acme.Checkout.Front
            links:
              - type: uses_backend
                target: projects/Acme.Checkout.Api/index
              - type: documents
                target: domains/api/index
            ---
            # Front uses backend
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_RejectsEvidencesToPatternsHub()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/snapshots/2026-07-01-hub-evidence.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            links:
              - type: evidences
                target: domains/api/patterns
              - type: documents
                target: domains/api/index
            ---
            # Hub evidence
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("evidences", StringComparison.OrdinalIgnoreCase)
                && error.Contains("directory hub", StringComparison.OrdinalIgnoreCase)
                && error.Contains("domains/api/patterns", StringComparison.Ordinal)
                && error.Contains("projects/Acme.Api/snapshots/2026-07-01-hub-evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_AllowsEvidencesToConcretePatternNote()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: index
            scope: domain
            domain: api
            ---
            # API
            """);
        await WriteMarkdownAsync(root, "domains/api/patterns/http-versioning.md", """
            ---
            type: pattern
            scope: domain
            domain: api
            links:
              - type: documents
                target: domains/api/index
            ---
            # HTTP versioning
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/snapshots/2026-07-01-concrete-evidence.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            links:
              - type: evidences
                target: domains/api/patterns/http-versioning
              - type: documents
                target: domains/api/index
            ---
            # Concrete evidence
            """);
        var service = CreateService(root);

        var result = await service.ValidateAsync();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ReindexAsync_WritesConfiguredSqliteIndex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nIndice.");
        var service = CreateService(root, out var databasePath);

        var result = await service.ReindexAsync();

        Assert.Equal(1, result.IndexedNodes);
        Assert.Equal(root, result.KnowledgeRootPath);
        Assert.Equal(databasePath, result.IndexDatabasePath);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    public async Task CheckIndexConsistencyAsync_ReturnsPerformanceDiagnostics()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nIndice.");
        var service = CreateService(root);
        await service.ReindexAsync();

        var result = await service.CheckIndexConsistencyAsync();

        Assert.True(result.IsConsistent);
        Assert.Equal(1, result.MarkdownFileCount);
        Assert.Equal(1, result.IndexedNodeCount);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(AdminIndexConsistencyOptions.DefaultThresholdMilliseconds, result.ThresholdMilliseconds);
        Assert.Equal(result.ElapsedMilliseconds > result.ThresholdMilliseconds, result.ExceededThreshold);
    }

    [Fact]
    public async Task CheckIndexConsistencyAsync_ManyFileFixture_CompletesUnderBudgetWithDiagnostics()
    {
        const int markdownCount = 80;
        // Generous CI guard: far above stretch UX (2s), well under hard SLO (60s).
        const long maxElapsedMilliseconds = 30_000;

        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        for (var i = 0; i < markdownCount; i++)
        {
            await WriteMarkdownAsync(
                root,
                $"global/notes/note-{i:D3}.md",
                $"# Note {i}\n\nFixture content {i}.");
        }

        var service = CreateService(
            root,
            new AdminIndexConsistencyOptions { ThresholdMilliseconds = 5_000 },
            out _);
        await service.ReindexAsync();

        var result = await service.CheckIndexConsistencyAsync();

        Assert.True(result.IsConsistent);
        Assert.Equal(markdownCount, result.MarkdownFileCount);
        Assert.Equal(markdownCount, result.IndexedNodeCount);
        Assert.Equal(5_000, result.ThresholdMilliseconds);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(result.ElapsedMilliseconds > result.ThresholdMilliseconds, result.ExceededThreshold);
        Assert.True(
            result.ElapsedMilliseconds < maxElapsedMilliseconds,
            $"Expected consistency check under {maxElapsedMilliseconds}ms, got {result.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task CheckIndexConsistencyAsync_DetectsIndexedNodeWithoutMarkdownFile()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        var markdownPath = await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nIndice.");
        var service = CreateService(root);
        await service.ReindexAsync();
        File.Delete(markdownPath);

        var result = await service.CheckIndexConsistencyAsync();

        Assert.False(result.IsConsistent);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("IndexedNodeMissingFile", issue.Type);
        Assert.Equal("global/index", issue.Id);
        Assert.Contains("restore the missing file", issue.Recommendation);
    }

    [Fact]
    public async Task CheckIndexConsistencyAsync_DetectsMarkdownWithoutIndexedNode()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nIndice.");
        var service = CreateService(root);
        await service.ReindexAsync();
        await WriteMarkdownAsync(root, "global/patterns.md", "# Padroes\n\nNovo arquivo.");

        var result = await service.CheckIndexConsistencyAsync();

        Assert.False(result.IsConsistent);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("MarkdownMissingIndexedNode", issue.Type);
        Assert.Equal("global/patterns", issue.Id);
        Assert.Contains("Run nero_admin_reindex", issue.Recommendation);
    }

    [Fact]
    public async Task CheckIndexConsistencyAsync_DetectsMarkdownNewerThanIndex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        var markdownPath = await WriteMarkdownAsync(root, "global/index.md", "# Global\n\nIndice.");
        var service = CreateService(root);
        await service.ReindexAsync();
        File.SetLastWriteTimeUtc(markdownPath, DateTime.UtcNow.AddMinutes(5));

        var result = await service.CheckIndexConsistencyAsync();

        Assert.False(result.IsConsistent);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("MarkdownNewerThanIndex", issue.Type);
        Assert.Equal("global/index", issue.Id);
        Assert.NotNull(issue.IndexedUpdatedUtc);
        Assert.NotNull(issue.FileLastWriteUtc);
    }

    [Fact]
    public async Task CheckProjectHealthAsync_DetectsUnknownProject()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        var service = CreateService(root);

        var result = await service.CheckProjectHealthAsync("Acme.Projeto.Novo", "api");

        Assert.False(result.ExistsInFilesystem);
        Assert.False(result.ExistsInIndex);
        Assert.False(result.HasIndex);
        Assert.False(result.HasContext);
        Assert.False(result.HasBaseStructure);
        Assert.False(result.HasRecentSnapshot);
        Assert.Null(result.LatestSnapshotDate);
        Assert.Contains(result.Issues, issue => issue.Type == "ProjectMissing");
        Assert.Contains(result.Issues, issue => issue.Type == "MissingRecentSnapshot");
        Assert.Equal("Register the project with nero_register_project.", result.Recommendation);
        var missingSnapshot = Assert.Single(result.Issues, issue => issue.Type == "MissingRecentSnapshot");
        Assert.Contains(
            AdminKnowledgeMaintenanceService.KnowledgeReviewPromptPath,
            missingSnapshot.Recommendation,
            StringComparison.Ordinal);
        Assert.Contains("Acme.Projeto.Novo", missingSnapshot.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckProjectHealthAsync_DetectsPartialProjectWithNotesWithoutBaseStructure()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-01-decisao.md", "# Decisao\n\nNota.");
        var service = CreateService(root);
        await service.ReindexAsync();

        var result = await service.CheckProjectHealthAsync("Acme.Api", "api");

        Assert.True(result.ExistsInFilesystem);
        Assert.True(result.ExistsInIndex);
        Assert.False(result.HasIndex);
        Assert.False(result.HasContext);
        Assert.True(result.HasNotesWithoutBaseStructure);
        Assert.Contains(result.Issues, issue => issue.Type == "MissingIndex");
        Assert.Contains(result.Issues, issue => issue.Type == "MissingContext");
        Assert.Contains(result.Issues, issue => issue.Type == "NotesWithoutBaseStructure");
        Assert.NotNull(result.LastIndexedUtc);
    }

    [Fact]
    public async Task CheckProjectHealthAsync_ReturnsHealthyProject()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api
            ---
            # Inventory API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Contexto\n\nResumo.");
        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        await WriteMarkdownAsync(root, $"projects/Acme.Api/snapshots/{snapshotDate}-review.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            origin: "Repository review"
            links:
              - type: documents
                target: projects/Acme.Api/index
            ---
            # Review
            """);
        var service = CreateService(root);
        await service.ReindexAsync();

        var result = await service.CheckProjectHealthAsync("Acme.Api", "api");

        Assert.True(result.ExistsInFilesystem);
        Assert.True(result.ExistsInIndex);
        Assert.True(result.HasIndex);
        Assert.True(result.HasContext);
        Assert.True(result.HasBaseStructure);
        Assert.True(result.HasBelongsToDomain);
        Assert.False(result.HasNotesWithoutBaseStructure);
        Assert.True(result.HasRecentSnapshot);
        Assert.Equal(snapshotDate, result.LatestSnapshotDate);
        Assert.Equal(0, result.LatestSnapshotAgeDays);
        Assert.Equal("Repository review", result.LatestSnapshotOrigin);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.LastIndexedUtc);
        Assert.Equal("Project knowledge structure is healthy.", result.Recommendation);
    }

    [Fact]
    public async Task CheckProjectHealthAsync_SignalsStaleSnapshot()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api
            ---
            # Inventory API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Contexto\n\nResumo.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/snapshots/2020-01-02-review.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            origin: "Legacy review"
            links:
              - type: documents
                target: projects/Acme.Api/index
            ---
            # Review antiga
            """);
        var service = CreateService(root);
        await service.ReindexAsync();

        var result = await service.CheckProjectHealthAsync("Acme.Api", "api");

        Assert.False(result.HasRecentSnapshot);
        Assert.Equal("2020-01-02", result.LatestSnapshotDate);
        Assert.True(result.LatestSnapshotAgeDays > result.RecentSnapshotDays);
        Assert.Equal("Legacy review", result.LatestSnapshotOrigin);
        Assert.Contains(result.Issues, issue => issue.Type == "StaleSnapshot");
        Assert.Contains(
            AdminKnowledgeMaintenanceService.KnowledgeReviewPromptPath,
            result.Recommendation,
            StringComparison.Ordinal);
        Assert.Contains("Acme.Api", result.Recommendation, StringComparison.Ordinal);
        Assert.Contains("primaryDomain=api", result.Recommendation, StringComparison.Ordinal);
        Assert.Contains("hasRecentSnapshot=false", result.Recommendation, StringComparison.Ordinal);
        var staleIssue = Assert.Single(result.Issues, issue => issue.Type == "StaleSnapshot");
        Assert.Equal(result.Recommendation, staleIssue.Recommendation);
    }

    [Fact]
    public async Task CheckEcosystemHealthAsync_AggregatesProjectsAndDomainsAndDetailsIssues()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Healthy.Api/index.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Healthy API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Healthy.Api/context.md", "# Contexto\n\nResumo.");
        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        await WriteMarkdownAsync(root, $"projects/Acme.Healthy.Api/snapshots/{snapshotDate}-review.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Healthy.Api
            links:
              - type: documents
                target: projects/Acme.Healthy.Api/index
            ---
            # Review
            """);
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Partial.Api/decisions/2026-08-04-note.md",
            "# Partial project note");
        var service = CreateService(root);
        await service.ReindexAsync();

        var result = await service.CheckEcosystemHealthAsync();

        Assert.Equal(2, result.ProjectCount);
        Assert.Equal(1, result.DomainCount);
        Assert.Equal(1, result.HealthyProjectCount);
        Assert.Equal(1, result.ProjectsWithIssuesCount);
        Assert.Equal(1, result.HealthyDomainCount);
        Assert.Equal(0, result.DomainsWithIssuesCount);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(AdminIndexConsistencyOptions.DefaultThresholdMilliseconds, result.ThresholdMilliseconds);
        Assert.Equal(result.ElapsedMilliseconds > result.ThresholdMilliseconds, result.ExceededThreshold);

        var partial = Assert.Single(result.ProjectsWithIssues);
        Assert.Equal("Acme.Partial.Api", partial.Name);
        Assert.Contains(partial.Issues, issue => issue.Type == "MissingIndex");
        Assert.Contains(partial.Issues, issue => issue.Type == "MissingContext");
        Assert.Contains(partial.Issues, issue => issue.Type == "NotesWithoutBaseStructure");
        Assert.Empty(result.DomainsWithIssues);
    }

    [Fact]
    public async Task CheckEcosystemHealthAsync_MarksFilesystemScopesMissingFromIndex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteRequiredStructureAsync(root);
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API");
        await WriteMarkdownAsync(root, "projects/Acme.Unindexed.Api/index.md", """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Unindexed API
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Unindexed.Api/context.md", "# Contexto");
        var service = CreateService(root);

        var result = await service.CheckEcosystemHealthAsync();

        var project = Assert.Single(result.ProjectsWithIssues);
        Assert.Contains(project.Issues, issue => issue.Type == "ProjectNotIndexed");
        var domain = Assert.Single(result.DomainsWithIssues);
        Assert.Contains(domain.Issues, issue => issue.Type == "DomainNotIndexed");
    }

    private static AdminKnowledgeMaintenanceService CreateService(string root)
    {
        return CreateService(root, new AdminIndexConsistencyOptions(), out _);
    }

    private static AdminKnowledgeMaintenanceService CreateService(string root, out string databasePath)
    {
        return CreateService(root, new AdminIndexConsistencyOptions(), out databasePath);
    }

    private static AdminKnowledgeMaintenanceService CreateService(
        string root,
        AdminIndexConsistencyOptions indexConsistencyOptions,
        out string databasePath)
    {
        databasePath = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge.db");
        var databaseOptions = new KnowledgeDatabaseOptions
        {
            Path = databasePath
        };

        return new AdminKnowledgeMaintenanceService(
            new KnowledgeDatabaseConnectionFactory(databaseOptions),
            databaseOptions,
            new KnowledgeRootOptions { Path = root },
            new KnowledgeIndexer(),
            new KnowledgeMarkdownReader(),
            indexConsistencyOptions,
            new AdminProjectFreshnessOptions());
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static Task WriteRequiredStructureAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        return Task.CompletedTask;
    }

    private static async Task<string> WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
