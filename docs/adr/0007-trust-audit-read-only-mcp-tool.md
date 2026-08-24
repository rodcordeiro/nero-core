# Trust audit como tool MCP somente leitura

**Status**: Accepted

**Problem**: O primeiro experimento P0 precisa produzir um relatório de confiança reproduzível sobre o Knowledge Repo, sem corrigir, reindexar, arquivar ou promover conhecimento. Um playbook isolado deixaria classificação, ordenação e evidência de zero escrita dependentes do cliente de agente.

**Options**: (1) playbook report-only; (2) tool MCP read-only; (3) tool e playbook no mesmo incremento.

**Decision**: Implementar primeiro `nero_admin_trust_audit` como tool MCP read-only sobre o Markdown canônico. A data de referência é um input explícito opcional, e os códigos de achado são contrato estável. Adiar playbook até existir evidência de que uma camada de orientação humana adicional reduz custo ou erro operacional.

**Consequences**: O relatório é determinístico para o mesmo corpus e `asOfDate`, testável ponta a ponta e independente do SQLite. A superfície MCP cresce de forma aditiva. Ausência de metadados não prova falta de verificação; `NeverVerified` e `UnverifiableClaim` exigem marcadores explícitos. Nenhum achado executa mutação automática.
