using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;

namespace Nero.Knowledge.Base.Tests;

public class DomainWriterServiceTests
{
    [Fact]
    public async Task RegisterUpdateInactivate_LifecycleWorks()
    {
        var root = CreateTempKnowledgeRoot();
        var writer = new DomainWriterService();

        var registered = await writer.RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "billing",
                Purpose = "Dominio de billing.",
                Origin = "Marco 22"
            });

        Assert.True(registered.Created);
        Assert.Equal("active", registered.Status);
        Assert.Contains("status: active", await File.ReadAllTextAsync(registered.Path));

        var updated = await writer.UpdateAsync(
            root,
            new UpdateDomainRequest
            {
                Domain = "billing",
                Purpose = "Dominio de billing atualizado.",
                Arquivos = ["patterns.md"],
                RegrasLeitura = "- Preferir MCP.",
                Origin = "Marco 22 update"
            });

        Assert.Equal("update", updated.Action);
        Assert.Contains("Dominio de billing atualizado.", await File.ReadAllTextAsync(updated.Path));

        var inactivated = await writer.InactivateAsync(
            root,
            new InactivateDomainRequest
            {
                Domain = "billing",
                Motivo = "Obsoleto",
                Origin = "Marco 22"
            });

        Assert.Equal("inactive", inactivated.Status);
        Assert.Contains("status: inactive", await File.ReadAllTextAsync(inactivated.Path));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.RegisterAsync(
                root,
                new RegisterDomainRequest
                {
                    Domain = "billing",
                    Purpose = "x",
                    Origin = "y"
                }));

        var reactivated = await writer.UpdateAsync(
            root,
            new UpdateDomainRequest
            {
                Domain = "billing",
                Purpose = "Reativado.",
                Arquivos = ["patterns.md"],
                Reativar = true
            });

        Assert.Equal("reactivate", reactivated.Action);
        Assert.Equal("active", reactivated.Status);
    }

    [Fact]
    public async Task Update_PreservesFonteConsolidadaAndSourceFor()
    {
        var root = CreateTempKnowledgeRoot();
        var writer = new DomainWriterService();
        await writer.RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "billing",
                Purpose = "Billing.",
                Origin = "test",
                FonteConsolidada = "Fonte inicial.",
                SourceFor = ["Acme.Billing.API"]
            });

        await writer.UpdateAsync(
            root,
            new UpdateDomainRequest
            {
                Domain = "billing",
                Titulo = "Dominio Billing Acme",
                Purpose = "Billing atualizado.",
                FonteConsolidada = "Fonte consolidada preservavel.",
                Arquivos = ["`patterns.md`: padroes."],
                RegrasLeitura = "- Ler patterns primeiro.",
                SourceFor = ["Acme.Billing.API", "Acme.Billing.Front"]
            });

        var markdown = await File.ReadAllTextAsync(Path.Combine(root, "domains", "billing", "index.md"));
        Assert.Contains("## Fonte consolidada", markdown);
        Assert.Contains("Fonte consolidada preservavel.", markdown);
        Assert.Contains("target: projects/Acme.Billing.API", markdown);
        Assert.Contains("target: projects/Acme.Billing.Front", markdown);
        Assert.Contains("# Dominio Billing Acme", markdown);
        Assert.Contains("`patterns.md`: padroes.", markdown);
    }

    [Fact]
    public async Task Update_OmittingSourceFor_PreservesExistingLinks()
    {
        var root = CreateTempKnowledgeRoot();
        var writer = new DomainWriterService();
        await writer.RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "billing",
                Purpose = "Billing.",
                Origin = "test",
                SourceFor = ["Acme.Billing.API", "Acme.Billing.Front"]
            });

        await writer.UpdateAsync(
            root,
            new UpdateDomainRequest
            {
                Domain = "billing",
                Purpose = "Billing sem tocar sourceFor.",
                Arquivos = ["patterns.md"]
                // SourceFor omitted on purpose
            });

        var markdown = await File.ReadAllTextAsync(Path.Combine(root, "domains", "billing", "index.md"));
        Assert.Contains("target: projects/Acme.Billing.API", markdown);
        Assert.Contains("target: projects/Acme.Billing.Front", markdown);
        Assert.Contains("Billing sem tocar sourceFor.", markdown);
    }

    [Fact]
    public async Task Update_EmptySourceFor_ClearsLinksIntentionally()
    {
        var root = CreateTempKnowledgeRoot();
        var writer = new DomainWriterService();
        await writer.RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "billing",
                Purpose = "Billing.",
                Origin = "test",
                SourceFor = ["Acme.Billing.API"]
            });

        await writer.UpdateAsync(
            root,
            new UpdateDomainRequest
            {
                Domain = "billing",
                Purpose = "Billing limpo.",
                Arquivos = ["patterns.md"],
                SourceFor = []
            });

        var markdown = await File.ReadAllTextAsync(Path.Combine(root, "domains", "billing", "index.md"));
        Assert.DoesNotContain("source_for", markdown);
        Assert.Contains("target: domains/billing", markdown);
    }

    [Fact]
    public async Task Inactivate_WithLinkedProjects_RequiresConfirmation()
    {
        var root = CreateTempKnowledgeRoot();
        var domains = new DomainWriterService();
        await domains.RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "legacy",
                Purpose = "Legacy",
                Origin = "test"
            });

        await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Legacy.API",
                Domain = "legacy",
                Purpose = "API",
                Origin = "test"
            });

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            domains.InactivateAsync(
                root,
                new InactivateDomainRequest
                {
                    Domain = "legacy",
                    Motivo = "sunset",
                    Origin = "test"
                }));
        Assert.Contains("INACTIVATE_WITH_LINKED_PROJECTS", blocked.Message);

        var forced = await domains.InactivateAsync(
            root,
            new InactivateDomainRequest
            {
                Domain = "legacy",
                Motivo = "sunset",
                Origin = "test",
                Confirmacao = DomainWriterService.LinkedProjectsConfirmation,
                Evidencia = "Projetos migrados para api."
            });

        Assert.Equal("inactive", forced.Status);
        Assert.Contains("Acme.Legacy.API", forced.LinkedProjects);
    }

    [Fact]
    public async Task RegisterProject_AcceptsNewlyRegisteredDomain()
    {
        var root = CreateTempKnowledgeRoot();
        await new DomainWriterService().RegisterAsync(
            root,
            new RegisterDomainRequest
            {
                Domain = "observability",
                Purpose = "Obs",
                Origin = "test"
            });

        var project = await new ProjectWriterService().WriteAsync(
            root,
            new RegisterProjectRequest
            {
                Project = "Acme.Obs.API",
                Domain = "observability",
                Purpose = "API",
                Origin = "test"
            });

        Assert.True(project.Created);
        Assert.Equal("observability", project.Domain);
    }

    [Fact]
    public void ValidateSlug_RejectsReservedAndInvalid()
    {
        var catalog = new ActiveDomainCatalog();
        Assert.Throws<ArgumentException>(() => catalog.ValidateSlug("API"));
        Assert.Throws<ArgumentException>(() => catalog.ValidateSlug("global"));
        Assert.Throws<ArgumentException>(() => catalog.ValidateSlug("../x"));
        catalog.ValidateSlug("billing");
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
}
