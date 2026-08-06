using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Patterns;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Patterns;

public sealed class PatternWriterService(KnowledgeWritePolicy? writePolicy = null)
{
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterPatternResult> WriteAsync(
        string knowledgeRootPath,
        RegisterPatternRequest request,
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

        return new RegisterPatternResult
        {
            Path = fullPath,
            RelativePath = writeLocation.RelativePath,
            Title = request.Title.Trim()
        };
    }

    private static void ValidateRequest(RegisterPatternRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Context);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WhenToApply);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WhenNotToApply);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);
        ValidateExamples(request.Examples);
        ValidateTargets(request.UsedBy, nameof(request.UsedBy));
        ValidateTargets(request.CandidateForReuse, nameof(request.CandidateForReuse));

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
            (request.Context, nameof(request.Context)),
            (request.Pattern, nameof(request.Pattern)),
            (request.WhenToApply, nameof(request.WhenToApply)),
            (request.WhenNotToApply, nameof(request.WhenNotToApply)),
            (request.Exceptions, nameof(request.Exceptions)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Title, nameof(request.Title)),
            (request.Context, nameof(request.Context)),
            (request.Pattern, nameof(request.Pattern)),
            (request.WhenToApply, nameof(request.WhenToApply)),
            (request.WhenNotToApply, nameof(request.WhenNotToApply)),
            (request.Exceptions, nameof(request.Exceptions)),
            (request.Origin, nameof(request.Origin)),
            (request.Domain, nameof(request.Domain)),
            (request.Project, nameof(request.Project)));

        foreach (var example in request.Examples ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((example, nameof(request.Examples)));
        }

        foreach (var target in request.UsedBy ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.UsedBy)));
        }

        foreach (var target in request.CandidateForReuse ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.CandidateForReuse)));
        }
    }

    private static string ResolveRelativePath(RegisterPatternRequest request)
    {
        var fileName = $"{Slugify(request.Title)}.md";
        return request.Scope switch
        {
            KnowledgeScope.Global => Path.Combine("global", "patterns", fileName),
            KnowledgeScope.Domain => Path.Combine("domains", request.Domain!, "patterns", fileName),
            KnowledgeScope.Project => Path.Combine("projects", request.Project!, "patterns", fileName),
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
            throw new InvalidOperationException("Resolved pattern path escapes the knowledge root.");
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

    private static void ValidateExamples(IReadOnlyList<string>? examples)
    {
        if (examples is null)
        {
            return;
        }

        foreach (var example in examples)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(example, nameof(examples));
        }
    }

    private static string RenderMarkdown(RegisterPatternRequest request)
    {
        var scope = request.Scope.ToString().ToLowerInvariant();
        var title = EscapeYaml(request.Title.Trim());
        var origin = EscapeYaml(request.Origin.Trim());
        var domain = request.Domain is null ? string.Empty : $"domain: \"{EscapeYaml(request.Domain)}\"\n";
        var project = request.Project is null ? string.Empty : $"project: \"{EscapeYaml(request.Project)}\"\n";
        var exceptions = string.IsNullOrWhiteSpace(request.Exceptions)
            ? "Nenhuma excecao registrada."
            : request.Exceptions.Trim();
        var examples = RenderExamples(request.Examples);
        var links = RenderLinks(request.UsedBy, request.CandidateForReuse);
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var reviewUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)).ToString("yyyy-MM-dd");

        return $$"""
            ---
            type: pattern
            scope: {{scope}}
            title: "{{title}}"
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            {{domain}}{{project}}origin: "{{origin}}"
            {{links}}
            ---
            # {{request.Title.Trim()}}

            ## Contexto

            {{request.Context.Trim()}}

            ## Padrao

            {{request.Pattern.Trim()}}

            ## Quando aplicar

            {{request.WhenToApply.Trim()}}

            ## Quando nao aplicar

            {{request.WhenNotToApply.Trim()}}

            ## Excecoes

            {{exceptions}}

            ## Exemplos

            {{examples}}

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

    private static string RenderExamples(IReadOnlyList<string>? examples)
    {
        var values = examples?
            .Where(example => !string.IsNullOrWhiteSpace(example))
            .Select(example => example.Trim())
            .ToList();

        if (values is null || values.Count == 0)
        {
            return "Nenhum exemplo registrado.";
        }

        var builder = new StringBuilder();
        foreach (var example in values)
        {
            builder.Append("- ");
            builder.AppendLine(example);
        }

        return builder.ToString().TrimEnd();
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
        IReadOnlyList<string>? usedBy,
        IReadOnlyList<string>? candidateForReuse)
    {
        var links = new List<(string Type, string Target)>();
        // usadoPor → source_for (pattern is a source for consumers)
        links.AddRange(ReadTargets(PreferredRelationLinks.SourceFor, usedBy));
        // candidatoParaReuso → related_* when path is reliable, else documents
        links.AddRange(ReadMappedTargets(candidateForReuse, PreferredRelationLinks.InferRelated));

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

    private static IEnumerable<(string Type, string Target)> ReadTargets(
        string type,
        IReadOnlyList<string>? targets)
    {
        return targets?
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => (type, target.Trim()))
            ?? [];
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
        return string.IsNullOrWhiteSpace(slug) ? "padrao" : slug;
    }
}
