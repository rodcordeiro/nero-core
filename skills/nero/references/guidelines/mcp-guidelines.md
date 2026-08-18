# MCP Guidelines

Use este guideline para repositorios que implementam ou operam Model Context Protocol (MCP): servidores, conectores, tools, resources, prompts, transports e repositorios de knowledge/skills que consomem MCP como workflow operacional. Aplique cada regra da secao que a tarefa tocar.

## Classificacao

Done when: o checkout esta em um dos tres ramos abaixo, com evidencia (path de entrypoint, SDK ou tools usadas).

- **Servidor MCP**: entrypoint instancia servidor, registra tools/resources/prompts e conecta transport (`stdio`, Streamable HTTP ou equivalente). Ex.: `Acme.Mcp` (.NET) ou um host TypeScript com `@modelcontextprotocol/sdk`.
- **Consumidor MCP / knowledge workflow**: o checkout usa tools MCP para consulta, escrita, reindexacao, validacao ou git administrativo, sem expor servidor proprio.
- **Hibrido**: servidor MCP que tambem contem skill, prompts ou Knowledge Repo. Documentar as duas superficies em secoes distintas.

Servidor MCP exige evidencia de entrypoint, dependencia SDK ou registro de capabilities/tools.

## Arquitetura MCP

- MCP e um protocolo JSON-RPC entre host/client e servidores focados. O host orquestra, autentica, pede consentimento e compoe resultados; o servidor nao deve assumir acesso ao historico completo da conversa nem a outros servidores.
- O `initialize` deve comunicar `protocolVersion`, `serverInfo` e `capabilities` reais. Documente capacidades efetivas: `tools`, `resources`, `prompts`, `logging`, `tasks` se existir.
- Servidores pequenos, composaveis e centrados numa fronteira operacional clara. Um servidor mistura produto, shell, cloud e knowledge so quando essa fronteira e o produto.
- Em AGENTS/references, registre entrypoint, transport, capabilities, configuracao, comandos de validacao e limites de escrita.

## Tools

- Nomeie tools por verbo + dominio (`nero_register_pattern`, `acme_create_todo`) e mantenha descricoes objetivas sobre efeito, pre-condicoes e risco.
- Use schema estruturado para inputs (Zod, atributos/tipos .NET ou equivalente). `object` livre so quando o contrato ainda e desconhecido.
- Retorne conteudo estruturado e legivel para agente. Para JSON em `content.text`, mantenha shape estavel e campos de erro previsiveis.
- Para escrita, prefira preview/confirmacao explicita, idempotencia quando possivel e resultado parcial por item em lotes.
- Anote hints quando o SDK suportar: `readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`.
- Secrets ficam fora de erros, logs, exemplos e payloads. Redija mensagens antes de devolver ao cliente.
- Erros devem indicar categoria, campo e acao recomendada sem vazar valor sensivel.

## Resources E Prompts

- Use `resources` e `resource templates` para contexto read-only, inventarios, documentos ou artefatos estaveis. Prefira resource a tool quando a operacao for leitura pura e enderecavel.
- URIs devem ser estaveis, sem caminhos absolutos sensiveis e sem depender de layout local do usuario quando puder haver alias logico.
- Use paginacao/cursors quando listas puderem crescer.
- Prompts operacionais versionados em arquivos ou registros pequenos. Recommendations apontam path relativo; o texto do prompt vive no arquivo.

## Transports

- **stdio**: stdout pertence ao JSON-RPC. Logs humanos vao para stderr ou capability de logging.
- **Streamable HTTP / HTTP**: valide session id, lifecycle e encerramento de transport; use autenticacao e escopos quando exposto fora do localhost.
- Documente no AGENTS qual transport e suportado, como iniciar o servidor e quais clients/hosts foram considerados.

## Seguranca

- Trate tool MCP como codigo executavel. Escritas, chamadas externas, filesystem, git e shell precisam de fronteiras explicitas e consentimento do usuario/host.
- Aplique least privilege em tokens, scopes, paths e operacoes. Prefira allowlist de diretorios e comandos a blacklist.
- Credenciais, API keys e dados de pagamento via fluxo URL/autorizacao do provedor. Formularios de elicitation ficam para dados nao-secretos.
- Valide path traversal, symlink/junction/reparse point e root autorizado antes de ler/escrever.
- Para git via MCP, exija worktree esperado, paths explicitos, scan de compliance e bloqueio de force/amend/rebase quando nao fizer parte do contrato.
- Knowledge e logs usam placeholders da allowlist no lugar de tokens, cookies, Authorization Bearer, URLs sensiveis, payloads sensiveis ou valores reais de ambiente.

## Validacao

- TypeScript MCP: preferir `pnpm run check`; quando indisponivel, `pnpm run typecheck`, `pnpm run lint`, `pnpm run test`, `pnpm run build`.
- .NET MCP: preferir `dotnet test` e `dotnet build` na solution/projeto MCP.
- Validar contrato de tools com testes de schema, erro, redacao de secrets, confirmacao de escrita e serializacao de resposta.
- Validar lifecycle/host smoke quando houver entrypoint MCP dedicado.
- Use MCP Inspector somente quando aplicavel ao transport e depois de buildar o servidor.

## AGENTS/references Para MCP

Ao criar ou atualizar `.agents/references/` em repositorio MCP, priorize:

- `structure.md`: entrypoint, pastas de tools/resources/prompts, clients, services e testes.
- `runtime.md`: transport, comando de start/build, env vars obrigatorias e host/client esperado.
- `contracts.md`: inventario de tools/resources/prompts, schemas, outputs, hints e modos read/write.
- `security.md`: secrets, consentimento, allowlists, fronteiras de filesystem/git/rede e redacao.
- `conventions.md`: padroes de nomeacao, preview/confirm, erros, logging e validacao.
- `tech-debt.md`: gaps entre checkout e guideline, sem reestruturar codigo neste playbook.

Para repositorios consumidores de MCP, documente o fluxo operacional real (`consultar contexto`, `registrar`, `reindexar`, `validar`, `git`) e cite as tools usadas. Comportamento de servidor so com evidencia no checkout.
