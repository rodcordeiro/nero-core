# Spec: Superfície MCP derivada (Fase 8 P2)

Issue: [#11](https://github.com/rodcordeiro/nero-core/issues/11)  
Prefer after: [#12](https://github.com/rodcordeiro/nero-core/issues/12) (P1)  
Seam: contrato MCP stdio — resources/prompts read-only + tools com efeito; preview de links sem persistir

## Problem Statement

Hosts and agents need read-only MCP resources and versioned prompts for capture/review flows, plus safe link suggestions, without turning resources into mutable backdoors or writing the graph on preview. Optional embeddings must stay derived and disableable so Core does not hard-depend on vector infrastructure.

## Solution

Extend the MCP stdio surface so that resources and prompts are read-only and versioned for capture/review, while tools remain the only operations with side effects. Link suggestions appear in preview only and persist solely after explicit acceptance. Evaluate incremental per-file indexing; if embeddings exist, keep them as an optional derived index that can be turned off.

## User Stories

1. As an MCP host user, I want read-only resources for capture/review context, so that I can inspect drafts and guidance without mutating the Knowledge Repo.
2. As an MCP host user, I want versioned prompts for capture and review, so that prompt drift is visible across Core releases.
3. As a Core maintainer, I want tools to be the only MCP surface that writes or reindexes, so that resources never become a write API.
4. As an agent, I want link suggestions in a preview response, so that I can propose `links:` without committing them.
5. As a Knowledge Repo owner, I want link persistence only after my acceptance, so that preview never alters the graph.
6. As a Core maintainer, I want preview calls to leave Markdown and SQLite unchanged, so that tests can assert zero writes.
7. As an operator, I want incremental indexing by file evaluated with a clear accept/reject decision, so that full reindex remains available when safer.
8. As an operator, I want full reindex to remain correct even if incremental indexing ships, so that recovery stays simple.
9. As a privacy-minded owner, I want embeddings absent or disableable, so that I can run Core without vector dependencies.
10. As a Core maintainer, I want embeddings, if present, treated only as a derived index, so that Markdown remains canonical.
11. As a Pack author, I want this surface usable without my Pack installed, so that ADR 0006 holds.
12. As an agent using `$nero`, I want docs that list new resources/prompts/tools contracts, so that skill routing stays accurate.
13. As a Gauntlet critic, I want fixtures proving resources cannot mutate Corpus, so that DoD is evidence-based.
14. As a Gauntlet critic, I want fixtures proving preview does not write links, so that DoD is evidence-based.
15. As a Clean Genesis steward, I want no employer-specific prompt content in shipped prompts, so that Kit stays neutral.
16. As a host integrator (Cursor/Codex/Claude), I want stdio transport unchanged, so that existing MCP configs keep working.
17. As a developer, I want DTO contracts for preview results stable enough for tests, so that external behavior is asserted at the tool boundary.
18. As an operator, I want disable flags or config for embeddings documented beside KnowledgeRoot paths, so that configuration stays discoverable.
19. As a Knowledge Repo owner, I want capture resources to respect Capture Zone vs canonical boundaries from P1, so that drafts are not presented as Corpus.
20. As a security-minded operator, I want resources and prompts to avoid echoing secrets from Corpus, so that compliance scanning still applies to writes.
21. As a maintainer, I want admin finalize_batch / trust_audit unchanged in spirit, so that P0 evidence paths remain.
22. As a board coordinator, I want this issue preferred after P1, so that capture/trust primitives exist before derived UI surface.
23. As a friend, I want README or INSTRUCTIONS to mention optional resources/prompts when they ship, so that bootstrap stays current.
24. As a Core maintainer, I want no mutable MCP resource templates that write files, so that “read-only” is absolute for this slice.
25. As a developer, I want tests that run without network embedding providers, so that CI (when present) stays feed-free and offline-friendly.
26. As an agent, I want clear errors when acceptance is missing for persist-link operations, so that partial writes do not occur.
27. As a Kit consumer, I want playbooks updated only when they add proven value beyond tools, so that ADR 0007’s playbook deferral pattern is respected.
28. As a maintainer, I want Out of Scope items (Slack/Linear/social publish, autonomous posting) excluded, so that Core stays a motor not a factory.
29. As a Knowledge Repo owner, I want derived indexes regenerable from Markdown, so that deleting `.nero` never loses Corpus.
30. As an AFK agent, I want this ticket `ready-for-agent` with a pointer to this spec file, so that implementation starts from one source of truth.

## Implementation Decisions

- MCP transport remains stdio; register read-only resources and prompts alongside existing tool classes without giving resources write side effects.
- Link suggestion is a preview-shaped tool (or dedicated preview operation) that returns candidates; a separate accept/persist path writes only after explicit acceptance.
- Incremental indexing is an evaluation with documented decision; default safety is full reindex until incremental proves correctness on fixtures.
- Embeddings: optional, derived, disableable; never the source of truth for Corpus.
- Contracts and payloads documented in `$nero` mcp-tools references when implemented.
- No Pack tools; no Schema node types for CRM/editorial content.

## Testing Decisions

- Good tests hit the MCP/tool DTO boundary and filesystem/SQLite observables: resource read returns content; resource/prompt paths do not change files; preview leaves graph unchanged; accept persists links; embeddings off → features degrade gracefully.
- Modules: MCP presentation/tools/resources/prompts registration; indexing path if incremental lands; config for embeddings flag.
- Prior art: existing MCP tool tests and Knowledge Repo fixtures; P0 admin report-only tests for zero-write assertions.
- CI (spec-ci-minimo) should remain runnable without embedding providers.

## Out of Scope

- Mutable MCP resources
- Mandatory embeddings or cloud vector services
- Autonomous social/Slack/Linear publishing
- Packs (People CRM, Content Factory) inside Core
- Replacing Markdown canonicality with SQLite or embeddings
- Azure Pipelines / private feeds

## Further Notes

- Align with ADR 0001 (external Knowledge Repo), 0002 (fixed Schema), 0006 (Packs outside), 0007/0008 (read-only audit / finalize evidence).
- Spec path: `docs/references/spec-mcp-derived-surface.md`.
