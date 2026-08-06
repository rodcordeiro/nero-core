using System.Text.RegularExpressions;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Compliance;

/// <summary>
/// Shared content scanner for writers and admin corpus scan (Marco 23).
/// Never returns raw match values — only RuleId + masked excerpt.
/// </summary>
public static partial class ComplianceScanner
{
    public static IReadOnlyList<ComplianceScanHit> Scan(string? text, string? field = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var hits = new List<ComplianceScanHit>();
        ScanRegex(hits, text, field, JwtRegex(), ComplianceTaxonomy.RuleJwt, ComplianceSeverity.Blocking, alwaysBlock: true);
        ScanRegex(hits, text, field, PrivateKeyRegex(), ComplianceTaxonomy.RulePrivateKey, ComplianceSeverity.Blocking, alwaysBlock: true);
        ScanRegex(hits, text, field, Base64ShapedRegex(), ComplianceTaxonomy.RuleBase64Secret, ComplianceSeverity.Blocking, alwaysBlock: true);
        ScanBearer(hits, text, field);
        ScanRegex(hits, text, field, ConnectionStringRegex(), ComplianceTaxonomy.RuleConnectionString, ComplianceSeverity.Blocking);
        ScanRegex(hits, text, field, ApiKeyRegex(), ComplianceTaxonomy.RuleApiKey, ComplianceSeverity.Blocking);
        ScanRegex(hits, text, field, UrlCredentialRegex(), ComplianceTaxonomy.RuleUrlCredential, ComplianceSeverity.Blocking);
        ScanRegex(hits, text, field, SessionCookieRegex(), ComplianceTaxonomy.RuleSessionCookie, ComplianceSeverity.Blocking);
        ScanBrazilianDocuments(hits, text, field);
        ScanPaymentCards(hits, text, field);
        ScanRegex(hits, text, field, EmailSuspectRegex(), ComplianceTaxonomy.RulePiiSuspectEmail, ComplianceSeverity.Warning);
        ScanRegex(hits, text, field, PhoneSuspectRegex(), ComplianceTaxonomy.RulePiiSuspectPhone, ComplianceSeverity.Warning);
        return Deduplicate(hits);
    }

    public static IReadOnlyList<ComplianceScanHit> ScanBlocking(string? text, string? field = null) =>
        Scan(text, field).Where(hit => hit.Severity == ComplianceSeverity.Blocking).ToArray();

    public static void EnsureNoBlockingHits(params (string? Value, string Field)[] fields)
    {
        foreach (var (value, field) in fields)
        {
            var hit = ScanBlocking(value, field).FirstOrDefault();
            if (hit is not null)
            {
                throw new ComplianceViolationException(field, hit.RuleId);
            }
        }
    }

