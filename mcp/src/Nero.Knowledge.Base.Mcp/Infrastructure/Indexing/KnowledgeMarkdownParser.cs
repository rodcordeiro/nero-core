using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed class KnowledgeMarkdownParser
{
    /// <summary>
    /// Parses one Markdown note from the canonical knowledge tree into indexable metadata.
    /// </summary>
    public KnowledgeMarkdownDocument Parse(string knowledgeRootPath, string markdownPath, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(markdownPath);
        ArgumentNullException.ThrowIfNull(markdown);

        var relativePath = GetRelativeKnowledgePath(knowledgeRootPath, markdownPath);
        var pathSegments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var (frontmatter, links, content) = ExtractFrontmatter(markdown);

        return new KnowledgeMarkdownDocument
        {
            Id = CreateId(relativePath),
            Title = ExtractTitle(content, frontmatter, relativePath),
            Path = $"knowledge/{relativePath}",
            Content = content,
            Scope = InferScope(pathSegments),
            Type = InferType(pathSegments, frontmatter),
            Domain = InferDomain(pathSegments),
            Project = InferProject(pathSegments),
            Frontmatter = frontmatter,
            Links = links
        };
    }

    private static string GetRelativeKnowledgePath(string knowledgeRootPath, string markdownPath)
    {
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(knowledgeRootPath),
            Path.GetFullPath(markdownPath));

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string CreateId(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return relativePath[..^extension.Length];
    }

    private static (IReadOnlyDictionary<string, string> Frontmatter, IReadOnlyList<KnowledgeMarkdownLink> Links, string Content) ExtractFrontmatter(string markdown)
    {
        var normalized = markdown.ReplaceLineEndings("\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], normalized);
        }

        var endIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], normalized);
        }

        var frontmatterText = normalized[4..endIndex];
        var content = normalized[(endIndex + 5)..];
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<KnowledgeMarkdownLink>();
        string? currentLinkType = null;
        string? currentLinkTarget = null;
        string? currentListKey = null;
        var readingLinks = false;

        foreach (var line in frontmatterText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("links:", StringComparison.Ordinal))
            {
                readingLinks = true;
                currentListKey = null;
                continue;
            }

            if (!readingLinks && currentListKey is not null && line.StartsWith("  - ", StringComparison.Ordinal))
            {
                var item = line[4..].Trim().Trim('"', '\'');
                if (item.Length > 0)
                {
                    frontmatter[currentListKey] = string.IsNullOrWhiteSpace(frontmatter[currentListKey])
                        ? item
                        : $"{frontmatter[currentListKey]}\n{item}";
                }

                continue;
            }

            if (readingLinks && line.StartsWith("  - ", StringComparison.Ordinal))
            {
                AddLinkIfComplete(links, currentLinkType, currentLinkTarget);
                currentLinkType = null;
                currentLinkTarget = null;

                var itemText = line[4..];
                ReadLinkField(itemText, ref currentLinkType, ref currentLinkTarget);
                continue;
            }

            if (readingLinks && line.StartsWith("    ", StringComparison.Ordinal))
            {
                ReadLinkField(line[4..], ref currentLinkType, ref currentLinkTarget);
                continue;
            }

            if (readingLinks && !char.IsWhiteSpace(line[0]))
            {
                AddLinkIfComplete(links, currentLinkType, currentLinkTarget);
                currentLinkType = null;
                currentLinkTarget = null;
                readingLinks = false;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            if (key.Length > 0)
            {
                frontmatter[key] = value;
                currentListKey = value.Length == 0 ? key : null;
            }
        }

        AddLinkIfComplete(links, currentLinkType, currentLinkTarget);

        return (frontmatter, links, content);
    }

    private static void ReadLinkField(string text, ref string? type, ref string? target)
    {
        var separatorIndex = text.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return;
        }

        var key = text[..separatorIndex].Trim();
        var value = text[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            type = value;
        }
        else if (key.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            target = value;
        }
    }

    private static void AddLinkIfComplete(List<KnowledgeMarkdownLink> links, string? type, string? target)
    {
        if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(target))
        {
            links.Add(new KnowledgeMarkdownLink
            {
                Type = type,
                Target = target
            });
        }
    }

    private static string ExtractTitle(
        string content,
        IReadOnlyDictionary<string, string> frontmatter,
        string relativePath)
    {
        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        if (frontmatter.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return Path.GetFileNameWithoutExtension(relativePath);
    }

    private static KnowledgeScope InferScope(string[] pathSegments)
    {
        return pathSegments.FirstOrDefault() switch
        {
            "domains" => KnowledgeScope.Domain,
            "projects" when pathSegments.Length > 2 => KnowledgeScope.Project,
            _ => KnowledgeScope.Global
        };
    }

    private static string? InferDomain(string[] pathSegments)
    {
        return pathSegments is ["domains", var domain, ..] ? domain : null;
    }

    private static string? InferProject(string[] pathSegments)
    {
        return pathSegments is ["projects", var project, ..] && pathSegments.Length > 2 ? project : null;
    }

    private static KnowledgeNodeType InferType(
        string[] pathSegments,
        IReadOnlyDictionary<string, string> frontmatter)
    {
        if (pathSegments.Contains("decisions"))
        {
            return KnowledgeNodeType.Decision;
        }

        if (pathSegments.Contains("troubleshooting"))
        {
            return KnowledgeNodeType.Troubleshooting;
        }

        var fileName = Path.GetFileName(pathSegments.LastOrDefault() ?? string.Empty);
        var inferred = fileName switch
        {
            "index.md" => KnowledgeNodeType.Index,
            "business-rules.md" => KnowledgeNodeType.BusinessRule,
            "context.md" => KnowledgeNodeType.ProjectContext,
            "patterns.md" => KnowledgeNodeType.Pattern,
            "validation-and-tests.md" => KnowledgeNodeType.ValidationRule,
            _ => TryReadType(frontmatter)
        };

        return inferred ?? KnowledgeNodeType.Context;
    }

    private static KnowledgeNodeType? TryReadType(IReadOnlyDictionary<string, string> frontmatter)
    {
        if (!frontmatter.TryGetValue("type", out var value))
        {
            return null;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var candidate in Enum.GetValues<KnowledgeNodeType>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
