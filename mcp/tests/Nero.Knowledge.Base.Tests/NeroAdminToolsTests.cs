using System.Security.Cryptography;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;
using Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

namespace Nero.Knowledge.Base.Tests;

public class NeroAdminToolsTests
{
    [Fact]
    public async Task ReindexAndValidate_ReturnNextStepRecommendations()
    {
        var root = CreateTempKnowledgeRoot();
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        await File.WriteAllTextAsync(Path.Combine(root, "global", "index.md"), "# Global");
        var tools = CreateTools(root);

        var reindex = await tools.nero_admin_reindex();
        var validation = await tools.nero_admin_validate();

        Assert.Contains("nero_admin_validate", reindex.Recommendation);
        Assert.True(validation.IsValid);
        Assert.True(validation.IsCompliant);
        Assert.Empty(validation.ActionableGaps);
        Assert.Contains("Structure and compliance both passed", validation.Recommendation);
    }

    [Fact]
    public async Task Validate_WhenInvalid_ReturnsActionableGapsAndRecommendation()
    {
        var root = CreateTempKnowledgeRoot();
        var tools = CreateTools(root);

        var validation = await tools.nero_admin_validate();

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.ActionableGaps);
        Assert.Contains(validation.Errors[0], validation.ActionableGaps);
        Assert.True(
            validation.Recommendation.Contains("Fix each actionable gap", StringComparison.Ordinal)
            || validation.Recommendation.Contains("Fix structural Errors", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EcosystemHealth_ReturnsAggregatedToolContract()
    {
        var root = CreateTempKnowledgeRoot();
        Directory.CreateDirectory(Path.Combine(root, "domains", "api"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects", "Acme.Sample.Api"));
        await File.WriteAllTextAsync(Path.Combine(root, "domains", "api", "index.md"), "# API");
        await File.WriteAllTextAsync(Path.Combine(root, "projects", "Acme.Sample.Api", "index.md"), "# Sample");
        var tools = CreateTools(root);

        var result = await tools.nero_admin_ecosystem_health();

        Assert.Equal(1, result.ProjectCount);
        Assert.Equal(1, result.DomainCount);
        var project = Assert.Single(result.ProjectsWithIssues);
        Assert.Equal("Acme.Sample.Api", project.Name);
        Assert.Contains(project.Issues, issue => issue.Type == "MissingContext");
        Assert.Single(result.DomainsWithIssues);
    }

    [Fact]
    public async Task TrustAudit_MapsReadOnlyReportWithExplicitReferenceDate()
    {
        var root = CreateTempKnowledgeRoot();
        var snapshotDirectory = Path.Combine(root, "projects", "Acme.Api", "snapshots");
        Directory.CreateDirectory(snapshotDirectory);
        await File.WriteAllTextAsync(Path.Combine(snapshotDirectory, "2025-01-01-review.md"), """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            origin: "Repository review"
            verification_status: verified
            ---
            # Review
            """);
        var tools = CreateTools(root);
        var before = CreateManifest(root);

        var result = await tools.nero_admin_trust_audit("2026-08-24");
        var repeated = await tools.nero_admin_trust_audit("2026-08-24");

        Assert.Equal("2026-08-24", result.AsOfDate);
        Assert.Equal(result.Issues.Count, result.IssueCount);
        Assert.Contains(result.Issues, issue => issue.Type == "StaleSnapshot");
        Assert.Contains("does not edit", result.Recommendation);
        Assert.Equal(result.Issues, repeated.Issues);
        Assert.Equal(before, CreateManifest(root));
        Assert.False(Directory.Exists(Path.Combine(root, ".nero")));
    }

    [Fact]
    public async Task TrustAudit_RejectsInvalidReferenceDateWithActionableFieldError()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateTools(CreateTempKnowledgeRoot()).nero_admin_trust_audit("24/08/2026"));

        Assert.Contains("Field: asOfDate", exception.Message);
        Assert.Contains("yyyy-MM-dd", exception.Message);
        Assert.Contains("Recommendation", exception.Message);
    }

    private static NeroAdminTools CreateTools(string root)
    {
        var databaseOptions = new KnowledgeDatabaseOptions
        {
            Path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge.db")
        };
        var rootOptions = new KnowledgeRootOptions { Path = root };
        var gitCommandRunner = new GitCommandRunner();
        var maintenance = new AdminKnowledgeMaintenanceService(
            new KnowledgeDatabaseConnectionFactory(databaseOptions),
            databaseOptions,
            rootOptions,
            new KnowledgeIndexer(),
            new KnowledgeMarkdownReader(),
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());

        return new NeroAdminTools(
            new AdminStatusService(databaseOptions, rootOptions, new KnowledgeWriteOptions(), gitCommandRunner),
            new AdminGitService(rootOptions, gitCommandRunner, new KnowledgeWriteOptions()),
            maintenance,
            new AdminTrustAuditService(rootOptions, new KnowledgeMarkdownReader(), new AdminProjectFreshnessOptions()));
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string[] CreateManifest(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.OrdinalIgnoreCase)
        .Select(path => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}:{File.GetLastWriteTimeUtc(path).Ticks}")
        .ToArray();
}
