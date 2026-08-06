using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeProjectContextServiceTests
{
    [Fact]
    public async Task GetProjectContextAsync_AggregatesProjectSections()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice do projeto.");
        await WriteProjectMarkdownAsync(root, "context.md", "# Contexto\n\nFluxo principal.");
        await WriteProjectMarkdownAsync(root, "patterns.md", "# Padroes\n\nPadrao local.");
        await WriteProjectMarkdownAsync(root, "business-rules.md", "# Regras\n\nRegra local.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-01-primeira.md", "# Primeira decisao\n\nAntiga.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-02-segunda.md", "# Segunda decisao\n\nRecente.");
        await WriteProjectMarkdownAsync(root, "troubleshooting/2026-07-03-ajuste.md", "# Ajuste\n\nCorrecao.");
        await WriteMarkdownAsync(root, "projects/Acme.Receiving.Api/decisions/2026-07-04-outra.md", "# Outra\n\nNao entra.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Api");

        Assert.True(result.Exists);
        Assert.Equal("Acme.Api", result.Project);
        Assert.Equal("Inventory API", result.Index?.Title);
        Assert.Equal("Contexto", result.Context?.Title);
        Assert.Equal("Padroes", result.Patterns?.Title);
        Assert.Equal("Regras", result.BusinessRules?.Title);
        Assert.Equal(["Segunda decisao", "Primeira decisao"], result.Decisions.Select(section => section.Title));
        Assert.Equal("Ajuste", Assert.Single(result.Troubleshooting).Title);
        Assert.All(result.Decisions, section => Assert.Contains("Acme.Api", section.Path));
    }

    [Fact]
    public async Task GetProjectContextAsync_RespectsInclusionFlagsAndRecentLimit()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-01-primeira.md", "# Primeira decisao\n\nAntiga.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-02-segunda.md", "# Segunda decisao\n\nRecente.");
        await WriteProjectMarkdownAsync(root, "troubleshooting/2026-07-03-ajuste.md", "# Ajuste\n\nCorrecao.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Api",
            includeDecisions: true,
            includeTroubleshooting: false,
            recentLimit: 1);

        Assert.True(result.Exists);
        Assert.Equal("Segunda decisao", Assert.Single(result.Decisions).Title);
        Assert.Empty(result.Troubleshooting);
    }

    [Fact]
    public async Task GetProjectContextAsync_SeparatesActiveAndSupersededDecisions()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-01-antiga.md", "# Decisao antiga\n\nAntiga.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-02-nova.md", """
            ---
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-01-antiga
            ---
            # Decisao nova

            Vigente.
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Api");

        Assert.Equal(["Decisao nova"], result.ActiveDecisions.Select(decision => decision.Title));
        var superseded = Assert.Single(result.SupersededDecisions);
        Assert.Equal("Decisao antiga", superseded.Decision.Title);
        Assert.Equal("Decisao nova", Assert.Single(superseded.SupersededBy).Title);
        Assert.Equal(["Decisao nova"], result.Decisions.Select(decision => decision.Title));
        Assert.True(result.HasSupersededDecisions);
    }

    [Fact]
    public async Task GetProjectContextAsync_SeparatesChainedSupersededDecisions()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-01-antiga.md", "# Decisao antiga\n\nAntiga.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-02-intermediaria.md", """
            ---
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-01-antiga
            ---
            # Decisao intermediaria

            Temporaria.
            """);
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-03-final.md", """
            ---
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-02-intermediaria
            ---
            # Decisao final

            Vigente.
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Api");

        Assert.Equal(["Decisao final"], result.ActiveDecisions.Select(decision => decision.Title));
        Assert.Equal(["Decisao intermediaria", "Decisao antiga"], result.SupersededDecisions.Select(item => item.Decision.Title));
        Assert.Equal(
            "Decisao final",
            Assert.Single(result.SupersededDecisions.Single(item => item.Decision.Title == "Decisao intermediaria").SupersededBy).Title);
        Assert.Equal(
            "Decisao intermediaria",
            Assert.Single(result.SupersededDecisions.Single(item => item.Decision.Title == "Decisao antiga").SupersededBy).Title);
    }

    [Theory]
    [InlineData("global/decisions/2026-07-02-global.md", "Decisao global")]
    [InlineData("domains/api/decisions/2026-07-02-dominio.md", "Decisao de dominio")]
    [InlineData("projects/Acme.Governanca.Api/decisions/2026-07-02-outro-projeto.md", "Decisao de outro projeto")]
    public async Task GetProjectContextAsync_RecognizesCrossScopeSupersedes(
        string replacingDecisionPath,
        string replacingDecisionTitle)
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice.");
        await WriteProjectMarkdownAsync(root, "decisions/2026-07-01-antiga.md", "# Decisao antiga\n\nAntiga.");
        await WriteMarkdownAsync(root, replacingDecisionPath, $$"""
            ---
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-01-antiga
            ---
            # {{replacingDecisionTitle}}

            Vigente em escopo externo.
            """);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Api");

        Assert.Empty(result.ActiveDecisions);
        Assert.Empty(result.Decisions);
        Assert.True(result.HasSupersededDecisions);
        var superseded = Assert.Single(result.SupersededDecisions);
        Assert.Equal("Decisao antiga", superseded.Decision.Title);
        var supersededBy = Assert.Single(superseded.SupersededBy);
        Assert.Equal(replacingDecisionTitle, supersededBy.Title);
        Assert.EndsWith(replacingDecisionPath, supersededBy.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProjectContextAsync_ReturnsEmptyResultForUnknownProject()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteProjectMarkdownAsync(root, "index.md", "# Inventory API\n\nIndice.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeProjectContextService().GetProjectContextAsync(
            connection,
            "Acme.Missing.Project");

        Assert.False(result.Exists);
        Assert.Equal("Acme.Missing.Project", result.Project);
        Assert.Null(result.Index);
        Assert.Null(result.Context);
        Assert.Null(result.Patterns);
        Assert.Null(result.BusinessRules);
        Assert.Empty(result.Decisions);
        Assert.Empty(result.Troubleshooting);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static Task WriteProjectMarkdownAsync(string root, string relativePath, string content)
    {
        return WriteMarkdownAsync(root, $"projects/Acme.Api/{relativePath}", content);
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
