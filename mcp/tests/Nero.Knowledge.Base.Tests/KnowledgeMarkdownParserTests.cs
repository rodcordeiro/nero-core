using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeMarkdownParserTests
{
    [Fact]
    public void Parse_WithDomainMarkdown_ExtractsTitleContentFrontmatterAndScope()
    {
        var root = CreateTempKnowledgeRoot();
        var path = Path.Combine(root, "domains", "mobile", "patterns.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var markdown = """
            ---
            owner: plataforma
            type: pattern
            ---
            # Padroes Mobile

            Conteudo do dominio.
            """;

        var document = new KnowledgeMarkdownParser().Parse(root, path, markdown);

        Assert.Equal("domains/mobile/patterns", document.Id);
        Assert.Equal("Padroes Mobile", document.Title);
        Assert.Equal("knowledge/domains/mobile/patterns.md", document.Path);
        Assert.DoesNotContain("owner: plataforma", document.Content);
        Assert.Contains("Conteudo do dominio.", document.Content);
        Assert.Equal("plataforma", document.Frontmatter["owner"]);
        Assert.Equal(KnowledgeScope.Domain, document.Scope);
        Assert.Equal(KnowledgeNodeType.Pattern, document.Type);
        Assert.Equal("mobile", document.Domain);
        Assert.Null(document.Project);
    }

    [Fact]
    public void Parse_WithProjectDecision_InfersProjectAndDecisionTypeFromPath()
    {
        var root = CreateTempKnowledgeRoot();
        var path = Path.Combine(root, "projects", "Acme.Mobile", "decisions", "2026-06-29-auth-manager.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var markdown = """
            # Migrar autenticacao para AuthManager

            ## Decisao

            Centralizar autenticacao.
            """;

        var document = new KnowledgeMarkdownParser().Parse(root, path, markdown);

        Assert.Equal(KnowledgeScope.Project, document.Scope);
        Assert.Equal(KnowledgeNodeType.Decision, document.Type);
        Assert.Null(document.Domain);
        Assert.Equal("Acme.Mobile", document.Project);
    }

    [Fact]
    public void Parse_WithProjectsIndex_DoesNotInferFakeProject()
    {
        var root = CreateTempKnowledgeRoot();
        var path = Path.Combine(root, "projects", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var document = new KnowledgeMarkdownParser().Parse(root, path, "# Projetos\n");

        Assert.Equal(KnowledgeScope.Global, document.Scope);
        Assert.Null(document.Project);
        Assert.Equal(KnowledgeNodeType.Index, document.Type);
    }

    [Fact]
    public void Parse_WithFrontmatterLinks_ExtractsSemanticLinks()
    {
        var root = CreateTempKnowledgeRoot();
        var path = Path.Combine(root, "projects", "Acme.Api", "context.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var markdown = """
            ---
            type: context
            links:
              - type: belongs_to_domain
                target: domains/api
              - type: depends_on
                target: knowledge/projects/Acme.Events.API/index.md
            ---
            # Inventory API
            """;

        var document = new KnowledgeMarkdownParser().Parse(root, path, markdown);

        Assert.Equal(2, document.Links.Count);
        Assert.Equal("belongs_to_domain", document.Links[0].Type);
        Assert.Equal("domains/api", document.Links[0].Target);
        Assert.Equal("depends_on", document.Links[1].Type);
        Assert.Equal("knowledge/projects/Acme.Events.API/index.md", document.Links[1].Target);
    }

    [Fact]
    public async Task ReadAsync_ReadsMarkdownFilesRecursively()
    {
        var root = CreateTempKnowledgeRoot();
        var firstPath = Path.Combine(root, "global", "index.md");
        var secondPath = Path.Combine(root, "projects", "Acme.Mobile", "context.md");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        await File.WriteAllTextAsync(firstPath, "# Global\n");
        await File.WriteAllTextAsync(secondPath, "# Inventory Context\n");
        await File.WriteAllTextAsync(Path.Combine(root, "ignored.txt"), "ignore");

        var documents = await new KnowledgeMarkdownReader().ReadAsync(root);

        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document => document.Path == "knowledge/global/index.md");
        Assert.Contains(documents, document => document.Path == "knowledge/projects/Acme.Mobile/context.md");
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
