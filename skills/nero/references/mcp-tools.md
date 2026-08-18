# Tools MCP Nero.Knowledge.Base

Inputs, outputs e contratos por tool. Ordem do lote: `workflow.md`. Significado de `links:` e `Dono`: `knowledge-routing.md`. Pos-lote e anti-leak: `compliance-security.md`.

## Indice

| Quando | Tool |
|---|---|
| Search / contexto / grafo | `nero_search_knowledge`, `nero_get_project_context`, `nero_get_domain_context`, `nero_find_related_knowledge` |
| Projeto e dominio | `nero_register_project`, `nero_register_domain`, `nero_update_domain`, `nero_inactivate_domain`, `nero_update_project_index`, `nero_update_project_context`, `nero_update_project_inventory` |
| Notas | `nero_register_pattern`, `nero_register_validation_rule`, `nero_register_snapshot`, `nero_register_troubleshooting`, `nero_register_business_rule`, `nero_register_decision`, `nero_link_knowledge` (business_rule/decision: schema no MCP; mesmo contrato de writer) |
| Admin | `nero_admin_status`, `nero_admin_validate`, `nero_admin_compliance_scan`, `nero_admin_reindex`, `nero_admin_check_index_consistency`, `nero_admin_project_health`, `nero_admin_ecosystem_health` |
| Git | `nero_admin_git_status`, `nero_admin_git_fetch`, `nero_admin_git_pull`, `nero_admin_create_commit`, `nero_admin_git_push` |

## Contrato dos writers

`nero_register_project` cria `index.md` / `context.md` so quando ausentes. Atualizar: `nero_update_project_index` / `nero_update_project_context` / `nero_update_project_inventory`. `linksSemanticos`: omitir preserva nao-minimos; lista substitui; `[]` limpa.

Dominio: `nero_register_domain` / `nero_update_domain` / `nero_inactivate_domain`. Allowlist = `domains/*/index.md` com `status` ausente ou `active`. Dominio inativo recusa projetos novos ate `nero_update_domain` com `reativar=true`.

Writers gravam Markdown. Sucesso inclui `recommendation` para concluir o **lote**. Reindex exclusivo e checklist: `compliance-security.md`. Scan reject-only e mascara de leitura: `compliance-security.md`. Git: secao `nero_admin_git_*`.

## Campo da tool → `links:`

Significado das relacoes: `knowledge-routing.md`. Writers emitem so o vocabulario preferencial.

- `nero_register_snapshot.relacionadoA` → `documents`; `evidenciaDe` → `evidences` (hubs rejeitados).
- `nero_register_troubleshooting.causadoPor` → `related_decision` ou `documents`; `relacionadoA` → `related_pattern` / `related_decision` / `documents`.
- `nero_register_pattern.usadoPor` → `source_for`; `candidatoParaReuso` → `documents` / `related_pattern` / `related_decision`.
- `nero_register_decision.supersedes` → `supersedes` (so decision→decision).
- `nero_register_business_rule`, `nero_register_validation_rule` e `nero_register_decision` emitem `documents` (+ `belongs_to_domain` se houver dominio). Decision acrescenta `supersedes` quando informado.
- `nero_register_decision` deixa `Dono` vazio — preencha apos o register (`knowledge-routing.md`).

`nero_admin_validate` rejeita legado, nota de conteudo sem `links:`, `supersedes` fora de decision→decision, `depends_on`/`uses_backend` invertidos e `evidences` para hub.

## Erros das tools de escrita

As tools de escrita preservam o contrato de sucesso documentado abaixo. Em falhas, retornam erro MCP com mensagem padronizada contendo:

- tool que falhou;
- categoria (`Compliance`, `InvalidInput`, `FileWrite`, `ReadOnly`, `InvalidPath`, `ReindexOrGraph`, `UnauthorizedWrite`, `SqliteBusy`, `Sqlite` ou `WriteOperation`);
- campo relacionado quando disponivel;
- `RuleId` quando `Category: Compliance`;
- motivo original (nunca ecoa o valor sensivel em hits de compliance);
- target path, indicador `MarkdownWritten` e `WrittenPaths` com arquivos concluidos antes da falha;
- recomendacao operacional.

Exemplo:

```text
Tool 'nero_register_pattern' failed. Category: FileWrite. Field: n/a. Reason: The file already exists. TargetPath: C:/.../patterns/exemplo.md. MarkdownWritten: false. WrittenPaths: none. Recommendation: Check whether the calculated target file already exists or is locked, then retry with a different title if needed.
```

Exemplo compliance (reject-only; `MarkdownWritten: false`):

```text
Tool 'nero_register_business_rule' failed. Category: Compliance. Field: rule. RuleId: secret.bearer_token. Reason: Compliance rule 'secret.bearer_token' blocked the write. ... MarkdownWritten: false. WrittenPaths: none. Recommendation: Remove the sensitive value or replace it with an exact allowlisted placeholder...
```

## `nero_search_knowledge`

Busca conhecimento indexado no SQLite a partir dos Markdown canonicos do Knowledge Repo (`KnowledgeRoot__Path`).

Input:

```json
{
  "query": "webhook material",
  "domain": "api",
  "project": "Acme.Api",
  "limit": 10
}
```

