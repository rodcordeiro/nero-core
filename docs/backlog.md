# Backlog Nero

> **Canônico:** [GitHub Issues](https://github.com/rodcordeiro/nero-core/issues) no [Nero Scrum board](https://github.com/users/rodcordeiro/projects/14).  
> Este arquivo é o seed histórico (genesis + Fase 8). Agentes: seguir `docs/agents/iteration-workflow.md` — não tratar esta página como tracker vivo.

## Objetivo

Nascer o Nero como motor imparcial (Core + Kit), com Corpus fora do repositório, Clean Genesis no git e superfície compartilhavel sem material de origem corporativa.

Decisões: `CONTEXT.md`, `docs/adr/0001`–`0008`.

## Mapa seed → issues (migrate 2026-08-26)

| Fatia | Issue | Estado |
| --- | --- | --- |
| Fase 0 — Freeze | [#1](https://github.com/rodcordeiro/nero-core/issues/1) | closed |
| Fase 1 — Purge | [#2](https://github.com/rodcordeiro/nero-core/issues/2) | closed |
| Fase 2 — Rename Core | [#3](https://github.com/rodcordeiro/nero-core/issues/3) | closed |
| Fase 3 — Kit | [#5](https://github.com/rodcordeiro/nero-core/issues/5) | closed |
| Fase 4 — Knowledge Scaffold | [#4](https://github.com/rodcordeiro/nero-core/issues/4) | closed |
| Fase 5 — Docs de produto | [#6](https://github.com/rodcordeiro/nero-core/issues/6) | closed |
| Fase 6 — Checklist de higiene | [#8](https://github.com/rodcordeiro/nero-core/issues/8) | closed |
| Fase 7 — Clean Genesis | [#7](https://github.com/rodcordeiro/nero-core/issues/7) | closed |
| Fase 7b — CI mínimo | [#10](https://github.com/rodcordeiro/nero-core/issues/10) | **open** (next) |
| Fase 8 P0 — Trust e evidência | [#9](https://github.com/rodcordeiro/nero-core/issues/9) | closed |
| Fase 8 P1 — Captura, proveniência e drift | [#12](https://github.com/rodcordeiro/nero-core/issues/12) | **open** (next) |
| Fase 8 P2 — Superfície MCP derivada | [#11](https://github.com/rodcordeiro/nero-core/issues/11) | **open** (next) |

Hub de coordenação: este repo. Packs (ex. `nero-code-graph`) mantêm issues no próprio repo, no mesmo board #14.

## Estado atual (resumo)

- **Fases 0–7** (exceto CI) e **8 P0** — concluídas (issues fechadas).
- **Aberto:** CI (#10), P1 (#12), P2 (#11) — iteration **next** no board.
- Glossário e ADRs de fronteira gravados.
- Checklist de higiene local verde.

## Premissas

- Produto = Core (MCP + `$nero`) + Kit (guidelines/references/prompts genéricos).
- Corpus = Knowledge Repo separado por pessoa; scaffold vazio em `examples/knowledge-scaffold/`.
- Schema de knowledge fixo e versionado no Nero.
- Domain Skills só documentadas; nunca incluídas no canônico.
- Rename total (`nero_*`, fixtures fictícios); divergência total vs knowledge-base de origem.
- Distribuição inicial: repo privado + amigos; LICENSE MIT no 1º commit.

## Definition of Done (produto v0)

- [x] Tree sem corpus de origem, skills de produto, pipeline Azure ou feeds corporativos.
- [x] MCP e skill `$nero` buildam/testam apontando ao scaffold.
- [x] Kit genérico (guidelines + prompts de extrair projeto / gerar instruções de agente).
- [x] `README` + `INSTRUCTIONS` + `LICENSE` (MIT) coerentes com o modelo.
- [x] Checklist de higiene verde (Fase 6).
- [x] Primeiro commit = apenas Core + Kit + Scaffold + docs de produto (Clean Genesis).

---

## Histórico por fase (seed)

Detalhe completo das checklists permanece nas issues fechadas. Resumo:

### Fases 0–7

Gauntlet ACCEPT (com gaps só na Fase 4 `.gitignore`, corrigido). Genesis: commit + remote privado; CI → #10.

### Fase 8 — Primitivos

- **P0** (#9 closed): trust audit + finalize batch (ADR 0007/0008).
- **P1** (#12 open): Capture Zone, metadados de confiança opt-in, detector de drift.
- **P2** (#11 open): resources/prompts read-only, link preview, embeddings opcionais.

Packs (People CRM, Content Factory) fora do canônico (ADR 0006).

## Fora do escopo (agora)

- Sync/cherry-pick com knowledge-base de trabalho.
- Publicar repo público (só após audit explícito pós-Fase 7).
- Domain Skills ou Packs de produto dentro do Nero.
- Pipeline Azure DevOps.
- CLI gerador de knowledge (pode voltar depois do scaffold).

## Gauntlet loop

Para fatias abertas (sob pedido): Builder → Critic (Nyx) → ACCEPT | ACCEPT-WITH-GAPS | REJECT. Registrar veredito na issue.
