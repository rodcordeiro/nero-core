# Playbooks Nero

Playbooks operacionais da skill `$nero`. Caminho no repositorio: `skills/nero/prompts/`.

Quando uma tool MCP (ex.: `nero_admin_project_health`, `nero_admin_ecosystem_health`) retornar `recommendation` com path de playbook, carregue o arquivo indicado e execute o fluxo — nao espere o texto do prompt embutido na recommendation.

| Arquivo | Uso |
|---|---|
| `knowledge-review-app-mcp.txt` | Revisar aplicacao e atualizar o Knowledge Repo via MCP (frescor, drift, inventario). |
| `wiki-ingest-mcp.txt` | Ingerir wiki/Markdown externo no Knowledge Repo com sanitizacao e anti-duplicacao. |
| `agents-md-references.txt` | Criar/atualizar `AGENTS.md` enxuto + `.agents/references/` por responsabilidade (api/front/mobile/integracoes/powershell/mcp). Apos classificar o dominio, ler e aplicar o guideline em `references/guidelines/` (`api`/`front`/`mobile`/`powershell`/`mcp`; `integracoes` herda api) sem copiar o texto para o app — so ponteiro + debito se o checkout divergir. Domain Skills: `references/domain-skills.md`. |

Guidelines de estruturacao (fonte canonica na skill):

| Dominio | Arquivo |
|---|---|
| `api` / `integracoes` | `references/guidelines/api-guidelines.md` |
| `front` | `references/guidelines/front-guidelines.md` |
| `mobile` | `references/guidelines/mobile-guidelines.md` |
| `powershell` | `references/guidelines/powershell-guidelines.md` |
| `mcp` | `references/guidelines/mcp-guidelines.md` |

Convencao: manter paths relativos ao root do repositorio da skill (`skills/nero/prompts/...`) nas recommendations do MCP.
