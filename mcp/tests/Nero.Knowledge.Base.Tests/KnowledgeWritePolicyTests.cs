using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeWritePolicyTests
{
    [Fact]
    public void ResolveWriteLocation_WithDirectMode_UsesRequestedRelativePath()
    {
        var root = CreateTempKnowledgeRoot();
        var policy = new KnowledgeWritePolicy(new KnowledgeWriteOptions { Mode = "direct" });

        var location = policy.ResolveWriteLocation(root, Path.Combine("domains", "api", "patterns", "cache.md"));

        Assert.Equal("domains/api/patterns/cache.md", location.RelativePath);
        Assert.StartsWith(Path.GetFullPath(root), location.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveWriteLocation_WithDraftMode_WritesBelowDrafts()
    {
        var root = CreateTempKnowledgeRoot();
        var policy = new KnowledgeWritePolicy(new KnowledgeWriteOptions { Mode = "draft" });

        var location = policy.ResolveWriteLocation(root, Path.Combine("domains", "api", "patterns", "cache.md"));

        Assert.Equal("_drafts/domains/api/patterns/cache.md", location.RelativePath);
        Assert.StartsWith(Path.Combine(Path.GetFullPath(root), "_drafts"), location.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveWriteLocation_WithReadOnlyMode_BlocksWrite()
    {
        var root = CreateTempKnowledgeRoot();
        var policy = new KnowledgeWritePolicy(new KnowledgeWriteOptions { Mode = "read_only" });

        var exception = Assert.Throws<InvalidOperationException>(
            () => policy.ResolveWriteLocation(root, Path.Combine("domains", "api", "patterns", "cache.md")));

        Assert.Contains("read_only", exception.Message);
    }

    [Fact]
    public void ResolveWriteLocation_BlocksTraversalOutsideRoot()
    {
        var root = CreateTempKnowledgeRoot();
        var policy = new KnowledgeWritePolicy();

        var exception = Assert.Throws<InvalidOperationException>(
            () => policy.ResolveWriteLocation(root, Path.Combine("..", "outside.md")));

        Assert.Contains("escapes the knowledge root", exception.Message);
    }

    [Fact]
    public async Task Writer_WithReadOnlyMode_BlocksFileCreation()
    {
        var root = CreateTempKnowledgeRoot();
        var service = new BusinessRuleWriterService(
            new KnowledgeWritePolicy(new KnowledgeWriteOptions { Mode = "read_only" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteAsync(
            root,
            new RegisterBusinessRuleRequest
            {
                Title = "Regra bloqueada",
                Scope = KnowledgeScope.Global,
                Rule = "Nao deve gravar quando read_only esta ativo.",
                Evidence = "Teste automatizado.",
                Origin = "Sprint 12.2"
            }));
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
