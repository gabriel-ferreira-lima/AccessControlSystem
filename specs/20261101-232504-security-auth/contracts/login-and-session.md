# Contrato: login, token e sessão

**Spec**: [../spec.md](../spec.md) | **Modelo**: [../data-model.md](../data-model.md)

Contrato **observável** da Api depois desta spec. Nenhuma rota, campo ou status existente é removido ou
renomeado; só se acrescenta o `429` por conta no login e a exigência de `iss`, `aud` e
`security_version` no token (Princípio VI: acrescentar não é mudança incompatível). O token antigo,
sem essas claims, deixa de ser aceito: os clientes fazem login de novo.

## 1. `POST v1/account/login`

Anônimo (`[AllowAnonymous]`). Ignora qualquer token enviado. Ordem de decisão:

| # | Condição | Status | `message` | Cabeçalho |
|---|---|---|---|---|
| 1 | Limite **por IP** estourado (20/min por origem) | `429` | `Muitas tentativas. Tente novamente em instantes.` | `Retry-After: <s>` |
| 2 | Tenant padrão ausente/inexistente/inativo | `401` | `Credenciais inválidas` | — |
| 3 | E-mail em formato inválido | `400` | `Requisição inválida` + `notifications` | — |
| 4 | Conta **bloqueada** (e-mail do tenant com tentativas demais, existindo ou não) | `429` | `Muitas tentativas. Tente novamente em instantes.` | `Retry-After: <s>` (≥ 1) |
| 5 | E-mail inexistente, conta inativa **ou** senha errada | `401` | `Credenciais inválidas` | — |
| 6 | Credenciais corretas e conta ativa | `200` | `Usuário autenticado com sucesso` | — |

Regras:

- Em **5** os três casos têm o mesmo corpo (`status`, `message`, `notifications`) e a Api executa o mesmo
  trabalho de verificação de senha.
- A tentativa é **admitida de forma atômica antes** da busca do operador e do hash (a 4 acontece nesse
  momento): cada tentativa admitida conta como falha provisória da conta, e o sucesso (**6**) zera. Assim,
  por mais requisições simultâneas que cheguem, só 5 por nível alcançam a verificação de senha.
- Em **4** a senha correta também é recusada enquanto durar o bloqueio, e nenhum hash é calculado nem
  tentativa é contada.
- O corpo do `429` por conta é o envelope do use case (traz `data: null`); o do `429` por IP é o
  `ErrorResponse` (sem `data`). Mesmo status, mesma `message`, mesmo `Retry-After`.
- Um login bem-sucedido (**6**) zera a contagem de falhas da conta.
- A conta é `(tenant do login, e-mail sem espaços e em minúsculas)`. O mesmo e-mail em outro tenant é
  outra conta.
- E-mail com maiúsculas passa a ser aceito (antes dava `401`); ver [research.md](../research.md), R-09.

### Escada do bloqueio por conta (padrões)

| Bloqueio nº | Depois de | Espera |
|---|---|---|
| 1 | 5 falhas seguidas | 1 min |
| 2 | mais 5 falhas | 2 min |
| 3 | mais 5 falhas | 4 min |
| 4 | mais 5 falhas | 8 min |
| 5 em diante | mais 5 falhas | 15 min (teto) |

Sem nova falha por 15 min (desde a última falha ou o fim do bloqueio), o registro some e a escada
recomeça em 1 min.

## 2. Token de acesso

| Regra | Valor |
|---|---|
| Algoritmo aceito | somente HS256 |
| Emissor (`iss`) | deve ser `Jwt:Issuer`; ausente ou diferente → `401` |
| Destinatário (`aud`) | deve ser `Jwt:Audience`; ausente ou diferente → `401` |
| Validade | 8 h; tolerância de relógio de **30 s** |
| `tenant` | Guid de um tenant existente e ativo no catálogo; senão `401` |
| `security_version` | inteiro igual ao do operador agora; ausente, malformado ou diferente → `401` |

Resposta de qualquer recusa de token, em qualquer rota protegida: `401` com `{"message":"Não autenticado","status":401}`
(envelope `ErrorResponse`). Perfil sem permissão: `403` `Sem permissão`.

## 3. Quando um token deixa de valer (na requisição seguinte)

| Ação | Quem perde o token |
|---|---|
| Operador troca a própria senha (`update-me` com `password`) | ele mesmo |
| Administrador troca a senha de um operador (`update/{id}` com `password`) | esse operador |
| Administrador muda o perfil de um operador para um perfil **diferente** | esse operador |
| Administrador desativa um operador (`deactivate/{id}`) | esse operador; **reativar não o revalida** |
| Troca só de e-mail; perfil igual; senha em branco | ninguém |

`password` presente na requisição conta como troca **mesmo igual à atual**; senha em branco (não enviada)
não conta.

## 4. Rotas protegidas

Toda rota existente exige autenticação por padrão (política de fallback). A única exceção é o login. As
rotas `/swagger/*` só existem em Development; fora dele respondem `404`. Rota inexistente responde `404`.
Matriz completa de testes: [plan.md](../plan.md), seção "Segurança e dados pessoais".

## 5. Console web

Depois de `update-me` com `password` e resposta de sucesso, o console encerra a sessão e mostra no login
**"Senha alterada. Faça login novamente."**. A Api não devolve token novo.
