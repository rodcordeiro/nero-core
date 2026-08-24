# Backlog Nero

## Objetivo

Nascer o Nero como motor imparcial (Core + Kit), com Corpus fora do repositório, Clean Genesis no git e superfície compartilhavel sem material de origem corporativa.

Decisões: `CONTEXT.md`, `docs/adr/0001`–`0006`.

## Estado atual

- **Fase 0** em vigor (freeze: sem remote de share; sem commit até Fases 1–6).
- **Fases 1–6** — ACCEPT (ver tabela Gauntlet).
- **Fase 7 Clean Genesis** — primeiro commit local feito; remote privado OK; CI ainda aberto.
- **Fase 8** — backlog de primitivos (confiança, captura, evidência, drift); Packs fora do canônico.
- Glossário e ADRs de fronteira gravados (texto sem marca de origem).
- Checklist de higiene local verde.

## Premissas

- Produto = Core (MCP + `$nero`) + Kit (guidelines/references/prompts genéricos).
- Corpus = Knowledge Repo separado por pessoa; scaffold vazio em `examples/knowledge-scaffold/`.
- Schema de knowledge fixo e versionado no Nero.
- Domain Skills só documentadas; nunca incluídas no canônico.
- Rename total (`nero_*`, fixtures fictícios); divergência total vs knowledge-base de origem.
- Distribuição inicial: repo privado + amigos; LICENSE MIT no 1º commit.
- Sem commit/share até checklist de higiene (Fase 6). Remote privado e CI = pós-commit.

## Definition of Done (produto v0)

- [x] Tree sem corpus de origem, skills de produto, pipeline Azure ou feeds corporativos.
- [x] MCP e skill `$nero` buildam/testam apontando ao scaffold.
- [x] Kit genérico (guidelines + prompts de extrair projeto / gerar instruções de agente).
- [x] `README` + `INSTRUCTIONS` + `LICENSE` (MIT) coerentes com o modelo.
- [x] Checklist de higiene verde (Fase 6).
- [x] Primeiro commit = apenas Core + Kit + Scaffold + docs de produto (Clean Genesis).

---

## Backlog ativo

### Fase 0 — Freeze

- [x] Modelo acordado (grilling + ADRs).
- [x] Não criar remote de share.
- [x] Não commitar enquanto Fases 1–6 estiverem abertas.

### Fase 1 — Purge

- [x] Apagar corpus Markdown de origem sob a skill operacional.
- [x] Apagar skills de produto (auth, webhook, components mobile/web).
- [x] Apagar DBs/índices e artefatos de publish herdados.
- [x] Remover `.azuredevops/` (pipeline + auth de feed corporativo).
- [x] Remover docs operacionais herdados sob `docs/` — manter só `docs/adr/` e este backlog.
- [x] Apagar playbooks de produto (ex. telemetria/HTTP específica de empregador).

**DoD:** nenhum path de domínio de origem no tree; docs de produto antigos fora.  
**Gauntlet:** ACCEPT — [Nyx](5fbc22d5-e7e1-449c-bba6-71965c03d83e).

### Fase 2 — Rename Core

- [x] Renomear skill operacional → `skills/nero`.
- [x] Renomear solution/projetos/namespaces → `Nero.Knowledge.Base.*`.
- [x] Renomear tools MCP → `nero_*`.
- [x] Atualizar env examples para Knowledge Repo externo + `nero-knowledge.db`.
- [x] Fixtures de teste → nomes fictícios (`Acme.Api`, etc.).
- [x] `dotnet restore` / `build` / `test` verdes com scaffold.

**DoD:** build/test passam; zero identificador de marca de origem no MCP/skill.  
**Gauntlet:** ACCEPT — [Nyx](1e068456-562e-4c5d-b6db-1d0d6d0176f2).

### Fase 3 — Kit (genericização forte)

- [x] Guidelines `{api,front,mobile}` sem marca/libs/org.
- [x] Prompts sanitizados (`agents-md-references`, `knowledge-review-app-mcp`, `wiki-ingest-mcp`).
- [x] `prompts/index.md` + references atualizados.
- [x] `references/domain-skills.md` (extensão fora do canônico).
- [x] Sem referências a packages/skills de design system de empregador.

