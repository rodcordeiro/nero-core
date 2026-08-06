# Fluxo operacional Nero

Use esta referencia quando a tarefa exigir ordem de consulta, health check, hibrido MCP+filesystem ou escrita em lote no Knowledge Repo.

O corpus canônico vive no **Knowledge Repo** apontado por `KnowledgeRoot__Path` (scaffold de exemplo: `examples/knowledge-scaffold/`). O path `skills/nero/knowledge/` **nao** e canonico no produto Nero.

## Antes de implementar

1. Identificar projeto e dominio da tarefa.
2. Consultar o MCP `nero-knowledge-base` quando disponivel: comecar por `nero_admin_project_health` (frescor/estrutura), depois contexto de projeto/dominio, busca textual e grafo relacionado.
3. Se `hasRecentSnapshot=false`, executar o playbook indicado na `recommendation` (arquivo em `prompts/`) antes de confiar em knowledge antigo.
4. Se o projeto for novo ou nao existir no knowledge, registrar a estrutura minima com `nero_register_project` quando o MCP estiver disponivel; caso contrario, criar `projects/<projeto>/index.md` e `context.md` no Knowledge Repo seguindo o Schema antes de registrar notas do projeto.
5. Ler o contexto do projeto quando existir e o MCP nao estiver disponivel ou for insuficiente.
6. Ler o indice do dominio relacionado quando o MCP nao estiver disponivel ou for insuficiente.
7. Consultar `global/index.md` no Knowledge Repo quando a tarefa envolver convencao ampla, seguranca, arquitetura, autenticacao, observabilidade ou pipeline.
8. Usar o frontmatter `links:` das notas como mapa de navegacao semantica para encontrar decisoes, padroes, evidencias, dominios e projetos diretamente relacionados.
9. Procurar precedentes em projetos irmaos quando a tarefa envolver regra de negocio, validacao, offline, integracao ou incidente recorrente.
10. Implementar a menor mudanca verificavel.
11. Se surgir conhecimento reutilizavel, registrar via MCP quando disponivel, priorizando a tool de registro correspondente ao tipo de nota; caso contrario, registrar no Knowledge Repo usando os templates do Schema. Escritas no knowledge em serie. Apos o lote, executar `nero_admin_reindex` e `nero_admin_validate`.

## Fluxo recomendado com MCP

1. Use `nero_admin_project_health` com `project` e `primaryDomain` quando a tarefa envolver um projeto especifico. Inspecione `hasRecentSnapshot`, idade do snapshot e `issues`.
2. Se `hasRecentSnapshot=false` (issues `MissingRecentSnapshot` / `StaleSnapshot`), leia o path relativo indicado na `recommendation` (playbook em `prompts/`) e execute esse fluxo — nao espere o texto do prompt embutido na recommendation.
3. Em triage multi-projeto, use `nero_admin_ecosystem_health`; priorize escopos com `MissingRecentSnapshot` / `StaleSnapshot` e dispare o playbook por projeto.
4. Use `nero_get_project_context` para contexto consolidado; prefira `activeDecisions` (nao o bucket generico `decisions` quando houver `supersedes`).
5. Se o projeto for novo ou o health indicar ausencia de estrutura base, use `nero_register_project` antes de registrar decisions, patterns, regras, validacoes ou troubleshootings do projeto.
6. Use `nero_get_domain_context` quando a tarefa envolver um dominio como `api`, `front`, `mobile` ou `integracoes`.
7. Use `nero_search_knowledge` para busca textual ampla.
8. Use `nero_find_related_knowledge` antes de implementar regra nova ou reaproveitar precedente.
9. Para criar notas, use primeiro a tool `nero_register_*` correspondente (detalhe em `mcp-tools.md`). Writers apenas gravam Markdown; **nao** reindexam. Payloads com secrets/PII verificavel sao rejeitados com `Category: Compliance` (reject-only; placeholders da allowlist).
10. Depois de concluir o lote de escritas (register ou edicao manual), o **cliente** deve chamar `nero_admin_reindex` uma vez e em seguida `nero_admin_validate` antes de confiar em search/grafo/contexto ou de commitar. Exija `isValid=true` **e** `isCompliant=true`. Para triage de corpus, use `nero_admin_compliance_scan`.
11. Recorra aos templates do Schema/scaffold somente como fallback quando o MCP nao puder criar a nota corretamente ou ainda nao houver tool de registro para aquele tipo de nota.

Analise de checkouts de produto pode ser paralela; **escrita no knowledge** (`register_*`, `nero_update_project_*`, edicao de Markdown) deve ser **serial** no fim do lote, para evitar corrida no filesystem/indice.

## Hibrido MCP + filesystem

Use o MCP como camada preferencial para busca, contexto consolidado, grafo, registro controlado e reindexacao **sob demanda do cliente**.

Nao use o MCP como unica fonte de verdade quando a tarefa exigir validar implementacao real no checkout local. Para inventario tecnico, rotas, DI, `.csproj`, migrations, contratos publicos, configuracoes, Dockerfile, pipelines, riscos operacionais e comportamento efetivo do codigo, leia os arquivos diretamente no repositorio.

- Revisar codigo de produto → leitura via filesystem e **obrigatoria**.
- Evoluir `index.md` / `context.md` / `inventory.md` do knowledge → preferir `nero_update_project_index|context|inventory` apos bootstrap com `nero_register_project`. Em `linksSemanticos`: omitir **preserva** links nao-minimos; lista explicita substitui; `[]` limpa. Nunca depender so de `nero_link_knowledge` para durar apos reindex.
- Lifecycle de dominio → preferir `nero_register_domain` / `nero_update_domain` / `nero_inactivate_domain`. Em `nero_update_domain`, omitir `sourceFor` **preserva** os `source_for` existentes; so altere links passando lista explicita (ou `[]` para limpar de proposito).
- Filesystem permanece para codigo de produto e para edicao manual so se o MCP estiver indisponivel.

Se houver divergencia entre o MCP e o filesystem, trate como desalinhamento de indice ou checkout. Valide o estado real nos arquivos antes de concluir e rode `nero_admin_reindex` + `nero_admin_validate` apos qualquer lote de escrita.

Ao ler decisoes, prefira `activeDecisions`. Considere relacoes `supersedes`: `supersededDecisions` e `supersededBy` sao historico, nao orientacao vigente, salvo evidencia contraria.

## Quando priorizar MCP

Priorize as tools MCP antes de ler arquivos do Knowledge Repo quando a tarefa pedir:

- busca de precedente, padrao, regra, decisao ou troubleshooting;
- contexto consolidado de projeto ou dominio;
- relacoes de grafo entre conhecimentos;
- registro controlado de projeto, regra de negocio, decisao, padrao, validacao, teste, troubleshooting ou incidente.

Para criacao de notas, prefira sempre as tools `nero_register_*` quando disponiveis. Escreva Markdown diretamente no Knowledge Repo apenas quando o MCP estiver indisponivel, falhar, nao tiver tool compativel com o tipo de nota ou quando for necessario corrigir manualmente uma nota existente.

Detalhes de tools, inputs e payloads: `mcp-tools.md`.
Checklist pos-register: `compliance-security.md`.
Domain Skills fora do canônico: `domain-skills.md`.
