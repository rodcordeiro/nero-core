# Conventions

- Follow existing file layout and naming before applying generic preferences.
- Keep MCP tool names, result shapes, recommendations and CLI command behavior stable unless the task includes compatibility and rollback notes.
- Keep public tool methods asynchronous and pass `CancellationToken` through read/write/indexing/admin paths when the local pattern supports it.
- Use constructor DI and explicit options objects in the .NET host; avoid scattered direct environment reads.
- Keep generated or external artifacts separate from hand-authored source.
- Keep `skills/nero/` generic; document product-specific extensions in `references/domain-skills.md`, not as embedded Domain Skills.
- Sanitize all examples, logs, errors and durable docs before writing them.
- Prefer short Markdown references with paths and facts from checkout evidence over copied guideline prose.
