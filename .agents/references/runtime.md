# Runtime

## Commands

Run from the repository root.

| Need | Command |
|---|---|
| Restore | `dotnet restore .\mcp\Nero.Knowledge.Base.sln` |
| Test | `dotnet test .\mcp\Nero.Knowledge.Base.sln` |
| Release build | `dotnet build .\mcp\Nero.Knowledge.Base.sln -c Release --no-restore` |
| Publish staged DLL | `dotnet publish .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -c Release --no-build -o .\mcp\publish\staged` |
| Validate scaffold or configured Knowledge Repo | `dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate` |
| Reindex configured Knowledge Repo | `dotnet run --project .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- reindex` |

Prefer the narrowest command that validates the change. For docs-only `.agents` changes, `git diff --check` is usually enough unless a related code/config change exists.

## Server

- Runtime target: `net8.0`.
- Transport: stdio via `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`.
- Human logs are sent to stderr through console logging; stdout belongs to protocol/CLI output.
- The published DLL used by clients is staged at `mcp/publish/staged/Nero.Knowledge.Base.Mcp.dll`.

## Configuration

| Setting | Meaning |
|---|---|
| `KnowledgeRoot__Path` | External Knowledge Repo root; do not point to Nero canonical repo except scaffold development. |
| `KnowledgeDatabase__Path` | SQLite derived index path, typically `<KNOWLEDGE_REPO>\.nero\nero-knowledge.db`. |
| `KnowledgeWrite__Mode` | Write mode used by write/admin tools; `read_only` blocks write and git sync tools. |

If validation cannot run, record the reason and the strongest inspection evidence.
