using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Nero.Knowledge.Base.Mcp.Application.Contracts.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Patterns;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Contracts.ValidationRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Services.Graph;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Graph;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Links;
using Nero.Knowledge.Base.Mcp.Application.Services.Links;
using Nero.Knowledge.Base.Mcp.Application.Services.Patterns;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Search;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Search;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Services.ValidationRules;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

[McpServerToolType]
public sealed class NeroKnowledgeTools(
    KnowledgeDatabaseConnectionFactory connectionFactory,
    KnowledgeRootOptions knowledgeRootOptions,
    BusinessRuleWriterService businessRuleWriterService,
    DecisionWriterService decisionWriterService,
    PatternWriterService patternWriterService,
    ProjectWriterService projectWriterService,
    ProjectUpdateWriterService projectUpdateWriterService,
    DomainWriterService domainWriterService,
    SnapshotWriterService snapshotWriterService,
    TroubleshootingWriterService troubleshootingWriterService,
    ValidationRuleWriterService validationRuleWriterService,
    KnowledgeDomainContextService domainContextService,
    KnowledgeProjectContextService projectContextService,
    RelatedKnowledgeService relatedKnowledgeService,
    KnowledgeLinkService knowledgeLinkService,
    KnowledgeSearchService searchService,
    ILogger<NeroKnowledgeTools>? logger = null)
{
    [McpServerTool]
    [Description("Searches indexed Nero knowledge using SQLite FTS, optionally filtering by domain and project.")]
    public async Task<IReadOnlyList<NeroSearchKnowledgeToolResult>> nero_search_knowledge(
        [Description("Full-text search query.")]
        string query,
        [Description("Optional domain filter, for example api, mobile, front or integracoes.")]
        string? domain = null,
        [Description("Optional project filter, for example Acme.Api.")]
        string? project = null,
        [Description("Maximum number of results to return.")]
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var results = await searchService.SearchAsync(
            connection,
            query,
            domain,
            project,
            limit,
            cancellationToken);

        return results.Select(ToSearchToolResult).ToList();
    }

    [McpServerTool]
    [Description("Gets grouped Nero knowledge context for a project from the indexed SQLite knowledge base.")]
    public async Task<NeroGetProjectContextToolResult> nero_get_project_context(
        [Description("Project name, for example Acme.Api.")]
        string project,
        [Description("Whether to include recent project decisions.")]
        bool includeDecisions = true,
        [Description("Whether to include recent project troubleshooting notes.")]
        bool includeTroubleshooting = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var result = await projectContextService.GetProjectContextAsync(
            connection,
            project,
            includeDecisions,
            includeTroubleshooting,
            cancellationToken: cancellationToken);

        return new NeroGetProjectContextToolResult
        {
            Project = result.Project,
            Exists = result.Exists,
            Index = ToNullableToolResult(result.Index),
            Context = ToNullableToolResult(result.Context),
            Patterns = ToNullableToolResult(result.Patterns),
            BusinessRules = ToNullableToolResult(result.BusinessRules),
            Decisions = result.Decisions.Select(ToToolResult).ToList(),
            ActiveDecisions = result.ActiveDecisions.Select(ToToolResult).ToList(),
            SupersededDecisions = result.SupersededDecisions.Select(ToToolResult).ToList(),
            HasSupersededDecisions = result.HasSupersededDecisions,
            Recommendation = result.HasSupersededDecisions
                ? "Use activeDecisions as current guidance. supersededDecisions and supersededBy are historical; superseded decisions are omitted from decisions."
                : null,
            Troubleshooting = result.Troubleshooting.Select(ToToolResult).ToList()
        };
    }

    [McpServerTool]
    [Description("Gets grouped Nero knowledge context for a domain from the indexed SQLite knowledge base.")]
    public async Task<NeroGetDomainContextToolResult> nero_get_domain_context(
        [Description("Domain name, for example api, mobile, front or integracoes.")]
        string domain,
        [Description("Whether to include projects linked to this domain by belongs_to_domain knowledge edges.")]
        bool includeProjects = true,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var result = await domainContextService.GetDomainContextAsync(
            connection,
            domain,
            includeProjects,
            cancellationToken);

        return new NeroGetDomainContextToolResult
        {
            Domain = result.Domain,
            Exists = result.Exists,
            Index = ToNullableToolResult(result.Index),
            Patterns = ToNullableToolResult(result.Patterns),
            BusinessRules = ToNullableToolResult(result.BusinessRules),
            ValidationAndTests = ToNullableToolResult(result.ValidationAndTests),
            Projects = result.Projects.Select(ToToolResult).ToList()
        };
    }

    [McpServerTool]
    [Description("Finds related Nero knowledge using direct graph edges, common domains and sibling projects.")]
    public async Task<IReadOnlyList<NeroRelatedKnowledgeToolResult>> nero_find_related_knowledge(
        [Description("Optional project filter, for example Acme.Receiving.Api.")]
        string? project = null,
        [Description("Optional topic used to seed the graph query with indexed full-text search.")]
        string? topic = null,
        [Description("Optional relation type filter, for example related_pattern or BelongsToDomain.")]
        IReadOnlyList<string>? relationTypes = null,
        [Description("Graph expansion depth for direct edges.")]
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parsedRelationTypes = ParseRelationTypes(relationTypes);
        var results = await relatedKnowledgeService.FindRelatedAsync(
            connection,
            project,
            topic,
            parsedRelationTypes,
            depth,
            cancellationToken);

        return results.Select(ToToolResult).ToList();
    }

    [McpServerTool]
    [Description("Registers the minimal Nero project knowledge structure, creating project index/context Markdown when missing. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterProjectToolResult> nero_register_project(
        [Description("Project name, for example Acme.Auth.Api.")]
        string projeto,
        [Description("Primary project domain slug. Must be an active domain under knowledge/domains (status missing or active).")]
        string dominio,
        [Description("Short project purpose.")]
        string proposito,
        [Description("Origin of the project registration, for example user request, repository review or ticket.")]
        string origem,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await projectWriterService.WriteAsync(
                knowledgeRootPath,
                new RegisterProjectRequest
                {
                    Project = projeto,
                    Domain = dominio,
                    Purpose = proposito,
                    Origin = origem
                },
                cancellationToken);

            return new NeroRegisterProjectToolResult
            {
                Project = writeResult.Project,
                Domain = writeResult.Domain,
                Created = writeResult.Created,
                ProjectDirectoryPath = writeResult.ProjectDirectoryPath,
                ProjectRelativePath = writeResult.ProjectRelativePath,
                IndexPath = writeResult.IndexPath,
                ContextPath = writeResult.ContextPath,
                CreatedFiles = writeResult.CreatedFiles
            };
        }
        catch (Exception exception) when (ToolFailureDiagnostics.IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_project", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_project", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_project", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Rewrites projects/<Projeto>/index.md from structured fields (hybrid template). Requires existing index.md; bootstrap with nero_register_project. Does not reindex; call nero_admin_reindex after the write batch.")]
    public async Task<NeroUpdateProjectFileToolResult> nero_update_project_index(
        [Description("Project name, for example Acme.Auth.Api.")]
        string projeto,
        [Description("Primary project domain slug. Must be an active domain under knowledge/domains (status missing or active).")]
        string dominio,
        [Description("Short project purpose written into the index body.")]
        string proposito,
        [Description("Known project knowledge files to list under Arquivos, for example context.md and inventory.md.")]
        IReadOnlyList<string> arquivos,
        [Description("Optional origin note for the update.")]
        string? origem = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await projectUpdateWriterService.UpdateIndexAsync(
                knowledgeRootPath,
                new UpdateProjectIndexRequest
                {
                    Project = projeto,
                    Domain = dominio,
                    Purpose = proposito,
                    Arquivos = arquivos,
                    Origin = origem
                },
                cancellationToken);

            return ToUpdateProjectFileToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_index", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_update_project_index", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_index", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Rewrites projects/<Projeto>/context.md from structured fields (hybrid template). Requires existing context.md and index.md; bootstrap with nero_register_project. Does not reindex; call nero_admin_reindex after the write batch.")]
    public async Task<NeroUpdateProjectFileToolResult> nero_update_project_context(
        [Description("Project name, for example Acme.Auth.Api.")]
        string projeto,
        [Description("Primary project domain slug. Must be an active domain under knowledge/domains (status missing or active).")]
        string dominio,
        [Description("Short project purpose.")]
        string proposito,
        [Description("Primary stack summary, for example ASP.NET Core + SQL Server.")]
        string stack,
        [Description("Product surface summary, for example API HTTP interna / BFF.")]
        string superficie,
        [Description("Consolidated operational context. Prefer short summaries; put long evidence in snapshots.")]
        string resumoOperacional,
        [Description("Optional operational skill pointer, for example $nero-auth-lib.")]
        string? skillOperacional = null,
        [Description("Optional origin note for the update.")]
        string? origem = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await projectUpdateWriterService.UpdateContextAsync(
                knowledgeRootPath,
                new UpdateProjectContextRequest
                {
                    Project = projeto,
                    Domain = dominio,
                    Purpose = proposito,
                    Stack = stack,
                    Superficie = superficie,
                    ResumoOperacional = resumoOperacional,
                    SkillOperacional = skillOperacional,
                    Origin = origem
                },
                cancellationToken);

            return ToUpdateProjectFileToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_context", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_update_project_context", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_context", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Creates or rewrites projects/<Projeto>/inventory.md from structured fields (upsert). Requires existing index.md. Does not reindex; call nero_admin_reindex after the write batch. Do not pass secrets, tokens or absolute local paths with credentials.")]
    public async Task<NeroUpdateProjectFileToolResult> nero_update_project_inventory(
        [Description("Project name, for example Acme.Auth.Api.")]
        string projeto,
        [Description("Primary project domain slug. Must be an active domain under knowledge/domains (status missing or active).")]
        string dominio,
        [Description("ISO review date (yyyy-MM-dd) written as reviewed_at.")]
        string revisadoEm,
        [Description("Classification summary for the inventory.")]
        string classificacao,
        [Description("Technical signals observed in the product checkout (solutions, packages, apps).")]
        IReadOnlyList<string> sinaisTecnicos,
        [Description("Optional git branch name.")]
        string? gitBranch = null,
        [Description("Optional short git HEAD.")]
        string? gitHead = null,
        [Description("Optional git remote URL without credentials.")]
        string? gitRemote = null,
        [Description("Optional origin note for the update.")]
        string? origem = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await projectUpdateWriterService.UpdateInventoryAsync(
                knowledgeRootPath,
                new UpdateProjectInventoryRequest
                {
                    Project = projeto,
                    Domain = dominio,
                    ReviewedAt = revisadoEm,
                    Classificacao = classificacao,
                    SinaisTecnicos = sinaisTecnicos,
                    GitBranch = gitBranch,
                    GitHead = gitHead,
                    GitRemote = gitRemote,
                    Origin = origem
                },
                cancellationToken);

            return ToUpdateProjectFileToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_inventory", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_update_project_inventory", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_project_inventory", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a new Nero knowledge domain by creating domains/<dominio>/index.md with status active. Fails if the domain already exists (active or inactive). Does not reindex; call nero_admin_reindex after the write batch.")]
    public async Task<NeroDomainWriteToolResult> nero_register_domain(
        [Description("Domain slug, for example billing. Must match ^[a-z][a-z0-9_-]{1,31}$.")]
        string dominio,
        [Description("Short domain purpose.")]
        string proposito,
        [Description("Origin of the domain registration.")]
        string origem,
        [Description("Optional display title, for example Domain API.")]
        string? titulo = null,
        [Description("Optional consolidated source summary under Fonte consolidada.")]
        string? fonteConsolidada = null,
        [Description("Optional list of main knowledge files (bullet text without leading dash).")]
        IReadOnlyList<string>? arquivos = null,
        [Description("Optional quick-read rules body.")]
        string? regrasLeitura = null,
        [Description("Optional projects this domain is source_for (Acme.X.API or projects/Acme.X.API).")]
        IReadOnlyList<string>? sourceFor = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await domainWriterService.RegisterAsync(
                knowledgeRootPath,
                new RegisterDomainRequest
                {
                    Domain = dominio,
                    Purpose = proposito,
                    Origin = origem,
                    Titulo = titulo,
                    FonteConsolidada = fonteConsolidada,
                    Arquivos = arquivos,
                    RegrasLeitura = regrasLeitura,
                    SourceFor = sourceFor
                },
                cancellationToken);

            return ToDomainWriteToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_domain", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_domain", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_domain", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Rewrites domains/<dominio>/index.md from structured fields (titulo, proposito, fonteConsolidada, arquivos, regras, sourceFor). Omitting sourceFor preserves existing source_for links; pass an explicit list to replace (empty clears). Inactive domains require reativar=true. Never sets status inactive. Does not reindex.")]
    public async Task<NeroDomainWriteToolResult> nero_update_domain(
        [Description("Domain slug.")]
        string dominio,
        [Description("Short domain purpose.")]
        string proposito,
        [Description("Main knowledge files listed under Arquivos principais (bullet text without leading dash).")]
        IReadOnlyList<string> arquivos,
        [Description("Optional display title, for example Domain API.")]
        string? titulo = null,
        [Description("Optional consolidated source summary under Fonte consolidada.")]
        string? fonteConsolidada = null,
        [Description("Optional quick-read rules body.")]
        string? regrasLeitura = null,
        [Description("Optional origin note.")]
        string? origem = null,
        [Description("Optional source_for projects. Omit to preserve existing links; pass the full desired list to replace; pass [] to clear all intentionally.")]
        IReadOnlyList<string>? sourceFor = null,
        [Description("When true, reactivates an inactive domain and rewrites the index. Required if the domain is inactive.")]
        bool reativar = false,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await domainWriterService.UpdateAsync(
                knowledgeRootPath,
                new UpdateDomainRequest
                {
                    Domain = dominio,
                    Purpose = proposito,
                    Arquivos = arquivos,
                    Titulo = titulo,
                    FonteConsolidada = fonteConsolidada,
                    RegrasLeitura = regrasLeitura,
                    Origin = origem,
                    SourceFor = sourceFor,
                    Reativar = reativar
                },
                cancellationToken);

            return ToDomainWriteToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_domain", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_update_domain", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_update_domain", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Soft-inactivates a Nero knowledge domain by setting status: inactive on index.md. Always requires motivo and origem. If projects still link via belongs_to_domain, also require confirmacao=INACTIVATE_WITH_LINKED_PROJECTS and evidencia. Does not delete the folder. Does not reindex.")]
    public async Task<NeroDomainWriteToolResult> nero_inactivate_domain(
        [Description("Domain slug.")]
        string dominio,
        [Description("Reason for inactivation.")]
        string motivo,
        [Description("Origin of the inactivation request.")]
        string origem,
        [Description("Required when linked projects exist: exactly INACTIVATE_WITH_LINKED_PROJECTS.")]
        string? confirmacao = null,
        [Description("Evidence required together with confirmacao when linked projects exist.")]
        string? evidencia = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await domainWriterService.InactivateAsync(
                knowledgeRootPath,
                new InactivateDomainRequest
                {
                    Domain = dominio,
                    Motivo = motivo,
                    Origin = origem,
                    Confirmacao = confirmacao,
                    Evidencia = evidencia
                },
                cancellationToken);

            return ToDomainWriteToolResult(writeResult);
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_inactivate_domain", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_inactivate_domain", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_inactivate_domain", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled Nero business rule Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterBusinessRuleToolResult> nero_register_business_rule(
        [Description("Business rule title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Business rule content.")]
        string regra,
        [Description("Evidence supporting the rule.")]
        string evidencia,
        [Description("Origin of the rule, for example ticket, PR, incident or user request.")]
        string origem,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterBusinessRuleRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Rule = regra,
                Evidence = evidencia,
                Origin = origem
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await businessRuleWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterBusinessRuleToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_business_rule", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_business_rule", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_business_rule", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled Nero technical decision Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterDecisionToolResult> nero_register_decision(
        [Description("Decision title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Problem that motivated the decision.")]
        string problema,
        [Description("Options considered before the decision.")]
        string opcoes,
        [Description("Chosen decision.")]
        string decisao,
        [Description("Known consequences of the decision.")]
        string consequencias,
        [Description("Origin of the decision, for example ticket, PR, incident or user request.")]
        string origem,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        [Description("Optional decision targets superseded by this decision, as node ids or logical knowledge paths. Creates supersedes edges (decision-only special relation; not remapped to updates).")]
        IReadOnlyList<string>? supersedes = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterDecisionRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Problem = problema,
                Options = opcoes,
                Decision = decisao,
                Consequences = consequencias,
                Origin = origem,
                Supersedes = supersedes
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await decisionWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterDecisionToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_decision", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_decision", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_decision", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled reusable Nero implementation pattern Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterPatternToolResult> nero_register_pattern(
        [Description("Pattern title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Context where this pattern solves a recurring problem.")]
        string contexto,
        [Description("Reusable implementation pattern.")]
        string padrao,
        [Description("When this pattern should be applied.")]
        string quandoAplicar,
        [Description("When this pattern should not be applied.")]
        string quandoNaoAplicar,
        [Description("Origin of the pattern, for example ticket, PR, incident or user request.")]
        string origem,
        [Description("Optional exceptions or caveats for the pattern.")]
        string? excecoes = null,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        [Description("Optional short examples.")]
        IReadOnlyList<string>? exemplos = null,
        [Description("Optional node ids or logical knowledge paths that already use this pattern. Creates source_for edges.")]
        IReadOnlyList<string>? usadoPor = null,
        [Description("Optional node ids or logical knowledge paths that are reuse candidates for this pattern. Creates documents, related_pattern or related_decision edges based on the target path.")]
        IReadOnlyList<string>? candidatoParaReuso = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterPatternRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Context = contexto,
                Pattern = padrao,
                WhenToApply = quandoAplicar,
                WhenNotToApply = quandoNaoAplicar,
                Exceptions = excecoes,
                Examples = exemplos,
                Origin = origem,
                UsedBy = usadoPor,
                CandidateForReuse = candidatoParaReuso
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await patternWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterPatternToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_pattern", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_pattern", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_pattern", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled reusable Nero validation or test rule Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterValidationRuleToolResult> nero_register_validation_rule(
        [Description("Validation or test rule title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Validation or test rule content.")]
        string regra,
        [Description("Acceptance criterion or expected behavior protected by the validation.")]
        string criterio,
        [Description("Minimum evidence expected for the validation or test.")]
        string evidencia,
        [Description("Origin of the validation rule, for example ticket, PR, incident or user request.")]
        string origem,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        [Description("Optional known gaps or uncovered scenarios.")]
        string? lacunasConhecidas = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterValidationRuleRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Rule = regra,
                Criteria = criterio,
                Evidence = evidencia,
                KnownGaps = lacunasConhecidas,
                Origin = origem
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await validationRuleWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterValidationRuleToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_validation_rule", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_validation_rule", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_validation_rule", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled Nero snapshot Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterSnapshotToolResult> nero_register_snapshot(
        [Description("Snapshot title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Context captured by the snapshot.")]
        string contexto,
        [Description("Evidence captured or summarized by the snapshot.")]
        string evidencia,
        [Description("Origin of the snapshot, for example repository review, command output, incident, ticket or user request.")]
        string origem,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        [Description("Optional node ids or logical knowledge paths related to this snapshot. Creates documents edges.")]
        IReadOnlyList<string>? relacionadoA = null,
        [Description("Optional node ids or logical knowledge paths evidenced by this snapshot. Creates evidences edges.")]
        IReadOnlyList<string>? evidenciaDe = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterSnapshotRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Context = contexto,
                Evidence = evidencia,
                Origin = origem,
                RelatesTo = relacionadoA,
                Evidences = evidenciaDe
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await snapshotWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterSnapshotToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_snapshot", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_snapshot", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_snapshot", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Registers a controlled Nero troubleshooting Markdown note. Does not reindex; the client must call nero_admin_reindex after finishing writes.")]
    public async Task<NeroRegisterTroubleshootingToolResult> nero_register_troubleshooting(
        [Description("Troubleshooting title.")]
        string titulo,
        [Description("Knowledge scope: global, domain or project.")]
        string escopo,
        [Description("Observable symptom.")]
        string sintoma,
        [Description("Confirmed or strongest known cause.")]
        string causa,
        [Description("Action taken or recommended.")]
        string acao,
        [Description("Evidence supporting the diagnosis.")]
        string evidencia,
        [Description("Operational or user impact.")]
        string impacto,
        [Description("Origin of the troubleshooting note, for example incident, ticket, PR or user request.")]
        string origem,
        [Description("Domain name when escopo is domain, for example api, mobile, front or integracoes.")]
        string? dominio = null,
        [Description("Project name when escopo is project, for example Acme.Api.")]
        string? projeto = null,
        [Description("Optional correction or mitigation.")]
        string? solucao = null,
        [Description("Optional recurrence prevention.")]
        string? prevencao = null,
        [Description("Optional node ids or logical knowledge paths that caused this troubleshooting note. Creates related_decision edges when the target path contains decisions; otherwise documents.")]
        IReadOnlyList<string>? causadoPor = null,
        [Description("Optional node ids or logical knowledge paths related to this troubleshooting note. Creates related_pattern or related_decision when the target path is reliable; otherwise documents.")]
        IReadOnlyList<string>? relacionadoA = null,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var request = new RegisterTroubleshootingRequest
            {
                Title = titulo,
                Scope = ParseScope(escopo),
                Domain = string.IsNullOrWhiteSpace(dominio) ? null : dominio,
                Project = string.IsNullOrWhiteSpace(projeto) ? null : projeto,
                Symptom = sintoma,
                Cause = causa,
                Action = acao,
                Evidence = evidencia,
                Impact = impacto,
                Origin = origem,
                Solution = solucao,
                Prevention = prevencao,
                CausedBy = causadoPor,
                RelatesTo = relacionadoA
            };

            var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
            var writeResult = await troubleshootingWriterService.WriteAsync(
                knowledgeRootPath,
                request,
                cancellationToken);

            return new NeroRegisterTroubleshootingToolResult
            {
                Title = writeResult.Title,
                Path = writeResult.Path,
                RelativePath = writeResult.RelativePath
            };
        }
        catch (Exception exception) when (IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_troubleshooting", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_register_troubleshooting", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_register_troubleshooting", exception, startedTimestamp);
            throw;
        }
    }

    [McpServerTool]
    [Description("Creates an idempotent manual knowledge edge between two indexed Nero knowledge nodes. The Markdown frontmatter is not changed.")]
    public async Task<NeroLinkKnowledgeToolResult> nero_link_knowledge(
        [Description("Source node id or logical knowledge path.")]
        string source,
        [Description("Target node id or logical knowledge path.")]
        string target,
        [Description("Relation type, for example belongs_to_domain, related_pattern, documents or evidences.")]
        string relation,
        [Description("Confidence between 0 and 1.")]
        decimal confidence = 1m,
        [Description("Evidence for the manual relationship.")]
        string evidence = "",
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await using var connection = connectionFactory.CreateConnection();
            var result = await knowledgeLinkService.LinkAsync(
                connection,
                new RegisterKnowledgeLinkRequest
                {
                    Source = source,
                    Target = target,
                    Relation = relation,
                    Confidence = confidence,
                    Evidence = evidence
                },
                cancellationToken);

            return new NeroLinkKnowledgeToolResult
            {
                EdgeId = result.EdgeId,
                SourceNodeId = result.SourceNodeId,
                TargetNodeId = result.TargetNodeId,
                Relation = result.Relation,
                Created = result.Created
            };
        }
        catch (Exception exception) when (ToolFailureDiagnostics.IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_link_knowledge", exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException("nero_link_knowledge", exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, "nero_link_knowledge", exception, startedTimestamp);
            throw;
        }
    }

    private NeroProjectContextSectionToolResult? ToNullableToolResult(KnowledgeProjectContextSection? section)
    {
        return section is null
            ? null
            : ToToolResult(section);
    }

    private NeroProjectContextSectionToolResult ToToolResult(KnowledgeProjectContextSection section)
    {
        var dataClass = TryResolveDataClass(section.Path);
        return new NeroProjectContextSectionToolResult
        {
            Id = section.Id,
            Title = ComplianceReadRedactor.Redact(section.Title, dataClass),
            Path = section.Path,
            Type = section.Type.ToString(),
            Content = ComplianceReadRedactor.Redact(section.Content, dataClass)
        };
    }

    private NeroSupersededDecisionToolResult ToToolResult(KnowledgeSupersededDecision decision)
    {
        return new NeroSupersededDecisionToolResult
        {
            Decision = ToToolResult(decision.Decision),
            SupersededBy = decision.SupersededBy.Select(ToToolResult).ToList()
        };
    }

    private NeroDomainContextSectionToolResult? ToNullableToolResult(KnowledgeDomainContextSection? section)
    {
        return section is null
            ? null
            : ToToolResult(section);
    }

    private NeroDomainContextSectionToolResult ToToolResult(KnowledgeDomainContextSection section)
    {
        var dataClass = TryResolveDataClass(section.Path);
        return new NeroDomainContextSectionToolResult
        {
            Id = section.Id,
            Title = ComplianceReadRedactor.Redact(section.Title, dataClass),
            Path = section.Path,
            Type = section.Type.ToString(),
            Content = ComplianceReadRedactor.Redact(section.Content, dataClass)
        };
    }

    private static NeroDomainProjectToolResult ToToolResult(KnowledgeDomainProjectSummary project)
    {
        return new NeroDomainProjectToolResult
        {
            Id = project.Id,
            Title = project.Title,
            Path = project.Path,
            Project = project.Project
        };
    }

    private NeroRelatedKnowledgeToolResult ToToolResult(RelatedKnowledgeNodeResult result)
    {
        var dataClass = TryResolveDataClass(result.Path);
        return new NeroRelatedKnowledgeToolResult
        {
            Id = result.Id,
            Title = ComplianceReadRedactor.Redact(result.Title, dataClass),
            Path = result.Path,
            Scope = result.Scope.ToString(),
            Type = result.Type.ToString(),
            Domain = result.Domain,
            Project = result.Project,
            Relation = result.Relation.ToString(),
            Evidence = ComplianceReadRedactor.Redact(result.Evidence, dataClass),
            Score = result.Score
        };
    }

    private static NeroDomainWriteToolResult ToDomainWriteToolResult(DomainWriteResult writeResult)
    {
        return new NeroDomainWriteToolResult
        {
            Domain = writeResult.Domain,
            Status = writeResult.Status,
            Path = writeResult.Path,
            RelativePath = writeResult.RelativePath,
            Action = writeResult.Action,
            Created = writeResult.Created,
            LinkedProjects = writeResult.LinkedProjects
        };
    }

    private static NeroUpdateProjectFileToolResult ToUpdateProjectFileToolResult(UpdateProjectFileResult writeResult)
    {
        return new NeroUpdateProjectFileToolResult
        {
            Project = writeResult.Project,
            Domain = writeResult.Domain,
            FileKind = writeResult.FileKind,
            Path = writeResult.Path,
            RelativePath = writeResult.RelativePath,
            Created = writeResult.Created
        };
    }

    private NeroSearchKnowledgeToolResult ToSearchToolResult(KnowledgeSearchResult result)
    {
        var dataClass = TryResolveDataClass(result.Path);
        return new NeroSearchKnowledgeToolResult
        {
            Id = result.Id,
            Title = ComplianceReadRedactor.Redact(result.Title, dataClass),
            Path = result.Path,
            Scope = result.Scope.ToString(),
            Type = result.Type.ToString(),
            Domain = result.Domain,
            Project = result.Project,
            Snippet = ComplianceReadRedactor.Redact(result.Snippet, dataClass)
        };
    }

    private string? TryResolveDataClass(string? relativeOrLogicalPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrLogicalPath))
        {
            return ComplianceFrontmatter.DefaultDataClass;
        }

        try
        {
            var root = knowledgeRootOptions.ResolvePath();
            var normalized = relativeOrLogicalPath.Replace('\\', '/').TrimStart('/');
            if (normalized.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["knowledge/".Length..];
            }

            if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".md";
            }

            var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(fullPath))
            {
                return ComplianceFrontmatter.DefaultDataClass;
            }

            // Read only the opening frontmatter block (bounded).
            using var reader = new StreamReader(fullPath);
            var first = reader.ReadLine();
            if (first is not "---")
            {
                return ComplianceFrontmatter.DefaultDataClass;
            }

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line is "---")
                {
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.StartsWith("data_class:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = trimmed["data_class:".Length..].Trim().Trim('"', '\'');
                    return string.IsNullOrWhiteSpace(value)
                        ? ComplianceFrontmatter.DefaultDataClass
                        : value;
                }
            }
        }
        catch (IOException)
        {
            return ComplianceFrontmatter.DefaultDataClass;
        }
        catch (UnauthorizedAccessException)
        {
            return ComplianceFrontmatter.DefaultDataClass;
        }

        return ComplianceFrontmatter.DefaultDataClass;
    }

    private static IReadOnlyList<KnowledgeRelationType>? ParseRelationTypes(IReadOnlyList<string>? relationTypes)
    {
        if (relationTypes is null || relationTypes.Count == 0)
        {
            return null;
        }

        return relationTypes.Select(ParseRelationType).ToList();
    }

    private static KnowledgeRelationType ParseRelationType(string relationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationType);

        var normalized = relationType.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var candidate in Enum.GetValues<KnowledgeRelationType>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"Unsupported knowledge relation type '{relationType}'.", nameof(relationType));
    }

    private static bool IsActionableWriteException(Exception exception)
    {
        return ToolFailureDiagnostics.IsActionableWriteException(exception);
    }

    private static KnowledgeScope ParseScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        foreach (var candidate in Enum.GetValues<KnowledgeScope>())
        {
            if (string.Equals(candidate.ToString(), scope, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"Unsupported knowledge scope '{scope}'. Use global, domain or project.", nameof(scope));
    }
}
