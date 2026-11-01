# AccessControlSystem.Tests

Estrutura espelha a das outras camadas, por contexto / use case:

```
Domain/
  ValueObjects/        EmailTests, PasswordTests
  Entities/            OperatorTests
  SharedContext/       EntityTests
Application/
  AccountContext/...            testes de Specification e Handler de cada use case
  SharedContext/Tenancy/        TenantResolverTests
Architecture/          NoStaticTenantStateTests   (nenhum estado estático com dado de tenant)
Infrastructure/
  Fixtures/            PostgresContainerFixture, PostgresRoleBootstrap, DatabaseProbe
  Premises/            premissas do isolamento por tenant (P-01 a P-07), com container próprio
  Tenancy/             isolamento (filtro do EF + RLS), interceptors, contrato do esquema, mobilidade
  Catalog/             mapeamento e leitura do catálogo (TenantCatalog)
  Migrations/          adoção de base legada (LegacyDataMigrationTests)
  AccountContext/...            testes de repositório de cada use case (com casos de isolamento)
Migrator/              executor, catálogo, orquestrador, verbos (migrate e seed-dev), códigos de saída
Api/                   TokenService, validação do JWT e do tenant, login padrão, autorização,
                       contrato dos 8 endpoints, opções (WebApplicationFactory em Api/Support/)
```

## Rodando

**Rápidos** (Domain + Application + parte da Api, sem Docker):

```
dotnet test --filter "Category!=Integration"
```

**Tudo**, incluindo os testes de infraestrutura, do Migrator e da Api (precisam do Docker ligado):

```
dotnet test
```

## A fixture e os papéis do banco

`PostgresContainerFixture` sobe **um** PostgreSQL real (Testcontainers) por execução e o prepara
como o ambiente de desenvolvimento: os papéis `acs_migrator` (dono das tabelas, DDL, `BYPASSRLS`) e
`acs_app` (sem DDL, sem DELETE, **sujeito ao RLS**) vêm dos **mesmos** SQL de `docker/postgres/sql/`
(embutidos neste assembly), e há três bancos: o catálogo, uma base compartilhada e uma base
dedicada. Os testes de repositório e de Api usam `acs_app`, então o RLS vale de verdade nos testes.

As bases de teste são migradas pelo mesmo executor do Migrator (`EfDatabaseMigrationRunner`), que
neutraliza a migration `SeedAdmin`: **as bases de teste não recebem o administrador de seed**. Cada
teste cria os seus próprios dados (tenants com `Guid` novo, e-mails únicos) em vez de depender de
dados pré-existentes. Testes que contam ou listam bases do catálogo usam um catálogo isolado
(`CreateIsolatedCatalogAsync`), porque o catálogo da fixture é compartilhado.

Conexões: o Npgsql cria um *pool* por connection string, e cada pool guarda conexões ociosas por 5
minutos. Bases criadas por teste (`CreateEmptyDatabaseAsync`, `CreateIsolatedCatalogAsync`) usam
connection strings sem pool (ou com ociosidade curta) para não esgotar o `max_connections` do
container.

## Ferramentas

- **xUnit** — runner
- **NSubstitute** — fakes de `IRepository`, `ITokenService`, `ITenantCatalog` e `IDatabaseMigrationRunner`
- **Testcontainers.PostgreSql** — PostgreSQL efêmero para os testes de infraestrutura, Migrator e Api
- **Microsoft.AspNetCore.Mvc.Testing** — `WebApplicationFactory` com o pipeline real da Api
  (`Api/Support/ApiFactory.cs`, em ambiente `Testing` e com configuração em memória)
