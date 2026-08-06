using System.Text.RegularExpressions;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Domains;

/// <summary>
/// Filesystem-backed discovery and validation of Nero knowledge domains (Marco 22).
/// </summary>
public sealed partial class ActiveDomainCatalog
{
    public const string StatusActive = "active";
    public const string StatusInactive = "inactive";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "_drafts",
        "global",
        "projects",
        "templates",
        "domains"
    };

    public void ValidateSlug(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var trimmed = domain.Trim();
        var slug = trimmed.ToLowerInvariant();
        if (!string.Equals(trimmed, slug, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Domain must be lowercase ASCII matching ^[a-z][a-z0-9_-]{1,31}$.",
                nameof(domain));
        }

        if (!DomainSlugRegex().IsMatch(slug))
        {
            throw new ArgumentException(
                "Domain must match ^[a-z][a-z0-9_-]{1,31}$ (lowercase ASCII, 2-32 chars).",
                nameof(domain));
        }

        if (ReservedNames.Contains(slug))
        {
            throw new ArgumentException(
                $"Domain '{slug}' is reserved and cannot be used as a knowledge domain slug.",
                nameof(domain));
        }
    }

    public bool IsActiveDomain(string knowledgeRootPath, string domain)
    {
        var slug = domain.Trim().ToLowerInvariant();
        if (!DomainIndexExists(knowledgeRootPath, slug))
        {
            return false;
        }

        var status = ReadStatusFromIndex(GetIndexPath(knowledgeRootPath, slug)) ?? StatusActive;
        return string.Equals(status, StatusActive, StringComparison.OrdinalIgnoreCase);
    }

    public void EnsureActiveDomain(string knowledgeRootPath, string domain)
    {
        ValidateSlug(domain);
        var slug = domain.Trim().ToLowerInvariant();
        var indexPath = GetIndexPath(knowledgeRootPath, slug);
        if (!File.Exists(indexPath))
        {
            throw new ArgumentException(
                $"Domain '{slug}' is not registered. Run nero_register_domain first.",
                nameof(domain));
        }

        var status = ReadStatusFromIndex(indexPath) ?? StatusActive;
        if (string.Equals(status, StatusInactive, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Domain '{slug}' is inactive. Reactivate with nero_update_domain (reativar=true) before using it.",
                nameof(domain));
        }
    }

    public string? TryGetStatus(string knowledgeRootPath, string domain)
    {
        var slug = domain.Trim().ToLowerInvariant();
        var indexPath = GetIndexPath(knowledgeRootPath, slug);
        if (!File.Exists(indexPath))
        {
            return null;
        }

        return ReadStatusFromIndex(indexPath);
    }

    public bool DomainIndexExists(string knowledgeRootPath, string domain)
    {
        return File.Exists(GetIndexPath(knowledgeRootPath, domain.Trim().ToLowerInvariant()));
    }

    public static string GetIndexPath(string knowledgeRootPath, string domainSlug) =>
        Path.Combine(knowledgeRootPath, "domains", domainSlug, "index.md");

    public static string? ReadStatusFromIndex(string indexPath)
    {
        var markdown = File.ReadAllText(indexPath);
        var match = StatusFrontmatterRegex().Match(markdown);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim().ToLowerInvariant();
    }

    public IReadOnlyList<string> FindProjectsLinkedToDomain(string knowledgeRootPath, string domain)
    {
        var slug = domain.Trim().ToLowerInvariant();
        var projectsRoot = Path.Combine(knowledgeRootPath, "projects");
        if (!Directory.Exists(projectsRoot))
        {
            return [];
        }

        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"domains/{slug}",
            $"domains/{slug}/index"
        };

        var linked = new List<string>();
        foreach (var projectDirectory in Directory.EnumerateDirectories(projectsRoot))
        {
            var projectName = Path.GetFileName(projectDirectory);
            foreach (var fileName in new[] { "index.md", "context.md", "inventory.md" })
            {
                var path = Path.Combine(projectDirectory, fileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                var markdown = File.ReadAllText(path);
                if (!ContainsBelongsToDomain(markdown, targets))
                {
                    continue;
                }

                linked.Add(projectName);
                break;
            }
        }

        linked.Sort(StringComparer.OrdinalIgnoreCase);
        return linked;
    }

    private static bool ContainsBelongsToDomain(string markdown, HashSet<string> targets)
    {
        var normalized = markdown.ReplaceLineEndings("\n");
        if (!normalized.Contains("belongs_to_domain", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var target in targets)
        {
            if (normalized.Contains($"target: {target}", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains($"target: \"{target}\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_-]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainSlugRegex();

    [GeneratedRegex(@"^status:\s*[""']?([A-Za-z]+)[""']?\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex StatusFrontmatterRegex();
}
