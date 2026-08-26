# nero Agent Instructions

## Identity

- Project: nero
- Domain: integracoes, with explicit MCP server surface.
- Stack: .NET 8 MCP server over stdio, Codex skill/playbook kit, guidelines and knowledge scaffold examples.
- Purpose: knowledge base motor for agents; the canonical corpus lives in an external Knowledge Repo, not in this repository.
- Primary entrypoints: `skills/nero/SKILL.md`, `skills/nero/prompts/`, `skills/nero/references/`, `mcp/Nero.Knowledge.Base.sln`, `docs/adr/`.

## How to use this context

| Need | Read |
|---|---|
| Structure and ownership | `.agents/references/structure.md` |
| Runtime, config and validation | `.agents/references/runtime.md` |
| MCP tools/resources/prompts contract | `.agents/references/contracts.md` |
| Security and write boundaries | `.agents/references/security.md` |
| Product/domain purpose | `.agents/references/domain.md` |
| Local conventions | `.agents/references/conventions.md` |
| Observed patterns | `.agents/references/patterns.md` |
| Known debt | `.agents/references/tech-debt.md` |
| Integracoes/API guideline | `$nero -> references/guidelines/api-guidelines.md` (integracoes inherits API) |
| MCP guideline | `$nero -> references/guidelines/mcp-guidelines.md` |

## Quick rules

- Keep changes small, local, reversible and supported by checkout evidence.
- Do not record secrets, tokens, cookies, Authorization headers, sensitive URLs, sensitive payloads or personal data in docs, tests or knowledge notes.
- Prefer focused validation: `dotnet test .\mcp\Nero.Knowledge.Base.sln`; use build/publish commands only when the change needs them.
- Do not change CI/CD, infrastructure, migrations, publish targets or machine-affecting scripts without explicit scope.
- Keep `skills/nero/` generic. Product, org or library-specific Domain Skills stay outside the canonical Nero repo.

## Conditional skills

- Use `$nero` for any work in this repository.
- Use `$dotnet-backend-patterns` for changes under `mcp/` or any .NET MCP server behavior.
- Apply `$nero -> references/guidelines/api-guidelines.md` for integracoes/API concerns and `$nero -> references/guidelines/mcp-guidelines.md` for MCP server, tools, resources, prompts, transport and host behavior.
- Omit product Domain Skills unless the checkout contains concrete evidence for that product/lib; Nero only documents the extension pattern.

## Agent skills

### Issue tracker

GitHub Issues neste repo, no Nero Scrum board (#14); hub de backlog dos projetos Nero. See `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulário canônico Matt Pocock (1:1). See `docs/agents/triage-labels.md`.

### Domain docs

single-context (`CONTEXT.md` + `docs/adr/`). See `docs/agents/domain.md`.

### Iteration / Project board

Nero Scrum board: prefer **current**; new tasks → **next**. Hub de coordenação em `docs/agents/iteration-workflow.md`.
