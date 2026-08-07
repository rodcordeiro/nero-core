# Patterns

- Preserve the current ownership boundaries described in `structure.md`.
- The MCP host centralizes registration in `McpHost.Configure`: logging, options, singleton services and `WithToolsFromAssembly()`.
- Tool entrypoints live in `Presentation/Mcp/Tools`; application services own behavior and contracts live under `Application/Contracts`.
- Writers create or replace Markdown in the external Knowledge Repo, then admin workflows reindex and validate the derived SQLite graph.
- Git admin tools are production-sensitive: they enforce expected worktree state, compliance scans and non-force sync behavior.
- Prefer focused validation from `runtime.md` over broad unrelated checks.
- Promote reusable learnings to Nero only when evidence shows reuse beyond this repository.
