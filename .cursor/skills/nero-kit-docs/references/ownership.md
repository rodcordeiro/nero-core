# SoT map — Nero kit agent docs

Load this when classifying files in `skills/nero/prompts/` or `skills/nero/references/` (step 1 of the skill). One job per file. Duplicate meaning → ponteiro to the owner, or delete.

## References

| File | Owns |
|---|---|
| `workflow.md` | Ordem (health → context → implementar/lote) e hibrido MCP + checkout |
| `knowledge-routing.md` | Camada, promocao, `Dono`, significado de `links:` |
| `compliance-security.md` | Checklist pos-lote, anti-leak, sanitizacao |
| `mcp-tools.md` | Inputs/outputs por tool, campo→edge, git via MCP, indice no topo |
| `git-merging.md` | Tres ramos de conflito (snapshot / regra de negocio / codigo) |
| `domain-skills.md` | Padrao de extensao fora do canonico |
| `guidelines/<dominio>.md` | Regras daquele dominio (`integracoes` herda `api`) |

`prompts/index.md` e router: um trigger por playbook. A recommendation MCP aponta o path; o roteiro vive no arquivo.

## Playbooks

| File | Unique in-file | Disclose |
|---|---|---|
| `knowledge-review-app-mcp.txt` | Review da app, achado→tool, anti-dup, duas entradas (health vs indice) | Campos, vocab `links:`, validate/commit |
| `wiki-ingest-mcp.txt` | Intervalo wiki, classificar tipo/escopo, origem sanitizada | Mesmo contrato MCP |
| `agents-md-references.txt` | Classificar dominio, uma tabela de guidelines, arvore AGENTS, skills condicionais | Register/reindex (opcional) |

Dominios no Kit: `api`, `front`, `mobile`, `integracoes`, `powershell`, `mcp`.

## Stable paths

MCP `recommendation` for stale snapshot (do not rename):

`skills/nero/prompts/knowledge-review-app-mcp.txt`

C# constant: `AdminKnowledgeMaintenanceService.KnowledgeReviewPromptPath`.