**DoD:** Kit neutro; playbooks utilizáveis.  
**Gauntlet:** ACCEPT — [Nyx](2ef2653b-1187-46fc-ba63-44188abe82bd).

### Fase 4 — Knowledge Scaffold

- [x] `examples/knowledge-scaffold/` (`global/`, `domains/`, `projects/`, índices).
- [x] README: copiar → Knowledge Repo + ligar MCP.
- [x] CLI `validate` passa no scaffold (`Validated 4 nodes and 0 edges.`, exit 0).
- [x] `.gitignore` no scaffold (`.nero/`, `*.db`).

**DoD:** amigo cria Knowledge Repo só copiando o scaffold.  
**Gauntlet:** ACCEPT-WITH-GAPS — [Nyx](bedbc263-f0fe-44d7-a4e2-f089b971f32d) (gap `.gitignore` corrigido após critic).

### Fase 5 — Docs de produto

- [x] `README.md` Nero (MCP, Knowledge Repo externo, skill).
- [x] `INSTRUCTIONS.md` bootstrap sem setup de origem corporativa.
- [x] `LICENSE` (MIT).
- [x] `docs/backlog.md` + `CONTEXT.md` / ADRs coerentes (sem corpus; sem sync com origem).

**DoD:** clone → publish → config → `$nero`.  
**Gauntlet:** ACCEPT — [Nyx](c3472920-34e3-447e-9eae-44f9a82a0fb6).

### Fase 6 — Checklist de higiene

Antes de qualquer commit/share:

- [x] Full-repo brand scan returns **zero** matches for known origin tokens (do not re-embed those tokens in docs; critic re-runs the scan).
- [x] Sem feeds NuGet privados / autenticação de feed corporativo / orgs Azure DevOps de origem.
- [x] Sem nomes de projetos reais de empregador em testes, docs ou exemplos.
- [x] SQLite e publish paths sem marca de origem; DBs gitignored.
- [x] Skills de produto ausentes; Corpus ausente do canônico.

**DoD:** audit local verde.  
**Gauntlet:** ACCEPT — [Nyx](13ef56c0-8dee-4d67-8633-981fecceb819).

### Fase 7 — Clean Genesis

- [x] Primeiro commit = tree limpo (Core + Kit + Scaffold + docs).
- [x] Remote **privado** (`origin` → `nero-core`); convidar amigos.
- [ ] (Depois) CI mínimo (ex. GitHub Actions) — fora do dia 1.

**DoD:** histórico sem blob de origem; MIT no commit inicial.

---

## Fase 8 — Primitivos de confiança e captura

Independência: esta fase **não** implementa Packs. People CRM e Content Factory são produtos complementares fora do canônico (ADR 0006). O Core deve continuar build/test/share com zero Packs instalados. Packs só passam a consumir estes primitivos depois que existirem.

Decisões: `CONTEXT.md` (Pack), `docs/adr/0006`.

### P0 — Trust e evidência

- [ ] Trust audit somente leitura (`nero_admin_trust_audit` e/ou playbook report-only): fontes ausentes, notas nunca verificadas, snapshots vencidos, claims não verificáveis, candidatos a arquivo. Não corrigir nem arquivar automaticamente.
- [ ] Finalização de lote com evidência: ciclo `write → compliance → reindex → validate → evidências`, retornando arquivos, validade, compliance, indexação, falhas parciais e próximo passo. O escritor não valida o próprio resultado.

**DoD P0:** fixture de Knowledge Repo de teste; zero escrita no audit; relatório reproduzível; lote pequeno de register passa pelo ciclo completo com evidência independente.

### P1 — Captura, proveniência e drift

- [ ] Capture Zone (Inbox/Draft) fora do grafo canônico, com promoção humana explícita para projeto → domínio → global.
- [ ] Metadados de confiança opt-in (`sources`, `last_verified`, `verification_status`, `confidence`) sem quebrar corpus antigo. Distinguir `verified`, `unverifiable`, `stale`, `contradicted`.
- [ ] Validador de drift entre tools MCP, `$nero`, `mcp-tools.md`, prompts, manifests, scaffold, schema e versões Core/Kit.