Campos opcionais: `domain`, `project` e `limit`.

Output:

```json
[
  {
    "id": "projects/Acme.Api/context",
    "title": "Acme.Api",
    "path": "knowledge/projects/Acme.Api/context.md",
    "scope": "Project",
    "type": "ProjectContext",
    "domain": null,
    "project": "Acme.Api",
    "snippet": "..."
  }
]
```



## `nero_get_project_context`

Retorna o contexto consolidado de um projeto a partir do indice SQLite derivado dos Markdown canonicos do Knowledge Repo.
Use no inicio de tarefas de projeto para recuperar indice, contexto, padroes, regras de negocio, decisoes e troubleshootings recentes.

Input:

```json
{
  "project": "Acme.Api",
  "includeDecisions": true,
  "includeTroubleshooting": true
}
```

Campos opcionais: `includeDecisions` e `includeTroubleshooting`.

Output:

```json
{
  "project": "Acme.Api",
  "exists": true,
  "index": {
    "id": "projects/Acme.Api/index",
    "title": "Acme.Api",
    "path": "knowledge/projects/Acme.Api/index.md",
    "type": "Index",
    "content": "..."
  },
  "context": {
    "id": "projects/Acme.Api/context",
    "title": "Contexto",
    "path": "knowledge/projects/Acme.Api/context.md",
    "type": "ProjectContext",
    "content": "..."
  },
  "patterns": null,
  "businessRules": null,
  "decisions": [
    {
      "id": "projects/Acme.Api/decisions/2026-07-02-nova",
      "title": "Decisao nova",
      "path": "knowledge/projects/Acme.Api/decisions/2026-07-02-nova.md",
      "type": "Decision",
      "content": "..."
    }
  ],
  "activeDecisions": [
    {
      "id": "projects/Acme.Api/decisions/2026-07-02-nova",
      "title": "Decisao nova",
      "path": "knowledge/projects/Acme.Api/decisions/2026-07-02-nova.md",
      "type": "Decision",
      "content": "..."
    }
  ],
  "supersededDecisions": [
    {
      "decision": {
        "id": "projects/Acme.Api/decisions/2026-07-01-antiga",
        "title": "Decisao antiga",
        "path": "knowledge/projects/Acme.Api/decisions/2026-07-01-antiga.md",
        "type": "Decision",
        "content": "..."
      },
      "supersededBy": [
        {
          "id": "projects/Acme.Api/decisions/2026-07-02-nova",
          "title": "Decisao nova",
          "path": "knowledge/projects/Acme.Api/decisions/2026-07-02-nova.md",
          "type": "Decision",
          "content": "..."
        }
      ]
    }
  ],
  "hasSupersededDecisions": true,
  "recommendation": "Use activeDecisions as current guidance. supersededDecisions and supersededBy are historical; superseded decisions are omitted from decisions.",
  "troubleshooting": []
}
```

`decisions` e mantido por compatibilidade, mas omite decisoes superseded e portanto acompanha as decisoes vigentes recentes. Agentes devem preferir `activeDecisions` como contrato explicito de orientacao vigente e tratar `supersededDecisions` como historico, observando `supersededBy` para saber qual decisao substituiu a antiga. `hasSupersededDecisions` e `recommendation` tornam essa distincao explicita.
Uma decision do projeto e considerada superseded quando for alvo de uma edge `supersedes` decision→decision, mesmo que a decision substituta esteja em escopo global, de dominio ou de outro projeto; `supersededBy` retorna essa decision cross-scope.

`nero_search_knowledge` e `nero_find_related_knowledge` nao aplicam o split vigente/historico: filtrem `supersedes` no cliente ou use `nero_get_project_context`.

## `nero_register_project`

Cria a estrutura minima de knowledge para um projeto no Knowledge Repo (Markdown). **Nao reindexa**; o cliente deve chamar `nero_admin_reindex` apos concluir o lote de escritas.
Use antes de registrar decisions, patterns, regras, validacoes ou troubleshootings em um projeto que ainda nao possui `projects/<Projeto>/index.md` e `context.md`.
Se a pasta do projeto ja existir com outros arquivos, a tool preserva o conteudo e cria apenas os arquivos base ausentes.

Input:

```json
{
  "projeto": "Acme.Auth.Api",
  "dominio": "api",
  "proposito": "API de autenticacao da Acme.",
  "origem": "Registro manual"
}
```

Output:

```json
{
  "project": "Acme.Auth.Api",
  "domain": "api",
  "created": true,
  "projectDirectoryPath": "C:/.../knowledge-repo/projects/Acme.Auth.Api",
  "projectRelativePath": "projects/Acme.Auth.Api",
  "indexPath": "C:/.../knowledge-repo/projects/Acme.Auth.Api/index.md",
  "contextPath": "C:/.../knowledge-repo/projects/Acme.Auth.Api/context.md",
  "createdFiles": [
    "C:/.../knowledge-repo/projects/Acme.Auth.Api/index.md",
    "C:/.../knowledge-repo/projects/Acme.Auth.Api/context.md"
  ],
  "recommendation": "The Markdown was written, but the SQLite index may be stale. Finish the write batch, then run nero_admin_reindex once."
}
```

