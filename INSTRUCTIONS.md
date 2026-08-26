# Configuração autônoma do Nero

Configure o ambiente local para a skill `$nero` e o MCP `nero-knowledge-base`.

## Objetivo

Preparar o clone do **Nero** (motor), criar/ligar um **Knowledge Repo** pessoal a partir do scaffold, publicar o MCP e configurar o cliente de agente.

## Premissas

- Nero = Core + Kit. Sem corpus de domínio no canônico.
- Knowledge Repo = repositório **separado** (não path dentro do Nero para uso diário).
- Não sincronizar código com forks corporativos de origem; só ideias conscientes.

## 1. Repositório Nero

1. Identifique a pasta de projetos do usuário (pergunte se incerto).
2. Clone o Nero (URL do remote privado que o dono compartilhou):

```bash
git clone <URL_DO_NERO> nero
cd nero
```

Se já existir, entre na pasta e atualize com `git pull` (sem force).

## 2. Knowledge Repo

```powershell
Copy-Item -Recurse .\examples\knowledge-scaffold <CAMINHO>\my-knowledge
cd <CAMINHO>\my-knowledge
git init
# opcional: remote privado do knowledge
```

O scaffold já inclui `.gitignore` para `.nero/` e `*.db`.

## 3. Skill

Vincule **apenas** `skills/nero` ao diretório de skills do agente (symlink/junction ou cópia).

Não instale skills de domínio de produto dentro do canônico Nero. Se precisar de Domain Skill, siga `skills/nero/references/domain-skills.md` **fora** deste repo.

## 4. Instruções globais do agente

Crie/atualize instruções globais (Codex `AGENTS.md`, Cursor rules, etc.) com:

```md
# Nero

- Use a skill `$nero` para consultar/registrar knowledge operacional via MCP `nero-knowledge-base`.
- O Corpus fica no Knowledge Repo configurado em `KnowledgeRoot__Path` — nunca invente corpus dentro do repo Nero.
- Após lotes `nero_register_*`: `nero_admin_reindex` → `nero_admin_validate` antes de confiar no índice ou commitar o Knowledge Repo.
- Para estruturar um app: playbooks em `skills/nero/prompts/` (`agents-md-references`, `knowledge-review-app-mcp`).
- Domain Skills de libs/produtos ficam fora do Nero; veja `references/domain-skills.md`.
```

## 5. Publicar o MCP

Na raiz do Nero:

```powershell
dotnet restore .\mcp\Nero.Knowledge.Base.sln
dotnet test .\mcp\Nero.Knowledge.Base.sln
dotnet build .\mcp\Nero.Knowledge.Base.sln -c Release --no-restore
dotnet publish .\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -c Release --no-build -o .\mcp\publish\
```

## 6. Configurar o cliente MCP

Aponte para a DLL publicada e para o **Knowledge Repo** (não para `examples/knowledge-scaffold` em uso diário).

### Codex (`~/.codex/config.toml`)

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

### Cursor (`.cursor/mcp.json` ou `~/.cursor/mcp.json`)

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

Reinicie o cliente. Confirme que tools `nero_*` aparecem.

## 7. Smoke test

```powershell
$env:KnowledgeRoot__Path = "<KNOWLEDGE_REPO>"
dotnet run --project <NERO_REPO>\mcp\src\Nero.Knowledge.Base.Mcp\Nero.Knowledge.Base.Mcp.csproj -- validate
```

Exit `0` esperado. No chat: `nero_admin_status` / busca trivial.

## 8. Packs complementares (opcional)

O Core funciona sozinho. **Packs** são produtos extras (skill + MCP próprio, quando houver) em repositórios separados — não entram em `skills/` do Nero canônico. Veja [README — Packs complementares](./README.md#packs-complementares) e ADR [0006](./docs/adr/0006-complementary-packs-core-independent.md).

**Sugestão:** depois do smoke test do Nero Knowledge, instale packs que complementem o fluxo:

| Pergunta | Pack sugerido |
| --- | --- |
| Quem importa/chama o quê no código? | [nero-code-graph](https://github.com/rodcordeiro/nero-code-graph) — MCP de code-graph estrutural (generate / status / query). Clone o repo, publique o MCP e adicione a skill conforme o `README`/`AGENTS.md` dele. |
| Como colaborar com uma pessoa / reunião / 1:1? | [nero-people-crm](https://github.com/rodcordeiro/nero-people-crm) — skill `$people-crm` + template; fichas no vault. Clone, instale a skill, copie `people-crm.local.json.example`. Sem MCP sidecar na v0. |

Regras rápidas:

- Decisões, regras, ops → `nero-knowledge-base` (`$nero`).
- Estrutura de código (AST, imports, calls) → pack de code-graph.
- Pessoas / fichas → pack People CRM (vault). Não copie Profiles para o Knowledge Repo.
- Não misture arestas AST com `links:` do Knowledge Repo.

Backlog dos packs no [Nero Scrum board](https://github.com/users/rodcordeiro/projects/14), issues no repo de cada pack.

## 9. Checklist final

- [ ] Skill `$nero` resolvida pelo agente
- [ ] MCP `nero-knowledge-base` up
- [ ] `KnowledgeRoot__Path` = Knowledge Repo pessoal
- [ ] DB sob `.nero/` gitignored
- [ ] Sem skills de produto corporativas / de empregador neste setup
- [ ] Plano/status: [GitHub Issues](https://github.com/rodcordeiro/nero-core/issues) + specs em `docs/references/`
- [ ] (Opcional) Packs complementares instalados — ex. [nero-code-graph](https://github.com/rodcordeiro/nero-code-graph), [nero-people-crm](https://github.com/rodcordeiro/nero-people-crm)

## Fora de escopo desta instrução

- Commit/push do Nero (Clean Genesis = pedido explícito ao dono do repo).
- Publicar o Nero como repo público.
- Instalar Domain Skills de terceiros dentro de `skills/` do Nero.
