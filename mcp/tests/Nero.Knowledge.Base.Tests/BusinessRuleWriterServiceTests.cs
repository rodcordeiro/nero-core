using Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class BusinessRuleWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/business-rules/regra-de-estoque.md")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/business-rules/regra-de-estoque.md")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Receiving.Api", "projects/Acme.Receiving.Api/business-rules/regra-de-estoque.md")]
    public async Task WriteAsync_ResolvesPathByScope(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativePath)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);

        var result = await new BusinessRuleWriterService().WriteAsync(root, request);

        Assert.Equal(expectedRelativePath, result.RelativePath);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: business_rule", markdown);
        Assert.Contains("# Regra de estoque", markdown);
        Assert.Contains("## Evidencia", markdown);
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
        var service = new BusinessRuleWriterService();
        var request = CreateRequest(KnowledgeScope.Global);
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

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new BusinessRuleWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "../api");

        await Assert.ThrowsAsync<ArgumentException>(() => new BusinessRuleWriterService().WriteAsync(root, request));
    }

    private static RegisterBusinessRuleRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterBusinessRuleRequest
        {
            Title = "Regra de estoque",
            Scope = scope,
            Domain = domain,
            Project = project,
            Rule = "Produtos sem saldo nao podem ser transferidos.",
            Evidence = "Validacao confirmada em teste de integracao.",
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
