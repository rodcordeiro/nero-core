using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Services.Graph;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class RelatedKnowledgeServiceTests
{
    [Fact]
    public async Task FindRelatedAsync_ExpandsDirectRelationsAndCommonDomain()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nDominio API.");
        await WriteMarkdownAsync(root, "domains/api/patterns.md", "# Padroes API\n\nPadrao de estoque.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Receiving.Api/index.md",
            """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Recebimento API

            Indice do Recebimento.
            """);
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Receiving.Api/context.md",
            """
            ---
            links:
              - type: related_pattern
                target: domains/api/patterns
            ---
            # Contexto Recebimento

            Estoque e recebimento de produtos.
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new RelatedKnowledgeService().FindRelatedAsync(
            connection,
            project: "Acme.Receiving.Api",
            topic: "estoque");

        Assert.Contains(results, result =>
            result.Id == "domains/api/patterns"
            && result.Relation == KnowledgeRelationType.RelatedPattern
            && result.Score == 1.0m);
        Assert.Contains(results, result =>
            result.Id == "domains/api/index"
            && result.Relation == KnowledgeRelationType.BelongsToDomain
            && result.Score == 0.45m);
    }

    [Fact]
    public async Task FindRelatedAsync_FindsSiblingProjectByCommonDomainAndTopic()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nDominio API.");
        await WriteProjectIndexAsync(root, "Acme.Receiving.Api", "Recebimento API");
        await WriteMarkdownAsync(root, "projects/Acme.Receiving.Api/context.md", "# Recebimento\n\nEstoque em recebimento.");
        await WriteProjectIndexAsync(root, "Acme.Api", "Inventory API");
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Inventory\n\nEstoque em transito.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new RelatedKnowledgeService().FindRelatedAsync(
            connection,
            project: "Acme.Receiving.Api",
            topic: "estoque");

        var sibling = Assert.Single(results, result => result.Id == "projects/Acme.Api/context");
        Assert.Equal("Acme.Api", sibling.Project);
        Assert.Equal(KnowledgeRelationType.BelongsToDomain, sibling.Relation);
        Assert.Equal(0.35m, sibling.Score);
        Assert.Contains("common domain", sibling.Evidence);
    }

    [Fact]
    public async Task FindRelatedAsync_RespectsRelationTypeFilterForDirectEdges()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nDominio API.");
        await WriteMarkdownAsync(root, "domains/api/patterns.md", "# Padroes API\n\nPadrao de estoque.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Receiving.Api/context.md",
            """
            ---
            links:
              - type: related_pattern
                target: domains/api/patterns
            ---
            # Contexto Recebimento

            Estoque e recebimento de produtos.
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new RelatedKnowledgeService().FindRelatedAsync(
            connection,
            project: "Acme.Receiving.Api",
            topic: "estoque",
            relationTypes: [KnowledgeRelationType.DependsOn]);

        Assert.DoesNotContain(results, result => result.Id == "domains/api/patterns");
    }

    [Fact]
    public async Task FindRelatedAsync_DoesNotLetBelongsToDomainFloodTopResultsWhenTypedEdgesExist()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nDominio API compartilhado.");
        await WriteMarkdownAsync(root, "domains/api/patterns/estoque.md", "# Padrao estoque\n\nPadrao tipado de estoque.");
        await WriteMarkdownAsync(root, "domains/api/decisions/reservas.md", "# Decisao reservas\n\nDecisao tipada de reservas.");

        await WriteProjectWithTypedEdgesAsync(root, "Acme.Receiving.Api", "Recebimento");

        // Many same-domain siblings — without ranking/cap these would dominate via BelongsToDomain.
        for (var i = 1; i <= 12; i++)
        {
            await WriteProjectIndexAsync(root, $"Acme.Sibling{i:00}.Api", $"Sibling {i:00} API");
            await WriteMarkdownAsync(
                root,
                $"projects/Acme.Sibling{i:00}.Api/context.md",
                $"# Sibling {i:00}\n\nEstoque compartilhado no dominio API.");
        }

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var results = await new RelatedKnowledgeService().FindRelatedAsync(
            connection,
            project: "Acme.Receiving.Api",
            topic: "estoque",
            depth: 2);

        Assert.True(results.Count >= 2, "Expected typed edges plus limited BelongsToDomain context.");

        var topHalfCount = Math.Max(1, results.Count / 2);
        var topHalf = results.Take(topHalfCount).ToList();
        var belongsToDomainShare = topHalf.Count(result => result.Relation == KnowledgeRelationType.BelongsToDomain)
            / (double)topHalf.Count;

        Assert.True(
            belongsToDomainShare < 0.5,
            $"Expected BelongsToDomain share in top half < 50%, got {belongsToDomainShare:P0} ({topHalf.Count(r => r.Relation == KnowledgeRelationType.BelongsToDomain)}/{topHalf.Count}).");
        Assert.Contains(results, result =>
            result.Relation == KnowledgeRelationType.RelatedPattern && result.Score == 1.0m);
        Assert.Contains(results, result =>
            result.Relation == KnowledgeRelationType.RelatedDecision && result.Score == 1.0m);
        Assert.True(
            results.Count(result =>
                result.Relation == KnowledgeRelationType.BelongsToDomain
                && result.Project is not null
                && !string.Equals(result.Project, "Acme.Receiving.Api", StringComparison.OrdinalIgnoreCase))
            <= RelatedKnowledgeService.MaxBelongsToDomainSiblings);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static Task WriteProjectIndexAsync(string root, string project, string title)
    {
        return WriteMarkdownAsync(
            root,
            $"projects/{project}/index.md",
            $$"""
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # {{title}}

            Projeto API.
            """);
    }

    private static Task WriteProjectWithTypedEdgesAsync(string root, string project, string title)
    {
        return WriteMarkdownAsync(
            root,
            $"projects/{project}/index.md",
            $$"""
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
              - type: related_pattern
                target: domains/api/patterns/estoque
              - type: related_decision
                target: domains/api/decisions/reservas
              - type: documents
                target: domains/api/index
            ---
            # {{title}} API

            Estoque e reservas no projeto {{title}}.
            """);
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
