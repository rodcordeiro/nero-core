# Compliance e security no Knowledge Repo

Checklist pos-register e regras anti-leak para escrita/leitura via MCP. Detalhe de tools: `mcp-tools.md`.

## Checklist pos-register

- nenhum token/cookie/connection string/PII verificavel no payload;
- placeholders so os da allowlist exata (`<token>`, `REDACTED`, `***`, `YOUR_API_KEY`, …);
- se `Category: Compliance`, corrigir o campo indicado por `Field`/`RuleId` e retentar (sem ecoar o valor);
- se `Category: Security`, usar path real sob o Knowledge Repo root (sem symlink/junction); nao contornar com escrita shell;
- leitura via search/context/related ja mascara Blocking com `[REDACTED:<ruleId>]` (Warning so em `data_class=restricted`); nunca pedir `include_unmasked`;
- notas legadas sensiveis: quarentenar com `compliance_status: quarantined` + `compliance_reason`, depois `nero_admin_reindex`;
- para commit/push do Knowledge Repo: preferir `nero_admin_create_commit` + `nero_admin_git_push` em vez de shell git; pull so via `nero_admin_git_pull` (ff-only). Em REJECT de compliance no commit, sanitizar o diff e retentar — nunca `--no-verify` / force / amend.

## Apos o lote de escritas

1. `nero_admin_reindex` uma vez.
2. `nero_admin_validate` — exigir `isValid=true` **e** `isCompliant=true`.
3. (Opcional) `nero_admin_compliance_scan` para triage do corpus.
4. (Opcional) `nero_admin_check_index_consistency` se houver suspeita de drift MCP ↔ filesystem.

## Nunca registrar

Tokens, cookies, Authorization Bearer, URLs sensiveis, payloads sensiveis, connection strings ou credenciais em notas, logs, exemplos ou testes. Use placeholders da allowlist.
