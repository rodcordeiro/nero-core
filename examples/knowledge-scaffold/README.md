# Knowledge Scaffold

Empty Schema tree for a **Nero Knowledge Repo**. Nero Core (MCP + `$nero`) stays impartial; your corpus lives in a **separate git repository** created from this scaffold.

## Quick start

1. Copy this folder into a new directory (or use it as the root of a new repo):

```powershell
Copy-Item -Recurse .\examples\knowledge-scaffold C:\path\to\my-knowledge
cd C:\path\to\my-knowledge
git init
```

2. Point the Nero MCP at that path (client env):

```text
KnowledgeRoot__Path=C:\path\to\my-knowledge
KnowledgeDatabase__Path=C:\path\to\my-knowledge\.nero\nero-knowledge.db
```

Keep the SQLite DB **outside** git (or gitignore `.nero/`).

3. Validate from the Nero repo root (sanity check against this scaffold or your copy):

```powershell
dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate
```

Override root for a copy:

```powershell
$env:KnowledgeRoot__Path = "C:\path\to\my-knowledge"
dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate
```

Expect exit code `0` and a line like `Validated N nodes and M edges.`

## Layout (Schema — fixed by Nero)

| Path | Role |
|------|------|
| `global/` | Cross-cutting decisions, patterns, snapshots |
| `domains/` | One subdirectory per domain |
| `projects/` | One subdirectory per project; keep `projects/index.md` as hub |
| `*/index.md` | Hub notes (bare Markdown is OK; no `links:` required on hubs) |

Do **not** invent alternate top-level layouts. Schema changes ship with Nero Core version bumps.

## After copy

- Register domains/projects via `$nero` / `nero_register_*` tools (then `nero_admin_reindex` → `nero_admin_validate`).
- Content notes under `decisions/`, `patterns/`, etc. need a non-empty `links:` block — see `skills/nero/references/knowledge-routing.md`.
- Domain Skills (product/lib-specific) stay **outside** the Nero canônico; see `skills/nero/references/domain-skills.md`.
