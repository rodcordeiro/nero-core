using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Services.Patterns;
using Nero.Knowledge.Base.Mcp.Application.Services.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Services.ValidationRules;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Graph;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Graph;
using Nero.Knowledge.Base.Mcp.Application.Services.Links;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Search;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Search;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;
using Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

namespace Nero.Knowledge.Base.Tests;

public class NeroKnowledgeToolsTests
{
    [Fact]
    public async Task NeroSearchKnowledge_ReturnsToolOutputFromIndexedDatabase()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Inventory API\n\nWebhook material.");
        var databasePath = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge.db");
        var connectionFactory = new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions
        {
            Path = databasePath
        });
        await using (var connection = connectionFactory.CreateConnection())
        {
            await new KnowledgeIndexer().ReindexAsync(connection, root);
        }
        var tools = new NeroKnowledgeTools(
            connectionFactory,
            new KnowledgeRootOptions { Path = root },
            new BusinessRuleWriterService(),
            new DecisionWriterService(),
            new PatternWriterService(),
            new ProjectWriterService(),
            new ProjectUpdateWriterService(),
            new DomainWriterService(),
            new SnapshotWriterService(),
            new TroubleshootingWriterService(),
            new ValidationRuleWriterService(),
            new KnowledgeDomainContextService(),
            new KnowledgeProjectContextService(),
            new RelatedKnowledgeService(),
            new KnowledgeLinkService(),
            new KnowledgeSearchService());

        var results = await tools.nero_search_knowledge(
            query: "webhook",
            project: "Acme.Api",
            limit: 5);

        var result = Assert.Single(results);
        Assert.Equal("projects/Acme.Api/context", result.Id);
        Assert.Equal("Inventory API", result.Title);
        Assert.Equal("knowledge/projects/Acme.Api/context.md", result.Path);
        Assert.Equal("Project", result.Scope);
        Assert.Equal("ProjectContext", result.Type);
        Assert.Equal("Acme.Api", result.Project);
        Assert.Contains("Webhook", result.Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NeroLinkKnowledge_CreatesManualEdgeAndFindsItLater()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nProjeto API.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_link_knowledge(
            source: "projects/Acme.Api/index",
            target: "domains/api",
            relation: "belongs_to_domain",
            confidence: 0.95m,
            evidence: "Relacionamento manual confirmado em teste.");

        Assert.True(result.Created);
        Assert.Equal("projects/Acme.Api/index", result.SourceNodeId);
        Assert.Equal("domains/api/index", result.TargetNodeId);
        Assert.Equal("BelongsToDomain", result.Relation);

        var duplicate = await tools.Tools.nero_link_knowledge(
            source: "knowledge/projects/Acme.Api/index.md",
            target: "knowledge/domains/api/index.md",
            relation: "belongs_to_domain",
            evidence: "Relacionamento manual confirmado em teste.");
        Assert.False(duplicate.Created);
        Assert.Equal(result.EdgeId, duplicate.EdgeId);

        var related = await tools.Tools.nero_find_related_knowledge(
            project: "Acme.Api",
            topic: "API",
            relationTypes: ["belongs_to_domain"]);
        Assert.Contains(related, item => item.Title == "API" && item.Relation == "BelongsToDomain");
    }

    [Fact]
    public async Task NeroLinkKnowledge_WhenDatabaseIsLocked_ReturnsActionableSqliteBusyError()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nProjeto API.");
        var tools = await CreateToolsAsync(root, busyTimeoutMilliseconds: 100);
        await using var lockConnection = tools.ConnectionFactory.CreateConnection();
        await lockConnection.OpenAsync();
        await using var lockCommand = lockConnection.CreateCommand();
        lockCommand.CommandText = "BEGIN EXCLUSIVE;";
        await lockCommand.ExecuteNonQueryAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => tools.Tools.nero_link_knowledge(
                source: "projects/Acme.Api/index",
                target: "domains/api",
                relation: "belongs_to_domain"));

        Assert.Contains("Category: SqliteBusy.", exception.Message);
        Assert.Contains("Serialize reindex", exception.Message);
    }

    [Fact]
    public async Task NeroRegisterSnapshot_WritesMarkdownAndCreatesEdgesAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "domains/api/patterns/http-versioning.md", """
            ---
            type: pattern
            scope: domain
            domain: api
            links:
              - type: documents
                target: domains/api/index
            ---
            # HTTP versioning

            Padrao concreto de versionamento HTTP.
            """);
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_snapshot(
            titulo: "Snapshot de rotas",
            escopo: "project",
            projeto: "Acme.Api",
            contexto: "Inventario tecnico das rotas publicas revisadas.",
            evidencia: "Arquivos de controller e contratos analisados no checkout local.",
            origem: "Sprint 16.1",
            relacionadoA: ["projects/Acme.Api/index"],
            evidenciaDe: ["domains/api/patterns/http-versioning"]);

        Assert.Equal("Snapshot de rotas", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.StartsWith("projects/Acme.Api/snapshots/", result.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith("-snapshot-de-rotas.md", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Path));

        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: snapshot", markdown);
        Assert.Contains("type: documents", markdown);
        Assert.Contains("target: \"projects/Acme.Api/index\"", markdown);
        Assert.Contains("type: evidences", markdown);
        Assert.Contains("target: \"domains/api/patterns/http-versioning\"", markdown);

        await tools.ReindexAsync();

        var related = await tools.Tools.nero_find_related_knowledge(
            project: "Acme.Api",
            topic: "rotas",
            relationTypes: ["evidences"]);
        var evidenced = Assert.Single(related, item => item.Title == "HTTP versioning");
        Assert.Equal("Evidences", evidenced.Relation);
    }

    [Fact]
    public async Task NeroRegisterSnapshot_RejectsEvidenciaDeHub()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tools.Tools.nero_register_snapshot(
            titulo: "Snapshot hub blocked",
            escopo: "project",
            projeto: "Acme.Api",
            contexto: "Tentativa de evidenciaDe em hub.",
            evidencia: "Nao deve gravar Markdown.",
            origem: "NeroKnowledgeToolsTests",
            relacionadoA: ["projects/Acme.Api/index"],
            evidenciaDe: ["domains/api/patterns"]));

        Assert.Contains("InvalidInput", exception.Message, StringComparison.Ordinal);
        Assert.Contains("directory hub", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("domains/api/patterns", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(
                Path.Combine(root, "projects", "Acme.Api"),
                "*.md",
                SearchOption.AllDirectories),
            path => path.Contains("snapshots", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NeroRegisterSnapshot_RejectsOversizedEvidenceWithActionableError()
    {
        var root = CreateTempKnowledgeRoot();
        var tools = await CreateToolsAsync(root);
        var oversizedEvidence = new string('x', SnapshotWriterService.MaximumLongFieldSizeBytes + 1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => tools.Tools.nero_register_snapshot(
                titulo: "Snapshot payload oversized",
                escopo: "global",
                contexto: "Contexto valido.",
                evidencia: oversizedEvidence,
                origem: "NeroKnowledgeToolsTests"));

        Assert.Contains("Category: InvalidInput.", exception.Message);
        Assert.Contains("Field: Evidence.", exception.Message);
        Assert.Contains("64 KiB", exception.Message);
        Assert.Contains("MarkdownWritten: false.", exception.Message);
        Assert.Empty(Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task NeroRegisterTroubleshooting_WritesMarkdownAndIsSearchableAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_troubleshooting(
            titulo: "Falha ao sincronizar estoque",
            escopo: "project",
            projeto: "Acme.Api",
            sintoma: "Sincronizacao retorna timeout ao processar estoque.",
            causa: "Servico externo ficou indisponivel durante a janela de retry.",
            acao: "Reprocessar mensagens pendentes apos estabilizacao do servico.",
            evidencia: "Logs resumidos mostram timeout HTTP 504 na integracao.",
            impacto: "Estoque pode ficar defasado ate o reprocessamento.",
            origem: "Sprint 10.2",
            solucao: "Executar reprocessamento controlado da fila afetada.",
            prevencao: "Monitorar latencia e configurar alerta para aumento de retries.",
            causadoPor: ["domains/api/index"],
            relacionadoA: ["projects/Acme.Api/index"]);

        Assert.Equal("Falha ao sincronizar estoque", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.StartsWith("projects/Acme.Api/troubleshooting/", result.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith("-falha-ao-sincronizar-estoque.md", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Path));

        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: troubleshooting", markdown);
        Assert.Contains("type: documents", markdown);
        Assert.Contains("target: \"domains/api/index\"", markdown);
        Assert.Contains("target: \"projects/Acme.Api/index\"", markdown);
        Assert.DoesNotContain("type: caused_by", markdown);
        Assert.DoesNotContain("type: relates_to", markdown);

        await tools.ReindexAsync();

        var related = await tools.Tools.nero_find_related_knowledge(
            project: "Acme.Api",
            topic: "timeout",
            relationTypes: ["documents"]);
        var documented = Assert.Single(related, item => item.Title == "API");
        Assert.Equal("Documents", documented.Relation);

        var projectContext = await tools.Tools.nero_get_project_context(
            project: "Acme.Api",
            includeDecisions: false,
            includeTroubleshooting: true);
        Assert.Contains(projectContext.Troubleshooting, item => item.Title == "Falha ao sincronizar estoque");
    }

    [Fact]
    public async Task NeroRegisterValidationRule_WritesMarkdownAndIsSearchableAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_validation_rule(
            titulo: "Validar estoque disponivel",
            escopo: "domain",
            dominio: "api",
            regra: "Proteger o fluxo contra pedido sem saldo suficiente.",
            criterio: "Dado produto sem saldo, a validacao deve recusar o pedido antes da persistencia.",
            evidencia: "Teste automatizado cobrindo produto sem saldo com mensagem acionavel.",
            origem: "Sprint 9.2",
            lacunasConhecidas: "Cenarios de concorrencia exigem teste integrado.");

        Assert.Equal("Validar estoque disponivel", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.Equal("domains/api/validation-and-tests/validar-estoque-disponivel.md", result.RelativePath);
        Assert.True(File.Exists(result.Path));

        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: validation_rule", markdown);
        Assert.Contains("## Criterio", markdown);
        Assert.Contains("Teste automatizado cobrindo produto sem saldo", markdown);

        await tools.ReindexAsync();

        var searchResults = await tools.Tools.nero_search_knowledge(
            query: "estoque",
            domain: "api");
        Assert.Contains(searchResults, item => item.Title == "Validar estoque disponivel");
    }

    [Fact]
    public async Task NeroRegisterPattern_WritesMarkdownAndCreatesEdgesAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_pattern(
            titulo: "Cache por chave de negocio",
            escopo: "domain",
            dominio: "api",
            contexto: "Consultas repetidas sobre dados pouco volateis geram custo desnecessario.",
            padrao: "Centralizar cache por chave de negocio com invalidacao explicita.",
            quandoAplicar: "Aplicar em consultas idempotentes e com baixa volatilidade.",
            quandoNaoAplicar: "Nao aplicar em dados transacionais que exigem leitura estritamente atualizada.",
            excecoes: "Usar TTL curto quando invalidacao explicita nao estiver disponivel.",
            exemplos: ["Cachear consulta de produtos por codigo."],
            origem: "Sprint 8.2",
            usadoPor: ["projects/Acme.Api/index"],
            candidatoParaReuso: ["domains/api/index"]);

        Assert.Equal("Cache por chave de negocio", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.Equal("domains/api/patterns/cache-por-chave-de-negocio.md", result.RelativePath);
        Assert.True(File.Exists(result.Path));

        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: pattern", markdown);
        Assert.Contains("type: source_for", markdown);
        Assert.Contains("target: \"projects/Acme.Api/index\"", markdown);
        Assert.Contains("type: documents", markdown);
        Assert.Contains("target: \"domains/api/index\"", markdown);
        Assert.DoesNotContain("type: used_by", markdown);
        Assert.DoesNotContain("type: candidate_for_reuse", markdown);

        await tools.ReindexAsync();

        var related = await tools.Tools.nero_find_related_knowledge(
            topic: "cache",
            relationTypes: ["source_for"]);
        var sourceFor = Assert.Single(related, item => item.Title == "Inventory API");
        Assert.Equal("SourceFor", sourceFor.Relation);

        var searchResults = await tools.Tools.nero_search_knowledge(
            query: "cache",
            domain: "api");
        Assert.Contains(searchResults, item => item.Title == "Cache por chave de negocio");
    }

    [Fact]
    public async Task NeroGetProjectContext_ReturnsGroupedProjectOutput()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/context.md", "# Contexto\n\nFluxo principal.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/patterns.md", "# Padroes\n\nPadrao local.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/business-rules.md", "# Regras\n\nRegra local.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-02-decisao.md", "# Decisao\n\nOpcao definida.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/troubleshooting/2026-07-03-ajuste.md", "# Ajuste\n\nCorrecao.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_project_context("Acme.Api");

        Assert.True(result.Exists);
        Assert.Equal("Acme.Api", result.Project);
        Assert.Equal("Inventory API", result.Index?.Title);
        Assert.Equal("Contexto", result.Context?.Title);
        Assert.Equal("Padroes", result.Patterns?.Title);
        Assert.Equal("Regras", result.BusinessRules?.Title);
        Assert.Equal("ProjectContext", result.Context?.Type);
        Assert.Contains("Fluxo principal", result.Context?.Content);
        Assert.Equal("Decisao", Assert.Single(result.Decisions).Title);
        Assert.Equal("Ajuste", Assert.Single(result.Troubleshooting).Title);
    }

    [Fact]
    public async Task NeroGetProjectContext_ReturnsActiveAndSupersededDecisionOutput()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-01-antiga.md", "# Decisao antiga\n\nAntiga.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-02-nova.md", """
            ---
            links:
              - type: supersedes
                target: projects/Acme.Api/decisions/2026-07-01-antiga
            ---
            # Decisao nova

            Vigente.
            """);
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_project_context("Acme.Api");

        Assert.Equal("Decisao nova", Assert.Single(result.ActiveDecisions).Title);
        var superseded = Assert.Single(result.SupersededDecisions);
        Assert.Equal("Decisao antiga", superseded.Decision.Title);
        Assert.Equal("Decisao nova", Assert.Single(superseded.SupersededBy).Title);
        Assert.Equal(["Decisao nova"], result.Decisions.Select(decision => decision.Title));
        Assert.True(result.HasSupersededDecisions);
        Assert.Contains("activeDecisions", result.Recommendation);
    }

    [Fact]
    public async Task NeroGetProjectContext_RespectsInclusionFlags()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/decisions/2026-07-02-decisao.md", "# Decisao\n\nOpcao definida.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/troubleshooting/2026-07-03-ajuste.md", "# Ajuste\n\nCorrecao.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_project_context(
            project: "Acme.Api",
            includeDecisions: false,
            includeTroubleshooting: false);

        Assert.True(result.Exists);
        Assert.Empty(result.Decisions);
        Assert.Empty(result.Troubleshooting);
    }

    [Fact]
    public async Task NeroGetDomainContext_ReturnsGroupedDomainOutputWithProjects()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice do dominio.");
        await WriteMarkdownAsync(root, "domains/api/patterns.md", "# Padroes API\n\nPadrao do dominio.");
        await WriteMarkdownAsync(root, "domains/api/business-rules.md", "# Regras API\n\nRegra do dominio.");
        await WriteMarkdownAsync(root, "domains/api/validation-and-tests.md", "# Validacoes API\n\nCriterios.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Api/index.md",
            """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Inventory API

            Projeto API.
            """);
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_domain_context("api");

        Assert.True(result.Exists);
        Assert.Equal("api", result.Domain);
        Assert.Equal("API", result.Index?.Title);
        Assert.Equal("Padroes API", result.Patterns?.Title);
        Assert.Equal("Regras API", result.BusinessRules?.Title);
        Assert.Equal("Validacoes API", result.ValidationAndTests?.Title);
        Assert.Equal("ValidationRule", result.ValidationAndTests?.Type);
        var project = Assert.Single(result.Projects);
        Assert.Equal("Acme.Api", project.Project);
        Assert.Equal("projects/Acme.Api/index", project.Id);
    }

    [Fact]
    public async Task NeroGetDomainContext_RespectsIncludeProjectsFlag()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice do dominio.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Api/index.md",
            """
            ---
            links:
              - type: belongs_to_domain
                target: domains/api/index
            ---
            # Inventory API

            Projeto API.
            """);
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_domain_context(
            domain: "api",
            includeProjects: false);

        Assert.True(result.Exists);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task NeroGetDomainContext_ReturnsEmptyOutputForUnknownDomain()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice do dominio.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_domain_context("dados");

        Assert.False(result.Exists);
        Assert.Equal("dados", result.Domain);
        Assert.Null(result.Index);
        Assert.Null(result.Patterns);
        Assert.Null(result.BusinessRules);
        Assert.Null(result.ValidationAndTests);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task NeroGetProjectContext_ReturnsEmptyOutputForUnknownProject()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_get_project_context("Acme.Missing.Project");

        Assert.False(result.Exists);
        Assert.Equal("Acme.Missing.Project", result.Project);
        Assert.Null(result.Index);
        Assert.Null(result.Context);
        Assert.Null(result.Patterns);
        Assert.Null(result.BusinessRules);
        Assert.Empty(result.Decisions);
        Assert.Empty(result.Troubleshooting);
    }

    [Fact]
    public async Task NeroFindRelatedKnowledge_ReturnsGraphResults()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nDominio API.");
        await WriteMarkdownAsync(root, "domains/api/patterns.md", "# Padroes API\n\nPadrao de estoque.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Receiving.Api/context.md",
            """
            ---
            links:
              - type: related_pattern
                target: domains/api/patterns
            ---
            # Contexto Recebimento

            Estoque em recebimento.
            """);
        var tools = await CreateToolsAsync(root);

        var results = await tools.Tools.nero_find_related_knowledge(
            project: "Acme.Receiving.Api",
            topic: "estoque",
            relationTypes: ["related_pattern"],
            depth: 1);

        var result = Assert.Single(results, result => result.Id == "domains/api/patterns");
        Assert.Equal("RelatedPattern", result.Relation);
        Assert.Equal("Pattern", result.Type);
        Assert.Equal(1.0m, result.Score);
        Assert.Contains("frontmatter links", result.Evidence);
    }

    [Fact]
    public async Task NeroRegisterProject_CreatesMissingProjectBaseFiles()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Auth.Api/decisions/2026-07-22-decisao.md",
            "# Decisao existente\n\nPreservar.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Auth.Api/patterns/contexto-operacional.md",
            "# Padrao existente\n\nPreservar.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_project(
            projeto: "Acme.Auth.Api",
            dominio: "api",
            proposito: "API de autenticacao da Acme.",
            origem: "Teste automatizado");

        Assert.True(result.Created);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.Equal("Acme.Auth.Api", result.Project);
        Assert.Equal("api", result.Domain);
        Assert.Equal("projects/Acme.Auth.Api", result.ProjectRelativePath);
        Assert.Equal(2, result.CreatedFiles.Count);
        Assert.True(File.Exists(Path.Combine(root, "projects", "Acme.Auth.Api", "index.md")));
        Assert.True(File.Exists(Path.Combine(root, "projects", "Acme.Auth.Api", "context.md")));
        Assert.True(File.Exists(Path.Combine(root, "projects", "Acme.Auth.Api", "decisions", "2026-07-22-decisao.md")));
        Assert.True(File.Exists(Path.Combine(root, "projects", "Acme.Auth.Api", "patterns", "contexto-operacional.md")));

        await tools.ReindexAsync();

        var projectContext = await tools.Tools.nero_get_project_context(
            project: "Acme.Auth.Api",
            includeDecisions: true,
            includeTroubleshooting: false);
        Assert.True(projectContext.Exists);
        Assert.Equal("Acme.Auth.Api", projectContext.Index?.Title);
        Assert.Equal("Acme.Auth.Api", projectContext.Context?.Title);
        Assert.Contains(projectContext.Decisions, item => item.Title == "Decisao existente");
    }

    [Fact]
    public async Task NeroRegisterProject_IsIdempotentWhenBaseFilesAlreadyExist()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/front/index.md", "# Front\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Admin.Web/index.md", "# Admin\n\nIndice existente.");
        await WriteMarkdownAsync(root, "projects/Acme.Admin.Web/context.md", "# Admin Context\n\nContexto existente.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_project(
            projeto: "Acme.Admin.Web",
            dominio: "front",
            proposito: "Frontend administrativo da Acme.",
            origem: "Teste automatizado");

        Assert.False(result.Created);
        Assert.Empty(result.CreatedFiles);
        var indexMarkdown = await File.ReadAllTextAsync(Path.Combine(root, "projects", "Acme.Admin.Web", "index.md"));
        Assert.Contains("Indice existente", indexMarkdown);
    }

    [Fact]
    public async Task NeroUpdateProjectTools_RewriteIndexContextAndUpsertInventory()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        await tools.Tools.nero_register_project(
            projeto: "Acme.UpdateTools.API",
            dominio: "api",
            proposito: "Bootstrap",
            origem: "Teste");

        var indexResult = await tools.Tools.nero_update_project_index(
            projeto: "Acme.UpdateTools.API",
            dominio: "api",
            proposito: "Proposito via update tool",
            arquivos: ["context.md", "inventory.md"],
            origem: "Marco 21");
        Assert.False(indexResult.Created);
        Assert.Equal("index", indexResult.FileKind);
        Assert.Contains("nero_admin_reindex", indexResult.Recommendation);

        var contextResult = await tools.Tools.nero_update_project_context(
            projeto: "Acme.UpdateTools.API",
            dominio: "api",
            proposito: "API update tools",
            stack: ".NET",
            superficie: "API",
            resumoOperacional: "Resumo curto.",
            skillOperacional: "$nero");
        Assert.False(contextResult.Created);
        Assert.Equal("context", contextResult.FileKind);

        var inventoryResult = await tools.Tools.nero_update_project_inventory(
            projeto: "Acme.UpdateTools.API",
            dominio: "api",
            revisadoEm: "2026-08-05",
            classificacao: "API de teste",
            sinaisTecnicos: ["csproj detectado"],
            gitBranch: "main");
        Assert.True(inventoryResult.Created);
        Assert.Equal("inventory", inventoryResult.FileKind);
        Assert.True(File.Exists(inventoryResult.Path));

        await tools.ReindexAsync();
        var projectContext = await tools.Tools.nero_get_project_context(
            project: "Acme.UpdateTools.API",
            includeDecisions: false,
            includeTroubleshooting: false);
        Assert.True(projectContext.Exists);
        Assert.Contains("Proposito via update tool", await File.ReadAllTextAsync(indexResult.Path));
    }

    [Fact]
    public async Task NeroUpdateProjectIndex_FailsWhenProjectMissing()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(
            root,
            "domains/api/index.md",
            "---\ntype: domain_index\nscope: domain\ndomain: api\nstatus: active\n---\n# api\n");
        var tools = await CreateToolsAsync(root);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => tools.Tools.nero_update_project_index(
                projeto: "Acme.Ghost.API",
                dominio: "api",
                proposito: "x",
                arquivos: ["context.md"]));

        Assert.Contains("Tool 'nero_update_project_index' failed.", exception.Message);
        Assert.Contains("missing index.md", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recommendation:", exception.Message);
    }

    [Fact]
    public async Task NeroRegisterBusinessRule_WritesMarkdownAndIsSearchableAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_business_rule(
            titulo: "Cupom exige filial origem",
            escopo: "project",
            projeto: "Acme.Api",
            regra: "Todo pedido com cupom deve informar a filial de origem.",
            evidencia: "Validacao solicitada no refinamento da regra.",
            origem: "Sprint 6.2");

        Assert.Equal("Cupom exige filial origem", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.Equal("projects/Acme.Api/business-rules/cupom-exige-filial-origem.md", result.RelativePath);
        Assert.True(File.Exists(result.Path));

        await tools.ReindexAsync();

        var searchResults = await tools.Tools.nero_search_knowledge(
            query: "cupom",
            project: "Acme.Api");
        Assert.Contains(searchResults, item => item.Title == "Cupom exige filial origem");
    }

    [Fact]
    public async Task WriteTools_ReturnActionableErrorsForInvalidInput()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        var tools = await CreateToolsAsync(root);

        await AssertActionableWriteErrorAsync(
            "nero_register_business_rule",
            () => tools.Tools.nero_register_business_rule("", "project", "Regra", "Evidencia", "Origem", projeto: "Acme.Api"));
        await AssertActionableWriteErrorAsync(
            "nero_register_decision",
            () => tools.Tools.nero_register_decision("", "project", "Problema", "Opcoes", "Decisao", "Consequencias", "Origem", projeto: "Acme.Api"));
        await AssertActionableWriteErrorAsync(
            "nero_register_pattern",
            () => tools.Tools.nero_register_pattern("", "project", "Contexto", "Padrao", "Quando aplicar", "Quando nao aplicar", "Origem", projeto: "Acme.Api"));
        await AssertActionableWriteErrorAsync(
            "nero_register_validation_rule",
            () => tools.Tools.nero_register_validation_rule("", "project", "Regra", "Criterio", "Evidencia", "Origem", projeto: "Acme.Api"));
        await AssertActionableWriteErrorAsync(
            "nero_register_snapshot",
            () => tools.Tools.nero_register_snapshot("", "project", "Contexto", "Evidencia", "Origem", projeto: "Acme.Api"));
        await AssertActionableWriteErrorAsync(
            "nero_register_troubleshooting",
            () => tools.Tools.nero_register_troubleshooting("", "project", "Sintoma", "Causa", "Acao", "Evidencia", "Impacto", "Origem", projeto: "Acme.Api"));
    }

    [Fact]
    public async Task NeroRegisterPattern_ReturnsActionableErrorWhenTargetFileAlreadyExists()
    {
        var root = CreateTempKnowledgeRoot();
        var expectedTargetPath = Path.Combine(
            root, "domains", "api", "patterns", "cache-por-chave-de-negocio.md");
        await WriteMarkdownAsync(root, "domains/api/index.md", "# API\n\nIndice.");
        await WriteMarkdownAsync(root, "domains/api/patterns/cache-por-chave-de-negocio.md", "# Existente\n");
        var tools = await CreateToolsAsync(root);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => tools.Tools.nero_register_pattern(
                titulo: "Cache por chave de negocio",
                escopo: "domain",
                dominio: "api",
                contexto: "Contexto valido.",
                padrao: "Padrao valido.",
                quandoAplicar: "Quando aplicar.",
                quandoNaoAplicar: "Quando nao aplicar.",
                origem: "Teste automatizado"));

        Assert.Contains("Tool 'nero_register_pattern' failed.", exception.Message);
        Assert.Contains("Category: FileWrite.", exception.Message);
        Assert.Contains($"TargetPath: {expectedTargetPath}.", exception.Message);
        Assert.DoesNotContain("TargetPath: n/a.", exception.Message);
        Assert.Contains("MarkdownWritten: false.", exception.Message);
        Assert.Contains("WrittenPaths: none.", exception.Message);
        Assert.Contains("Recommendation:", exception.Message);
    }

    [Fact]
    public async Task NeroRegisterDecision_WritesMarkdownAndIsRecoverableAfterClientReindex()
    {
        var root = CreateTempKnowledgeRoot();
        await WriteMarkdownAsync(root, "projects/Acme.Api/index.md", "# Inventory API\n\nIndice.");
        await WriteMarkdownAsync(
            root,
            "projects/Acme.Api/decisions/2026-07-01-decisao-antiga.md",
            "# Decisao antiga\n\nModelo anterior.");
        var tools = await CreateToolsAsync(root);

        var result = await tools.Tools.nero_register_decision(
            titulo: "Usar ADR curto para decisoes",
            escopo: "project",
            projeto: "Acme.Api",
            problema: "Decisoes tecnicas precisam ser recuperaveis no contexto do projeto.",
            opcoes: "- Texto livre\n- ADR curto",
            decisao: "Registrar decisoes tecnicas com ADR curto.",
            consequencias: "O historico fica rastreavel e indexado.",
            origem: "Sprint 7.2",
            supersedes: ["projects/Acme.Api/decisions/2026-07-01-decisao-antiga"]);

        Assert.Equal("Usar ADR curto para decisoes", result.Title);
        Assert.Contains("nero_admin_reindex", result.Recommendation);
        Assert.StartsWith("projects/Acme.Api/decisions/", result.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith("-usar-adr-curto-para-decisoes.md", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Path));

        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: decision", markdown);
        Assert.Contains("type: supersedes", markdown);
        Assert.Contains("target: \"projects/Acme.Api/decisions/2026-07-01-decisao-antiga\"", markdown);

        await tools.ReindexAsync();

        var related = await tools.Tools.nero_find_related_knowledge(
            project: "Acme.Api",
            topic: "ADR",
            relationTypes: ["supersedes"]);
        var superseded = Assert.Single(related, item => item.Title == "Decisao antiga");
        Assert.Equal("Supersedes", superseded.Relation);

        var projectContext = await tools.Tools.nero_get_project_context(
            project: "Acme.Api",
            includeDecisions: true,
            includeTroubleshooting: false);
        Assert.Contains(projectContext.Decisions, item => item.Title == "Usar ADR curto para decisoes");
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteMarkdownAsync(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static async Task AssertActionableWriteErrorAsync(
        string toolName,
        Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains($"Tool '{toolName}' failed.", exception.Message);
        Assert.Contains("Category: InvalidInput.", exception.Message);
        Assert.Contains("Field:", exception.Message);
        Assert.Contains("Recommendation:", exception.Message);
    }

    private sealed class ToolsFixture
    {
        public required NeroKnowledgeTools Tools { get; init; }

        public required KnowledgeDatabaseConnectionFactory ConnectionFactory { get; init; }

        public required string Root { get; init; }

        public async Task ReindexAsync()
        {
            await using var connection = ConnectionFactory.CreateConnection();
            await new KnowledgeIndexer().ReindexAsync(connection, Root);
        }
    }

    private static async Task<ToolsFixture> CreateToolsAsync(
        string root,
        int busyTimeoutMilliseconds = KnowledgeDatabaseOptions.DefaultBusyTimeoutMilliseconds)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge.db");
        var connectionFactory = new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions
        {
            Path = databasePath,
            BusyTimeoutMilliseconds = busyTimeoutMilliseconds
        });
        await using (var connection = connectionFactory.CreateConnection())
        {
            await new KnowledgeIndexer().ReindexAsync(connection, root);
        }

        return new ToolsFixture
        {
            Root = root,
            ConnectionFactory = connectionFactory,
            Tools = new NeroKnowledgeTools(
                connectionFactory,
                new KnowledgeRootOptions { Path = root },
                new BusinessRuleWriterService(),
                new DecisionWriterService(),
                new PatternWriterService(),
                new ProjectWriterService(),
                new ProjectUpdateWriterService(),
                new DomainWriterService(),
                new SnapshotWriterService(),
                new TroubleshootingWriterService(),
                new ValidationRuleWriterService(),
                new KnowledgeDomainContextService(),
                new KnowledgeProjectContextService(),
                new RelatedKnowledgeService(),
                new KnowledgeLinkService(),
                new KnowledgeSearchService())
        };
    }
}
