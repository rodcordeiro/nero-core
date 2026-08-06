using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeDomainContextServiceTests
{
    [Theory]
    [InlineData("mobile")]
    [InlineData("api")]
    [InlineData("front")]
    [InlineData("integracoes")]
    public async Task GetDomainContextAsync_AggregatesDomainSections(string domain)
    {
        var root = CreateTempKnowledgeRoot();
        await WriteDomainMarkdownAsync(root, domain, "index.md", $"# Dominio {domain}\n\nIndice do dominio.");
        await WriteDomainMarkdownAsync(root, domain, "patterns.md", "# Padroes\n\nPadrao do dominio.");
        await WriteDomainMarkdownAsync(root, domain, "business-rules.md", "# Regras\n\nRegra do dominio.");
        await WriteDomainMarkdownAsync(root, domain, "validation-and-tests.md", "# Validacoes\n\nCriterios do dominio.");
        await WriteDomainMarkdownAsync(root, "outro", "patterns.md", "# Outro\n\nNao entra.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeDomainContextService().GetDomainContextAsync(connection, domain);

        Assert.True(result.Exists);
        Assert.Equal(domain, result.Domain);
        Assert.Equal($"Dominio {domain}", result.Index?.Title);
        Assert.Equal("Padroes", result.Patterns?.Title);
        Assert.Equal("Regras", result.BusinessRules?.Title);
        Assert.Equal("Validacoes", result.ValidationAndTests?.Title);
        Assert.Equal(KnowledgeNodeType.Index, result.Index?.Type);
        Assert.Equal(KnowledgeNodeType.Pattern, result.Patterns?.Type);
        Assert.Equal(KnowledgeNodeType.BusinessRule, result.BusinessRules?.Type);
        Assert.Equal(KnowledgeNodeType.ValidationRule, result.ValidationAndTests?.Type);
        Assert.Contains($"knowledge/domains/{domain}/", result.Patterns?.Path);
    }

    [Fact]
    public async Task GetDomainContextAsync_ReturnsEmptyResultForUnknownDomain()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteDomainMarkdownAsync(root, "api", "index.md", "# API\n\nIndice.");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await new KnowledgeIndexer().ReindexAsync(connection, root);

        var result = await new KnowledgeDomainContextService().GetDomainContextAsync(connection, "dados");

        Assert.False(result.Exists);
        Assert.Equal("dados", result.Domain);
        Assert.Null(result.Index);
        Assert.Null(result.Patterns);
        Assert.Null(result.BusinessRules);
        Assert.Null(result.ValidationAndTests);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static Task WriteDomainMarkdownAsync(string root, string domain, string relativePath, string content)
    {
        return WriteMarkdownAsync(root, $"domains/{domain}/{relativePath}", content);
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
