# Motor imparcial; corpus em Knowledge Repo externo

Nero é um monorepo de Core + Kit compartilhável. O Corpus de cada pessoa fica em um Knowledge Repo separado, ligado só por config (`KnowledgeRoot`). Assim o artefato canônico permanece imparcial e o risco de push acidental de domínio (compliance) não mora no mesmo histórico do motor.

**Considered Options**: corpus gitignored no mesmo clone; path externo sem repo; monorepo com dados de exemplo de domínio.

**Consequences**: setup em dois remotes; Schema e Scaffold precisam ser claros; MCP nunca assume knowledge dentro de `skills/nero/knowledge` no canônico.
