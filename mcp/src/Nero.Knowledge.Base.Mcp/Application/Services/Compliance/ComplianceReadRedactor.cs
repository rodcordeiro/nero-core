using System.Text;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

/// <summary>
/// Read-path span redaction (Marco 24). Replaces matched spans with [REDACTED:ruleId].
/// Blocking hits always; Warning hits only when data_class is restricted.
/// </summary>
public static class ComplianceReadRedactor
{
    public static string Redact(string? text, string? dataClass = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var includeWarnings = string.Equals(
            (dataClass ?? ComplianceFrontmatter.DefaultDataClass).Trim(),
            "restricted",
            StringComparison.OrdinalIgnoreCase);

        var hits = ComplianceScanner.Scan(text)
            .Where(hit => hit.Severity == ComplianceSeverity.Blocking
                || (includeWarnings && hit.Severity == ComplianceSeverity.Warning))
            .OrderByDescending(hit => hit.MatchIndex)
            .ThenByDescending(hit => hit.MatchLength)
            .ToArray();

        if (hits.Length == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text);
        var nextProtectedStart = int.MaxValue;
        foreach (var hit in hits)
        {
            var start = hit.MatchIndex;
            var end = hit.MatchIndex + hit.MatchLength;
            if (start < 0 || end > builder.Length || start >= end)
            {
                continue;
            }

            // Skip overlaps with a span already replaced further to the right.
            if (end > nextProtectedStart)
            {
                continue;
            }

            var replacement = $"[REDACTED:{hit.RuleId}]";
            builder.Remove(start, hit.MatchLength);
            builder.Insert(start, replacement);
            nextProtectedStart = start;
        }

        return builder.ToString();
    }
}
