using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

namespace Nero.Knowledge.Base.Tests;

public class ToolFailureDiagnosticsComplianceTests
{
    [Fact]
    public void CreateActionableWriteException_IncludesComplianceCategoryAndRuleId()
    {
        var exception = new ComplianceViolationException("evidence", ComplianceTaxonomy.RuleBearerToken);
        var wrapped = ToolFailureDiagnostics.CreateActionableWriteException("nero_register_snapshot", exception);

        Assert.Contains("Category: Compliance", wrapped.Message);
        Assert.Contains("Field: evidence", wrapped.Message);
        Assert.Contains($"RuleId: {ComplianceTaxonomy.RuleBearerToken}", wrapped.Message);
        Assert.Contains("MarkdownWritten: false", wrapped.Message);
        Assert.DoesNotContain("super-secret", wrapped.Message);
    }
}
