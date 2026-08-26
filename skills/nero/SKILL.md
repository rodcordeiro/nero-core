---
name: nero
description: >-
  Motor de knowledge base Nero (MCP + Schema). Use para consultar, relacionar e
  registrar conhecimento operacional (global → dominio → projeto). Perguntas
  estruturais de codigo (who-calls, imports, path) → skill `nero-code-graph`
  (`cg_*`), nao `nero_*` / `links:`.
---

# Nero

Oriente tarefas a partir de conhecimento compartilhado no Knowledge Repo (global → dominio → projeto).

## Fonte canonica

Fonte canonica: Knowledge Repo apontado por `KnowledgeRoot__Path` (scaffold de exemplo: `examples/knowledge-scaffold/`). O path `skills/nero/knowledge/` **nao** e canonico no produto Nero.

Prioridade:

1. `knowledge/global/` — regras e decisoes de todo o ecossistema.
2. `knowledge/domains/<dominio>/` — padroes por dominio (`api`, `front`, `mobile`, `integracoes`, `powershell`, `mcp`; novos via `nero_register_domain` quando justificado).
3. `knowledge/projects/<projeto>/` — contexto do projeto atual.

Conflito: prefira a camada mais especifica, exceto se contradisser regra global explicita.

Promocao: projeto → dominio quando orientar outro projeto do mesmo dominio; dominio → global quando afetar mais de um dominio ou for convencao ampla. Criterios: `references/knowledge-routing.md`.

## MCP (preferencial)

Quando o MCP `nero-knowledge-base` estiver disponivel, use-o para consultar, relacionar e registrar conhecimento. Markdown no Knowledge Repo e canonico; o SQLite e derivado.

Ordem tipica: `nero_admin_project_health` → (playbook se snapshot stale) → `nero_get_project_context` / dominio / search / related → `nero_register_*` → `nero_admin_reindex` + `nero_admin_validate` (`isValid` e `isCompliant`).

Writers gravam Markdown e **nao** reindexam. Escritas no knowledge em **serie** no fim do lote. Codigo de produto: leia o checkout (filesystem obrigatorio); knowledge: preferir MCP. Em `nero_update_project_*`, omitir `linksSemanticos` preserva links nao-minimos; lista substitui; `[]` limpa.

Fluxo completo e hibrido MCP+filesystem: `references/workflow.md`.
Tools, contratos e payloads: `references/mcp-tools.md`.
Compliance/security pos-register: `references/compliance-security.md`.

### Packs complementares

Routing estrutural vs knowledge: `references/domain-skills.md` (instalar, listar, quando acionar).

Perguntas **estruturais** do checkout → skill **`nero-code-graph`** e MCP `nero-code-graph` (`cg_*`). **Nao** use `nero_find_related_knowledge` / `links:` para arestas AST.

| Tipo | Superficie |
| --- | --- |
| Estrutura (`calls`, `imports`, `file:line`) | `cg_*` |
| Ops (decisao, regra, troubleshooting, contexto) | `nero_*` |
| Corpo de arquivo / WIP | filesystem |

## Playbooks

Playbooks em `prompts/` (indice: `prompts/index.md`). Se uma tool retornar `recommendation` com path de playbook, carregue o arquivo relativo ao repo da skill e execute-o.

## Tipos de registro

Preferir `nero_register_*` para: regra de negocio, decisao, padrao, validacao/teste, snapshot, troubleshooting/incidente.

Em **decision**, o `Dono` (`## Revisao`) e o autor humano do commit que implementou — nunca agente; sem e-mail. `nero_register_decision` deixa vazio: preencha apos o register. Detalhe: `references/knowledge-routing.md`.

Templates do Schema/scaffold so como fallback.

## Referencias

| Arquivo | Quando ler |
| --- | --- |
| `references/workflow.md` | Ordem antes de implementar, health, hibrido MCP+FS |
| `references/mcp-tools.md` | Nomes de tools, inputs, reindex, git via MCP |
| `references/compliance-security.md` | Checklist pos-register, anti-leak, commit/push |
| `references/knowledge-routing.md` | Camada, promocao, `links:`, Dono, checklist grafo |
| `references/git-merging.md` | Conflitos de merge no Knowledge Repo |
| `references/guidelines/` | Estruturacao por dominio (`api`/`front`/`mobile`/`powershell`/`mcp`; `integracoes` herda api) |
| `references/domain-skills.md` | Domain Skills e Packs **fora** do Nero; instalar/listar/routing |
| Skill `nero-code-graph` | Who-calls / imports / path / freshness / `cg_*` vs Nero |
| `prompts/index.md` | Indice de playbooks |

## Domain Skills

Skills de produto, lib ou organizacao **nao** fazem parte do canonico Nero. Implemente-as e acione-as fora deste repositorio.

Guia: `references/domain-skills.md`.
