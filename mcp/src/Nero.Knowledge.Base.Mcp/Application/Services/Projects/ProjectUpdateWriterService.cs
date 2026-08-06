using System.Globalization;
using System.Text;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Projects;

/// <summary>
/// Template-bound updates for project index/context/inventory (Marco 21).
/// </summary>
public sealed class ProjectUpdateWriterService(
    ActiveDomainCatalog? domainCatalog = null,
    KnowledgeWritePolicy? writePolicy = null)
{
    private const int MaxPurposeLength = 500;
    private const int MaxOriginLength = 300;
    private const int MaxArquivosCount = 30;
    private const int MaxArquivoItemLength = 120;
    private const int MaxContextFieldLength = 2000;
    private const int MaxSkillLength = 300;
    private const int MaxClassificacaoLength = 500;
    private const int MaxGitFieldLength = 200;
    private const int MaxSinaisCount = 40;
    private const int MaxSinalItemLength = 300;

    private readonly ActiveDomainCatalog domainCatalog = domainCatalog ?? new ActiveDomainCatalog();
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public Task<UpdateProjectFileResult> UpdateIndexAsync(
        string knowledgeRootPath,
        UpdateProjectIndexRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            knowledgeRootPath,
            request.Project,
            request.Domain,
            fileKind: "index",
            fileName: "index.md",
            requireExistingTarget: true,
            markdownFactory: () =>
            {
                ValidateIndexRequest(request);
                return RenderIndexMarkdown(request);
            },
            cancellationToken);

    public Task<UpdateProjectFileResult> UpdateContextAsync(
        string knowledgeRootPath,
        UpdateProjectContextRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            knowledgeRootPath,
            request.Project,
            request.Domain,
            fileKind: "context",
            fileName: "context.md",
            requireExistingTarget: true,
            markdownFactory: () =>
            {
                ValidateContextRequest(request);
                return RenderContextMarkdown(request);
            },
            cancellationToken);

    public Task<UpdateProjectFileResult> UpdateInventoryAsync(
        string knowledgeRootPath,
        UpdateProjectInventoryRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            knowledgeRootPath,
            request.Project,
            request.Domain,
            fileKind: "inventory",
            fileName: "inventory.md",
            requireExistingTarget: false,
            markdownFactory: () =>
            {
                ValidateInventoryRequest(request);
                return RenderInventoryMarkdown(request);
            },
            cancellationToken);

    private async Task<UpdateProjectFileResult> UpdateAsync(
        string knowledgeRootPath,
        string projectRaw,
        string domainRaw,
        string fileKind,
        string fileName,
        bool requireExistingTarget,
        Func<string> markdownFactory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRaw);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainRaw);

        var project = projectRaw.Trim();
        var domain = domainRaw.Trim().ToLowerInvariant();
        ValidatePathSegment(project, nameof(project));

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);
        domainCatalog.EnsureActiveDomain(rootPath, domain);

        var indexLocation = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("projects", project, "index.md"));
        if (!File.Exists(indexLocation.FullPath))
        {
            throw new InvalidOperationException(
                $"Project '{project}' is missing index.md. Run nero_register_project before update tools.");
        }

        var targetLocation = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("projects", project, fileName));
        var existed = File.Exists(targetLocation.FullPath);
        if (requireExistingTarget && !existed)
        {
            throw new InvalidOperationException(
                $"Project '{project}' is missing {fileName}. Bootstrap with nero_register_project (index/context) before updating.");
        }

        var markdown = markdownFactory();
        await KnowledgeMarkdownFileWriter.WriteReplaceAsync(targetLocation.FullPath, markdown, cancellationToken);

        return new UpdateProjectFileResult
        {
            Project = project,
            Domain = domain,
            FileKind = fileKind,
            Path = targetLocation.FullPath,
            RelativePath = targetLocation.RelativePath,
            Created = !existed
        };
    }

    private static void ValidateIndexRequest(UpdateProjectIndexRequest request)
    {
        RequireBounded(request.Purpose, nameof(request.Purpose), MaxPurposeLength);
        ValidateOptionalBounded(request.Origin, nameof(request.Origin), MaxOriginLength);
        ArgumentNullException.ThrowIfNull(request.Arquivos);
        if (request.Arquivos.Count == 0 || request.Arquivos.Count > MaxArquivosCount)
        {
            throw new ArgumentException(
                $"Arquivos must contain between 1 and {MaxArquivosCount} items.",
                nameof(request.Arquivos));
        }

        foreach (var item in request.Arquivos)
        {
            RequireBounded(item, nameof(request.Arquivos), MaxArquivoItemLength);
        }

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Project, nameof(request.Project)),
            (request.Domain, nameof(request.Domain)),
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)));

        foreach (var item in request.Arquivos)
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.Arquivos)));
        }
    }

    private static void ValidateContextRequest(UpdateProjectContextRequest request)
    {
        RequireBounded(request.Purpose, nameof(request.Purpose), MaxPurposeLength);
        RequireBounded(request.Stack, nameof(request.Stack), MaxContextFieldLength);
        RequireBounded(request.Superficie, nameof(request.Superficie), MaxContextFieldLength);
        RequireBounded(request.ResumoOperacional, nameof(request.ResumoOperacional), MaxContextFieldLength);
        ValidateOptionalBounded(request.SkillOperacional, nameof(request.SkillOperacional), MaxSkillLength);
        ValidateOptionalBounded(request.Origin, nameof(request.Origin), MaxOriginLength);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Purpose, nameof(request.Purpose)),
            (request.Stack, nameof(request.Stack)),
            (request.Superficie, nameof(request.Superficie)),
            (request.ResumoOperacional, nameof(request.ResumoOperacional)),
            (request.SkillOperacional, nameof(request.SkillOperacional)),
            (request.Origin, nameof(request.Origin)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Project, nameof(request.Project)),
            (request.Domain, nameof(request.Domain)),
            (request.Purpose, nameof(request.Purpose)),
            (request.Stack, nameof(request.Stack)),
            (request.Superficie, nameof(request.Superficie)),
            (request.ResumoOperacional, nameof(request.ResumoOperacional)),
            (request.SkillOperacional, nameof(request.SkillOperacional)),
            (request.Origin, nameof(request.Origin)));
    }

    private static void ValidateInventoryRequest(UpdateProjectInventoryRequest request)
    {
        RequireBounded(request.Classificacao, nameof(request.Classificacao), MaxClassificacaoLength);
        RequireBounded(request.ReviewedAt, nameof(request.ReviewedAt), 32);
        if (!DateOnly.TryParseExact(
                request.ReviewedAt.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new ArgumentException("ReviewedAt must be an ISO date (yyyy-MM-dd).", nameof(request.ReviewedAt));
        }

        ValidateOptionalBounded(request.GitBranch, nameof(request.GitBranch), MaxGitFieldLength);
        ValidateOptionalBounded(request.GitHead, nameof(request.GitHead), MaxGitFieldLength);
        ValidateOptionalBounded(request.GitRemote, nameof(request.GitRemote), MaxGitFieldLength);
        ValidateOptionalBounded(request.Origin, nameof(request.Origin), MaxOriginLength);
        ArgumentNullException.ThrowIfNull(request.SinaisTecnicos);
        if (request.SinaisTecnicos.Count == 0 || request.SinaisTecnicos.Count > MaxSinaisCount)
        {
            throw new ArgumentException(
                $"SinaisTecnicos must contain between 1 and {MaxSinaisCount} items.",
                nameof(request.SinaisTecnicos));
        }

        foreach (var item in request.SinaisTecnicos)
        {
            RequireBounded(item, nameof(request.SinaisTecnicos), MaxSinalItemLength);
        }

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Classificacao, nameof(request.Classificacao)),
            (request.Origin, nameof(request.Origin)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Project, nameof(request.Project)),
            (request.Domain, nameof(request.Domain)),
            (request.Classificacao, nameof(request.Classificacao)),
            (request.ReviewedAt, nameof(request.ReviewedAt)),
            (request.GitBranch, nameof(request.GitBranch)),
            (request.GitHead, nameof(request.GitHead)),
            (request.GitRemote, nameof(request.GitRemote)),
            (request.Origin, nameof(request.Origin)));

        foreach (var item in request.SinaisTecnicos)
        {
            ComplianceScanner.EnsureNoBlockingHits((item, nameof(request.SinaisTecnicos)));
        }
    }

    private static string RenderIndexMarkdown(UpdateProjectIndexRequest request)
    {
        var project = EscapeYaml(request.Project.Trim());
        var domain = EscapeYaml(request.Domain.Trim().ToLowerInvariant());
        var arquivos = string.Join(
            Environment.NewLine,
            request.Arquivos.Select(item => $"- `{item.Trim().Trim('`')}`"));
        var originBlock = string.IsNullOrWhiteSpace(request.Origin)
            ? string.Empty
            : $"""

            ## Origem

            {request.Origin.Trim()}
            """;

        return $$"""
            ---
            type: project_index
            scope: project
            project: {{project}}
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            links:
              - type: documents
                target: projects/{{project}}
              - type: belongs_to_domain
                target: domains/{{domain}}
            ---

            # {{request.Project.Trim()}}

            {{request.Purpose.Trim()}}

            ## Arquivos

            {{arquivos}}{{originBlock}}
            """;
    }

    private static string RenderContextMarkdown(UpdateProjectContextRequest request)
    {
        var project = EscapeYaml(request.Project.Trim());
        var domain = EscapeYaml(request.Domain.Trim().ToLowerInvariant());
        var updatedAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var skillBlock = string.IsNullOrWhiteSpace(request.SkillOperacional)
            ? string.Empty
            : $"""

            ## Skill operacional

            {request.SkillOperacional.Trim()}
            """;
        var originBlock = string.IsNullOrWhiteSpace(request.Origin)
            ? string.Empty
            : $"""

            ## Origem

            {request.Origin.Trim()}
            """;

        return $$"""
            ---
            type: project_context
            scope: project
            project: {{project}}
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            links:
              - type: documents
                target: projects/{{project}}
              - type: belongs_to_domain
                target: domains/{{domain}}
            ---

            # {{request.Project.Trim()}}

            Atualizado em: {{updatedAt}}

            ## Proposito

            {{request.Purpose.Trim()}}

            ## Dominio

            {{request.Domain.Trim().ToLowerInvariant()}}

            ## Stack

            {{request.Stack.Trim()}}

            ## Superficie

            {{request.Superficie.Trim()}}

            ## Contexto consolidado

            {{request.ResumoOperacional.Trim()}}{{skillBlock}}{{originBlock}}
            """;
    }

    private static string RenderInventoryMarkdown(UpdateProjectInventoryRequest request)
    {
        var project = EscapeYaml(request.Project.Trim());
        var domain = EscapeYaml(request.Domain.Trim().ToLowerInvariant());
        var reviewedAt = request.ReviewedAt.Trim();
        var sinais = string.Join(
            Environment.NewLine,
            request.SinaisTecnicos.Select(item => $"- {item.Trim()}"));

        var gitLines = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.GitBranch))
        {
            gitLines.AppendLine($"- Branch: `{request.GitBranch.Trim()}`");
        }

        if (!string.IsNullOrWhiteSpace(request.GitHead))
        {
            gitLines.AppendLine($"- HEAD: `{request.GitHead.Trim()}`");
        }

        if (!string.IsNullOrWhiteSpace(request.GitRemote))
        {
            gitLines.AppendLine($"- Remote: `{request.GitRemote.Trim()}`");
        }

        if (gitLines.Length == 0)
        {
            gitLines.AppendLine("- (git metadata omitted)");
        }

        var originBlock = string.IsNullOrWhiteSpace(request.Origin)
            ? string.Empty
            : $"""

            ## Origem

            {request.Origin.Trim()}
            """;

        return $$"""
            ---
            type: project_inventory
            scope: project
            project: {{project}}
            data_class: {{ComplianceFrontmatter.DefaultDataClass}}
            reviewed_at: "{{reviewedAt}}"
            links:
              - type: documents
                target: projects/{{project}}
              - type: belongs_to_domain
                target: domains/{{domain}}
            ---

            # Inventario local - {{request.Project.Trim()}}

            ## Estado Git (review {{reviewedAt}})

            {{gitLines.ToString().TrimEnd()}}

            ## Classificacao

            {{request.Classificacao.Trim()}}

            ## Sinais tecnicos

            {{sinais}}{{originBlock}}
            """;
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

    private static string EscapeYaml(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
