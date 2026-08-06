# Domain Skills (extensão fora do Nero)

Domain Skills são skills opcionais sobre produto, biblioteca ou organização específica. O Nero **documenta** o padrão de extensão; **não** inclui conteúdo de Domain Skills no repositório canônico.

Ver ADR `docs/adr/0005-domain-skills-documented-only.md`.

## Fronteira

| Camada | Onde vive | Exemplo |
|---|---|---|
| Core + Kit (`$nero`) | Repo Nero | workflow MCP, guidelines genéricos, playbooks |
| Knowledge Repo | Repo separado (`KnowledgeRoot__Path`) | corpus Markdown (global/domains/projects) |
| Domain Skill | Repo/skill do usuário ou time | auth lib interna, design system, webhook hub |

Nunca publique Domain Skills dentro de `skills/nero/` no canônico. Não trate `skills/nero/knowledge/` como corpus de domínio.

## Quando criar

Crie um Domain Skill quando:

- a orientação depende de uma lib, pacote ou produto concreto (API interna, UI kit, auth SDK);
- o playbook `$nero` sozinho ficaria genérico demais para ser acionável;
- o conhecimento muda com o produto, não com o Schema Nero.

Não crie Domain Skill para:

- convenções genéricas de API/front/mobile (use `references/guidelines/`);
- notas de projeto/decisão (use o Knowledge Repo via MCP);
- duplicar o que já está em `$nero` ou em skills públicas (Expo, .NET, etc.).

## Como implementar (fora do Nero)

1. Crie um diretório de skill no seu agente (ex.: `~/.agents/skills/<nome>/` ou um monorepo de skills do time).
2. Adicione `SKILL.md` com `name` estável e `description` acionável (quando carregar a skill).
3. Coloque playbooks/referências em `references/` e `prompts/` locais da Domain Skill.
4. Referencie `$nero` como pré-requisito para knowledge/MCP; não reimplemente Schema ou tools MCP.
5. Se a Domain Skill apontar para pacotes npm/NuGet, cite o package id — nunca copie o código do pacote para dentro da skill.

Estrutura mínima sugerida:

```text
<domain-skill>/
  SKILL.md
  references/
    overview.md          # fronteiras, quando acionar
    contracts.md         # contratos/API públicos relevantes
  prompts/               # opcional: playbooks do produto
```

## Como usar com `$nero`

Ordem típica:

1. `$nero` — health/contexto/search no Knowledge Repo; guidelines de domínio.
2. Domain Skill — só quando a tarefa toca a lib/produto coberto.
3. Skills de framework públicas — Expo, React Native, .NET, etc.

No `AGENTS.md` da aplicação, documente skills condicionais com evidência (package.json, imports, solution). Exemplo neutro:

| Condição | Skill |
|---|---|
| Checkout usa a lib de auth do time | `$acme-auth` (Domain Skill local) |
| Mobile Expo/RN | skills RN/Expo + `$nero` |
| Sem evidência | omitir a Domain Skill |

## Ligação com Knowledge Repo

- Regras estáveis do produto → Domain Skill (como operar a lib).
- Decisões/snapshots do projeto `Acme.Api` → Knowledge Repo via `nero_register_*`.
- Não grave corpus de Domain Skill dentro do Nero; se precisar de nota reutilizável no grafo, registre no Knowledge Repo com escopo adequado (`project` / `domain` / `global`).

## Anti-padrões

- Embarcar Domain Skills no PR/share do Nero.
- Assumir skills `$…-components-*` ou auth/hub sem evidência no checkout.
- Copiar guidelines Nero para dentro da Domain Skill (aponte; não clone).
- Usar nomes reais de org/produto de terceiros em exemplos do Kit — use `Acme.*` ou placeholders.
