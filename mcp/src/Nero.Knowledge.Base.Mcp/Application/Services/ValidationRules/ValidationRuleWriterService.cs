using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.ValidationRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.ValidationRules;

public sealed class ValidationRuleWriterService(KnowledgeWritePolicy? writePolicy = null)
{
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterValidationRuleResult> WriteAsync(
        string knowledgeRootPath,
        RegisterValidationRuleRequest request,
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

        return new RegisterValidationRuleResult
        {
            Path = fullPath,
            RelativePath = writeLocation.RelativePath,
            Title = request.Title.Trim()
        };
    }

    private static void ValidateRequest(RegisterValidationRuleRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Criteria);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);

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
            (request.Rule, nameof(request.Rule)),
            (request.Criteria, nameof(request.Criteria)),
            (request.Evidence, nameof(request.Evidence)),
            (request.KnownGaps, nameof(request.KnownGaps)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Title, nameof(request.Title)),
            (request.Rule, nameof(request.Rule)),
            (request.Criteria, nameof(request.Criteria)),
            (request.Evidence, nameof(request.Evidence)),
            (request.KnownGaps, nameof(request.KnownGaps)),
            (request.Origin, nameof(request.Origin)),
            (request.Domain, nameof(request.Domain)),
            (request.Project, nameof(request.Project)));
    }

    private static string ResolveRelativePath(RegisterValidationRuleRequest request)
    {
        var fileName = $"{Slugify(request.Title)}.md";
        return request.Scope switch
        {
            KnowledgeScope.Global => Path.Combine("global", "validation-and-tests", fileName),
            KnowledgeScope.Domain => Path.Combine("domains", request.Domain!, "validation-and-tests", fileName),
            KnowledgeScope.Project => Path.Combine("projects", request.Project!, "validation-and-tests", fileName),
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
            throw new InvalidOperationException("Resolved validation rule path escapes the knowledge root.");
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

    private static string RenderMarkdown(RegisterValidationRuleRequest request)
    {
        var scope = request.Scope.ToString().ToLowerInvariant();
        var title = EscapeYaml(request.Title.Trim());
        var origin = EscapeYaml(request.Origin.Trim());
        var domain = request.Domain is null ? string.Empty : $"domain: \"{EscapeYaml(request.Domain)}\"\n";
        var project = request.Project is null ? string.Empty : $"project: \"{EscapeYaml(request.Project)}\"\n";
        var knownGaps = string.IsNullOrWhiteSpace(request.KnownGaps)
            ? "Nenhuma lacuna conhecida registrada."
            : request.KnownGaps.Trim();
        var links = PreferredRelationLinks.RenderMinimalScopeLinks(
            request.Scope,
            request.Domain,
            request.Project,
            EscapeYaml);
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var reviewUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)).ToString("yyyy-MM-dd");

        return $$"""
            ---
            type: validation_rule
            scope: {{scope}}
            title: "{{title}}"
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            {{domain}}{{project}}origin: "{{origin}}"
            {{links}}
            ---
            # {{request.Title.Trim()}}

            ## Objetivo

            {{request.Rule.Trim()}}

            ## Criterio

            {{request.Criteria.Trim()}}

            ## Evidencia esperada

            {{request.Evidence.Trim()}}

            ## Lacunas conhecidas

            {{knownGaps}}

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
        return string.IsNullOrWhiteSpace(slug) ? "regra-de-validacao" : slug;
    }
}
