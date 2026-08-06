using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;

namespace Nero.Knowledge.Base.Tests;

public class ProjectUpdateWriterServiceTests
{
    [Fact]
    public async Task UpdateIndexAsync_RequiresExistingIndex()
    {
        var root = CreateTempKnowledgeRoot();
        Directory.CreateDirectory(Path.Combine(root, "projects", "Acme.Missing.Api"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProjectUpdateWriterService().UpdateIndexAsync(
                root,
                new UpdateProjectIndexRequest
                {
                    Project = "Acme.Missing.Api",
                    Domain = "api",
                    Purpose = "Proposito",
                    Arquivos = ["context.md"]
                }));

        Assert.Contains("missing index.md", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateContextAsync_RequiresExistingContext()
    {
        var root = CreateTempKnowledgeRoot();
        var projectDir = Path.Combine(root, "projects", "Acme.Partial.Api");
        Directory.CreateDirectory(projectDir);
        await File.WriteAllTextAsync(Path.Combine(projectDir, "index.md"), "# Index\n");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProjectUpdateWriterService().UpdateContextAsync(
                root,
                new UpdateProjectContextRequest
                {
                    Project = "Acme.Partial.Api",
                    Domain = "api",
                    Purpose = "Proposito",
                    Stack = "ASP.NET",
                    Superficie = "API",
                    ResumoOperacional = "Resumo"
                }));

        Assert.Contains("missing context.md", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateIndexAndContext_RewritesTemplates()
    {
        var root = CreateTempKnowledgeRoot();
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Update.Api",
                Domain = "api",
                Purpose = "Bootstrap",
                Origin = "test"
            });

        var updater = new ProjectUpdateWriterService();
        var indexResult = await updater.UpdateIndexAsync(
            root,
            new UpdateProjectIndexRequest
            {
                Project = "Acme.Update.Api",
                Domain = "api",
                Purpose = "Proposito atualizado",
                Arquivos = ["context.md", "inventory.md"],
                Origin = "Marco 21"
            });

        Assert.False(indexResult.Created);
        Assert.Equal("index", indexResult.FileKind);
        var indexMarkdown = await File.ReadAllTextAsync(indexResult.Path);
        Assert.Contains("Proposito atualizado", indexMarkdown);
        Assert.Contains("`inventory.md`", indexMarkdown);
        Assert.Contains("belongs_to_domain", indexMarkdown);

        var contextResult = await updater.UpdateContextAsync(
            root,
            new UpdateProjectContextRequest
            {
                Project = "Acme.Update.Api",
                Domain = "api",
                Purpose = "API de update",
                Stack = ".NET 8",
                Superficie = "HTTP API",
                ResumoOperacional = "Contexto consolidado curto.",
                SkillOperacional = "$nero"
            });

        Assert.False(contextResult.Created);
        var contextMarkdown = await File.ReadAllTextAsync(contextResult.Path);
        Assert.Contains(".NET 8", contextMarkdown);
        Assert.Contains("Contexto consolidado curto.", contextMarkdown);
        Assert.Contains("$nero", contextMarkdown);
    }

    [Fact]
    public async Task UpdateInventoryAsync_CreatesWhenMissing()
    {
        var root = CreateTempKnowledgeRoot();
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Inventory.Api",
                Domain = "api",
                Purpose = "Bootstrap",
                Origin = "test"
            });

        var result = await new ProjectUpdateWriterService().UpdateInventoryAsync(
            root,
            new UpdateProjectInventoryRequest
            {
                Project = "Acme.Inventory.Api",
                Domain = "api",
                ReviewedAt = "2026-08-05",
                Classificacao = "API de inventario de teste.",
                SinaisTecnicos = ["Solution: Acme.Inventory.Api.sln"],
                GitBranch = "develop",
                GitHead = "abc1234",
                GitRemote = "git@example.com:org/Acme.Inventory.Api"
            });

        Assert.True(result.Created);
        Assert.Equal("inventory", result.FileKind);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: project_inventory", markdown);
        Assert.Contains("reviewed_at: \"2026-08-05\"", markdown);
        Assert.Contains("develop", markdown);
        Assert.Contains("Solution: Acme.Inventory.Api.sln", markdown);
    }

    [Fact]
    public async Task UpdateInventoryAsync_RejectsInvalidDomainAndDate()
    {
        var root = CreateTempKnowledgeRoot();
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Bad.Api",
                Domain = "api",
                Purpose = "Bootstrap",
                Origin = "test"
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProjectUpdateWriterService().UpdateInventoryAsync(
                root,
                new UpdateProjectInventoryRequest
                {
                    Project = "Acme.Bad.Api",
                    Domain = "legacy",
                    ReviewedAt = "2026-08-05",
                    Classificacao = "x",
                    SinaisTecnicos = ["s"]
                }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProjectUpdateWriterService().UpdateInventoryAsync(
                root,
                new UpdateProjectInventoryRequest
                {
                    Project = "Acme.Bad.Api",
                    Domain = "api",
                    ReviewedAt = "05/08/2026",
                    Classificacao = "x",
                    SinaisTecnicos = ["s"]
                }));
    }

    [Fact]
    public async Task UpdateIndexAsync_HonorsReadOnlyWritePolicy()
    {
        var root = CreateTempKnowledgeRoot();
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Readonly.Api",
                Domain = "api",
                Purpose = "Bootstrap",
                Origin = "test"
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProjectUpdateWriterService(
                    writePolicy: new KnowledgeWritePolicy(new KnowledgeWriteOptions { Mode = "read_only" }))
                .UpdateIndexAsync(
                    root,
                    new UpdateProjectIndexRequest
                    {
                        Project = "Acme.Readonly.Api",
                        Domain = "api",
                        Purpose = "Nao deve gravar",
                        Arquivos = ["context.md"]
                    }));

