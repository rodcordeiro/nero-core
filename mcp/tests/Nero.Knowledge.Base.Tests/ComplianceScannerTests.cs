using Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class ComplianceScannerTests
{
    [Fact]
    public void Scan_BlocksBearerTokenAndDoesNotEchoValue()
    {
        const string secret = "super-secret-token-value-12345";
        var hits = ComplianceScanner.ScanBlocking($"Authorization: Bearer {secret}");

        var hit = Assert.Single(hits);
        Assert.Equal(ComplianceTaxonomy.RuleBearerToken, hit.RuleId);
        Assert.DoesNotContain(secret, hit.MaskedExcerpt);
        Assert.Contains("[REDACTED]", hit.MaskedExcerpt);
    }

    [Fact]
    public void Scan_AllowsPedagogicalShortBearerToken()
    {
        Assert.Empty(ComplianceScanner.ScanBlocking("Authorization: Bearer token"));
        Assert.Empty(ComplianceScanner.ScanBlocking("Auth uses Bearer JWT"));
    }

    [Fact]
    public void Scan_AlwaysBlocksJwtEvenNearExample()
    {
        const string jwt =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signaturepart";
        var hits = ComplianceScanner.ScanBlocking($"example JWT: {jwt}");

        Assert.Contains(hits, hit => hit.RuleId == ComplianceTaxonomy.RuleJwt);
        Assert.All(hits, hit => Assert.DoesNotContain("eyJ", hit.MaskedExcerpt));
    }

    [Fact]
    public void Scan_BlocksValidCpf()
    {
        // CPF 529.982.247-25 is a known valid checksum fixture (synthetic).
        var hits = ComplianceScanner.ScanBlocking("documento 529.982.247-25");
        Assert.Contains(hits, hit => hit.RuleId == ComplianceTaxonomy.RuleCpf);
    }

    [Fact]
    public void Scan_WarnsOnEmailAsPiiSuspect()
    {
        var hits = ComplianceScanner.Scan("contato: user@example.com");
        var warning = Assert.Single(hits);
        Assert.Equal(ComplianceSeverity.Warning, warning.Severity);
        Assert.Equal(ComplianceTaxonomy.RulePiiSuspectEmail, warning.RuleId);
    }

    [Fact]
    public void EnsureNoBlockingHits_ThrowsComplianceViolationWithoutEcho()
    {
        // Generic sk- fixture; avoid vendor-shaped key literals that trip GitHub push protection.
        const string secret = "sk-compliance-fixture-not-a-vendor-key";
        var exception = Assert.Throws<ComplianceViolationException>(() =>
            ComplianceScanner.EnsureNoBlockingHits(($"key={secret}", "rule")));

        Assert.Equal("rule", exception.ParamName);
        Assert.Equal(ComplianceTaxonomy.RuleApiKey, exception.RuleId);
        Assert.DoesNotContain(secret, exception.Message);
        Assert.Equal(ComplianceViolationException.CategoryName, exception.Data[ComplianceViolationException.CategoryDataKey]);
    }
}

public class ComplianceWriterRejectTests
{
    [Fact]
    public async Task BusinessRuleWriter_RejectsSecretWithoutWriting()
    {
        var root = CreateTempKnowledgeRoot();
        var request = new RegisterBusinessRuleRequest
        {
            Title = "Regra limpa",
            Scope = KnowledgeScope.Global,
            Rule = "Authorization: Bearer super-secret-token-value-12345",
            Evidence = "evidencia limpa",
            Origin = "teste"
        };

        var exception = await Assert.ThrowsAsync<ComplianceViolationException>(() =>
            new BusinessRuleWriterService().WriteAsync(root, request));

        Assert.Equal(nameof(request.Rule), exception.ParamName);
        Assert.Equal(ComplianceTaxonomy.RuleBearerToken, exception.RuleId);
        Assert.Empty(Directory.GetFiles(root, "*.md", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BusinessRuleWriter_AllowsPlaceholderAndWritesDataClass()
    {
        var root = CreateTempKnowledgeRoot();
        var request = new RegisterBusinessRuleRequest
        {
            Title = "Regra com placeholder",
            Scope = KnowledgeScope.Global,
            Rule = "Use Authorization: Bearer <token> no header.",
            Evidence = "docs internas",
            Origin = "teste"
        };

        var result = await new BusinessRuleWriterService().WriteAsync(root, request);
        var markdown = await File.ReadAllTextAsync(result.Path);

        Assert.True(File.Exists(result.Path));
        Assert.Contains("data_class: internal", markdown);
        Assert.Contains("Bearer <token>", markdown);
    }

    [Fact]
    public async Task BusinessRuleWriter_RejectsOversizeAsInvalidInput()
    {
        var root = CreateTempKnowledgeRoot();
        var oversized = new string('a', KnowledgeFieldLimits.MaxLongFieldUtf8Bytes + 1);
        var request = new RegisterBusinessRuleRequest
        {
            Title = "Regra grande",
            Scope = KnowledgeScope.Global,
            Rule = oversized,
            Evidence = "ok",
            Origin = "teste"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new BusinessRuleWriterService().WriteAsync(root, request));

        Assert.False(exception is ComplianceViolationException);
        Assert.Equal(nameof(request.Rule), exception.ParamName);
        Assert.Contains("64 KiB", exception.Message);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
