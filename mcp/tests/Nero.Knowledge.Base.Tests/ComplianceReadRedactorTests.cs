using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

namespace Nero.Knowledge.Base.Tests;

public class ComplianceReadRedactorTests
{
    [Fact]
    public void Redact_MasksBlockingBearerEvenWhenDataClassInternal()
    {
        const string secret = "super-secret-token-value-12345";
        var redacted = ComplianceReadRedactor.Redact(
            $"Authorization: Bearer {secret}",
            dataClass: "internal");

        Assert.DoesNotContain(secret, redacted);
        Assert.Contains($"[REDACTED:{ComplianceTaxonomy.RuleBearerToken}]", redacted);
    }

    [Fact]
    public void Redact_LeavesWarningEmailWhenInternal()
    {
        const string email = "user@example.com";
        var redacted = ComplianceReadRedactor.Redact($"contato {email}", dataClass: "internal");

        Assert.Contains(email, redacted);
        Assert.DoesNotContain("[REDACTED:", redacted);
    }

    [Fact]
    public void Redact_MasksWarningEmailWhenRestricted()
    {
        const string email = "user@example.com";
        var redacted = ComplianceReadRedactor.Redact($"contato {email}", dataClass: "restricted");

        Assert.DoesNotContain(email, redacted);
        Assert.Contains($"[REDACTED:{ComplianceTaxonomy.RulePiiSuspectEmail}]", redacted);
    }
}
