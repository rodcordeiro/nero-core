using Nero.Knowledge.Base.Mcp.Application.Contracts.Patterns;
using Nero.Knowledge.Base.Mcp.Application.Services.Patterns;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class PatternWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/patterns/padrao-de-cache.md")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/patterns/padrao-de-cache.md")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Api", "projects/Acme.Api/patterns/padrao-de-cache.md")]
    public async Task WriteAsync_ResolvesPathByScope(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativePath)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);

        var result = await new PatternWriterService().WriteAsync(root, request);

        Assert.Equal(expectedRelativePath, result.RelativePath);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: pattern", markdown);
        Assert.Contains("# Padrao de cache", markdown);
        Assert.Contains("## Contexto", markdown);
        Assert.Contains("## Padrao", markdown);
        Assert.Contains("## Quando aplicar", markdown);
        Assert.Contains("## Quando nao aplicar", markdown);
        Assert.Contains("## Exemplos", markdown);
        Assert.Contains("- Usar cache de leitura por chave de negocio.", markdown);
        Assert.Contains($"- Revisar ate: {DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)):yyyy-MM-dd}", markdown);
    }

    [Fact]
    public async Task WriteAsync_PreventsOverwrite()
    {
        var root = CreateTempKnowledgeRoot();
        var service = new PatternWriterService();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "api");
        await service.WriteAsync(root, request);

        await Assert.ThrowsAsync<IOException>(() => service.WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesRequiredScopeContext()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Domain) with
        {
            Domain = null
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new PatternWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesWhenToApply()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Global) with
        {
            WhenToApply = " "
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new PatternWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_ValidatesWhenNotToApply()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Global) with
        {
            WhenNotToApply = " "
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new PatternWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Project, project: "../Acme.Api");

        await Assert.ThrowsAsync<ArgumentException>(() => new PatternWriterService().WriteAsync(root, request));
    }

    private static RegisterPatternRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterPatternRequest
        {
            Title = "Padrao de cache",
            Scope = scope,
            Domain = domain,
            Project = project,
            Context = "Consultas repetidas sobre dados pouco volateis geram custo desnecessario.",
            Pattern = "Centralizar cache por chave de negocio com invalidacao explicita.",
            WhenToApply = "Aplicar em consultas idempotentes e com baixa volatilidade.",
            WhenNotToApply = "Nao aplicar em dados transacionais que exigem leitura estritamente atualizada.",
            Exceptions = "Pode usar TTL curto quando a invalidacao explicita nao estiver disponivel.",
            Examples = ["Usar cache de leitura por chave de negocio."],
            Origin = "Teste automatizado",
            UsedBy = ["projects/Acme.Api/index"],
            CandidateForReuse = ["domains/api/index"]
        };
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
