using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Troubleshooting;

public sealed class TroubleshootingWriterService(KnowledgeWritePolicy? writePolicy = null)
{
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterTroubleshootingResult> WriteAsync(
        string knowledgeRootPath,
        RegisterTroubleshootingRequest request,
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

        return new RegisterTroubleshootingResult
        {
            Path = fullPath,
            RelativePath = writeLocation.RelativePath,
            Title = request.Title.Trim()
        };
    }

    private static void ValidateRequest(RegisterTroubleshootingRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symptom);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Cause);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Impact);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);
        ValidateTargets(request.CausedBy, nameof(request.CausedBy));
        ValidateTargets(request.RelatesTo, nameof(request.RelatesTo));

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
            (request.Symptom, nameof(request.Symptom)),
            (request.Cause, nameof(request.Cause)),
            (request.Action, nameof(request.Action)),
            (request.Solution, nameof(request.Solution)),
            (request.Evidence, nameof(request.Evidence)),
            (request.Impact, nameof(request.Impact)),
            (request.Prevention, nameof(request.Prevention)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Title, nameof(request.Title)),
            (request.Symptom, nameof(request.Symptom)),
            (request.Cause, nameof(request.Cause)),
            (request.Action, nameof(request.Action)),
            (request.Solution, nameof(request.Solution)),
            (request.Evidence, nameof(request.Evidence)),
            (request.Impact, nameof(request.Impact)),
            (request.Prevention, nameof(request.Prevention)),
            (request.Origin, nameof(request.Origin)),
            (request.Domain, nameof(request.Domain)),
            (request.Project, nameof(request.Project)));

        foreach (var target in request.CausedBy ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.CausedBy)));
        }

        foreach (var target in request.RelatesTo ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.RelatesTo)));
        }
    }

    private static string ResolveRelativePath(RegisterTroubleshootingRequest request)
    {
        var fileName = $"{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}-{Slugify(request.Title)}.md";
        return request.Scope switch
        {
            KnowledgeScope.Global => Path.Combine("global", "troubleshooting", fileName),
            KnowledgeScope.Domain => Path.Combine("domains", request.Domain!, "troubleshooting", fileName),
            KnowledgeScope.Project => Path.Combine("projects", request.Project!, "troubleshooting", fileName),
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
            throw new InvalidOperationException("Resolved troubleshooting path escapes the knowledge root.");
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

    private static string RenderMarkdown(RegisterTroubleshootingRequest request)
    {
        var scope = request.Scope.ToString().ToLowerInvariant();
        var title = EscapeYaml(request.Title.Trim());
        var origin = EscapeYaml(request.Origin.Trim());
        var domain = request.Domain is null ? string.Empty : $"domain: \"{EscapeYaml(request.Domain)}\"\n";
        var project = request.Project is null ? string.Empty : $"project: \"{EscapeYaml(request.Project)}\"\n";
        var solution = string.IsNullOrWhiteSpace(request.Solution)
            ? request.Action.Trim()
            : request.Solution.Trim();
        var prevention = string.IsNullOrWhiteSpace(request.Prevention)
            ? "Nenhuma prevencao registrada."
            : request.Prevention.Trim();
        var links = RenderLinks(request.CausedBy, request.RelatesTo);
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var reviewUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)).ToString("yyyy-MM-dd");

        return $$"""
            ---
            type: troubleshooting
            scope: {{scope}}
            title: "{{title}}"
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            {{domain}}{{project}}origin: "{{origin}}"
            {{links}}
            ---
            # {{request.Title.Trim()}}

            ## Sintoma

            {{request.Symptom.Trim()}}

            ## Causa

            {{request.Cause.Trim()}}

            ## Acao

            {{request.Action.Trim()}}

            ## Correcao ou mitigacao

            {{solution}}

            ## Impacto

            {{request.Impact.Trim()}}

            ## Prevencao

            {{prevention}}

            ## Evidencias

            {{request.Evidence.Trim()}}

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

    private static void ValidateTargets(IReadOnlyList<string>? targets, string parameterName)
    {
        if (targets is null)
        {
            return;
        }

        foreach (var target in targets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target, parameterName);
        }
    }

    private static string RenderLinks(
        IReadOnlyList<string>? causedBy,
        IReadOnlyList<string>? relatesTo)
    {
        var links = new List<(string Type, string Target)>();
        links.AddRange(ReadMappedTargets(causedBy, PreferredRelationLinks.InferCausedBy));
        links.AddRange(ReadMappedTargets(relatesTo, PreferredRelationLinks.InferRelated));

        if (links.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("links:\n");
        foreach (var (type, target) in links.DistinctBy(link => $"{link.Type}\u001f{link.Target}", StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("  - type: ");
            builder.AppendLine(type);
            builder.Append("    target: \"");
            builder.Append(EscapeYaml(target));
            builder.AppendLine("\"");
        }

        return builder.ToString();
    }

    private static IEnumerable<(string Type, string Target)> ReadMappedTargets(
        IReadOnlyList<string>? targets,
        Func<string, string> mapRelation)
    {
        return targets?
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target =>
            {
                var trimmed = target.Trim();
                return (mapRelation(trimmed), trimmed);
            })
            ?? [];
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
        return string.IsNullOrWhiteSpace(slug) ? "troubleshooting" : slug;
    }
}
