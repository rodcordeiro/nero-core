# Nero

Motor imparcial de knowledge base para agentes: **MCP** + skill **`$nero`** + **Kit** (guidelines, references, playbooks).

O Corpus de domínio **não** vive neste repositório. Cada pessoa mantém um **Knowledge Repo** separado (copie `examples/knowledge-scaffold/`).

## TL;DR

1. Clone este repo (Core + Kit).
2. Copie o scaffold para o seu Knowledge Repo e faça `git init` lá.
3. Publique o MCP e aponte `KnowledgeRoot__Path` para o Knowledge Repo.
4. Vincule a skill `skills/nero` no seu cliente de agente.
5. Bootstrap detalhado: [INSTRUCTIONS.md](./INSTRUCTIONS.md).

Glossário: [CONTEXT.md](./CONTEXT.md). Plano: [docs/backlog.md](./docs/backlog.md). Decisões: [docs/adr/](./docs/adr/).

## O que é / o que não é

| É | Não é |
|---|---|
| Core (MCP + `$nero`) + Kit genérico | Corpus de projetos/empresas |
| Schema de knowledge versionado aqui | Skills de domínio de produto (só documentadas) |
| Scaffold vazio em `examples/knowledge-scaffold/` | Sync com qualquer fork de origem corporativa |

Extensão com Domain Skills: `skills/nero/references/domain-skills.md`.

## MCP

Código em `mcp/`. Markdown canônico no **Knowledge Repo**; SQLite é índice derivado. Transporte `stdio`.

Tools e payloads: `skills/nero/references/mcp-tools.md`.

### Build / test / publish

Na raiz do Nero:

```powershell
dotnet restore .\mcp\Nero.Knowledge.Base.sln
dotnet test .\mcp\Nero.Knowledge.Base.sln
dotnet build .\mcp\Nero.Knowledge.Base.sln -c Release --no-restore
dotnet publish .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -c Release --no-build -o .\mcp\publish
```

DLL: `<NERO_REPO>\mcp\publish\staged\Nero.Knowledge.Base.Mcp.dll`

Use `mcp\publish\staged` no desenvolvimento. Se o cliente MCP estiver com a DLL aberta em `mcp\publish`, o publish pode falhar no Windows.

### Variáveis de ambiente

```text
KnowledgeRoot__Path=<CAMINHO_DO_KNOWLEDGE_REPO>
KnowledgeDatabase__Path=<CAMINHO_DO_KNOWLEDGE_REPO>\.nero\nero-knowledge.db
```

Não aponte `KnowledgeRoot__Path` para dentro do Nero canônico (exceto o scaffold de exemplo em desenvolvimento).

### Validar o scaffold (ou seu Knowledge Repo)

```powershell
# scaffold empacotado (cwd = raiz do Nero)
dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate

# Knowledge Repo próprio
$env:KnowledgeRoot__Path = "<CAMINHO_DO_KNOWLEDGE_REPO>"
dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate
```

### Codex CLI

```toml
[mcp_servers.nero-knowledge-base]
command = "dotnet"
args = ["<NERO_REPO>\\mcp\\publish\\staged\\Nero.Knowledge.Base.Mcp.dll"]
enabled = true
startup_timeout_sec = 60
tool_timeout_sec = 120

[mcp_servers.nero-knowledge-base.env]
KnowledgeRoot__Path = "<KNOWLEDGE_REPO>"
KnowledgeDatabase__Path = "<KNOWLEDGE_REPO>\\.nero\\nero-knowledge.db"
```

### Cursor

`.cursor/mcp.json` (projeto) ou `~/.cursor/mcp.json` (global):

```json
{
  "mcpServers": {
    "nero-knowledge-base": {
      "command": "dotnet",
      "args": [
        "<NERO_REPO>\\mcp\\publish\\staged\\Nero.Knowledge.Base.Mcp.dll"
      ],
      "env": {
        "KnowledgeRoot__Path": "<KNOWLEDGE_REPO>",
        "KnowledgeDatabase__Path": "<KNOWLEDGE_REPO>\\.nero\\nero-knowledge.db"
      }
    }
  }
}
```

### Claude Desktop

Mesmo padrão: `command` = `dotnet`, `args` = path da DLL, `env` com `KnowledgeRoot__Path` e `KnowledgeDatabase__Path`.

## Skill `$nero`

Origem: `skills/nero/`.

Vincule (symlink/junction/cópia) ao diretório de skills do seu agente. Kit inclui guidelines api/front/mobile, playbooks de revisão/extração de projeto e geração de `AGENTS.md` + references.

Workflow: `skills/nero/references/workflow.md`.

## Knowledge Repo

Ver [examples/knowledge-scaffold/README.md](./examples/knowledge-scaffold/README.md).

Contrato de escrita: `nero_register_*` grava Markdown; o cliente chama `nero_admin_reindex` após o lote, depois `nero_admin_validate`.

## Licença

MIT — ver [LICENSE](./LICENSE).