Os demais outputs `nero_register_*` incluem a mesma propriedade `recommendation`.

## `nero_register_domain`

Cria `domains/<dominio>/index.md` com `status: active` (bootstrap minimo). Falha se o dominio ja existir (ativo ou inativo). Slug: `^[a-z][a-z0-9_-]{1,31}$`; nomes reservados (`global`, `projects`, `_drafts`, …) sao rejeitados. **Nao reindexa**.

## `nero_update_domain`

Reescreve `domains/<dominio>/index.md` a partir de campos estruturados (`titulo`, `proposito`, `fonteConsolidada`, `arquivos`, `regrasLeitura`, `sourceFor`, `origem`).

**Contrato de** `sourceFor` **(evita perda acidental de links):**


| Input              | Efeito                                                     |
| ------------------ | ---------------------------------------------------------- |
| omitido / `null`   | **Preserva** os `source_for` ja existentes no `index.md`   |
| lista explicita    | **Substitui** o conjunto (envie a lista completa desejada) |
| `[]` (lista vazia) | **Remove todos** os `source_for` de proposito              |


Para remover um projeto especifico, passe a lista completa **sem** esse item. Dominio inactive exige `reativar=true`. Nunca define `status: inactive` (use `nero_inactivate_domain`). **Nao reindexa**.

## `nero_inactivate_domain`

Soft-delete: grava `status: inactive` no `index.md` (pasta permanece). Sempre exige `motivo` e `origem`. Se houver projetos com `belongs_to_domain`, exige `confirmacao=INACTIVATE_WITH_LINKED_PROJECTS` e `evidencia`. **Nao reindexa**.

## `nero_update_project_index`

Reescreve `projects/<Projeto>/index.md` a partir de campos estruturados (template hibrido). **Exige** `index.md` existente; bootstrap so via `nero_register_project`. **Nao reindexa**.

**Contrato de** `linksSemanticos` **(preserva grafo):**

| Input | Efeito |
| --- | --- |
| omitido / `null` | **Preserva** links nao-minimos ja existentes (`uses_backend`, `depends_on`, …) |
| lista explicita | **Substitui** o conjunto (envie a lista completa desejada) |
| `[]` (lista vazia) | **Remove todos** os nao-minimos de proposito |

Links minimos (`documents`, `belongs_to_domain`) sempre sao derivados de `projeto` / `dominio` — nao passe esses tipos em `linksSemanticos`. Formato de cada item: `type:target` (ex.: `uses_backend:projects/Acme.X.Api`). Direcao G3 e aplicada (`uses_backend`/`depends_on` invertidos sao rejeitados). Nunca dependa so de `nero_link_knowledge` para durar (edge so no SQLite; reindex descarta se ausente no Markdown).

Input:

```json
{
  "projeto": "Acme.Auth.Api",
  "dominio": "api",
  "proposito": "API de autenticacao da Acme.",
  "arquivos": ["context.md", "inventory.md"],
  "origem": "Review de knowledge",
  "linksSemanticos": ["depends_on:projects/Acme.Ldap.Api"]
}
```



## `nero_update_project_context`

Reescreve `projects/<Projeto>/context.md` a partir de campos estruturados. **Exige** `index.md` e `context.md` existentes. Evidencia longa deve ir para snapshot, nao no resumo. **Nao reindexa**.

Mesmo contrato de `linksSemanticos` que `nero_update_project_index` (omitir preserva; lista substitui; `[]` limpa).

Input:

```json
{
  "projeto": "Acme.Auth.Api",
  "dominio": "api",
  "proposito": "API de autenticacao da Acme.",
  "stack": "ASP.NET Core + SQL Server",
  "superficie": "API HTTP",
  "resumoOperacional": "Contexto consolidado curto.",
  "skillOperacional": "$acme-auth",
  "origem": "Review de knowledge"
}
```



## `nero_update_project_inventory`

Cria ou reescreve `projects/<Projeto>/inventory.md` (upsert). **Exige** `index.md`. Nao gravar secrets, tokens nem paths locais com credenciais. **Nao reindexa**.

Mesmo contrato de `linksSemanticos` que `nero_update_project_index` (omitir preserva; lista substitui; `[]` limpa).

Input:

```json
{
  "projeto": "Acme.Auth.Api",
  "dominio": "api",
  "revisadoEm": "2026-08-05",
  "classificacao": "API Acme para autenticacao.",
  "sinaisTecnicos": ["Solution: Acme.Auth.Api.sln"],
  "gitBranch": "develop",
  "gitHead": "3c33764",
  "gitRemote": "https://github.com/acme/Acme.Auth.Api.git"
}
```

Output (compartilhado pelas tres tools):

```json
{
  "project": "Acme.Auth.Api",
  "domain": "api",
  "fileKind": "inventory",
  "path": "C:/.../knowledge-repo/projects/Acme.Auth.Api/inventory.md",
  "relativePath": "projects/Acme.Auth.Api/inventory.md",
  "created": true,
  "recommendation": "The Markdown was written, but the SQLite index may be stale. Finish the write batch, then run nero_admin_reindex once."
}
```



## `nero_get_domain_context`

