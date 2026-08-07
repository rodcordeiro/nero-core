# Structure

## Ownership

| Path | Role |
|---|---|
| `skills/nero/` | Generic Codex skill kit: `SKILL.md`, playbooks, references and domain guidelines. |
| `mcp/` | .NET 8 MCP server implementation and tests. |
| `examples/knowledge-scaffold/` | Empty Knowledge Repo scaffold/template only. |
| `docs/adr/` | Durable architecture decisions for the Nero core and kit. |
| `README.md`, `INSTRUCTIONS.md`, `CONTEXT.md` | Human setup, glossary and bootstrap documentation. |
| `.agents/references/` | Agent-facing checkout map; keep it factual and short. |

## MCP layout

| Path | Role |
|---|---|
| `mcp/src/Nero.Knowledge.Base.Mcp/Program.cs` | Entry point; runs CLI commands or starts the MCP host. |
| `mcp/src/Nero.Knowledge.Base.Mcp/Hosting/McpHost.cs` | Host composition, logging, options, DI and stdio MCP transport registration. |
| `mcp/src/Nero.Knowledge.Base.Mcp/Presentation/Mcp/Tools/` | MCP tool surface and tool result DTOs. |
| `mcp/src/Nero.Knowledge.Base.Mcp/Application/` | Application services and contracts for knowledge read/write/admin workflows. |
| `mcp/src/Nero.Knowledge.Base.Mcp/Infrastructure/` | SQLite persistence and Markdown indexing. |
| `mcp/src/Nero.Knowledge.Base.Mcp/Domain/` | Knowledge node/edge models, enums and validation types. |
| `mcp/tests/Nero.Knowledge.Base.Tests/` | xUnit tests for tools, services, indexing, compliance, git admin and host smoke behavior. |

## Boundaries

- Markdown in an external Knowledge Repo is canonical; SQLite is a derived index.
- `skills/nero/knowledge/` is not canonical product corpus in Nero.
- Domain Skills for specific products, teams or libraries are documented as an extension pattern only; do not place them under `skills/nero/`.
- Generic guidelines live in `skills/nero/references/guidelines/`; do not copy them wholesale into `.agents/references/`.
