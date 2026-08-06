using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Projects;

public sealed class ProjectWriterService(
    ActiveDomainCatalog? domainCatalog = null,
    KnowledgeWritePolicy? writePolicy = null)
{
    private readonly ActiveDomainCatalog domainCatalog = domainCatalog ?? new ActiveDomainCatalog();
    private readonly KnowledgeWritePolicy writePolicy = writePolicy ?? new KnowledgeWritePolicy();

    public async Task<RegisterProjectResult> WriteAsync(
        string knowledgeRootPath,
        RegisterProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(knowledgeRootPath, request);

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        KnowledgeRootOptions.ValidateRootExists(rootPath);

        var project = request.Project.Trim();
        var indexLocation = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("projects", project, "index.md"));
        var contextLocation = writePolicy.ResolveWriteLocation(rootPath, Path.Combine("projects", project, "context.md"));
        var createdFiles = new List<string>();
        var targetPath = indexLocation.FullPath;

        try
        {
            await WriteIfMissingAsync(indexLocation.FullPath, RenderIndexMarkdown(request), createdFiles, cancellationToken);
            targetPath = contextLocation.FullPath;
            await WriteIfMissingAsync(contextLocation.FullPath, RenderContextMarkdown(request), createdFiles, cancellationToken);
        }
        catch (Exception exception)
        {
            var currentTargetWasWritten =
                exception.Data[WriteFailureMetadata.MarkdownWrittenDataKey] as bool? ?? false;
            var writtenPaths = createdFiles
                .Concat(currentTargetWasWritten ? new[] { targetPath } : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            WriteFailureMetadata.Attach(
                exception,
                targetPath,
                markdownWritten: writtenPaths.Length > 0,
                writtenPaths);
            throw;
        }

        return new RegisterProjectResult
        {
            Project = project,
            Domain = request.Domain.Trim().ToLowerInvariant(),
            ProjectDirectoryPath = Path.GetDirectoryName(indexLocation.FullPath)!,
            ProjectRelativePath = Path.GetDirectoryName(indexLocation.RelativePath)!.Replace('\\', '/'),
            IndexPath = indexLocation.FullPath,
            ContextPath = contextLocation.FullPath,
            CreatedFiles = createdFiles,
            Created = createdFiles.Count > 0
        };
    }

    private static async Task WriteIfMissingAsync(
        string fullPath,
        string markdown,
        List<string> createdFiles,
        CancellationToken cancellationToken)
    {
        if (File.Exists(fullPath))
        {
            return;
        }

        await KnowledgeMarkdownFileWriter.WriteNewAsync(fullPath, markdown, cancellationToken);
        createdFiles.Add(fullPath);
    }

    private void ValidateRequest(string knowledgeRootPath, RegisterProjectRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Project);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin);
        ValidatePathSegment(request.Project, nameof(request.Project));

        var rootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);
        domainCatalog.EnsureActiveDomain(rootPath, request.Domain);

        KnowledgeFieldLimits.EnsureUtf8WithinLimit(
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)));

        ComplianceScanner.EnsureNoBlockingHits(
            (request.Project, nameof(request.Project)),
            (request.Domain, nameof(request.Domain)),
            (request.Purpose, nameof(request.Purpose)),
            (request.Origin, nameof(request.Origin)));
    }

    private static string RenderIndexMarkdown(RegisterProjectRequest request)
    {
        var project = EscapeYaml(request.Project.Trim());
        var domain = EscapeYaml(request.Domain.Trim().ToLowerInvariant());

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

            - `context.md`

            ## Origem

            {{request.Origin.Trim()}}
            """;
    }

    private static string RenderContextMarkdown(RegisterProjectRequest request)
    {
        var project = EscapeYaml(request.Project.Trim());
        var domain = EscapeYaml(request.Domain.Trim().ToLowerInvariant());
        var registeredAt = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

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

            Data de registro: {{registeredAt}}

            ## Proposito

            {{request.Purpose.Trim()}}

            ## Dominio

            {{request.Domain.Trim().ToLowerInvariant()}}

            ## Contexto

            Contexto inicial registrado para permitir decisoes, padroes, regras, validacoes e troubleshootings no projeto.

            ## Origem

            {{request.Origin.Trim()}}
            """;
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

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
