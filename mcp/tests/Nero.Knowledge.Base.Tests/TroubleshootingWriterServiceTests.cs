using Nero.Knowledge.Base.Mcp.Application.Contracts.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Services.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class TroubleshootingWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/troubleshooting/")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/troubleshooting/")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Api", "projects/Acme.Api/troubleshooting/")]
    public async Task WriteAsync_ResolvesPathByScope(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativePathPrefix)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);

        var result = await new TroubleshootingWriterService().WriteAsync(root, request);

        Assert.StartsWith(expectedRelativePathPrefix, result.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith("-falha-ao-sincronizar-estoque.md", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: troubleshooting", markdown);
        Assert.Contains("# Falha ao sincronizar estoque", markdown);
        Assert.Contains("## Sintoma", markdown);
        Assert.Contains("## Causa", markdown);
        Assert.Contains("## Acao", markdown);
        Assert.Contains("## Impacto", markdown);
        Assert.Contains("## Evidencias", markdown);
        Assert.Contains($"- Revisar ate: {DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)):yyyy-MM-dd}", markdown);
    }

    [Fact]
    public async Task WriteAsync_PreventsOverwrite()
    {
        var root = CreateTempKnowledgeRoot();
        var service = new TroubleshootingWriterService();
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

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new TroubleshootingWriterService().WriteAsync(root, request));
    }

    [Theory]
    [InlineData(nameof(RegisterTroubleshootingRequest.Symptom))]
    [InlineData(nameof(RegisterTroubleshootingRequest.Cause))]
    [InlineData(nameof(RegisterTroubleshootingRequest.Action))]
    [InlineData(nameof(RegisterTroubleshootingRequest.Evidence))]
    [InlineData(nameof(RegisterTroubleshootingRequest.Impact))]
    public async Task WriteAsync_ValidatesRequiredFields(string field)
    {
        var root = CreateTempKnowledgeRoot();
        var request = field switch
        {
            nameof(RegisterTroubleshootingRequest.Symptom) => CreateRequest(KnowledgeScope.Global) with { Symptom = " " },
            nameof(RegisterTroubleshootingRequest.Cause) => CreateRequest(KnowledgeScope.Global) with { Cause = " " },
            nameof(RegisterTroubleshootingRequest.Action) => CreateRequest(KnowledgeScope.Global) with { Action = " " },
            nameof(RegisterTroubleshootingRequest.Evidence) => CreateRequest(KnowledgeScope.Global) with { Evidence = " " },
            nameof(RegisterTroubleshootingRequest.Impact) => CreateRequest(KnowledgeScope.Global) with { Impact = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported test field.")
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new TroubleshootingWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "../api");

        await Assert.ThrowsAsync<ArgumentException>(() => new TroubleshootingWriterService().WriteAsync(root, request));
    }

    private static RegisterTroubleshootingRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterTroubleshootingRequest
        {
            Title = "Falha ao sincronizar estoque",
            Scope = scope,
            Domain = domain,
            Project = project,
            Symptom = "Sincronizacao retorna timeout ao processar estoque.",
            Cause = "Servico externo ficou indisponivel durante a janela de retry.",
            Action = "Reprocessar mensagens pendentes apos estabilizacao do servico.",
            Evidence = "Logs resumidos mostram timeout HTTP 504 na integracao.",
            Impact = "Estoque pode ficar defasado ate o reprocessamento.",
            Solution = "Executar reprocessamento controlado da fila afetada.",
            Prevention = "Monitorar latencia e configurar alerta para aumento de retries.",
            Origin = "Teste automatizado",
            CausedBy = ["domains/api/index"],
            RelatesTo = ["projects/Acme.Api/index"]
        };
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