Retorna o contexto consolidado de um dominio a partir do indice SQLite derivado dos Markdown canonicos do Knowledge Repo.
Use para recuperar padroes, regras, validacoes e projetos diretamente ligados ao dominio por relacoes `belongs_to_domain`.

Input:

```json
{
  "domain": "api",
  "includeProjects": true
}
```

Campo opcional: `includeProjects`.

Output:

```json
{
  "domain": "api",
  "exists": true,
  "index": {
    "id": "domains/api/index",
    "title": "API",
    "path": "knowledge/domains/api/index.md",
    "type": "Index",
    "content": "..."
  },
  "patterns": {
    "id": "domains/api/patterns",
    "title": "Padroes API",
    "path": "knowledge/domains/api/patterns.md",
    "type": "Pattern",
    "content": "..."
  },
  "businessRules": null,
  "validationAndTests": null,
  "projects": [
    {
      "id": "projects/Acme.Api/index",
      "title": "Acme.Api",
      "path": "knowledge/projects/Acme.Api/index.md",
      "project": "Acme.Api"
    }
  ]
}
```



## `nero_find_related_knowledge`

Busca conhecimentos relacionados por grafo, combinando edges diretas, dominio comum e projetos irmaos.
Use antes de implementar regra nova para localizar precedentes reutilizaveis em projetos ou dominios do Knowledge Repo.

Input:

```json
{
  "project": "Acme.Recebimento.Api",
  "topic": "estoque",
  "relationTypes": ["related_pattern"],
  "depth": 1
}
```

Campos opcionais: `project`, `topic`, `relationTypes` e `depth`. Informe pelo menos `project` ou `topic`.

Output:

```json
[
  {
    "id": "domains/api/patterns",
    "title": "Padroes API",
    "path": "knowledge/domains/api/patterns.md",
    "scope": "Domain",
    "type": "Pattern",
    "domain": "api",
    "project": null,
    "relation": "RelatedPattern",
    "evidence": "frontmatter links in knowledge/projects/Acme.Recebimento.Api/context.md",
    "score": 1.0
  }
]
```



## `nero_register_pattern`

Registra um padrao reutilizavel em Markdown. **Nao reindexa**; o cliente chama `nero_admin_reindex` apos o lote de escritas.
Quando `usadoPor` ou `candidatoParaReuso` forem informados, a nota recebe `links:` no frontmatter; o reindex posterior cria edges preferenciais: `source_for` (usadoPor) e `documents` / `related_pattern` / `related_decision` (candidatoParaReuso, conforme o path do alvo).

Input:

```json
{
  "titulo": "Cache por chave de negocio",
  "escopo": "domain",
  "dominio": "api",
  "contexto": "Consultas repetidas sobre dados pouco volateis geram custo desnecessario.",
  "padrao": "Centralizar cache por chave de negocio com invalidacao explicita.",
  "quandoAplicar": "Aplicar em consultas idempotentes e com baixa volatilidade.",
  "quandoNaoAplicar": "Nao aplicar em dados transacionais que exigem leitura estritamente atualizada.",
  "excecoes": "Usar TTL curto quando invalidacao explicita nao estiver disponivel.",
  "exemplos": ["Cachear consulta de produtos por codigo."],
  "origem": "ADR, PR, incidente ou solicitacao",
  "usadoPor": ["projects/Acme.Api/index"],
  "candidatoParaReuso": ["domains/api/index"]
}
```

Campos opcionais: `dominio`, `projeto`, `excecoes`, `exemplos`, `usadoPor` e `candidatoParaReuso`.

Output:

```json
{
  "title": "Cache por chave de negocio",
  "path": "C:/.../knowledge-repo/domains/api/patterns/cache-por-chave-de-negocio.md",
  "relativePath": "domains/api/patterns/cache-por-chave-de-negocio.md"
}
```



## `nero_register_validation_rule`

Registra uma regra de validacao, teste ou criterio de aceite reutilizavel em Markdown. **Nao reindexa**; o cliente chama `nero_admin_reindex` apos o lote de escritas.
Sempre emite `links:` minimos por escopo (`documents` e, quando houver dominio, `belongs_to_domain`) para satisfazer `nero_admin_validate`.

Input:

```json
{
  "titulo": "Validar estoque disponivel",
  "escopo": "domain",
  "dominio": "api",
  "regra": "Proteger o fluxo contra pedido sem saldo suficiente.",
  "criterio": "Dado produto sem saldo, a validacao deve recusar o pedido antes da persistencia.",
  "evidencia": "Teste automatizado cobrindo produto sem saldo com mensagem acionavel.",
  "origem": "ADR, PR, incidente ou solicitacao",
  "lacunasConhecidas": "Cenarios de concorrencia exigem teste integrado."
}
```

Campos opcionais: `dominio`, `projeto` e `lacunasConhecidas`.

Output:

```json
{
  "title": "Validar estoque disponivel",
  "path": "C:/.../knowledge-repo/domains/api/validation-and-tests/validar-estoque-disponivel.md",
  "relativePath": "domains/api/validation-and-tests/validar-estoque-disponivel.md"
}
```



## `nero_register_snapshot`

