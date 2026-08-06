# Schema de knowledge fixo e versionado no Nero

O valor do MCP está no modelo (camadas, registers, health, routing), não em indexar Markdown arbitrário. O Schema vive e versiona no Nero; Knowledge Repos só obedecem. Breaking change de Schema implica bump de versão do Nero. O bootstrap vazio fica em `examples/knowledge-scaffold/`.

**Considered Options**: schema mínimo extensível ad hoc; schema definido por cada Knowledge Repo.
