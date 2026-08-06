namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

public static class ComplianceFrontmatter
{
    public const string StatusKey = "compliance_status";
    public const string ReasonKey = "compliance_reason";
    public const string DataClassKey = "data_class";
    public const string Quarantined = "quarantined";
    public const string DefaultDataClass = "internal";

    public static readonly HashSet<string> AllowedDataClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "public",
        "internal",
        "restricted"
    };

    public static bool IsQuarantined(IReadOnlyDictionary<string, string> frontmatter)
    {
        return frontmatter.TryGetValue(StatusKey, out var status)
            && string.Equals(status.Trim(), Quarantined, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeDataClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultDataClass;
        }

        var trimmed = value.Trim().ToLowerInvariant();
        if (!AllowedDataClasses.Contains(trimmed))
        {
            throw new ArgumentException(
                $"data_class must be one of: public, internal, restricted. Received '{trimmed}'.",
                nameof(value));
        }

        return trimmed;
    }
}
