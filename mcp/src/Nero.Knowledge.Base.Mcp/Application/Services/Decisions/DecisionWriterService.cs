using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Decisions;

public sealed class DecisionWriterService(KnowledgeWritePolicy? writePolicy = null)
{
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterDecisionResult> WriteAsync(
        string knowledgeRootPath,
        RegisterDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);

        var relativePath = ResolveRelativePath(request);
        var writeLocation = writePolicy.ResolveWriteLocation(rootPath, relativePath);
        var fullPath = writeLocation.FullPath;
        var markdown = RenderMarkdown(request);
        await KnowledgeMarkdownFileWriter.WriteNewAsync(fullPath, markdown, cancellationToken);

        return new RegisterDecisionResult
        {
            Path = fullPath,
            RelativePath = writeLocation.RelativePath,
            Title = request.Title.Trim()
        };
    }

    private static void ValidateRequest(RegisterDecisionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Problem);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Options);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Consequences);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);
        ValidateSupersedes(request.Supersedes);

        if (!Enum.IsDefined(request.Scope))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Scope), request.Scope, "Scope must be supported.");
        }

        if (request.Scope == KnowledgeScope.Domain)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Domain);
            ValidatePathSegment(request.Domain, nameof(request.Domain));
        }

        if (request.Scope == KnowledgeScope.Project)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Project);
            ValidatePathSegment(request.Project, nameof(request.Project));
        }

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Problem, nameof(request.Problem)),
            (request.Options, nameof(request.Options)),
            (request.Decision, nameof(request.Decision)),
            (request.Consequences, nameof(request.Consequences)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Title, nameof(request.Title)),
            (request.Problem, nameof(request.Problem)),
            (request.Options, nameof(request.Options)),
            (request.Decision, nameof(request.Decision)),
            (request.Consequences, nameof(request.Consequences)),
            (request.Origin, nameof(request.Origin)),
            (request.Domain, nameof(request.Domain)),
            (request.Project, nameof(request.Project)));

        foreach (var target in request.Supersedes ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.Supersedes)));
        }
    }

    private static string ResolveRelativePath(RegisterDecisionRequest request)
    {
        var fileName = $"{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}-{Slugify(request.Title)}.md";
        return request.Scope switch
        {
            KnowledgeScope.Global => Path.Combine("global", "decisions", fileName),
            KnowledgeScope.Domain => Path.Combine("domains", request.Domain!, "decisions", fileName),
            KnowledgeScope.Project => Path.Combine("projects", request.Project!, "decisions", fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scope), request.Scope, "Scope must be supported.")
        };
    }

    private static string ResolveSafeFullPath(string rootPath, string relativePath)
    {
        var resolvedRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(resolvedRoot, relativePath));
        var rootPrefix = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved decision path escapes the knowledge root.");
        }

        return fullPath;
    }

    private static void ValidatePathSegment(string value, string parameterName)
    {
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Path segment must not contain traversal, separators or invalid characters.", parameterName);
        }
    }

    private static void ValidateSupersedes(IReadOnlyList<string>? supersedes)
    {
        if (supersedes is null)
        {
            return;
        }

        foreach (var target in supersedes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target, nameof(supersedes));
        }
    }

    private static string RenderMarkdown(RegisterDecisionRequest request)
    {
        var scope = request.Scope.ToString().ToLowerInvariant();
        var title = EscapeYaml(request.Title.Trim());
        var origin = EscapeYaml(request.Origin.Trim());
        var domain = request.Domain is null ? string.Empty : $"domain: \"{EscapeYaml(request.Domain)}\"\n";
        var project = request.Project is null ? string.Empty : $"project: \"{EscapeYaml(request.Project)}\"\n";
        var links = RenderLinks(request.Scope, request.Domain, request.Project, request.Supersedes);
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var reviewUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)).ToString("yyyy-MM-dd");

        return $$"""
            ---
            type: decision
            scope: {{scope}}
            title: "{{title}}"
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            {{domain}}{{project}}origin: "{{origin}}"
            {{links}}
            ---
            # {{request.Title.Trim()}}

            ## Problema

            {{request.Problem.Trim()}}

            ## Opcoes

            {{request.Options.Trim()}}

            ## Decisao

            {{request.Decision.Trim()}}

            ## Consequencias

            {{request.Consequences.Trim()}}

            ## Origem

            {{request.Origin.Trim()}}

            ## Escopo

            - Camada: {{scope}}
            - Dominio: {{request.Domain?.Trim() ?? string.Empty}}
            - Projeto: {{request.Project?.Trim() ?? string.Empty}}

            ## Revisao

            - Registrado em: {{registeredAt}}
            - Revisar ate: {{reviewUntil}}
            - Dono:
            """;
    }

    private static string RenderLinks(
        KnowledgeScope scope,
        string? domain,
        string? project,
        IReadOnlyList<string>? supersedes)
    {
        var builder = new StringBuilder(
            PreferredRelationLinks.RenderMinimalScopeLinks(scope, domain, project, EscapeYaml));

        var targets = supersedes?
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets is null || targets.Count == 0)
        {
            return builder.ToString();
        }

        // Decision-only special relation; do not remap to updates (active/superseded split keys on Supersedes).
        foreach (var target in targets)
        {
            builder.Append("  - type: ");
            builder.Append(PreferredRelationLinks.Supersedes);
            builder.Append('\n');
            builder.Append("    target: \"");
            builder.Append(EscapeYaml(target));
            builder.AppendLine("\"");
        }

        return builder.ToString();
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "decisao" : slug;
    }
}
