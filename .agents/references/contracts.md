# Contracts

## MCP surface

- Tool classes: `NeroKnowledgeTools` and `NeroAdminTools`.
- Tool discovery: registered from assembly in `McpHost.Configure`.
- Transport: stdio.
- Result DTOs live beside the tool classes in `Presentation/Mcp/Tools`.

## Knowledge tools

Read/context tools include:

- `nero_get_project_context`
- `nero_get_domain_context`

Write/update/link tools include:

- `nero_register_project`
- `nero_update_project_index`
- `nero_update_project_context`
- `nero_update_project_inventory`
- `nero_register_domain`
- `nero_update_domain`
- `nero_inactivate_domain`
- `nero_register_business_rule`
- `nero_register_decision`
- `nero_register_pattern`
- `nero_register_validation_rule`
- `nero_register_snapshot`
- `nero_register_troubleshooting`
- `nero_link_knowledge`

Write tools return recommendations for post-write maintenance; callers are expected to reindex and validate after a write batch.

## Admin tools

Admin/readiness tools include:

- `nero_admin_status`
- `nero_admin_validate`
- `nero_admin_compliance_scan`
- `nero_admin_trust_audit`
- `nero_admin_reindex`
- `nero_admin_check_index_consistency`
- `nero_admin_project_health`
- `nero_admin_ecosystem_health`

Git admin tools include:

- `nero_admin_git_status`
- `nero_admin_git_fetch`
- `nero_admin_git_pull`
- `nero_admin_create_commit`
- `nero_admin_git_push`

Treat git admin contracts as high risk: preserve clean-worktree checks, allowlisted paths, compliance scanning, confirmation phrases and non-force behavior.

`nero_admin_trust_audit` reads canonical Markdown directly and never writes or reindexes. Its optional `asOfDate` (`yyyy-MM-dd`) makes age-based findings reproducible. Category codes are stable: `MissingSource`, `NeverVerified`, `UnverifiableClaim`, `StaleSnapshot`, and `ArchiveCandidate`. Verification findings require explicit `verification_status`; absence alone is not proof that verification never happened.

## CLI surface

`Program.cs` routes known CLI commands before starting the MCP host:

- `reindex`
- `validate`
- `dump-graph`
- `check-orphans`

Keep CLI output stable enough for operators and tests; do not use stdout for human diagnostics while serving MCP over stdio.