    private static void ScanBearer(List<ComplianceScanHit> hits, string text, string? field)
    {
        foreach (Match match in BearerRegex().Matches(text))
        {
            var token = match.Groups["token"].Value;
            var normalized = NormalizePlaceholderCandidate(token);
            if (ComplianceTaxonomy.IsExactPlaceholder(token)
                || ComplianceTaxonomy.IsExactPlaceholder(normalized))
            {
                continue;
            }

            // Real tokens tend to be longer; short bare words are treated as pedagogical.
            if (normalized.Length < 12 && !normalized.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            hits.Add(CreateHit(text, match.Index, match.Length, field, ComplianceTaxonomy.RuleBearerToken, ComplianceSeverity.Blocking));
        }
    }

    private static string NormalizePlaceholderCandidate(string value)
    {
        var trimmed = value.Trim().Trim('`', '"', '\'');
        if (ComplianceTaxonomy.IsExactPlaceholder(trimmed))
        {
            return trimmed;
        }

        // Strip at most one trailing sentence/code punct so `Bearer <token>.` / `Bearer ...` still match.
        // Do not strip balanced closers that are part of placeholders like (`JwtSettings`).
        if (trimmed.Length > 0 && ".,;:".Contains(trimmed[^1]))
        {
            trimmed = trimmed[..^1].TrimEnd('`', '"', '\'');
        }

        return trimmed;
    }

    private static void ScanRegex(
        List<ComplianceScanHit> hits,
        string text,
        string? field,
        Regex regex,
        string ruleId,
        ComplianceSeverity severity,
        bool alwaysBlock = false)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (!alwaysBlock)
            {
                var candidate = match.Groups.Count > 1 && match.Groups[1].Success
                    ? match.Groups[1].Value
                    : match.Value;
                if (ComplianceTaxonomy.IsExactPlaceholder(candidate))
                {
                    continue;
                }
            }

            hits.Add(CreateHit(text, match.Index, match.Length, field, ruleId, severity));
        }
    }

    private static void ScanBrazilianDocuments(List<ComplianceScanHit> hits, string text, string? field)
    {
        foreach (Match match in DigitsClusterRegex().Matches(text))
        {
            var digits = DigitsOnly(match.Value);
            if (digits.Length == 11 && IsValidCpf(digits))
            {
                hits.Add(CreateHit(text, match.Index, match.Length, field, ComplianceTaxonomy.RuleCpf, ComplianceSeverity.Blocking));
            }
            else if (digits.Length == 14 && IsValidCnpj(digits))
            {
                hits.Add(CreateHit(text, match.Index, match.Length, field, ComplianceTaxonomy.RuleCnpj, ComplianceSeverity.Blocking));
            }
        }
    }

    private static void ScanPaymentCards(List<ComplianceScanHit> hits, string text, string? field)
    {
        foreach (Match match in CardCandidateRegex().Matches(text))
        {
            var digits = DigitsOnly(match.Value);
            if (digits.Length is < 13 or > 19)
            {
                continue;
            }

            if (IsLuhnValid(digits))
            {
                hits.Add(CreateHit(text, match.Index, match.Length, field, ComplianceTaxonomy.RulePaymentCard, ComplianceSeverity.Blocking));
            }
        }
    }

    private static ComplianceScanHit CreateHit(
        string text,
        int index,
        int length,
        string? field,
        string ruleId,
        ComplianceSeverity severity)
    {
        var (line, column) = LineColumn(text, index);
        return new ComplianceScanHit
        {
            RuleId = ruleId,
            Severity = severity,
            Line = line,
            Column = column,
            MatchIndex = index,
            MatchLength = length,
            Field = field,
            MaskedExcerpt = MaskExcerpt(text, index, length)
        };
    }

    private static string MaskExcerpt(string text, int index, int length)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var lineEnd = text.IndexOf('\n', index);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var line = text[lineStart..lineEnd];
        var relativeStart = index - lineStart;
        var relativeEnd = Math.Min(line.Length, relativeStart + length);
        if (relativeStart < 0 || relativeStart >= line.Length)
        {
            return $"[REDACTED:{ComplianceTaxonomy.Version}]";
        }

        var prefix = line[..relativeStart];
        var suffix = relativeEnd < line.Length ? line[relativeEnd..] : string.Empty;
        if (prefix.Length > 24)
        {
            prefix = "..." + prefix[^24..];
        }

        if (suffix.Length > 24)
        {
            suffix = suffix[..24] + "...";
        }

        return $"{prefix}[REDACTED]{suffix}".Trim();
    }

    private static (int Line, int Column) LineColumn(string text, int index)
    {
        var line = 1;
        var lastBreak = -1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastBreak = i;
            }
        }

        return (line, index - lastBreak);
    }

    private static IReadOnlyList<ComplianceScanHit> Deduplicate(List<ComplianceScanHit> hits) =>
        hits
            .GroupBy(hit => $"{hit.RuleId}|{hit.Line}|{hit.Column}|{hit.Field}")
            .Select(group => group.First())
            .OrderBy(hit => hit.Line)
            .ThenBy(hit => hit.Column)
            .ThenBy(hit => hit.RuleId, StringComparer.Ordinal)
            .ToArray();

    private static string DigitsOnly(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool IsValidCpf(string digits)
    {
        if (digits.Length != 11 || digits.Distinct().Count() == 1)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (digits[i] - '0') * (10 - i);
        }

        var remainder = sum % 11;
        var dig1 = remainder < 2 ? 0 : 11 - remainder;
        if (digits[9] - '0' != dig1)
        {
            return false;
        }

        sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (digits[i] - '0') * (11 - i);
        }

        remainder = sum % 11;
        var dig2 = remainder < 2 ? 0 : 11 - remainder;
        return digits[10] - '0' == dig2;
    }

    private static bool IsValidCnpj(string digits)
    {
        if (digits.Length != 14 || digits.Distinct().Count() == 1)
        {
            return false;
        }

        int[] w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (digits[i] - '0') * w1[i];
        }

        var remainder = sum % 11;
        var dig1 = remainder < 2 ? 0 : 11 - remainder;
        if (digits[12] - '0' != dig1)
        {
            return false;
        }

        sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (digits[i] - '0') * w2[i];
        }

        remainder = sum % 11;
        var dig2 = remainder < 2 ? 0 : 11 - remainder;
        return digits[13] - '0' == dig2;
    }

    private static bool IsLuhnValid(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyRegex();

    [GeneratedRegex(@"\b(?:Bearer)\s+(?<token>[^\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(
        @"(?i)(?:Password|Pwd|AccountKey|SharedAccessKey|SharedAccessSignature|ClientSecret|Secret)\s*=\s*(?!\s*(?:REDACTED|\*\*\*|YOUR_SECRET|<password>|<secret>))(\S+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringRegex();

    [GeneratedRegex(
        @"\b(?:sk_live_|sk_test_|sk-|ghp_|gho_|glpat-|xox[baprs]-|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z\-_]{20,})\S*\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"[a-zA-Z][a-zA-Z0-9+.-]*://[^/\s:@]+:[^/\s@]+@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredentialRegex();

    [GeneratedRegex(
        @"(?i)(?:Set-Cookie|Cookie)\s*[:=]\s*[^\n]*(?:session|auth|token|jwt|refresh)[^=\n]*=\s*(?!\s*(?:REDACTED|\*\*\*|<token>))([^\s;]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SessionCookieRegex();

    /// <summary>
    /// Base64 with padding. Avoid matching C#/flag idioms like Identifier==false (require trailing '=/' only as padding, not '==' before a word).
    /// </summary>
    [GeneratedRegex(@"\b[A-Za-z0-9+/]{40,}={1,2}(?![A-Za-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex Base64ShapedRegex();

    [GeneratedRegex(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b|\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b|\b\d{11}\b|\b\d{14}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsClusterRegex();

    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CardCandidateRegex();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailSuspectRegex();

    /// <summary>
    /// Brazilian phone-like patterns; require parentheses or +55 to cut date/filename noise.
    /// </summary>
    [GeneratedRegex(@"\b(?:\+55\s*)?(?:\(?\d{2}\)\s*)(?:9\d{4}|\d{4})[-\s]?\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneSuspectRegex();
}