Registra um snapshot em Markdown. **Nao reindexa**; o cliente chama `nero_admin_reindex` apos o lote de escritas.
Use para capturar evidencia versionada de analises, inventarios tecnicos, outputs de comandos, diagnosticos pontuais ou estado observado do checkout local.
Quando `relacionadoA` ou `evidenciaDe` forem informados, a nota recebe `links:` no frontmatter; o reindex posterior cria edges `documents` e `evidences`.
`evidenciaDe` rejeita hubs/diretorios genericos no register (`ArgumentException`) — mesma heuristica G4 do validate; use slug de nota concreta.
`contexto` e `evidencia` aceitam no maximo 64 KiB (65.536 bytes UTF-8) cada; payload maior falha antes da escrita.

Input:

```json
{
  "titulo": "Snapshot de rotas",
  "escopo": "project",
  "projeto": "Acme.Api",
  "contexto": "Inventario tecnico das rotas publicas revisadas.",
  "evidencia": "Arquivos de controller e contratos analisados no checkout local.",
  "origem": "Revisao de repositorio",
  "relacionadoA": ["projects/Acme.Api/index"],
  "evidenciaDe": ["domains/api/patterns/http-versioning"]
}
```

Campos opcionais: `dominio`, `projeto`, `relacionadoA` e `evidenciaDe`.
`evidenciaDe` deve apontar para nota concreta (nao `domains/*/patterns`, `projects/*/decisions`, `index`, etc.).

Output:

```json
{
  "title": "Snapshot de rotas",
  "path": "C:/.../knowledge-repo/projects/Acme.Api/snapshots/2026-07-22-snapshot-de-rotas.md",
  "relativePath": "projects/Acme.Api/snapshots/2026-07-22-snapshot-de-rotas.md"
}
```



## `nero_register_troubleshooting`

Registra um incidente ou troubleshooting em Markdown. **Nao reindexa**; o cliente chama `nero_admin_reindex` apos o lote de escritas.
Quando `causadoPor` ou `relacionadoA` forem informados, a nota recebe `links:` no frontmatter; o reindex posterior cria edges preferenciais: `related_decision` ou `documents` (causadoPor) e `related_pattern` / `related_decision` / `documents` (relacionadoA, conforme o path do alvo). Nao emite `caused_by` nem `relates_to`.

Input:

```json
{
  "titulo": "Falha ao sincronizar estoque",
  "escopo": "project",
  "projeto": "Acme.Api",
  "sintoma": "Sincronizacao retorna timeout ao processar estoque.",
  "causa": "Servico externo ficou indisponivel durante a janela de retry.",
  "acao": "Reprocessar mensagens pendentes apos estabilizacao do servico.",
  "evidencia": "Logs resumidos mostram timeout HTTP 504 na integracao.",
  "impacto": "Estoque pode ficar defasado ate o reprocessamento.",
  "origem": "Incidente, ticket, PR ou solicitacao",
  "solucao": "Executar reprocessamento controlado da fila afetada.",
  "prevencao": "Monitorar latencia e configurar alerta para aumento de retries.",
  "causadoPor": ["domains/api/index"],
  "relacionadoA": ["projects/Acme.Api/index"]
}
```

Campos opcionais: `dominio`, `projeto`, `solucao`, `prevencao`, `causadoPor` e `relacionadoA`.

Output:

```json
{
  "title": "Falha ao sincronizar estoque",
  "path": "C:/.../knowledge-repo/projects/Acme.Api/troubleshooting/2026-07-22-falha-ao-sincronizar-estoque.md",
  "relativePath": "projects/Acme.Api/troubleshooting/2026-07-22-falha-ao-sincronizar-estoque.md"
}
```



## `nero_link_knowledge`

Cria uma edge manual e idempotente entre dois nodes ja indexados no SQLite.
A tool resolve `source` e `target` por id canonico ou path logico `knowledge/...`; ela nao altera automaticamente o frontmatter Markdown.

Input:

```json
{
  "source": "projects/Acme.Api/index",
  "target": "domains/api/index",
  "relation": "belongs_to_domain",
  "confidence": 0.95,
  "evidence": "Relacionamento manual confirmado em revisao."
}
```

Campos opcionais: `confidence` e `evidence`.

Output:

```json
{
  "edgeId": "projects/Acme.Api/index|BelongsToDomain|domains/api/index",
  "sourceNodeId": "projects/Acme.Api/index",
  "targetNodeId": "domains/api/index",
  "relation": "BelongsToDomain",
  "created": true
}
```



## `nero_admin_status`

Retorna o status administrativo local do MCP Nero, sem modificar arquivos ou executar sincronizacao Git.
Use para diagnosticar a sessao antes de escrever knowledge ou rodar comandos administrativos.

Input: nenhum.

Output:

```json
{
  "server": "nero-knowledge-base",
  "repositoryRoot": "C:/.../knowledge-repo",
  "branch": "main",
  "hasModifiedFiles": true,
  "modifiedFiles": ["docs/mcp-backlog.md"],
  "indexDatabaseExists": true,
  "indexDatabasePath": "C:/.../mcp/data/nero-knowledge.db",
  "lastIndexedUtc": "2026-07-22 12:34:56",
  "writeMode": "direct"
}
```



## `nero_admin_validate`

