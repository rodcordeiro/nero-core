# Roteamento de conhecimento Nero

O corpus canônico vive no **Knowledge Repo** (`KnowledgeRoot__Path`). Camadas abaixo são relativas a esse root.

## Escolha da camada

Use `global` quando a informacao valer para todo o ecossistema documentado: convencoes, autenticacao compartilhada, observabilidade, seguranca, pipelines, integracoes centrais e politicas de compatibilidade.

Use `domains/<dominio>` quando a informacao valer para uma familia de projetos: APIs .NET/NestJS, apps mobile Expo/React Native, fronts web, integracoes, dados ou pipelines.

Use `projects/<projeto>` quando a informacao depender do fluxo, contrato, backend, tela, banco local, pipeline ou historico daquele projeto (ex.: `Acme.Api`).

## Busca por precedentes

Antes de criar uma solucao nova para regra de negocio, validacao, teste, erro recorrente ou integracao:

1. Buscar no dominio relacionado.
2. Buscar em projetos irmaos do mesmo dominio.
3. Reaproveitar o padrao quando a regra for equivalente.
4. Registrar diferencas quando o projeto atual exigir comportamento proprio.

## Criterio de promocao

Promover projeto -> dominio quando:

- a regra pode ser aplicada em outro projeto do mesmo dominio;
- a causa raiz evita incidente recorrente;
- o padrao reduz retrabalho em novas implementacoes;
- a decisao afeta convencoes de teste, validacao, integracao ou deploy.

Promover dominio -> global quando:

- a decisao afeta mais de um dominio;
- a regra envolve seguranca, autenticacao, dados sensiveis, observabilidade ou pipeline compartilhado;
- o conhecimento define convencao ampla do ecossistema.

## Estilo das notas

- Ser curto e acionavel.
- Separar regra de evidencia.
- Incluir origem, escopo, exemplos e excecoes.
- Incluir arquivos, comandos ou endpoints quando forem relevantes.
- Registrar data de revisao quando houver risco de desatualizacao.
- Evitar copiar logs longos; resumir sintoma, causa raiz e acao util.

## Dono em decisions

No campo `Dono` da secao `## Revisao` de uma **decision**:

- Use o **autor do commit que implementou** a decisao (nome do `git log` / `Author`, sem e-mail — e-mail dispara compliance `pii_suspect`).
- Nao use autores-agente: nomes/identidades de Cursor, Codex, Claude, Copilot, GPT, Auto ou equivalentes nao sao dono.
- Se o commit de implementacao for de agente, deixe `Dono` vazio ou use o humano solicitante conhecido (ticket, PR, pedido na sessao) — nao invente.
- `nero_register_decision` deixa `Dono` em branco; preencha na edicao logo apos o register (ou no scaffold manual) antes de reindex/commit.

## Relações no grafo

Adicione nas notas frontmatter de relacionamentos (`links:`). Crie somente relações diretas, semânticas e comprovadas. Prefira as tools `nero_register_*`, que emitem apenas o vocabulário preferencial.

Vocabulário preferencial:

- `belongs_to_domain`;
- `documents`;
- `evidences` (alvo = nota concreta com slug; nunca hub/pasta como `domains/*/patterns` ou `index`);
- `updates`;
- `depends_on` / `uses_backend` (orientação esperada: consumer → backend; não inverter API/lib → Front/Mobile);
- `related_decision`;
- `related_pattern`;
- `source_for`.

Relação especial (decision→decision apenas): `supersedes`. Não colapsar em `updates`; o split active/superseded do contexto de projeto depende dela.

Evite tipos legados (`relates_to`, `caused_by`, `used_by`, `candidate_for_reuse`): `nero_admin_validate` rejeita. Notas de conteúdo (decisions, patterns, business-rules, troubleshooting, snapshots, validation-and-tests) precisam de `links:` não vazio.

Não crie relações apenas por coocorrência, mesma pasta, aparência ou proximidade temporal. O grafo é derivado e reindexável; Markdown continua canônico. As tools `nero_register_*` **não** reindexam: o cliente chama `nero_admin_reindex` uma vez após concluir o lote de escritas, depois `nero_admin_validate` → (opcional) `check_index_consistency`.

Em projetos, prefira o dominio primario do repositorio; use `domains/integracoes` como segundo dominio apenas para APIs/barramentos centrais de integracao ou agregados funcionais de integracao.

## Checklist antes de concluir revisoes no Knowledge Repo

- nenhum alvo de `links:` quebrado;
- nenhuma relacao duplicada no mesmo arquivo;
- nenhum `depends_on` apontando para o proprio projeto;
- `git diff --check` no Knowledge Repo.
