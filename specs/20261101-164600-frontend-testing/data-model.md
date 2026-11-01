# Modelo de dados de teste: Base de testes do frontend

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Esta feature não cria dados de negócio, tabelas nem contratos novos de API. O "modelo" aqui são os
**dados sintéticos de teste** e os formatos que as fábricas produzem. Todos os tipos vêm de
`src/types/api.ts` (não são redefinidos).

## Regras gerais dos dados de teste

- Todo dado é **sintético** (Princípio X): e-mails no domínio reservado `@example.test`, senhas
  fictícias, tokens como texto fixo (`token-de-teste`). Nenhum dado real de pessoa ou de tenant.
- Identificadores são `Guid` em texto, gerados por contador determinístico (não aleatório), para os
  testes serem repetíveis (SC-004).
- Nenhuma fábrica conhece regra de negócio: elas só montam o formato. Quem decide o que a API
  "responderia" é o cenário de cada teste (Princípio VIII).

## Entidades

### Sessão (`Session` = `AuthenticatedOperator`)

| Campo | Tipo | Regra de teste |
|---|---|---|
| `token` | texto | Obrigatório e não vazio; sem ele a sessão guardada é descartada |
| `id` | texto | Se ausente no armazenamento, vira `""` |
| `email` | texto | Se ausente, vira `""` |
| `role` | `"Admin"` \| `"Operator"` | Qualquer valor diferente de `"Admin"` vira `"Operator"` |

Estado guardado em `sessionStorage`, chave `acs.session`.
Transições observadas pelos testes: **sem sessão** → (`signIn`) → **com sessão** → (`signOut`) →
**sem sessão**; **com sessão** → (`401` em chamada autenticada) → **sem sessão + `sessionExpired`**.
`signIn` zera `sessionExpired`; `signOut` não o altera.

### Operador (`OperatorListItem`)

| Campo | Tipo | Observação |
|---|---|---|
| `id` | texto | Vai codificado na URL de editar/ativar/desativar |
| `email` | texto | Mostrado na lista e na janela de confirmação |
| `role` | `Role` | Rótulo: `Admin` → "Administrador", `Operator` → "Operador" |
| `isActive` | booleano | Decide entre "Desativar" e "Ativar" na linha |

Variações: `CreatedOperator` e `UpdatedOperator` têm `id`, `email`, `role`; `Me` tem os mesmos três;
`UpdatedMe` tem `id` e `email`.

### Página de operadores (`OperatorList`)

`operators`, `page`, `size`, `total`, `totalPages`. Cenários de teste: página única; várias
páginas (primeira, do meio, última); vazia (`operators: []`, `totalPages: 0`).

### Envelope de resposta (`ApiEnvelope<T>`)

`message`, `status`, `isSuccess`, `notifications` (lista ou `null`), `data` (`T` ou `null`).
Resultados que as fábricas conseguem produzir:

| Resultado | HTTP | Envelope |
|---|---|---|
| Sucesso | 200 (ou 201) | `isSuccess: true`, `data` preenchido |
| Falha da API | 400/401/403/404/409… | `isSuccess: false`, `message`, `notifications` |
| Sucesso HTTP com envelope de falha | 200 | `isSuccess: false` |
| Corpo fora do envelope | qualquer | texto ou vazio, não JSON |
| Falha de rede | — | a requisição é interrompida |

### Notificação (`ApiNotification`)

`key` e `message`. Chaves usadas nos testes de campo: `email`, `password`, `role` (comparadas sem
diferenciar maiúsculas de minúsculas, como a tela faz) e uma chave sem campo (ex.: `operator`),
que aparece só na lista de notificações do alerta.

### Cenário de caracterização

Não é dado em execução: é a unidade de rastreabilidade. Cada cenário de aceitação da spec
(US1…US7) mapeia para um ou mais `it(...)` em português. A tabela completa fica em
[contracts/cobertura-cenarios.md](contracts/cobertura-cenarios.md).

## Estado de interface exercitado (resumo por tela)

| Tela | Estados cobertos |
|---|---|
| Login | em branco · enviando · erro da API · aviso de sessão encerrada · já autenticado (redireciona) |
| Lista de operadores | carregando · com dados · vazia · erro de carga · janela aberta · janela enviando · erro dentro da janela |
| Criar operador | em branco · enviando · erro (geral e por campo) · sucesso |
| Editar operador | sem identificador · pré-preenchida pela lista · em branco (acesso direto) · enviando · erro · sucesso |
| Minha conta | carregando dados · dados carregados · erro de carga · enviando · erro de envio · sucesso |
