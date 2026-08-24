# Roteamento de conhecimento Nero

Camadas abaixo sao relativas ao Knowledge Repo (`KnowledgeRoot__Path`). Escolha de camada, promocao, estilo de nota, `Dono` e significado de `links:`.

Mapeamento campo-da-tool → edge: `mcp-tools.md`. Pos-lote: `compliance-security.md`.

## Camada

Done when: cada nota planejada tem `escopo` (`global` | `domain` | `project`) e o path correspondente.

| Camada    | Quando                                                                                                                       | Path                  |
| --------- | ---------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| `global`  | Vale para o ecossistema: convencoes, autenticacao compartilhada, observabilidade, seguranca, pipelines, integracoes centrais | `global/`             |
| `domain`  | Vale para uma familia (`api`, `front`, `mobile`, `integracoes`, `powershell`, `mcp`)                                         | `domains/<dominio>/`  |
| `project` | Depende do fluxo, contrato, tela, banco ou historico daquele app (ex.: `Acme.Api`)                                           | `projects/<projeto>/` |

Camada mais especifica. Dominio primario do repo em `belongs_to_domain`; `integracoes` como segundo dominio so para API/barramento central.

## Precedente

Done when: dominio + projetos irmaos foram buscados, e a nota nova ou reusa o padrao ou registra a diferenca com evidencia.

Antes de solucao nova para regra, validacao, teste, erro recorrente ou integracao: buscar no dominio, depois em irmaos; reaproveitar quando equivalente; registrar so o desvio local.

## Promocao

Done when: a promocao cita evidencia de reuso (outro projeto ou outro dominio), ou a nota permanece na camada atual.

Projeto → dominio quando a regra serve a outro projeto do mesmo dominio, evita incidente recorrente, reduz retrabalho, ou afeta teste/validacao/integracao/deploy.

Dominio → global quando afeta mais de um dominio, ou e convencao ampla (seguranca, autenticacao, dados sensiveis, observabilidade, pipeline).

## Estilo

Done when: a nota separa regra de evidencia, cita origem/escopo, e inclui path ou comando quando a regra depende deles.

Notas curtas e acionaveis. Incluir exemplos e excecoes. Data de revisao quando houver risco de stale. Sintoma, causa e acao no lugar de log longo.

## Dono em decisions

Done when: `## Revisao` → `Dono` e o autor humano do commit que implementou (nome do `git log` / `Author`), ou ficou vazio de proposito.

`nero_register_decision` deixa `Dono` em branco; preencha na edicao logo apos o register, antes de reindex/commit. Sem e-mail (`pii_suspect`). Identidades de agente (Cursor, Codex, Claude, Copilot, GPT, Auto) ficam de fora; se o commit for de agente, use o humano solicitante conhecido (ticket, PR, pedido) ou deixe vazio.

## Relacoes (`links:`)

Done when: cada nota de conteudo tem `links:` nao vazio no vocabulario abaixo, com alvo direto e comprovado.

Writers `nero_register_*` emitem este vocabulario. Markdown e canonico; SQLite deriva edges no reindex.

| Relacao             | Uso                                                                                                                     |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `belongs_to_domain` | Projeto/nota → dominio primario                                                                                         |
| `documents`         | Nota documenta/contextualiza um alvo                                                                                    |
| `evidences`         | Snapshot → nota concreta com slug (hubs `index`, `context`, `domains/*/patterns`, `projects/*/decisions` ficam de fora) |
| `updates`           | Evolucao entre notas                                                                                                    |
| `depends_on`        | Dependencia; consumer → backend                                                                                         |
| `uses_backend`      | Front/Mobile → API/lib                                                                                                  |
| `related_decision`  | Ligacao a uma decision                                                                                                  |
| `related_pattern`   | Ligacao a um pattern                                                                                                    |
| `source_for`        | Pattern/fonte → consumidores (`usadoPor`)                                                                               |
| `supersedes`        | **Somente** decision → decision; o split vigente/historico de `nero_get_project_context` depende dela                   |

`updates` e evolucao; `supersedes` substitui orientacao. `nero_admin_validate` rejeita legado (`relates_to`, `caused_by`, `used_by`, `candidate_for_reuse`) e qualquer tipo fora da tabela.

Relacao so com evidencia semantica direta (alvo nomeado, nao pasta/data/coocorrencia).

## Checklist do grafo

Done when: os quatro itens abaixo sao verdadeiros.

- alvos de `links:` resolvem;
- sem relacao duplicada no mesmo arquivo;
- sem `depends_on` para o proprio projeto;
- `git diff --check` no Knowledge Repo (ou `nero_admin_git_status`).
