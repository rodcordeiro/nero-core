# Playbooks Nero

Quando uma tool MCP retornar `recommendation` com path de playbook, carregue o arquivo relativo ao repo da skill (`skills/nero/prompts/...`) e execute o fluxo — a recommendation aponta o path; o roteiro vive no arquivo.

| Arquivo | Quando carregar |
|---|---|
| `knowledge-review-app-mcp.txt` | Review de aplicacao e Knowledge Repo (frescor, drift, inventario); `recommendation` de `nero_admin_project_health` / `nero_admin_ecosystem_health`. |
| `wiki-ingest-mcp.txt` | Ingestao de wiki/Markdown externo no Knowledge Repo. |
| `agents-md-references.txt` | Criar ou enxugar `AGENTS.md` + `.agents/references/` no checkout da app. |
