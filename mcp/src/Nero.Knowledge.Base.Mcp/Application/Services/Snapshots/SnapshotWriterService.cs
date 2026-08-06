using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;

public sealed class SnapshotWriterService(KnowledgeWritePolicy? writePolicy = null)
{
    /// <summary>Kept as an alias for backward compatibility; source of truth is <see cref="KnowledgeFieldLimits"/> (Marco 23).</summary>
    public const int MaximumLongFieldSizeBytes = KnowledgeFieldLimits.MaxLongFieldUtf8Bytes;

    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterSnapshotResult> WriteAsync(
        string knowledgeRootPath,
        RegisterSnapshotRequest request,
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

        return new RegisterSnapshotResult
        {
            Path = fullPath,
            RelativePath = writeLocation.RelativePath,
            Title = request.Title.Trim()
        };
    }

    private static void ValidateRequest(RegisterSnapshotRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Context);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);
        KnowledgeFieldLimits.EnsureUtf8WithinLimit(request.Context, nameof(request.Context));
        KnowledgeFieldLimits.EnsureUtf8WithinLimit(request.Evidence, nameof(request.Evidence));
        ValidateTargets(request.RelatesTo, nameof(request.RelatesTo));
        ValidateTargets(request.Evidences, nameof(request.Evidences));
        ValidateEvidencesNotHubs(request.Evidences);

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

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Title, nameof(request.Title)),
            (request.Context, nameof(request.Context)),
            (request.Evidence, nameof(request.Evidence)),
            (request.Origin, nameof(request.Origin)),
            (request.Domain, nameof(request.Domain)),
            (request.Project, nameof(request.Project)));

        foreach (var target in request.RelatesTo ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.RelatesTo)));
        }

        foreach (var target in request.Evidences ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((target, nameof(request.Evidences)));
        }
    }

    private static string ResolveRelativePath(RegisterSnapshotRequest request)
    {
        var fileName = $"{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}-{Slugify(request.Title)}.md";
        return request.Scope switch
        {
            KnowledgeScope.Global => Path.Combine("global", "snapshots", fileName),
            KnowledgeScope.Domain => Path.Combine("domains", request.Domain!, "snapshots", fileName),
            KnowledgeScope.Project => Path.Combine("projects", request.Project!, "snapshots", fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scope), request.Scope, "Scope must be supported.")
        };
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

    private static string RenderMarkdown(RegisterSnapshotRequest request)
    {
        var scope = request.Scope.ToString().ToLowerInvariant();
        var title = EscapeYaml(request.Title.Trim());
        var origin = EscapeYaml(request.Origin.Trim());
        var domain = request.Domain is null ? string.Empty : $"domain: \"{EscapeYaml(request.Domain)}\"\n";
        var project = request.Project is null ? string.Empty : $"project: \"{EscapeYaml(request.Project)}\"\n";
        var links = RenderLinks(request.RelatesTo, request.Evidences);
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var reviewUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)).ToString("yyyy-MM-dd");
        var retentionReview = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(180)).ToString("yyyy-MM-dd");
        var retentionArchive = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)).ToString("yyyy-MM-dd");

        return $$"""
            ---
            type: snapshot
            scope: {{scope}}
            title: "{{title}}"
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            {{domain}}{{project}}origin: "{{origin}}"
            {{links}}
            ---
            # {{request.Title.Trim()}}

            ## Contexto

            {{request.Context.Trim()}}

            ## Evidencia

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

            ## Retencao

            - Revisar apos: {{retentionReview}}
            - Arquivar apos: {{retentionArchive}}
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

    /// <summary>
    /// G4 at write time — reject <c>evidences</c> targets that are directory hubs (same path heuristic as validate).
    /// </summary>
    private static void ValidateEvidencesNotHubs(IReadOnlyList<string>? evidences)
    {
        if (evidences is null)
        {
            return;
        }

        foreach (var target in evidences)
        {
            if (KnowledgeEvidenceHubDetection.IsHubTarget(target))
            {
                throw new ArgumentException(
                    KnowledgeEvidenceHubDetection.FormatWriterError(target.Trim()),
                    nameof(RegisterSnapshotRequest.Evidences));
            }
        }
    }

    private static string RenderLinks(
        IReadOnlyList<string>? relatesTo,
        IReadOnlyList<string>? evidences)
    {
        var links = new List<(string Type, string Target)>();
        // relacionadoA → documents (snapshot documents the related note/project/index)
        links.AddRange(ReadTargets(PreferredRelationLinks.Documents, relatesTo));
        links.AddRange(ReadTargets(PreferredRelationLinks.Evidences, evidences));

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
        return string.IsNullOrWhiteSpace(slug) ? "snapshot" : slug;
    }
}
