# Conflitos de merge no Knowledge Repo

Use esta referencia quando resolver conflitos de merge em arquivos Markdown do Knowledge Repo (`KnowledgeRoot__Path`).

## Regra geral

Ao resolver um conflito, preserve a intencao das duas alteracoes ate entender o tipo de informacao em disputa.

Classifique o conflito antes de editar:

- snapshot;
- regra de negocio;
- regra de codigo ou implementacao.

## Snapshots

Quando o conflito envolver snapshots de conhecimento, mantenha ambos os registros.

Indique explicitamente que os snapshots estavam em conflito no merge, para preservar o historico e deixar claro que as duas versoes coexistiam no momento da integracao.

Use uma nota curta no proprio registro ou na secao afetada, por exemplo:

```md
> Nota de merge: este snapshot estava em conflito com outro registro e ambos foram mantidos.
```

## Regras de negocio

Quando o conflito envolver regra de negocio, questione o usuario antes de escolher uma versao.

Nao assuma qual regra e valida apenas pela versao mais recente, pela branch atual ou pela branch recebida. Regras de negocio podem representar decisoes externas ao codigo.

Ao perguntar, informe objetivamente:

- quais regras estao em conflito;
- onde cada uma aparece;
- qual seria o impacto pratico de manter cada versao, quando for possivel inferir.

## Regras de codigo ou implementacao

Quando o conflito envolver regras de codigo, padroes tecnicos, comandos, exemplos de implementacao ou comportamento derivado do repositorio, revise o codigo antes de decidir.

Mantenha apenas a informacao valida para o estado atual do projeto.

Se as duas versoes estiverem parcialmente corretas, consolide em uma regra unica e remova duplicidade, ambiguidade ou instrucao obsoleta.

Registre excecoes somente quando elas forem comprovadas pelo codigo, por testes, por configuracao do projeto ou por historico tecnico confiavel.
