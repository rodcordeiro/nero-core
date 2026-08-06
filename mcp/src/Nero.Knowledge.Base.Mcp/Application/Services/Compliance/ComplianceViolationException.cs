namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

/// <summary>
/// Reject-only write failure. Message must never include the matched secret/PII value.
/// </summary>
public sealed class ComplianceViolationException : ArgumentException
{
    public ComplianceViolationException(string field, string ruleId)
        : base($"Compliance rule '{ruleId}' blocked the write. Remove the sensitive value or replace it with an exact allowlisted placeholder.", field)
    {
        RuleId = ruleId;
        Data[CategoryDataKey] = CategoryName;
        Data[RuleIdDataKey] = ruleId;
    }

    public const string CategoryName = "Compliance";
    public const string CategoryDataKey = "NeroKnowledgeFailureCategory";
    public const string RuleIdDataKey = "NeroKnowledgeComplianceRuleId";

    public string RuleId { get; }
}
