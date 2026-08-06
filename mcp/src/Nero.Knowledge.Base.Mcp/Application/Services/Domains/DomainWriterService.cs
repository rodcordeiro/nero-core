using System.Globalization;
using System.Text.RegularExpressions;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Domains;

/// <summary>
/// Controlled create/update/inactivate for domain index.md (Marco 22).
/// </summary>
public sealed partial class DomainWriterService(
    ActiveDomainCatalog? domainCatalog = null,
    KnowledgeWritePolicy? writePolicy = null)
{
    public const string LinkedProjectsConfirmation = "INACTIVATE_WITH_LINKED_PROJECTS";

    private const int MaxPurposeLength = 2000;
    private const int MaxOriginLength = 300;
    private const int MaxMotivoLength = 1000;
    private const int MaxEvidenciaLength = 2000;
    private const int MaxRegrasLength = 4000;
    private const int MaxFonteLength = 4000;
    private const int MaxTituloLength = 120;
    private const int MaxArquivosCount = 40;
    private const int MaxArquivoItemLength = 400;
    private const int MaxSourceForCount = 50;
    private const int MaxSourceForItemLength = 120;

    private readonly ActiveDomainCatalog domainCatalog = domainCatalog ?? new ActiveDomainCatalog();
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<DomainWriteResult> RegisterAsync(
        string knowledgeRootPath,
        RegisterDomainRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentNullException.ThrowIfNull(request);
        RequireBounded(request.Purpose, nameof(request.Purpose), MaxPurposeLength);
        RequireBounded(request.Origin, nameof(request.Origin), MaxOriginLength);
        domainCatalog.ValidateSlug(request.Domain);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Purpose, nameof(request.Purpose)),
            (request.FonteConsolidada, nameof(request.FonteConsolidada)),
            (request.RegrasLeitura, nameof(request.RegrasLeitura)),
            (request.Origin, nameof(request.Origin)),
            (request.Titulo, nameof(request.Titulo)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Domain, nameof(request.Domain)),
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)),
            (request.Titulo, nameof(request.Titulo)),
            (request.FonteConsolidada, nameof(request.FonteConsolidada)),
            (request.RegrasLeitura, nameof(request.RegrasLeitura)));

        foreach (var item in request.Arquivos ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.Arquivos)));
        }

        foreach (var item in request.SourceFor ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.SourceFor)));
        }

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);
        var domain = request.Domain.Trim().ToLowerInvariant();

        if (domainCatalog.DomainIndexExists(rootPath, domain))
        {
            var existingStatus = domainCatalog.TryGetStatus(rootPath, domain) ?? ActiveDomainCatalog.StatusActive;
            if (string.Equals(existingStatus, ActiveDomainCatalog.StatusInactive, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Domain '{domain}' already exists as inactive. Reactivate with nero_update_domain (reativar=true); do not recreate.");
            }

            throw new InvalidOperationException(
                $"Domain '{domain}' already exists. Use nero_update_domain to change its index.");
        }

        var location = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("domains", domain, "index.md"));
        var markdown = RenderIndexMarkdown(
            domain,
            titulo: request.Titulo,
            purpose: request.Purpose.Trim(),
            fonteConsolidada: request.FonteConsolidada,
            arquivos: request.Arquivos ?? ["patterns.md", "business-rules.md", "validation-and-tests.md"],
            regrasLeitura: request.RegrasLeitura,
            origin: request.Origin.Trim(),
            sourceFor: request.SourceFor,
            status: ActiveDomainCatalog.StatusActive);

        await KnowledgeMarkdownFileWriter.WriteNewAsync(location.FullPath, markdown, cancellationToken);

        return new DomainWriteResult
        {
            Domain = domain,
            Status = ActiveDomainCatalog.StatusActive,
            Path = location.FullPath,
            RelativePath = location.RelativePath,
            Action = "register",
            Created = true
        };
    }

    public async Task<DomainWriteResult> UpdateAsync(
        string knowledgeRootPath,
        UpdateDomainRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentNullException.ThrowIfNull(request);
        RequireBounded(request.Purpose, nameof(request.Purpose), MaxPurposeLength);
        ValidateOptionalBounded(request.Origin, nameof(request.Origin), MaxOriginLength);
        ValidateOptionalBounded(request.Titulo, nameof(request.Titulo), MaxTituloLength);
        ValidateOptionalBounded(request.FonteConsolidada, nameof(request.FonteConsolidada), MaxFonteLength);
        ValidateOptionalBounded(request.RegrasLeitura, nameof(request.RegrasLeitura), MaxRegrasLength);
        ValidateArquivos(request.Arquivos);
        ValidateSourceFor(request.SourceFor);
        domainCatalog.ValidateSlug(request.Domain);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Purpose, nameof(request.Purpose)),
            (request.FonteConsolidada, nameof(request.FonteConsolidada)),
            (request.RegrasLeitura, nameof(request.RegrasLeitura)),
            (request.Origin, nameof(request.Origin)),
            (request.Titulo, nameof(request.Titulo)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Domain, nameof(request.Domain)),
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)),
            (request.Titulo, nameof(request.Titulo)),
            (request.FonteConsolidada, nameof(request.FonteConsolidada)),
            (request.RegrasLeitura, nameof(request.RegrasLeitura)));

        foreach (var item in request.Arquivos ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.Arquivos)));
        }

        foreach (var item in request.SourceFor ?? [])
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.SourceFor)));
        }

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);
        var domain = request.Domain.Trim().ToLowerInvariant();

        if (!domainCatalog.DomainIndexExists(rootPath, domain))
        {
            throw new InvalidOperationException(
                $"Domain '{domain}' is missing index.md. Run nero_register_domain first.");
        }

        var currentStatus = domainCatalog.TryGetStatus(rootPath, domain) ?? ActiveDomainCatalog.StatusActive;
        var isInactive = string.Equals(currentStatus, ActiveDomainCatalog.StatusInactive, StringComparison.OrdinalIgnoreCase);
        if (isInactive && !request.Reativar)
        {
            throw new InvalidOperationException(
                $"Domain '{domain}' is inactive. Pass reativar=true to reactivate and rewrite the index, or leave it inactive.");
        }

        if (!isInactive && request.Reativar)
        {
            throw new InvalidOperationException(
                $"Domain '{domain}' is already active. Omit reativar when updating an active domain.");
        }

        var location = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("domains", domain, "index.md"));
        var effectiveSourceFor = request.SourceFor
            ?? ReadExistingSourceForTargets(location.FullPath);
        var markdown = RenderIndexMarkdown(
            domain,
            titulo: request.Titulo,
            purpose: request.Purpose.Trim(),
            fonteConsolidada: request.FonteConsolidada,
            arquivos: request.Arquivos,
            regrasLeitura: request.RegrasLeitura,
            origin: request.Origin,
            sourceFor: effectiveSourceFor,
            status: ActiveDomainCatalog.StatusActive);

        await KnowledgeMarkdownFileWriter.WriteReplaceAsync(location.FullPath, markdown, cancellationToken);

        return new DomainWriteResult
        {
            Domain = domain,
            Status = ActiveDomainCatalog.StatusActive,
            Path = location.FullPath,
            RelativePath = location.RelativePath,
            Action = request.Reativar ? "reactivate" : "update",
            Created = false
        };
    }

    public async Task<DomainWriteResult> InactivateAsync(
        string knowledgeRootPath,
        InactivateDomainRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentNullException.ThrowIfNull(request);
        RequireBounded(request.Motivo, nameof(request.Motivo), MaxMotivoLength);
        RequireBounded(request.Origin, nameof(request.Origin), MaxOriginLength);
        ValidateOptionalBounded(request.Evidencia, nameof(request.Evidencia), MaxEvidenciaLength);
        domainCatalog.ValidateSlug(request.Domain);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Motivo, nameof(request.Motivo)),
            (request.Origin, nameof(request.Origin)),
            (request.Evidencia, nameof(request.Evidencia)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Domain, nameof(request.Domain)),
            (request.Motivo, nameof(request.Motivo)),
            (request.Origin, nameof(request.Origin)),
            (request.Evidencia, nameof(request.Evidencia)),
            (request.Confirmacao, nameof(request.Confirmacao)));

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);
        var domain = request.Domain.Trim().ToLowerInvariant();

        if (!domainCatalog.DomainIndexExists(rootPath, domain))
        {
            throw new InvalidOperationException($"Domain '{domain}' does not exist.");
        }

        var currentStatus = domainCatalog.TryGetStatus(rootPath, domain) ?? ActiveDomainCatalog.StatusActive;
        if (string.Equals(currentStatus, ActiveDomainCatalog.StatusInactive, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Domain '{domain}' is already inactive.");
        }

        var linkedProjects = domainCatalog.FindProjectsLinkedToDomain(rootPath, domain);
        if (linkedProjects.Count > 0)
        {
            if (!string.Equals(request.Confirmacao?.Trim(), LinkedProjectsConfirmation, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Domain '{domain}' still has belongs_to_domain from projects: {string.Join(", ", linkedProjects)}. "
                    + $"Pass confirmacao={LinkedProjectsConfirmation} and evidencia to soft-inactivate anyway.");
            }

            if (string.IsNullOrWhiteSpace(request.Evidencia))
            {
                throw new ArgumentException(
                    "Evidencia is required when inactivating a domain with linked projects.",
                    nameof(request.Evidencia));
            }
        }

        var location = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("domains", domain, "index.md"));
        var existing = await File.ReadAllTextAsync(location.FullPath, cancellationToken);
        var updated = ApplyInactiveStatus(existing, domain, request.Motivo.Trim(), request.Origin.Trim(), request.Evidencia);
        await KnowledgeMarkdownFileWriter.WriteReplaceAsync(location.FullPath, updated, cancellationToken);

        return new DomainWriteResult
        {
            Domain = domain,
            Status = ActiveDomainCatalog.StatusInactive,
            Path = location.FullPath,
            RelativePath = location.RelativePath,
            Action = "inactivate",
            Created = false,
            LinkedProjects = linkedProjects
        };
    }

    private static string ApplyInactiveStatus(
        string markdown,
        string domain,
        string motivo,
        string origin,
        string? evidencia)
    {
        var normalized = markdown.ReplaceLineEndings("\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Domain '{domain}' index.md is missing YAML frontmatter.");
        }

        var endIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new InvalidOperationException($"Domain '{domain}' index.md has malformed YAML frontmatter.");
        }

        var frontmatter = normalized[4..endIndex];
        var body = normalized[(endIndex + 5)..].TrimEnd() + "\n";
        if (StatusLineRegex().IsMatch(frontmatter))
        {
            frontmatter = StatusLineRegex().Replace(frontmatter, "status: inactive");
        }
        else if (DomainLineRegex().IsMatch(frontmatter))
        {
            frontmatter = DomainLineRegex().Replace(frontmatter, $"domain: {domain}\nstatus: inactive");
        }
        else
        {
            frontmatter = "status: inactive\n" + frontmatter;
        }

        var stamp = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var evidenceBlock = string.IsNullOrWhiteSpace(evidencia)
            ? string.Empty
            : $"\n\nEvidencia:\n\n{evidencia.Trim()}";

        if (body.Contains("\n## Inativacao\n", StringComparison.Ordinal))
        {
            body = InactivationSectionRegex().Replace(
                body,
                $"\n## Inativacao\n\nData: {stamp}\n\nMotivo: {motivo}\n\nOrigem: {origin}{evidenceBlock}\n");
        }
        else
        {
            body += $"\n## Inativacao\n\nData: {stamp}\n\nMotivo: {motivo}\n\nOrigem: {origin}{evidenceBlock}\n";
        }

        return $"---\n{frontmatter}\n---\n{body}";
    }

    private static string RenderIndexMarkdown(
        string domain,
        string? titulo,
        string purpose,
        string? fonteConsolidada,
        IReadOnlyList<string> arquivos,
        string? regrasLeitura,
        string? origin,
        IReadOnlyList<string>? sourceFor,
        string status)
    {
        ValidateArquivos(arquivos);
        ValidateSourceFor(sourceFor);
        ValidateOptionalBounded(titulo, nameof(titulo), MaxTituloLength);
        ValidateOptionalBounded(fonteConsolidada, nameof(fonteConsolidada), MaxFonteLength);
        ValidateOptionalBounded(regrasLeitura, nameof(regrasLeitura), MaxRegrasLength);
        ValidateOptionalBounded(origin, nameof(origin), MaxOriginLength);

        var heading = string.IsNullOrWhiteSpace(titulo)
            ? $"Dominio {domain}"
            : titulo.Trim();
        var arquivosBlock = string.Join(
            Environment.NewLine,
            arquivos.Select(item => $"- {item.Trim().TrimStart('-').Trim()}"));
        var regrasBlock = string.IsNullOrWhiteSpace(regrasLeitura)
            ? "- Documentar regras de leitura quando o dominio crescer."
            : regrasLeitura.Trim();
        var fonteBlock = string.IsNullOrWhiteSpace(fonteConsolidada)
            ? string.Empty
            : $"""


            ## Fonte consolidada

            {fonteConsolidada.Trim()}
            """;
        var originBlock = string.IsNullOrWhiteSpace(origin)
            ? string.Empty
            : $"""

            ## Origem

            {origin.Trim()}
            """;
        var sourceForLinks = string.Join(
            Environment.NewLine,
            NormalizeSourceFor(sourceFor).Select(target =>
                $"  - type: source_for{Environment.NewLine}    target: {target}"));
        var sourceForBlock = string.IsNullOrWhiteSpace(sourceForLinks)
            ? string.Empty
            : Environment.NewLine + sourceForLinks;

        return $$"""
            ---
            type: domain_index
            scope: domain
            domain: {{domain}}
            status: {{status}}
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            links:
              - type: documents
                target: domains/{{domain}}{{sourceForBlock}}
            ---

            # {{heading}}

            {{purpose}}{{fonteBlock}}

            ## Arquivos principais

            {{arquivosBlock}}

            ## Regras de leitura rapida

            {{regrasBlock}}{{originBlock}}
            """;
    }

    private static IReadOnlyList<string> ReadExistingSourceForTargets(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return [];
        }

        var markdown = File.ReadAllText(indexPath).ReplaceLineEndings("\n");
        var targets = new List<string>();
        var lines = markdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("type: source_for", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
            {
                var candidate = lines[j].Trim();
                if (candidate.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
                {
                    var target = candidate["target:".Length..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        targets.Add(target);
                    }

                    break;
                }

                if (candidate.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        return targets;
    }

    private static IReadOnlyList<string> NormalizeSourceFor(IReadOnlyList<string>? sourceFor)
    {
        if (sourceFor is null || sourceFor.Count == 0)
        {
            return [];
        }

        return sourceFor
            .Select(item =>
            {
                var value = item.Trim().Trim('`');
                if (value.StartsWith("projects/", StringComparison.OrdinalIgnoreCase))
                {
                    return "projects/" + value["projects/".Length..].Trim();
                }

                return "projects/" + value;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateSourceFor(IReadOnlyList<string>? sourceFor)
    {
        if (sourceFor is null)
        {
            return;
        }

        if (sourceFor.Count > MaxSourceForCount)
        {
            throw new ArgumentException(
                $"SourceFor must contain at most {MaxSourceForCount} items.",
                nameof(sourceFor));
        }

        foreach (var item in sourceFor)
        {
            RequireBounded(item, nameof(sourceFor), MaxSourceForItemLength);
            var normalized = item.Trim();
            if (normalized.Contains("..", StringComparison.Ordinal)
                || normalized.Contains('\\', StringComparison.Ordinal)
                || normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "SourceFor items must be project names or projects/<Projeto> paths without traversal.",
                    nameof(sourceFor));
            }
        }
    }

    private static void ValidateArquivos(IReadOnlyList<string> arquivos)
    {
        ArgumentNullException.ThrowIfNull(arquivos);
        if (arquivos.Count == 0 || arquivos.Count > MaxArquivosCount)
        {
            throw new ArgumentException(
                $"Arquivos must contain between 1 and {MaxArquivosCount} items.",
                nameof(arquivos));
        }

        foreach (var item in arquivos)
        {
            RequireBounded(item, nameof(arquivos), MaxArquivoItemLength);
        }
    }

    private static void RequireBounded(string? value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} must be at most {maxLength} characters.", parameterName);
        }
    }

    private static void ValidateOptionalBounded(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} must be at most {maxLength} characters.", parameterName);
        }
    }

    [GeneratedRegex(@"^status:\s*.*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex StatusLineRegex();

    [GeneratedRegex(@"^domain:\s*.*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DomainLineRegex();

    [GeneratedRegex(@"\n## Inativacao\n[\s\S]*?(?=\n## |\z)", RegexOptions.CultureInvariant)]
    private static partial Regex InactivationSectionRegex();
}
