using Nero.Knowledge.Base.Mcp.Application.Contracts.ValidationRules;
using Nero.Knowledge.Base.Mcp.Application.Services.ValidationRules;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class ValidationRuleWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/validation-and-tests/validar-estoque-disponivel.md")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/validation-and-tests/validar-estoque-disponivel.md")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Api", "projects/Acme.Api/validation-and-tests/validar-estoque-disponivel.md")]
    public async Task WriteAsync_ResolvesPathByScope(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativePath)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);

        var result = await new ValidationRuleWriterService().WriteAsync(root, request);

        Assert.Equal(expectedRelativePath, result.RelativePath);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: validation_rule", markdown);
        Assert.Contains("# Validar estoque disponivel", markdown);
        Assert.Contains("## Objetivo", markdown);
        Assert.Contains("## Criterio", markdown);
        Assert.Contains("## Evidencia esperada", markdown);
        Assert.Contains("## Lacunas conhecidas", markdown);
        Assert.Contains("links:", markdown);
        Assert.Contains("type: documents", markdown);
        if (scope == KnowledgeScope.Global)
        {
            Assert.Contains("target: \"global\"", markdown);
        }
        else if (scope == KnowledgeScope.Domain)
        {
            Assert.Contains("type: belongs_to_domain", markdown);
            Assert.Contains($"target: \"domains/{domain}\"", markdown);
        }
        else
        {
            Assert.Contains($"target: \"projects/{project}/index\"", markdown);
        }

        Assert.Contains($"- Revisar ate: {DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)):yyyy-MM-dd}", markdown);
    }

    [Fact]
    public async Task WriteAsync_PreventsOverwrite()
    {
        var root = CreateTempKnowledgeRoot();
        var service = new ValidationRuleWriterService();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "api");
        await service.WriteAsync(root, request);

        await Assert.ThrowsAsync<IOException>(() => service.WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesRequiredScopeContext()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Project) with
        {
            Project = null
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new ValidationRuleWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesCriteria()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Global) with
        {
            Criteria = " "
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new ValidationRuleWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesEvidence()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Global) with
        {
            Evidence = " "
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new ValidationRuleWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "../api");

        await Assert.ThrowsAsync<ArgumentException>(() => new ValidationRuleWriterService().WriteAsync(root, request));
    }

    private static RegisterValidationRuleRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterValidationRuleRequest
        {
            Title = "Validar estoque disponivel",
            Scope = scope,
            Domain = domain,
            Project = project,
            Rule = "Proteger o fluxo contra pedido sem saldo suficiente.",
            Criteria = "Dado produto sem saldo, a validacao deve recusar o pedido antes da persistencia.",
            Evidence = "Teste automatizado deve cobrir produto sem saldo com mensagem acionavel.",
            KnownGaps = "Cenarios de concorrencia ainda exigem teste integrado.",
            Origin = "Teste automatizado"
        };
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