**DoD P1:** corpus antigo reindexa e valida igual; promoção exige aceite humano; detector de drift é report-only com fixture divergente.

### P2 — Superfície MCP derivada

- [ ] Resources read-only e prompts versionados para captura/revisão; tools só para operações com efeito.
- [ ] Sugestão de links em preview (persistir só após aceite). Avaliar indexação incremental por arquivo. Embeddings, se existirem, só como índice derivado.

**DoD P2:** nenhum resource mutável; preview não grava grafo; embeddings ausentes ou desligáveis.

### Experimentos reversíveis

1. Trust audit em fixture; zero escrita.
2. Frontmatter de confiança opt-in; corpus antigo inalterado no validate.
3. Checklist critério → evidência numa mudança pequena; comparar defeitos e custo com baseline.
4. Detector read-only de drift com fixture divergente.
5. Dry-run de atualização do Kit em cópia temporária (customização local, untracked, symlink/reparse, conflito, interrupção).

Começar pelo experimento 1 e só então decidir se o audit vira tool MCP, playbook do Kit, ou ambos.

### Fora desta fase (Packs e corpus pessoal)

- Implementar People CRM ou Content Factory no `nero-core`.
- Novo `KnowledgeNodeType` (`Person`, editorial, daily).
- Publicação autônoma, screenshot de rede social, Slack/Linear/PostHog.
- Promoção automática de inferências para decisão/padrão/regra.
- Dependência obrigatória de Bash, Python, Claude ou cron.

Packs consomem P0/P1 quando existirem; o atraso ou ausência dos Packs **não** bloqueia a Fase 8.

**Gauntlet:** sob pedido explícito, por fatia P0/P1/P2 — não pelo bloco inteiro.

## Fora do escopo (agora)

- Sync/cherry-pick com knowledge-base de trabalho.
- Publicar repo público (só após audit explícito pós-Fase 7).
- Domain Skills ou Packs de produto dentro do Nero (People CRM, Content Factory, dailies, role packs).
- Pipeline Azure DevOps.
- Evolução de marcos MCP legados do corpus corporativo.
- CLI gerador de knowledge (pode voltar depois do scaffold).

## Ordem recomendada

`0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 (P0 → P1 → P2)`

## Gauntlet loop

Para **cada fase** (1–6; 7 só com pedido explícito de commit; 8 por fatia P0/P1/P2 sob pedido):

1. **Builder** implementa só o DoD da fase.
2. **Critic (Nyx)** com **contexto limpo**.
3. `ACCEPT` | `ACCEPT-WITH-GAPS` | `REJECT`.
4. Em `REJECT`: corrigir → novo critic.
5. Só então avança.
6. Registrar veredito na tabela.

| Fase | Status | Builder | Critic | Gaps |
|------|--------|---------|--------|------|
| 0 Freeze | done | Supervisor | — | política contínua |
| 1 Purge | ACCEPT | Supervisor | [Nyx](5fbc22d5-e7e1-449c-bba6-71965c03d83e) | — |
| 2 Rename | ACCEPT | Supervisor | [Nyx](1e068456-562e-4c5d-b6db-1d0d6d0176f2) | — |
| 3 Kit | ACCEPT | Atlas | [Nyx](2ef2653b-1187-46fc-ba63-44188abe82bd) | mojibake cosmético mcp-tools |
| 4 Scaffold | ACCEPT-WITH-GAPS | Supervisor | [Nyx](bedbc263-f0fe-44d7-a4e2-f089b971f32d) | `.gitignore` pós-critic |
| 5 Docs | ACCEPT | Supervisor | [Nyx](c3472920-34e3-447e-9eae-44f9a82a0fb6) | — |
| 6 Higiene | ACCEPT | Supervisor | [Nyx](13ef56c0-8dee-4d67-8633-981fecceb819) | — |
| 7 Genesis | commit + remote privado | Supervisor | — | CI pendente |
| 8 Primitivos | backlog | — | — | Packs fora; começar E1 trust-audit |
