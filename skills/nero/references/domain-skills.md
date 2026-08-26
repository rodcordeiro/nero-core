# Domain Skills (extensao fora do Nero)

Skills opcionais sobre produto, biblioteca ou organizacao. O Kit documenta o padrao; o conteudo dessas skills vive fora do repositorio canonico.

Ver ADR `docs/adr/0005-domain-skills-documented-only.md` e `docs/adr/0006-complementary-packs-core-independent.md`.

## Fronteira

| Camada               | Onde vive                                                | Exemplo                                       |
| -------------------- | -------------------------------------------------------- | --------------------------------------------- |
| Core + Kit (`$nero`) | Repo Nero                                                | workflow MCP, guidelines genericos, playbooks |
| Knowledge Repo       | Repo separado (`KnowledgeRoot__Path`)                    | corpus Markdown (global/domains/projects)     |
| Domain Skill         | Repo/skill do usuario ou time                            | auth lib interna, design system, webhook hub  |
| Pack                 | Fora do canonico (skill + MCP proprio quando houver)     | [nero-code-graph](https://github.com/rodcordeiro/nero-code-graph), People CRM, Content Factory |

Um **Pack** e produto complementar (padrao Domain Skill + MCP opcional). O Core nao depende de Pack algum; Packs consomem primitivos do Nero quando existirem (Capture Zone, trust, promocao). Nunca publique Packs dentro de `skills/nero/` no canonico.

Domain Skills ficam fora de `skills/nero/`. Corpus de produto fica no Knowledge Repo, nao em `skills/nero/knowledge/`.

## Packs conhecidos (fora do canonico)

Done when: o agente sabe **onde instalar** (bootstrap do repo) e **quando acionar** (routing abaixo), sem copiar roteiro de setup aqui.

Bootstrap de instalacao: README e INSTRUCTIONS na raiz do repo Nero (secao Packs complementares) — nao duplicar neste arquivo.

| Pack | Repo | Acionar quando |
| --- | --- | --- |
| nero-code-graph | https://github.com/rodcordeiro/nero-code-graph | Perguntas estruturais do checkout: imports, calls, vizinhos, path A→B, grafo stale (`cg_*`) |
| People CRM, Content Factory | produto do usuario | Fluxos editoriais/CRM fora do Schema Nero (ADR 0006) |

Routing operacional (nao misturar superficies):

| Tipo | Superficie |
| --- | --- |
| Ops — decisao, regra, troubleshooting, contexto | `nero_*` / `$nero` |
| Estrutura — `calls`, `imports`, `file:line` | MCP/skill do Pack (`cg_*` para code-graph) |
| Corpo de arquivo / WIP | filesystem |

Ordem quando ambos aplicam: Pack estrutural (status → generate se stale → query) → registrar conclusao operacional com `nero_register_*` se necessario. **Ponte permitida:** citar `file:line` do envelope code-graph em nota Nero — citacao, nao edge em `links:`.

Detalhe de tools/env do code-graph: skill `nero-code-graph` no checkout do Pack (`tools.md`, spec em `docs/references/`).

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
| Perguntas estruturais de codigo (imports/calls/path) | Pack `nero-code-graph` se instalado (`references/domain-skills.md`) |

## Ligacao com Knowledge Repo

- Como operar a lib → Domain Skill.
- Decisao/snapshot do projeto `Acme.Api` → `nero_register_*`.
- Nota reutilizavel no grafo → Knowledge Repo com `escopo` adequado (`project` / `domain` / `global`).

## Publicacao

- PRs/share do Nero carregam so Core + Kit.
- Skills `$…-components-*` / auth / hub so com evidencia no checkout.
- Domain Skill aponta para guidelines `$nero`.
- Exemplos do Kit usam `Acme.*` ou placeholders.
