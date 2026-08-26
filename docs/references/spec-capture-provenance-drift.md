# Spec: Captura, proveniência e drift (Fase 8 P1)

Issue: [#12](https://github.com/rodcordeiro/nero-core/issues/12)  
Depends on: Fase 8 P0 complete ([#9](https://github.com/rodcordeiro/nero-core/issues/9), ADR 0007 / 0008)  
Seam: Admin/validate surface + fixture Knowledge Repo (Markdown canônico) — promoção humana, metadados opt-in, drift report-only

## Problem Statement

Agents and humans need a place to park unverified notes before they enter the Schema graph, optional trust metadata that does not break old Corpus, and a report-only way to see when Core, Kit, Schema, and docs drift apart. Without these primitives, Packs and operators either skip provenance or invent ad-hoc folders inside Core.

## Solution

Deliver three Core primitives, each testable against a Knowledge Repo fixture and existing admin/validate tools: (1) a Capture Zone outside the canonical graph with explicit human promotion into project → domain → global; (2) opt-in frontmatter for trust/provenance that leaves legacy Corpus validating unchanged; (3) a report-only drift detector across MCP tools, `$nero`, docs, scaffold, Schema, and Core/Kit versions—without auto-fixing or promoting.

## User Stories

1. As a Knowledge Repo owner, I want an Inbox/Draft Capture Zone outside the canonical graph, so that raw notes do not pollute search and links until I accept them.
2. As a Knowledge Repo owner, I want promotion to project, then domain, then global to require my explicit acceptance, so that agents cannot silently elevate claims.
3. As an agent using `$nero`, I want clear guidance that Capture Zone content is not Schema Corpus until promoted, so that I do not call `nero_register_*` on drafts by mistake.
4. As a Core maintainer, I want Capture Zone paths excluded from graph index defaults, so that FTS and related-knowledge stay clean.
5. As a Knowledge Repo owner, I want optional `sources` metadata on notes, so that I can record where a claim came from.
6. As a Knowledge Repo owner, I want optional `last_verified` metadata, so that freshness can be reasoned about later.
7. As a Knowledge Repo owner, I want optional `verification_status` values including `verified`, `unverifiable`, `stale`, and `contradicted`, so that trust state is explicit.
8. As a Knowledge Repo owner, I want optional `confidence` metadata, so that soft certainty can be recorded without forcing it on every note.
9. As an owner of a legacy Corpus, I want old Markdown without trust fields to reindex and validate exactly as today, so that opt-in never becomes a migration tax.
10. As a Core maintainer, I want absence of verification metadata not to mean `NeverVerified`, so that ADR 0007 semantics stay consistent with trust audit.
11. As an operator, I want `nero_admin_trust_audit` to keep respecting explicit markers only, so that P1 metadata feeds audit without inventing new silent categories.
12. As a Core maintainer, I want a drift detector that is report-only, so that mismatches are visible without automatic rewrites.
13. As a Core maintainer, I want drift checks across MCP tool contracts, `$nero` references, playbooks/prompts, manifests, scaffold, Schema, and declared Core/Kit versions, so that one report covers the shareable surface.
14. As a developer, I want a fixture that intentionally diverges, so that the detector’s positive findings are testable.
15. As a developer, I want a fixture that matches, so that a clean report is also testable.
16. As an AFK agent, I want drift output to name stable finding codes or paths, so that I can open a follow-up ticket without guessing.
17. As a Clean Genesis steward, I want Capture Zone and drift tooling free of Pack-specific types (no `Person`, no editorial nodes), so that ADR 0002 / 0006 hold.
18. As a Pack author, I want to consume these primitives later without Core depending on my Pack, so that Core remains deliverable with zero Packs.
19. As a Knowledge Repo owner, I want promotion to leave an auditable trail (what moved where), so that I can review agent suggestions after the fact.
20. As an agent, I want promotion previews that do not write until acceptance, so that dry-runs stay safe.
21. As a Core maintainer, I want validate/reindex behavior on promoted files to match existing writers, so that P0 finalize_batch remains the post-write evidence path.
22. As a security-minded operator, I want Capture Zone and trust fields to forbid secret/token payloads in docs and tests, so that compliance rules stay intact.
23. As a Kit consumer, I want `$nero` references updated when Capture Zone and drift contracts land, so that skill docs match tools.
24. As a Gauntlet critic, I want DoD evidence: legacy Corpus green validate; promotion blocked without human accept; drift report-only on divergent fixture.
25. As a board coordinator, I want this issue to stay behind P0 (done) and ahead of P2 preference, so that derived MCP surface builds on stable capture/trust.
26. As a maintainer, I want no automatic promotion of inferences to decision/pattern/business rule, so that Out of Scope stays enforced.
27. As a Knowledge Repo owner, I want drafts to be deletable or archivable without Schema side effects, so that Capture Zone is low commitment.
28. As a Core maintainer, I want Schema versioning to remain in Nero only, so that Capture Zone layout is documented as Core convention, not per-user schema forks.
29. As an agent, I want clear errors when asked to promote without required fields for the target layer, so that partial promotions fail loudly.
30. As a friend sharing Nero, I want examples in fixtures to use fictional names only, so that hygiene stays green.

## Implementation Decisions

- Capture Zone lives outside `global/`, `domains/`, and `projects/` canonical trees (exact folder name documented in Schema/Kit references when implemented); not indexed as Corpus nodes until promotion.
- Promotion is an explicit human-accepted operation (tool and/or playbook): never silent; writes Markdown into the target layer then relies on existing finalize/reindex paths.
- Trust frontmatter fields are optional and ignored by validate when absent; when present, enumerated `verification_status` values are validated.
- Drift detector is admin/report-only (CLI and/or MCP admin tool), reads checkout + configured Knowledge Repo as needed, writes nothing to Corpus.
- Reuse P0 patterns: deterministic reports, fixture Knowledge Repos under tests, zero mutation for audit-like operations.
- Do not extend Schema with Pack node types; do not add Pack tools under `nero_*`.
- Update `$nero` references (`mcp-tools`, workflow, compliance) in the same slice that lands contracts.

## Testing Decisions

- Good tests assert external behavior on fixtures: validate/reindex outcomes, report contents, and “no write” guarantees—not private method structure.
- Modules under test: admin/report surface, Schema/frontmatter validation paths, promotion boundary (Capture Zone → canonical path).
- Prior art: trust audit and finalize_batch tests with Knowledge Repo fixtures; admin status/validate suites.
- Required cases: (a) legacy Corpus without trust fields → validate unchanged; (b) Capture note not in graph until promote; (c) promote without accept does not write; (d) divergent fixture → drift findings; (e) aligned fixture → empty/clean drift.

## Out of Scope

- People CRM / Content Factory Packs and any `Person` / editorial Schema types (ADR 0006)
- Automatic promotion of inferences to durable knowledge types
- Embeddings / vector index (P2 concern)
- MCP resources/prompts for capture UI (P2)
- Sync or cherry-pick with workplace knowledge-base repos (ADR 0004)
- Auto-fixing drift by rewriting files
- Bash/Python/Claude/cron as required runtime

## Further Notes

- Glossary: Knowledge Repo, Schema, Corpus, Core, Kit, Pack, Upstream Divergence (`CONTEXT.md`).
- Experiments from original Fase 8 list that still apply: opt-in frontmatter; read-only drift with divergent fixture.
- Spec path: `docs/references/spec-capture-provenance-drift.md`.
