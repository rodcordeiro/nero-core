# Domain Skills (extensao fora do Nero)

Skills opcionais sobre produto, biblioteca ou organizacao. O Kit documenta o padrao; o conteudo dessas skills vive fora do repositorio canonico.

Ver ADR `docs/adr/0005-domain-skills-documented-only.md` e `docs/adr/0006-complementary-packs-core-independent.md`.

## Fronteira

| Camada               | Onde vive                                                | Exemplo                                       |
| -------------------- | -------------------------------------------------------- | --------------------------------------------- |
| Core + Kit (`$nero`) | Repo Nero                                                | workflow MCP, guidelines genericos, playbooks |
| Knowledge Repo       | Repo separado (`KnowledgeRoot__Path`)                    | corpus Markdown (global/domains/projects)     |
| Domain Skill         | Repo/skill do usuário ou time                            | auth lib interna, design system, webhook hub  |
| Pack                 | Fora do canônico (skill + templates + corpus do usuário) | People CRM, Content Factory                   |

Um **Pack** é um produto complementar construído com o padrão Domain Skill. O Core não depende de Pack algum; Packs consomem primitivos do Nero quando existirem (Capture Zone, trust, promoção). Nunca publique Packs dentro de `skills/nero/` no canônico.

Domain Skills ficam fora de `skills/nero/`. Corpus de produto fica no Knowledge Repo, nao em `skills/nero/knowledge/`.

## Quando criar

Done when: a skill cobre uma lib/produto concreto, e o que e generico ja esta em `$nero` ou no Knowledge Repo.

Crie quando:

- a orientacao depende de lib, pacote ou produto concreto (API interna, UI kit, auth SDK);
- o playbook `$nero` sozinho ficaria generico demais para ser acionavel;
- o conhecimento muda com o produto, nao com o Schema Nero.

Convenoes de API/front/mobile → `references/guidelines/`. Decisao/snapshot de projeto → Knowledge Repo via MCP. Skills publicas (Expo, .NET) permanecem skills publicas.

## Como implementar (fora do Nero)

Done when: a skill tem `SKILL.md` + references, aponta `$nero` para knowledge/MCP, e cita package id sem copiar codigo.

1. Diretorio de skill no agente (ex.: `~/.agents/skills/<nome>/` ou monorepo do time).
2. `SKILL.md` com `name` estavel e `description` acionavel (quando carregar).
3. Playbooks/referencias em `references/` e `prompts/` locais.
4. `$nero` como pre-requisito para knowledge/MCP; Schema e tools MCP permanecem no Nero.
5. Pacotes npm/NuGet: cite o package id.

Estrutura minima:

```text
<domain-skill>/
  SKILL.md
  references/
    overview.md          # fronteiras, quando acionar
    contracts.md         # contratos/API publicos relevantes
  prompts/               # opcional: playbooks do produto
```

## Como usar com `$nero`

Done when: `$nero` rodou para health/contexto; a Domain Skill so entra se o checkout evidenciar a lib/produto.

1. `$nero` — health/contexto/search; guidelines de dominio.
2. Domain Skill — quando a tarefa toca a lib/produto coberto.
3. Skills de framework publicas — Expo, React Native, .NET.

No `AGENTS.md` da aplicacao, skills condicionais com evidencia (package.json, imports, solution):

| Condicao                           | Skill                             |
| ---------------------------------- | --------------------------------- |
| Checkout usa a lib de auth do time | `$acme-auth` (Domain Skill local) |
| Mobile Expo/RN                     | skills RN/Expo + `$nero`          |
| Sem evidencia                      | omitir a Domain Skill             |

## Ligacao com Knowledge Repo

- Como operar a lib → Domain Skill.
- Decisao/snapshot do projeto `Acme.Api` → `nero_register_*`.
- Nota reutilizavel no grafo → Knowledge Repo com `escopo` adequado (`project` / `domain` / `global`).

## Publicacao

- PRs/share do Nero carregam so Core + Kit.
- Skills `$…-components-*` / auth / hub so com evidencia no checkout.
- Domain Skill aponta para guidelines `$nero`.
- Exemplos do Kit usam `Acme.*` ou placeholders.
