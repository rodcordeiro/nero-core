# Reference Index

| File | When to read |
|---|---|
| `structure.md` | Before touching files, moving responsibilities or changing ownership boundaries. |
| `runtime.md` | Before running commands, starting the MCP server, changing config or validating behavior. |
| `contracts.md` | Before changing MCP tools/resources/prompts, CLI commands or public result shapes. |
| `security.md` | Before changing writes, git operations, filesystem access, config, logs or compliance behavior. |
| `domain.md` | Before changing product behavior or wording. |
| `conventions.md` | Before editing code, docs or generated assets. |
| `patterns.md` | When reusing local implementation style. |
| `tech-debt.md` | When a change intersects known gaps or divergence from guideline. |

- Domain: `integracoes`; this checkout also implements an MCP server.
- Integracoes/API guideline: `$nero -> references/guidelines/api-guidelines.md` (integracoes inherits API).
- MCP guideline: `$nero -> references/guidelines/mcp-guidelines.md`.
- Framework skill: use `$dotnet-backend-patterns` for code under `mcp/`.
- Domain Skills: none are embedded here; see `$nero -> references/domain-skills.md` before documenting any product/lib-specific skill.