Valida a estrutura obrigatoria do Knowledge Repo, frontmatter minimo quando existir, nodes e edges do grafo, e qualidade semantica do vocabulario de `links:`.
Tambem calcula `isCompliant` de forma **independente** (scan P0 no corpus; notas com `compliance_status: quarantined` nao falham compliance).
Nao grava no SQLite. Prontidao/publicacao exige `isValid=true` **e** `isCompliant=true`.

Rejeita (alem da validacao estrutural):

- tipos de relacao legados ou nao preferenciais em `links:` (`relates_to`, `caused_by`, `used_by`, `candidate_for_reuse`, e qualquer outro fora do vocabulario preferencial);
- notas de conteudo sem bloco `links:` nao vazio (decisions, patterns, business-rules, troubleshooting, snapshots, validation-and-tests);
- `supersedes` fora de decision→decision (permitido apenas quando origem e alvo parecem decisions por path `/decisions/` ou tipo `decision`);
- `depends_on` / `uses_backend` invertidos (API/backend/lib → Front/Mobile); orientacao esperada e consumer → backend (heuristicas preferem falso negativo em nomes ambiguos);
- `evidences` apontando para hubs/diretorios genericos (`domains/*/patterns`, `projects/*/decisions`, `index`, `context`, etc.) em vez de uma nota concreta com slug.

Vocabulario preferencial: `belongs_to_domain`, `documents`, `evidences`, `updates`, `depends_on`, `uses_backend`, `related_decision`, `related_pattern`, `source_for`. Especial: `supersedes` (decision→decision).

Input: nenhum.

Output:

```json
{
  "isValid": true,
  "isCompliant": true,
  "nodeCount": 273,
  "edgeCount": 911,
  "errors": [],
  "complianceGaps": [],
  "actionableGaps": [],
  "recommendation": "Structure and compliance both passed. Ready for reindex/commit when the batch is finished."
}
```

Quando `isValid=false` ou `isCompliant=false`, `actionableGaps` agrega gaps estruturais e `compliance: ...` (trechos ja mascarados).

## `nero_admin_compliance_scan`

Varre todo Markdown sob o knowledge root com a taxonomia versionada. Nao reescreve arquivos nem faz commit.

Output (resumo):

- `isCompliant`, `taxonomyVersion`, contagens de hits ativos / quarentenados / warnings (`pii_suspect.*`);
- `activeHits` / `quarantinedHits` / `warnings`: `path`, `ruleId`, `severity`, `line`, `maskedExcerpt` (nunca o valor bruto).

Use para triage humana apos o one-shot inicial e antes de publicar knowledge.

## `nero_admin_reindex`

Reindexa os Markdown canonicos do Knowledge Repo no SQLite configurado.
**Responsabilidade do cliente:** chamar uma vez apos concluir o lote de `nero_register_`* / `nero_update_project_*` / edicoes manuais. Writers nao reindexam automaticamente.
Nao execute em paralelo com search, context, validate ou outras operacoes SQLite. O busy timeout e configuravel por `KnowledgeDatabase__BusyTimeoutMilliseconds` (default `5000`) e pooling fica habilitado por default.

Input: nenhum.

Output:

```json
{
  "indexedNodes": 273,
  "knowledgeRootPath": "C:/.../knowledge-repo",
  "indexDatabasePath": "C:/.../mcp/data/nero-knowledge.db",
  "recommendation": "Run nero_admin_validate next before trusting the index or committing knowledge changes."
}
```



## `nero_admin_check_index_consistency`

Compara o SQLite configurado com os Markdown do Knowledge Repo sem reindexar.
Use apos edicoes em massa (passo 3 opcional do checklist) ou quando houver suspeita de desalinhamento entre MCP e filesystem.
Nao substitui `nero_admin_validate` (consistencia de indice != qualidade semantica do grafo).

Detecta:

- node no SQLite sem arquivo Markdown correspondente;
- Markdown no filesystem sem node indexado;
- Markdown modificado depois da ultima indexacao do node.

Campos de performance (sempre presentes no output):


| Campo                   | Significado                                                                                                                                          |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `elapsedMilliseconds`   | Tempo de parede da checagem                                                                                                                          |
| `thresholdMilliseconds` | Limiar soft de UX (default `2000`). Configuravel via `AdminIndexConsistency:ThresholdMilliseconds` ou `AdminIndexConsistency__ThresholdMilliseconds` |
| `exceededThreshold`     | `true` quando `elapsedMilliseconds > thresholdMilliseconds` — **nao falha** a tool; apenas sinaliza degradacao                                       |


Baseline tipica ~170–230 ms; stretch UX ≤2 s; hard SLO ≤60 s (abaixo do timeout MCP).

Input: nenhum.

Output:

```json
{
  "isConsistent": false,
  "knowledgeRootPath": "C:/.../knowledge-repo",
  "indexDatabasePath": "C:/.../skills/nero/data/nero-knowledge.db",
  "indexedNodeCount": 275,
  "markdownFileCount": 276,
  "elapsedMilliseconds": 187,
  "thresholdMilliseconds": 2000,
  "exceededThreshold": false,
  "issues": [
    {
      "type": "MarkdownMissingIndexedNode",
      "id": "projects/Acme.Auth.Api/index",
      "path": "knowledge/projects/Acme.Auth.Api/index.md",
      "indexedUpdatedUtc": null,
      "fileLastWriteUtc": "2026-07-22T13:00:00.0000000Z",
      "recommendation": "Run nero_admin_reindex to add this Markdown file to the SQLite index."
    }
  ]
}
```



