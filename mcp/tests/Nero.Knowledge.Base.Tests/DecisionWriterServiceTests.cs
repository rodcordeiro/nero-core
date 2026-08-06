using Nero.Knowledge.Base.Mcp.Application.Contracts.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Services.Decisions;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class DecisionWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/decisions/")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/decisions/")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Api", "projects/Acme.Api/decisions/")]
    public async Task WriteAsync_ResolvesPathByScopeWithDateAndSlug(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativeDirectory)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);
        var expectedFileName = $"{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}-usar-template-adr-curto.md";

        var result = await new DecisionWriterService().WriteAsync(root, request);

        Assert.Equal(expectedRelativeDirectory + expectedFileName, result.RelativePath);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: decision", markdown);
        Assert.Contains("# Usar template ADR curto", markdown);
        Assert.Contains("## Problema", markdown);
        Assert.Contains("## Opcoes", markdown);
        Assert.Contains("## Decisao", markdown);
        Assert.Contains("## Consequencias", markdown);
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
        var service = new DecisionWriterService();
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

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new DecisionWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "../api");

        await Assert.ThrowsAsync<ArgumentException>(() => new DecisionWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesRequiredDecisionFields()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Global) with
        {
            Consequences = " "
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new DecisionWriterService().WriteAsync(root, request));
    }

    private static RegisterDecisionRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterDecisionRequest
        {
            Title = "Usar template ADR curto",
            Scope = scope,
            Domain = domain,
            Project = project,
            Problem = "Decisoes tecnicas precisam ser registradas de forma consistente.",
            Options = "- Registrar em texto livre\n- Usar template ADR curto",
            Decision = "Usar template ADR curto para decisoes tecnicas.",
            Consequences = "Decisoes ficam rastreaveis e comparaveis.",
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
