namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

/// <summary>
/// Versioned compliance taxonomy (Marco 23). RuleIds are stable and may appear in agent-facing errors.
/// </summary>
public static class ComplianceTaxonomy
{
    public const string Version = "2026-08-05.3";

    public const string RuleBearerToken = "secret.bearer_token";
    public const string RuleJwt = "secret.jwt";
    public const string RulePrivateKey = "secret.private_key";
    public const string RuleConnectionString = "secret.connection_string";
    public const string RuleApiKey = "secret.api_key";
    public const string RuleUrlCredential = "secret.url_credential";
    public const string RuleSessionCookie = "secret.session_cookie";
    public const string RuleBase64Secret = "secret.base64_shaped";
    public const string RuleCpf = "pii.cpf";
    public const string RuleCnpj = "pii.cnpj";
    public const string RulePaymentCard = "pii.payment_card";
    public const string RulePiiSuspectEmail = "pii_suspect.email";
    public const string RulePiiSuspectPhone = "pii_suspect.phone";

    /// <summary>
    /// Exact placeholder tokens allowed next to secret-shaped prefixes (case-sensitive after trim).
    /// JWT / private-key / long base64-shaped values never qualify as placeholders.
    /// </summary>
    public static readonly HashSet<string> ExactPlaceholders = new(StringComparer.Ordinal)
    {
        "<token>",
        "<TOKEN>",
        "<api_key>",
        "<API_KEY>",
        "<password>",
        "<PASSWORD>",
        "<secret>",
        "<SECRET>",
        "REDACTED",
        "[REDACTED]",
        "***",
        "****",
        "YOUR_API_KEY",
        "YOUR_SECRET",
        "YOUR_TOKEN",
        "your-api-key",
        "your-token",
        "example-token",
        "placeholder",
        "xxx",
        "XXXX",
        "<redacted>",
        // Pedagogical bare words used next to Bearer in existing knowledge docs.
        "token",
        "Token",
        "TOKEN",
        "jwt",
        "JWT",
        "auth",
        "Auth",
        "authentication",
        "header",
        "Header",
        "value",
        "Value",
        "...",
        "…",
        "<accessToken>",
        "<access_token>",
        "`JwtSettings`",
        "(`JwtSettings`)",
        "opcional"
    };

    public static bool IsExactPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return ExactPlaceholders.Contains(value.Trim());
    }
}
