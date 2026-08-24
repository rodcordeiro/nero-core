# Fluxo operacional Nero

Use esta referencia para a ordem de consulta, health, hibrido MCP+filesystem e escrita em **lote** no Knowledge Repo. Tools e payloads: `mcp-tools.md`. Camada/`links:`/Dono: `knowledge-routing.md`. Pos-lote: `compliance-security.md`.

## Ordem

### Health e bootstrap

Done when: `nero_admin_project_health` (ou `ecosystem_health` na fila) rodou, o playbook de snapshot stale foi executado se a `recommendation` apontou path, e a estrutura base do projeto existe.

1. Identificar projeto e dominio (`api`, `front`, `mobile`, `integracoes`, `powershell`, `mcp`).
2. `nero_admin_project_health` (`project`, `primaryDomain`): inspecionar `hasRecentSnapshot`, idade e `issues`.
3. Snapshot stale/ausente (`MissingRecentSnapshot` / `StaleSnapshot`): carregar o **playbook** no path da `recommendation` (`prompts/`) e executar — a recommendation aponta o path.
4. Fila multi-projeto: `nero_admin_ecosystem_health`; um playbook por projeto, frescor antes de gaps estruturais.
5. Estrutura base ausente: `nero_register_project` (MCP) ou `projects/<projeto>/index.md` + `context.md` (Schema) **antes** de outras notas do projeto.

### Contexto

Done when: `activeDecisions` vigentes (ou `context.md`) foram lidas, e precedente de dominio/irmaos/global foi buscado quando a tarefa pede regra, validacao, integracao ou incidente.

1. `nero_get_project_context` — `activeDecisions` **vigentes**; `supersededDecisions` historico. Sem MCP: ler `context.md`.
2. `nero_get_domain_context` quando a tarefa for de dominio. Sem MCP: ler o indice do dominio.
3. `global/index.md` quando a tarefa for convencao ampla, seguranca, autenticacao, observabilidade ou pipeline.
4. `nero_search_knowledge` e `nero_find_related_knowledge` (e `links:` nas notas) para precedente; projetos irmaos quando a regra for de negocio, validacao, integracao ou incidente.

### Implementar e lote

Done when: a mudanca de codigo (se houver) cita path no checkout, e o lote de knowledge — se houve escrita — passou o checklist em `compliance-security.md`.

 1. Implementar a menor mudanca verificavel. Codigo de produto: filesystem obrigatorio.
 2. Conhecimento reutilizavel: `nero_register_*` no fim, **serial**. Sem MCP: templates do Schema.

## Hibrido MCP + checkout

Done when: cada conclusao de codigo cita path no checkout, e cada nota de knowledge passou por writer MCP (ou fallback documentado).

MCP primeiro para search, contexto, grafo e registro. Checkout primeiro para inventario, rotas, DI, contratos, config, pipeline e comportamento efetivo.

- Index/context/inventory: `nero_update_project_*` apos bootstrap. Contrato `linksSemanticos`: `mcp-tools.md`.
- Dominio: `nero_register_domain` / `nero_update_domain` / `nero_inactivate_domain`.
- Markdown direto no Knowledge Repo so se o MCP estiver indisponivel, falhar, ou a nota vigente precisar de edicao pontual.
- MCP vs filesystem divergente → desalinhamento de indice; confirme nos arquivos e rode reindex + validate.

Writers gravam Markdown. Reindex exclusivo ao fim do lote: `compliance-security.md`.
