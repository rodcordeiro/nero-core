# Compliance e security no Knowledge Repo

Checklist pos-lote e anti-leak. Erros, `RuleId` e git via MCP: `mcp-tools.md`. Ordem do lote: `workflow.md`.

## Sanitizar

Done when: payloads, notas, logs e exemplos usam so placeholders da allowlist, e nenhum token/cookie/connection string/PII verificavel permanece.

Allowlist exata: `<token>`, `REDACTED`, `***`, `YOUR_API_KEY` (lista completa em `ComplianceTaxonomy`). Tokens, cookies, Authorization Bearer, URLs/payloads sensiveis, connection strings e credenciais ficam fora de notas, testes e exemplos.

Scan reject-only nos writers: P0 bloqueia secrets de alta precisao e PII verificavel (CPF/CNPJ com checksum, cartao Luhn). JWT / private key / blob base64-like bloqueiam mesmo ao lado de "example". Campos > 64 KiB UTF-8 → `InvalidInput`. Frontmatter novo: `data_class: internal` (`public|internal|restricted`). Snapshots: secao Retencao (revisar 180d / arquivar 365d — recomendacao).

## Pos-register (writer)

Done when: a escrita passou, ou `Category: Compliance` / `Security` foi corrigida e retentada.

- `Category: Compliance` → corrigir o campo `Field`/`RuleId` (placeholder da allowlist); retentar. O valor real nao volta na mensagem.
- `Category: Security` → path real sob o Knowledge Repo (symlink/junction/reparse ficam de fora); writer MCP, nao shell, para contornar.
- Leitura (`nero_search_knowledge` / `nero_get_*_context` / `nero_find_related_knowledge`) mascara Blocking com `[REDACTED:<ruleId>]`. Warning (`pii_suspect`) so em `data_class=restricted`. Mascara e o contrato; nao ha modo unmasked.
- Nota legada sensivel → `compliance_status: quarantined` + `compliance_reason`, depois reindex.

## Apos o lote

Done when: `nero_admin_finalize_batch` retorna `success=true`, ou o fallback manual termina com `isValid=true` e `isCompliant=true`.

1. Preferencial: `nero_admin_finalize_batch` com todos os paths retornados pelos writers. Exigir `success=true`, `isValid=true`, `isCompliant=true` e `missingIndexedPaths=[]`.
2. Fallback manual: `nero_admin_compliance_scan`; se houver hit P0 ativo, parar antes do reindex.
3. `nero_admin_reindex` uma vez (writers so gravam Markdown).
4. `nero_admin_validate`: exigir `isValid=true` e `isCompliant=true`.
5. (Opcional) `nero_admin_check_index_consistency` se MCP ↔ filesystem divergirem.

Search/context/related de notas recem-gravadas depois da finalizacao preferencial ou do reindex no fallback.

## Commit e push

Done when: commit (e push, se pedido) cobre so paths allowlisted, ou a secao foi omitida porque o diff estava vazio.

Preferir `nero_admin_create_commit` + `nero_admin_git_push`. Em REJECT de compliance no commit: sanitizar o diff e retentar. Gates: `mcp-tools.md`.
