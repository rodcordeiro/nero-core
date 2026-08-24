# Finalização de lote sem estado persistido

**Status**: Accepted

**Problem**: Writers materializam Markdown sem reindexar. O cliente precisava coordenar compliance, reindex, validação e comprovação de indexação em chamadas separadas, podendo omitir etapas ou perder evidência de falhas parciais.

**Options**: (1) manter apenas o checklist do cliente; (2) persistir entidades de lote; (3) criar uma operação administrativa stateless sobre paths explícitos.

**Decision**: Adicionar `nero_admin_finalize_batch(expectedPaths)`. A operação aceita até 100 paths Markdown relativos e únicos, exige que todos existam, executa compliance como gate antes de um único reindex, valida e comprova cada path no SQLite. Não cria batch ID nem manifesto persistente.

**Consequences**: O ciclo pós-write passa a retornar evidência estruturada e falhas parciais. Compliance bloqueante ou arquivo ausente impede reindex. Validação inválida pode ocorrer depois de o índice derivado ser substituído e exige correção do Markdown seguida de nova finalização. A operação nunca escreve corpus, commita ou faz push; writers continuam independentes. As tools administrativas individuais permanecem compatíveis.