        Assert.Contains("read_only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateContextAsync_PreservesSemanticLinksWhenOmitted()
    {
        var root = CreateTempKnowledgeRoot(includeFront: true);
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Line.Front",
                Domain = "front",
                Purpose = "Bootstrap front",
                Origin = "test"
            });

        var updater = new ProjectUpdateWriterService();
        await updater.UpdateContextAsync(
            root,
            new UpdateProjectContextRequest
            {
                Project = "Acme.Line.Front",
                Domain = "front",
                Purpose = "Front com backend",
                Stack = "Next.js",
                Superficie = "Web",
                ResumoOperacional = "Contexto inicial.",
                SemanticLinks =
                [
                    new ProjectSemanticLink
                    {
                        Type = "uses_backend",
                        Target = "projects/Acme.Line.Api"
                    },
                    new ProjectSemanticLink
                    {
                        Type = "depends_on",
                        Target = "projects/Acme.Auth.Api"
                    }
                ]
            });

        var preserved = await updater.UpdateContextAsync(
            root,
            new UpdateProjectContextRequest
            {
                Project = "Acme.Line.Front",
                Domain = "front",
                Purpose = "Front atualizado",
                Stack = "Next.js 15",
                Superficie = "Web",
                ResumoOperacional = "Sem tocar nos links."
            });

        var preservedMarkdown = await File.ReadAllTextAsync(preserved.Path);
        Assert.Contains("Front atualizado", preservedMarkdown);
        Assert.Contains("uses_backend", preservedMarkdown);
        Assert.Contains("projects/Acme.Line.Api", preservedMarkdown);
        Assert.Contains("depends_on", preservedMarkdown);
        Assert.Contains("projects/Acme.Auth.Api", preservedMarkdown);

        var replaced = await updater.UpdateContextAsync(
            root,
            new UpdateProjectContextRequest
            {
                Project = "Acme.Line.Front",
                Domain = "front",
                Purpose = "Front com gateway",
                Stack = "Next.js",
                Superficie = "Web",
                ResumoOperacional = "Substitui links.",
                SemanticLinks =
                [
                    new ProjectSemanticLink
                    {
                        Type = "uses_backend",
                        Target = "projects/Acme.Gateway.Api"
                    }
                ]
            });

        var replacedMarkdown = await File.ReadAllTextAsync(replaced.Path);
        Assert.Contains("projects/Acme.Gateway.Api", replacedMarkdown);
        Assert.DoesNotContain("projects/Acme.Line.Api", replacedMarkdown);
        Assert.DoesNotContain("depends_on", replacedMarkdown);

        var cleared = await updater.UpdateContextAsync(
            root,
            new UpdateProjectContextRequest
            {
                Project = "Acme.Line.Front",
                Domain = "front",
                Purpose = "Sem links extras",
                Stack = "Next.js",
                Superficie = "Web",
                ResumoOperacional = "Limpeza.",
                SemanticLinks = []
            });

        var clearedMarkdown = await File.ReadAllTextAsync(cleared.Path);
        Assert.Contains("belongs_to_domain", clearedMarkdown);
        Assert.DoesNotContain("uses_backend", clearedMarkdown);
        Assert.DoesNotContain("projects/Acme.Gateway.Api", clearedMarkdown);
    }

    [Fact]
    public async Task UpdateIndexAsync_RejectsInvertedUsesBackend()
    {
        var root = CreateTempKnowledgeRoot(includeFront: true);
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Line.Api",
                Domain = "api",
                Purpose = "Bootstrap api",
                Origin = "test"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProjectUpdateWriterService().UpdateIndexAsync(
                root,
                new UpdateProjectIndexRequest
                {
                    Project = "Acme.Line.Api",
                    Domain = "api",
                    Purpose = "Nao deve gravar",
                    Arquivos = ["context.md"],
                    SemanticLinks =
                    [
                        new ProjectSemanticLink
                        {
                            Type = "uses_backend",
                            Target = "projects/Acme.Line.Front"
                        }
                    ]
                }));

        Assert.Contains("inverted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateIndexAsync_RejectsMinimalTypeInSemanticLinks()
    {
        var root = CreateTempKnowledgeRoot();
        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Update.Api",
                Domain = "api",
                Purpose = "Bootstrap",
                Origin = "test"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProjectUpdateWriterService().UpdateIndexAsync(
                root,
                new UpdateProjectIndexRequest
                {
                    Project = "Acme.Update.Api",
                    Domain = "api",
                    Purpose = "x",
                    Arquivos = ["context.md"],
                    SemanticLinks =
                    [
                        new ProjectSemanticLink
                        {
                            Type = "documents",
                            Target = "projects/Acme.Update.Api"
                        }
                    ]
                }));

        Assert.Contains("minimal link", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempKnowledgeRoot(bool includeFront = false)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Nero.Knowledge.Base.Tests",
            Guid.NewGuid().ToString("N"),
            "knowledge");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "domains", "api"));
        File.WriteAllText(
            Path.Combine(path, "domains", "api", "index.md"),
            "---\ntype: domain_index\nscope: domain\ndomain: api\nstatus: active\n---\n# api\n");
        if (includeFront)
        {
            Directory.CreateDirectory(Path.Combine(path, "domains", "front"));
            File.WriteAllText(
                Path.Combine(path, "domains", "front", "index.md"),
                "---\ntype: domain_index\nscope: domain\ndomain: front\nstatus: active\n---\n# front\n");
        }

        return path;
    }
}
