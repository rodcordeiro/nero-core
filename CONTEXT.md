# Nero

Motor imparcial de knowledge base para agentes (MCP + skill + kit de guidelines/prompts). O corpus de domínio de cada pessoa ou time vive fora deste repositório.

## Language

### Product

**Nero**:
The shareable product: MCP server, `$nero` skill, and generic Kit in one repository. It contains no domain corpus.
_Avoid_: corporate skill fork (as product name), personal fork (as product name), template-only

**Core**:
The executable and orchestration layer: MCP, `$nero` skill workflow, schema enforcement, and admin/search tools.
_Avoid_: engine (ambiguous), backend-only

**Kit**:
Generic, brand-neutral guidelines, references, and playbooks that ship inside Nero to bootstrap agent instructions and project extraction.
_Avoid_: knowledge, corpus, employer-specific guidelines

**Knowledge Repo**:
A separate git repository owned by a person or group that holds the Markdown corpus Nero indexes. Bound to the MCP via configuration, never vendored into Nero.
_Avoid_: knowledge folder inside Nero, monorepo corpus, instance data

**Knowledge Scaffold**:
An empty tree under `examples/knowledge-scaffold/` that matches the Schema so a Knowledge Repo can be created by copy.
_Avoid_: sample domain data, demo projects with real names

### Boundaries

**Schema**:
The fixed layout and document types for a Knowledge Repo (`global/`, `domains/`, `projects/`, frontmatter, promotion rules). Versioned only in Nero.
_Avoid_: flexible layout, per-user structure, schema-in-knowledge-repo

**Domain Skill**:
An optional skill about a specific product, library, or organization. Documented as an extension pattern in Nero; never shipped as content in the canonical Nero tree.
_Avoid_: shipping auth/webhook/components product skills inside Nero

**Pack**:
An optional complementary product that uses Nero primitives and lives entirely outside Core: a skill, templates, and the user's own corpus, with an optional sidecar MCP. Core remains deliverable with zero Packs installed.
_Avoid_: plugin inside Core, schema extension, role pack shipped in Nero, COG-style monolithic plugin

**Corpus**:
The set of domain Markdown documents (projects, decisions, business rules, snapshots, troubleshooting) inside a Knowledge Repo.
_Avoid_: Kit, Core, README of Nero

### Compliance

**Clean Genesis**:
The rule that Nero’s first shared commit contains only Core + Kit + Scaffold, with no employer brand names, packages, feeds, pipelines, or Corpus blobs in git history.
_Avoid_: commit-then-purge, filter-repo-after-share

**Upstream Divergence**:
Nero and any workplace knowledge-base repository do not sync code; only ideas may transfer, consciously and scrubbed.
_Avoid_: cherry-pick pipeline, shared submodule, automated port