## `nero_admin_project_health`

Diagnostica se um projeto existe no filesystem da knowledge e no indice SQLite, se possui estrutura base (`index.md` e `context.md`), se ha notas de projeto sem estrutura minima, se existe link `belongs_to_domain` para o dominio esperado e o frescor interno do snapshot de projeto mais recente.
Use antes de registrar knowledge em um projeto quando houver duvida se a estrutura minima ja existe.

Input:

```json
{
  "project": "Acme.Api",
  "primaryDomain": "api"
}
```

Campo opcional: `primaryDomain`. Quando omitido, a tool aceita qualquer link `belongs_to_domain`.

Output:

```json
{
  "project": "Acme.Api",
  "primaryDomain": "api",
  "knowledgeRootPath": "C:/.../knowledge-repo",
  "projectDirectoryPath": "C:/.../knowledge-repo/projects/Acme.Api",
  "existsInFilesystem": true,
  "existsInIndex": true,
  "hasIndex": true,
  "hasContext": true,
  "hasBaseStructure": true,
  "hasNotesWithoutBaseStructure": false,
  "hasBelongsToDomain": true,
  "lastIndexedUtc": "2026-07-22 12:34:56",
  "recentSnapshotDays": 90,
  "hasRecentSnapshot": true,
  "latestSnapshotPath": "knowledge/projects/Acme.Api/snapshots/2026-07-22-review.md",
  "latestSnapshotDate": "2026-07-22",
  "latestSnapshotAgeDays": 13,
  "latestSnapshotOrigin": "Repository review",
  "recommendation": "Project knowledge structure is healthy.",
  "issues": []
}
```

O frescor usa apenas snapshots internos do knowledge cujo nome inicia com `yyyy-MM-dd`. O limiar e configuravel por `AdminProjectFreshness:RecentSnapshotDays` ou `AdminProjectFreshness__RecentSnapshotDays` (default `90`). `latestSnapshotOrigin` vem somente do frontmatter do snapshot selecionado. Ausencia e snapshot stale aparecem em `issues` como `MissingRecentSnapshot` ou `StaleSnapshot`.

Quando `hasRecentSnapshot` e `false`, a `recommendation` do issue (e a recommendation de topo se esse for o unico problema relevante) aponta o playbook relativo `skills/nero/prompts/knowledge-review-app-mcp.txt` com projeto, dominio e reason curto — **nao** embute o texto do prompt. Exemplo:

```text
Run knowledge review with skills/nero/prompts/knowledge-review-app-mcp.txt for project Acme.Api (primaryDomain=api). Reason: hasRecentSnapshot=false; latest dated snapshot is 120 days old (threshold: 90).
```

Nao ha comparacao com path, branch ou HEAD de checkout de produto. Um eventual `checkoutPath` permanece para avaliacao futura.

## `nero_admin_ecosystem_health`

Executa healthcheck agregado de todos os projetos em `knowledge/projects/*` e dominios em `knowledge/domains/*`.
A implementacao faz uma unica leitura/parsing da arvore Markdown e, quando o SQLite existe, uma unica consulta de nodes do indice. O diagnostico de frescor dos projetos segue o mesmo limiar de `nero_admin_project_health`.

Para limitar o payload MCP, projetos e dominios saudaveis aparecem apenas nos contadores; `projectsWithIssues` e `domainsWithIssues` detalham somente escopos com problemas. Os issues de projeto reutilizam o contrato de `nero_admin_project_health` (`MissingIndex`, `MissingContext`, `MissingBelongsToDomain`, `ProjectNotIndexed`, `MissingRecentSnapshot`, `StaleSnapshot`, etc.). Dominios podem retornar `DomainMissing`, `MissingIndex` e `DomainNotIndexed`.

Input: nenhum.

Output:

```json
{
  "knowledgeRootPath": "C:/.../knowledge-repo",
  "indexDatabasePath": "C:/.../mcp/data/nero-knowledge.db",
  "projectCount": 42,
  "domainCount": 4,
  "healthyProjectCount": 39,
  "projectsWithIssuesCount": 3,
  "healthyDomainCount": 4,
  "domainsWithIssuesCount": 0,
  "elapsedMilliseconds": 191,
  "thresholdMilliseconds": 2000,
  "exceededThreshold": false,
  "recommendation": "Resolve the detailed project and domain issues; run nero_admin_reindex when filesystem/index drift is reported.",
  "projectsWithIssues": [
    {
      "name": "Acme.Example.Api",
      "issues": [
        {
          "type": "MissingContext",
          "path": "C:/.../knowledge/projects/Acme.Example.Api/context.md",
          "recommendation": "Run nero_register_project to create the missing project context.md."
        }
      ]
    }
  ],
  "domainsWithIssues": []
}
```

Os campos de performance usam o mesmo limiar configuravel de `nero_admin_check_index_consistency`: `AdminIndexConsistency:ThresholdMilliseconds` (default `2000`). `exceededThreshold` e diagnostico soft e nao falha a tool.

