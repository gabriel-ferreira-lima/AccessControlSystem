# Contrato: configuração da Api e do Migrator

**Plano**: [../plan.md](../plan.md) | **Spec**: FR-004, FR-017, FR-028, FR-029, FR-034

Nenhum segredo vai para `appsettings.json` versionado: os valores vêm de variáveis de ambiente,
user-secrets ou `.env` (não versionado). Nas variáveis de ambiente, `:` vira `__`. Todas as classes
de opções usam `ValidateOnStart`, exceto onde indicado.

## Api (`AccessControlSystem.Api`)

| Chave | Tipo | Obrigatória | Observação |
|---|---|---|---|
| `Secrets:JwtPrivateKey` | texto | sim | assinatura do JWT. A exigência de 256 bits é da spec de segurança |
| `ConnectionStrings:Catalog` | texto | sim | usuário `acs_app` no catálogo (só leitura) |
| `TenantDatabases:{name}:ConnectionString` | texto | uma por base usada | usuário `acs_app`; `{name}` = `tenant_databases.name`. Sem `MigrationConnectionString` na Api |
| `Login:DefaultTenantId` | Guid | não | sem ela, o login responde `401` e registra o motivo (sem `ValidateOnStart`). Existe só até a spec do login multi-tenant |
| `HttpsRedirection:Enabled`, `Cors:*` | | | como hoje |

Removidas: `ConnectionStrings:DefaultConnection`. Mantida sem uso no código (legado, spec
própria): `Secrets:PasswordSaltKey`.

## Migrator (`AccessControlSystem.Migrator`)

| Chave | Tipo | Obrigatória | Observação |
|---|---|---|---|
| `DOTNET_ENVIRONMENT` | texto | sim | `Development` libera o `seed-dev`; qualquer outro valor o recusa |
| `ConnectionStrings:CatalogMigration` | texto | sim | usuário `acs_migrator` (DDL e escrita) no catálogo |
| `TenantDatabases:{name}:MigrationConnectionString` | texto | uma por base | usuário `acs_migrator`; base sem ela é marcada `Failed` |
| `TenantDatabases:{name}:ConnectionString` | texto | só no `seed-dev` | usuário `acs_app`; o seed grava sob RLS |
| `Migrator:BatchSize` | inteiro | não | bases por lote. Padrão inicial `10`, não medido |
| `Migrator:MaxDegreeOfParallelism` | inteiro | não | bases em paralelo dentro do lote. Padrão inicial `2`, não medido |
| `Migrator:LockTimeoutSeconds` | inteiro | não | espera pela trava global. Padrão inicial `600` |
| `InitialTenant:Id` / `Cnpj` / `Name` | Guid / texto / texto | só na adoção | tenant inicial (FR-027/FR-028). O CNPJ é gravado como informado, **sem validação** (o catálogo é do provisionamento); só a unicidade é imposta pelo banco |
| `InitialTenant:DatabaseName` | texto | só na adoção | base que já tem os dados atuais; é onde o backfill usa o tenant inicial |
| `InitialTenant:DatabaseKind` | `Shared`/`Dedicated` | não | padrão `Shared` |
| `DevSeed:AdminPassword` | texto | só no `seed-dev` | senha do `admin@admin.com.br` nos três tenants |
| `DevSeed:OperatorPasswordTenantA` / `OperatorPasswordTenantB` | texto | só no `seed-dev` | senhas diferentes para o mesmo e-mail em A e B |

Se `InitialTenant` não estiver configurado, o `migrate` não registra tenant inicial e as bases
precisam já estar cadastradas no catálogo. Se uma base com linhas legadas for migrada **sem**
`InitialTenant`, a migration `EnforceTenantIsolationOnOperators` falha no `NOT NULL` (comportamento
desejado, R-06).

## Design-time (`dotnet ef migrations add`)

| Chave | Tipo | Obrigatória | Observação |
|---|---|---|---|
| `ConnectionStrings:Migration` | texto | só para gerar migrations do `AppDbContext` | lida por `AppDbContextFactory` (`Api/DesignTime/`). **Só para `dotnet ef migrations add`**: pode ser fictícia, porque gerar a migration não precisa de um banco acessível. Nunca serve para aplicar migrations (quem aplica é o Migrator) |
| `ConnectionStrings:CatalogMigration` | texto | só para gerar migrations do `CatalogDbContext` | a mesma chave usada pelo Migrator em execução; no design-time também pode ser fictícia |

## Ambiente local (`docker-compose.yml` / `.env.example`)

| Variável do `.env` | Uso |
|---|---|
| `POSTGRES_PASSWORD` | superusuário do Postgres, usado só pelos scripts de inicialização |
| `ACS_MIGRATOR_PASSWORD`, `ACS_APP_PASSWORD` | senhas dos papéis `acs_migrator` e `acs_app` (dev) |
| `JWT_PRIVATE_KEY` | `Secrets__JwtPrivateKey` das Apis |
| `PASSWORD_SALT_KEY` | mantida só porque o compose a exige hoje (legado) |
| `DEV_ADMIN_PASSWORD` e as de operador | `DevSeed__*` |
| `BACKEND_PORT` (5290), `BACKEND_B_PORT` (5291), `BACKEND_DEDICATED_PORT` (5292), `FRONTEND_PORT`, `DB_PORT` | portas no host |
| `ASPNETCORE_ENVIRONMENT` | padrão `Development` no compose |

`POSTGRES_USER` e `POSTGRES_DB` deixam de existir (os bancos e papéis são fixos e criados pelos
scripts `docker/postgres/init/01-init.sh` e `docker/postgres/sql/`).
