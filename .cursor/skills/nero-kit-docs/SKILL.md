---
name: nero-kit-docs
description: >
  Review and rewrite Nero kit agent docs (skills/nero/prompts, skills/nero/references,
  guidelines). Use when the user asks for a writing-for-agents pass on those folders,
  when playbooks or references sprawl or duplicate MCP contract, or when tightening
  AGENTS playbooks, mcp-tools, workflow, knowledge-routing, compliance, or domain
  guidelines — even if they do not say "writing-for-agents".
license: MIT
metadata:
  author: nero-kit
  version: "1.0"
---

# Nero kit agent docs

How to review and rewrite the Kit corpus so each file is one job, shared contract lives behind a **ponteiro**, and every recipe step has **Done when**.

**Failure pattern:** cache sprawl — workflow / `links:` vocab / validate+commit restated in every playbook and reference, so copies drift (`Marco N`, mirrored guideline tables) and the agent coin-flips which copy to follow.

**Verified by:** commits `41a7dcc` (prompts, net −150 lines) and `681e1cb` (references, net −117 lines); two-axis code-review (Standards + Spec) with hard findings fixed; MCP recommendation path `skills/nero/prompts/knowledge-review-app-mcp.txt` unchanged; every `## nero_*` heading still present after the `mcp-tools.md` splice.

Load `$writing-for-agents` for the levers. This skill is the Nero corpus loop. Ownership map: `references/ownership.md` (load in step 1).

## When to use this

- `/writing-for-agents` on `skills/nero/prompts/` or `skills/nero/references/`
- Playbooks, `mcp-tools.md`, `workflow.md`, guidelines, or `index.md` feel long or contradict each other
- Adding a domain, playbook, or MCP tool and the Kit docs need to stay one SoT each

## Procedure

- [ ] 1. **Assign SoT.** Read `references/ownership.md`. Done when: every file in the folder has one job, and duplicated meaning is marked disclose or delete.
- [ ] 2. **Specify per file.** For each file: what stays in-file (unique recipe), what becomes a ponteiro, which steps need **Done when**, which negations become a positive + paired rail. Done when: the spec list exists before the first rewrite.
- [ ] 3. **Rewrite.** Keep MCP recommendation paths byte-stable. Kit operational Portuguese: no accents. Examples: `Acme.*`. Full MCP tool names (`nero_update_project_context`, never `_context`). Done when: the spec for that file is applied and the SoT table still holds.
- [ ] 4. **Two-axis review** (`$code-review`) on `git diff HEAD -- <folder>`. Fixed point: current `HEAD` for uncommitted WIP. Spec = the list from step 2. Done when: hard Standards findings (tool names, recommendation paths) and wrong triggers are fixed.
- [ ] 5. **Commit** only if the user or `$implement` asked. Message says why (one job per file / disclose contract), not the file list.

TDD: skip. Markdown playbooks have no pre-agreed code seam.

### Example

Input: `/writing-for-agents review @skills/nero/prompts/ /implement`

Output: `index.md` is a router (one trigger per playbook); playbooks keep unique steps + **Done when**; field lists and `links:` vocab point at `references/mcp-tools.md` and `knowledge-routing.md`; path `skills/nero/prompts/knowledge-review-app-mcp.txt` unchanged.

## Gotchas

- **Health vs index entry.** `knowledge-review-app-mcp.txt` has two branches: health (`MissingRecentSnapshot` / `StaleSnapshot`) loads this playbook; index (review geral) still runs the full flow even with a recent snapshot. Do not write “stale snapshot is why this run exists” as the only entry.
- **`**Nao reindexa**` on each writer** in `mcp-tools.md` is a local gotcha, not identity to prune. Leave it on the tool section.
- **Guidelines are not writers.** In `api-guidelines.md` / `front-guidelines.md` say “Encode only proven business rules”, never “Register” — that token is `nero_register_*`.
- **Large `mcp-tools.md` edits.** StrReplace on the opening can fail on mojibake (`gravaÃ§Ãµes`). Splice with Node (keep from `## \`nero_search_knowledge\``) rather than a 100-line search/replace.
- **PowerShell.** `&&` is not a statement separator on older Windows PowerShell; use `;`. Commit messages: PowerShell here-string (`@"..."@`).
- **SKILL.md pointers.** Sharpen them only if a reference’s job changed. This loop does not require editing `skills/nero/SKILL.md`.

## What didn't work

- **`prompts/fragments/`** — the old `<!-- FRAGMENT -->` comment wanted extraction “when it grows”. The domain deltas fit a compact table; splitting spent cognitive load for no branch.
- **Splitting `mcp-tools.md`** into several files — extra always-loaded SKILL pointers or a human index. One file + top **indice** + pruned opening is the SoT for tool schemas.
- **TDD on the markdown** — no public code seam; tests would lock prose.
- **Abbreviated tool names** (`nero_update_project_index` / `_context` / `_inventory`) — `conventions.md` requires stable full MCP names.
- **Inlined field-minimos and vocab tables** in playbooks — they drifted from `mcp-tools.md` / `knowledge-routing.md`. Ponteiro, keep only the achado→tool route.
