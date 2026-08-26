# Iteration workflow (GitHub Projects)

Preferences for agents working **any Nero repo** against the shared **Nero Scrum board** (`https://github.com/users/rodcordeiro/projects/14`).

## Hub

**`nero-core`** is the coordination hub for Nero backlogs. Agents must not invent parallel priority orders per pack.

- Core / Kit / Genesis / primitives → issues on [`rodcordeiro/nero-core`](https://github.com/rodcordeiro/nero-core/issues).
- Packs (ex. code-graph) → issues on their own repo, still on board #14.
- Before starting work: check **current** on the board across repos; do not starve agreed pack work by inventing Core scope in current while pack tickets are actionable.

## Rules

1. **Current iteration first.** Before starting work, list open issues in the **current** iteration that are `ready-for-agent` (or otherwise actionable) and unblocked. Prefer those.
2. **Pull next only when current is empty.** If the current iteration has no pending actionable tickets, move the next priority unblocked ticket from **next** (or backlog) into **current**, then work it.
3. **New tasks default to next.** Any newly created task goes to the **next** iteration unless:
   - the user explicitly requests current, or
   - the task is required to complete / unblock work already in the **current** iteration.
4. **Do not starve the board.** Do not invent scope in current while next holds the agreed priority order.
5. **Repo affinity.** Implement in the repo that owns the issue. Cross-repo coordination notes go in issue comments or the hub `iteration-workflow` — not duplicate tickets for the same work.

## Priority order (nero-core seed — 2026-08-26)

Open work after genesis migrate (placed in **next**):

| # | Title | Notes |
| --- | --- | --- |
| [#10](https://github.com/rodcordeiro/nero-core/issues/10) | Fase 7b — CI mínimo (GitHub Actions) | **closed** (mcp-ci green) |
| [#12](https://github.com/rodcordeiro/nero-core/issues/12) | Fase 8 P1 — Captura, proveniência e drift | Após P0 (#9 closed) |
| [#11](https://github.com/rodcordeiro/nero-core/issues/11) | Fase 8 P2 — Superfície MCP derivada | Prefer after P1 |

Closed: [#1](https://github.com/rodcordeiro/nero-core/issues/1)–[#9](https://github.com/rodcordeiro/nero-core/issues/9) (fases 0–7 + 8 P0). Open specs: `docs/references/`.

Pack priority (code-graph Iteration 1 / current) remains documented in that repo’s `docs/agents/iteration-workflow.md`.

## Tracker pointers

- Issues: GitHub (`docs/agents/issue-tracker.md`)
- Labels: `docs/agents/triage-labels.md`
- Project: Nero Scrum board (#14), Iteration field from the board’s iteration template
