using Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Graph;
using Nero.Knowledge.Base.Mcp.Application.Services.Search;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

/// <summary>
/// W1 — objective fixture for register → Markdown → reindex → validate → search/graph.
/// </summary>
public class RegisterValidateSearchLoopTests
{
    private const string ProjectName = "Acme.Api";
    private const string ConcretePatternId = "domains/api/patterns/http-versioning";
    private const string UniqueEvidencePhrase = "W1LoopUniqueRotasEvidencePhrase";

    [Fact]
    public async Task RegisterSnapshot_PreferredLinks_ReindexValidateSearchAndGraphSucceed()
    {
        var root = CreateTempKnowledgeRoot();
        await SeedConcreteFixtureAsync(root);

        var writeResult = await new SnapshotWriterService().WriteAsync(
            root,
            new RegisterSnapshotRequest
            {
                Title = "Snapshot W1 loop rotas",
                Scope = KnowledgeScope.Project,
                Project = ProjectName,
                Context = "Inventario tecnico das rotas publicas no loop W1.",
                Evidence = UniqueEvidencePhrase,
                Origin = "RegisterValidateSearchLoopTests",
                RelatesTo = [$"projects/{ProjectName}/index"],
                Evidences = [ConcretePatternId]
            });

        var markdown = await File.ReadAllTextAsync(writeResult.Path);
        Assert.DoesNotContain("relates_to", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type: documents", markdown, StringComparison.Ordinal);
        Assert.Contains("type: evidences", markdown, StringComparison.Ordinal);
        Assert.Contains($"target: \"projects/{ProjectName}/index\"", markdown, StringComparison.Ordinal);
        Assert.Contains($"target: \"{ConcretePatternId}\"", markdown, StringComparison.Ordinal);

        var admin = CreateAdminService(root, out var databasePath, out var connectionFactory);
        var reindex = await admin.ReindexAsync();
        Assert.True(reindex.IndexedNodes >= 3, $"Expected seeded + snapshot nodes, got {reindex.IndexedNodes}.");
        Assert.True(File.Exists(databasePath));

        var validation = await admin.ValidateAsync();
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Empty(validation.Errors);
        Assert.True(validation.EdgeCount >= 2);

        await using var connection = connectionFactory.CreateConnection();
        var searchHits = await new KnowledgeSearchService().SearchAsync(
            connection,
            UniqueEvidencePhrase,
            project: ProjectName);
        Assert.Contains(
            searchHits,
            hit => hit.Id.Contains("/snapshots/", StringComparison.OrdinalIgnoreCase)
                && hit.Title.Contains("W1 loop", StringComparison.OrdinalIgnoreCase));

        var related = await new RelatedKnowledgeService().FindRelatedAsync(
            connection,
            project: ProjectName,
            topic: "W1");
        Assert.Contains(
            related,
            node => node.Id.Equals(ConcretePatternId, StringComparison.OrdinalIgnoreCase)
                && node.Relation == KnowledgeRelationType.Evidences);
    }

    [Fact]
    public async Task WriteAsync_EvidencesTargetingHub_Throws()
    {
        var root = CreateTempKnowledgeRoot();
        await SeedConcreteFixtureAsync(root);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => new SnapshotWriterService().WriteAsync(
            root,
            new RegisterSnapshotRequest
            {
                Title = "Snapshot hub evidence blocked",
                Scope = KnowledgeScope.Project,
                Project = ProjectName,
                Context = "Writer deve rejeitar hub em evidences no register.",
                Evidence = "Evidencia apontando para pasta patterns.",
                Origin = "RegisterValidateSearchLoopTests",
                RelatesTo = [$"projects/{ProjectName}/index"],
                Evidences = ["domains/api/patterns"]
            }));

        Assert.Equal(nameof(RegisterSnapshotRequest.Evidences), exception.ParamName);
        Assert.Contains("directory hub", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("domains/api/patterns", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories),
            path => path.Contains("snapshots", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_EvidencesTargetingHub_InLegacyMarkdown_Fails()
    {
        var root = CreateTempKnowledgeRoot();
        await SeedConcreteFixtureAsync(root);
        await WriteMarkdownAsync(root, $"projects/{ProjectName}/snapshots/2026-07-01-hub-evidence-legacy.md", """
            ---
            type: snapshot
            scope: project
            project: Acme.Api
            links:
              - type: evidences
                target: domains/api/patterns
              - type: documents
                target: projects/Acme.Api/index
            ---
            # Snapshot hub evidence legacy

            Markdown legado com evidences para hub (escritor atual ja bloqueia).
            """);

        var admin = CreateAdminService(root, out _, out _);
        var validation = await admin.ValidateAsync();

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            error => error.Contains("evidences", StringComparison.OrdinalIgnoreCase)
                && error.Contains("directory hub", StringComparison.OrdinalIgnoreCase)
                && error.Contains("domains/api/patterns", StringComparison.Ordinal));
    }

    private static async Task SeedConcreteFixtureAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "domains"));
        Directory.CreateDirectory(Path.Combine(root, "global"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));

        await WriteMarkdownAsync(root, "domains/api/index.md", """
            ---
            type: domain_index
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

            Padrao concreto de versionamento HTTP para o loop W1.
            """);
        await WriteMarkdownAsync(root, $"projects/{ProjectName}/index.md", """
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
    }

    private static AdminKnowledgeMaintenanceService CreateAdminService(
        string root,
        out string databasePath,
        out KnowledgeDatabaseConnectionFactory connectionFactory)
    {
        databasePath = Path.Combine(
            Path.GetTempPath(),
            "Nero.Knowledge.Base.Tests",
            Guid.NewGuid().ToString("N"),
            "knowledge.db");
        var databaseOptions = new KnowledgeDatabaseOptions { Path = databasePath };
        connectionFactory = new KnowledgeDatabaseConnectionFactory(databaseOptions);

        return new AdminKnowledgeMaintenanceService(
            connectionFactory,
            databaseOptions,
            new KnowledgeRootOptions { Path = root },
            new KnowledgeIndexer(),
            new KnowledgeMarkdownReader(),
            new AdminIndexConsistencyOptions(),
            new AdminProjectFreshnessOptions());
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Nero.Knowledge.Base.Tests",
            Guid.NewGuid().ToString("N"),
            "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
