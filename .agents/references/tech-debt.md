# Tech Debt

- Keep canonical corpus outside `skills/nero/knowledge`; do not mix product skill content with user Knowledge Repo content.
- `AGENTS.md` currently points to the integracoes/API guideline and MCP guideline. If the repo grows a distinct frontend/mobile/powershell surface, add only evidence-backed pointers.
- Publish artifacts under `mcp/publish/` can be locked by MCP clients on Windows; prefer `mcp/publish/staged` during development as documented in `README.md`.
- Revisit this file after each substantial review; keep debt actionable and tied to checkout evidence.
