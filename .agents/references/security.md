# Security

## Sensitive data

Never record secrets, tokens, cookies, Authorization Bearer values, sensitive URLs, sensitive payloads, real environment values or personal data in docs, tests, examples, logs or knowledge notes.

## Filesystem and Knowledge Repo

- `KnowledgeRoot__Path` points to an external Knowledge Repo; validate root existence and path boundaries before reading or writing.
- Markdown in the Knowledge Repo is canonical. SQLite under `.nero` is derived and can be rebuilt.
- Do not treat `skills/nero/knowledge/` as canonical corpus.
- Writes must stay inside the configured Knowledge Repo and use the existing write policy/path security services.

## MCP and stdio

- stdout belongs to JSON-RPC while the MCP server is running.
- Human diagnostics should go to stderr or structured tool responses.
- Tool errors should identify category, field and next action without echoing sensitive values.

## Git admin operations

- Preserve fast-forward/non-force behavior.
- Preserve dirty-worktree and allowlist protections for commit/pull/push tools.
- Preserve compliance scans before creating commits through MCP admin tools.
- Do not add `--amend`, force push, broad path staging or bypass flags without explicit design and rollback notes.
