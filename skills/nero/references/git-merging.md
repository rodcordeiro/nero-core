# Conflitos de merge no Knowledge Repo

Use quando houver conflito de merge em Markdown do Knowledge Repo (`KnowledgeRoot__Path`).

Classifique antes de editar. Done when: o tipo (snapshot / regra de negocio / codigo) esta nomeado e a resolucao segue o ramo abaixo.

## Snapshot

Done when: as duas versoes permanecem no corpus, cada uma com nota de merge.

Mantenha ambos os registros. No proprio registro ou na secao afetada:

```md
> Nota de merge: este snapshot estava em conflito com outro registro e ambos foram mantidos.
```

## Regra de negocio

Done when: o usuario escolheu a versao (ou a consolidacao), apos ver as duas regras, os paths e o impacto pratico.

Pergunte antes de escolher. Recencia, branch atual ou branch recebida nao decidem sozinhas — regra de negocio pode ser decisao externa ao codigo.

Ao perguntar, informe:

- quais regras estao em conflito;
- onde cada uma aparece;
- o impacto pratico de manter cada versao, quando for possivel inferir.

## Codigo ou implementacao

Done when: a regra unica restante bate com o checkout atual (codigo, teste, config ou historico tecnico).

Revise o codigo antes de decidir. Mantenha o que e valido para o estado atual. Se as duas versoes forem parcialmente corretas, consolide numa regra unica.

Excecoes so com evidencia no codigo, testes, config ou historico tecnico.
