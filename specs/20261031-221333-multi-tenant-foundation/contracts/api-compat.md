# Contrato: Api HTTP e token

**Plano**: [../plan.md](../plan.md) | **Spec**: FR-005, FR-014 a FR-018, FR-037

O contrato HTTP de `v1/account/*` **não muda**: rotas, métodos, campos de requisição e resposta,
status e mensagens permanecem. Portanto `src/Frontend/src/types/api.ts`, o
`src/Frontend/README.md` e os comentários XML dos endpoints não precisam ser alterados.

## Endpoints (inalterados)

| Método e rota | Autenticação | Status possíveis (como hoje) |
|---|---|---|
| `POST v1/account/login` | anônimo | 200, 400, 401, 429 |
| `POST v1/account/create` | Admin | 201, 400, 401, 403, 409 |
| `GET v1/account/get-me` | autenticado | 200, 401 |
| `GET v1/account/get-list` | Admin | 200, 400, 401, 403 |
| `PUT v1/account/update-me` | autenticado | 200, 400, 401, 409 |
| `PUT v1/account/update/{id}` | Admin | 200, 400, 401, 403, 404, 409 |
| `PUT v1/account/activate/{id}` | Admin | 200, 400, 401, 403, 404 |
| `PUT v1/account/deactivate/{id}` | Admin | 200, 400, 401, 403, 404 |

## Token (JWT)

Claims emitidas pelo `TokenService`:

| Claim | Valor | Mudança |
|---|---|---|
| `Id` | `Guid` do operador | inalterada |
| `name` (`ClaimTypes.Name`) | e-mail do operador | inalterada |
| `role` (`ClaimTypes.Role`) | `Admin` ou `Operator` | inalterada |
| `tenant` | `Guid` do tenant do operador | **NOVA, obrigatória** (Princípio I) |

Validade (8 horas) e algoritmo (HMAC-SHA256) não mudam.

## Rejeição de token (pipeline de autenticação)

Qualquer das condições abaixo resulta em **`401`** com o envelope `ErrorResponse` e a mensagem
genérica de hoje (`"Não autenticado"`), **sem** revelar qual condição ocorreu:

1. token sem a claim `tenant`;
2. claim `tenant` que não é um `Guid`;
3. tenant que não existe no catálogo;
4. tenant inativo no catálogo (vale a cada requisição: inativar corta o acesso na seguinte);
5. conta de operador inexistente ou desativada no tenant (como hoje, agora no escopo do tenant);
6. token emitido antes desta feature (sem `tenant`): o operador precisa fazer login de novo.

Erro de configuração (tenant existe, mas sua base não tem connection string): `500` pelo
envelope de erro padrão. Nunca `401`, nunca outra base.

## Login

- O login usa o tenant de `Login:DefaultTenantId` (ver [configuration.md](configuration.md)).
  Campos, status e mensagens da resposta são os de hoje; o token devolvido carrega o `tenant`.
- Tenant padrão ausente, inexistente ou inativo: `401` com `"Credenciais inválidas"` (o mesmo
  envelope do handler) e o motivo vai só para o log.
- Um header `Authorization` enviado ao login é **ignorado** (o endpoint não autentica por token),
  de modo que um token de outro tenant não influencia a base consultada.
- O limite de tentativas por IP (`login`) segue como está.

## Exigência para novos endpoints

Todo endpoint autenticado futuro herda a validação acima sem código extra. Endpoint anônimo só
com `[AllowAnonymous]` explícito, e que precise de tenant, deve declarar como o resolve (hoje só o
login, por `[UseDefaultLoginTenant]`).