### Triage operacional

Quando `projectsWithIssuesCount > 0`:

1. Priorize issues `MissingRecentSnapshot` e `StaleSnapshot` — a `recommendation` do issue aponta `skills/nero/prompts/knowledge-review-app-mcp.txt`; carregue o playbook e rode um review por projeto.
2. Em seguida trate gaps estruturais (`MissingIndex`, `MissingContext`, `MissingBelongsToDomain`, `ProjectNotIndexed`, etc.).
3. Dominios em `domainsWithIssues` costumam ser estruturais; corrija `index.md` / reindex antes de promover notas de dominio.



## `nero_admin_git_status`

Retorna status Git read-only do repositorio que contem a knowledge root.
Nao executa fetch, pull, merge ou checkout.
Para evitar timeout em workspaces com muitos arquivos novos, a leitura rapida usa `git status --porcelain --untracked-files=no`; portanto `modifiedFiles` lista alteracoes rastreadas e nao inventario completo de untracked.
Comandos Git internos usam timeout operacional e prompt interativo desabilitado.

Input: nenhum.

Output:

```json
{
  "repositoryRoot": "C:/.../knowledge-repo",
  "branch": "main",
  "hasRemote": true,
  "remote": "origin",
  "upstream": "origin/main",
  "ahead": 0,
  "behind": 2,
  "localHead": "abc123",
  "remoteHead": "def456",
  "hasModifiedFiles": false,
  "modifiedFiles": []
}
```



## `nero_admin_git_fetch`

Executa `git fetch --prune <remote>` no remote configurado e retorna o resultado sem fazer pull, merge ou checkout.
Se o repositorio nao tiver remote, a tool bloqueia o fetch e retorna `success: false`.

Input: nenhum.

Output:

```json
{
  "success": true,
  "repositoryRoot": "C:/.../knowledge-repo",
  "remote": "origin",
  "message": "Git fetch completed without merge.",
  "output": null,
  "error": null
}
```



## `nero_admin_git_pull`

Executa `git pull --ff-only <remote> <branch>` no alvo resolvido.
Bloqueia se `KnowledgeWrite__Mode=read_only`, se o worktree tiver modificacoes locais **ou** untracked (`git status --porcelain`), ou se nao for possivel fast-forward (historicos divergentes — MCP nao faz merge/rebase).
Remote/branch opcionais: default = remote preferido (`origin` quando existir) e branch atual.
Nunca aceita force, rebase ou credenciais no input.

Input:

```json
{
  "remote": "origin",
  "branch": "main"
}
```

Output:

```json
{
  "success": true,
  "repositoryRoot": "C:/.../knowledge-repo",
  "remote": "origin",
  "branch": "main",
  "message": "Git pull completed with fast-forward only.",
  "output": null,
  "error": null
}
```



## `nero_admin_create_commit`

Cria commit controlado para `paths[]` explicitos.
Gates obrigatorios:

1. `KnowledgeWrite__Mode` diferente de `read_only`.
2. Paths normalizados (`/` ou `\`) dentro da allowlist dura do Knowledge Repo / docs/data configurados no MCP (rejeita absoluto, `..`, fora do prefixo).
3. Index limpo antes (`git diff --cached --name-only` vazio).
4. `git add -- <paths>` e verificacao de que o staged set e **exatamente** `paths[]`.
5. Scan de compliance no `git diff --cached` — **qualquer** hit Blocking **ou** Warning falha com `Category: Compliance` / `Field: stagedDiff` / `RuleId`; em falha o MCP faz `git reset -- <paths>` para nao deixar index sujo.
6. `git commit -m <message>` sem `--no-verify`, sem `--amend`, sem force.

Input:

```json
{
  "message": "docs: update knowledge backlog",
  "paths": [
    "docs/mcp-backlog.md",
    "global/index.md"
  ]
}
```

Output:

```json
{
  "success": true,
  "repositoryRoot": "C:/.../knowledge-repo",
  "commitSha": "abc123",
  "paths": [
    "docs/mcp-backlog.md",
    "global/index.md"
  ],
  "message": "Git commit created for allowlisted paths.",
  "output": null,
  "error": null
}
```

Em REJECT de compliance: sanitize o diff (placeholders da allowlist), unstage ja foi feito pela tool, e retente.



## `nero_admin_git_push`

Push sem force do remote/branch resolvidos.
Exige `confirm: true` **e** `confirmPhrase` exatamente `PUSH <remote> <branch>` (case-sensitive; remote/branch sao os nomes resolvidos apos defaults).
Bloqueado em `read_only`. Credenciais apenas ambiente/SSH — nao existem campos de token/password no schema.
URLs com userinfo em output/erro sao sanitizadas (`[REDACTED]`).

Input:

```json
{
  "confirm": true,
  "confirmPhrase": "PUSH origin main",
  "remote": "origin",
  "branch": "main"
}
```

Output:

```json
{
  "success": true,
  "repositoryRoot": "C:/.../knowledge-repo",
  "remote": "origin",
  "branch": "main",
  "message": "Git push completed without force.",
  "output": null,
  "error": null
}
```

