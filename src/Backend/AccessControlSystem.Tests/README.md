# AccessControlSystem.Tests

Estrutura espelha a das outras camadas, por contexto / use case:

```
Domain/
  ValueObjects/        EmailTests, PasswordTests
  Entities/            OperatorTests, UserTests
  SharedContext/       EntityTests
Application/
  AccountContext/...            testes de Specification e Handler de cada use case
  SharedContext/Tenancy/        TenantResolverTests
  SharedContext/Validation/     ContractExtensionsTests (IsMaxLength: tamanho máximo em caracteres)
  UserContext/Create/           CreateSpecificationTests (só Specification: o Handler é legado vazio)
Architecture/          NoStaticTenantStateTests   (nenhum estado estático com dado de tenant)
                       RequestTextLimitsTests     (todo campo string de todo Request tem limite de tamanho, por reflexão)
                       PackageAuditConfigTests    (NuGet Audit, exceção do SSH.NET por aviso, Dockerfiles e workflows de auditoria)
                       FictitiousPasswordsNotInRepoTests (SC-010: nenhuma senha fictícia literal em código de produção,
                       appsettings*.json, docker-compose*.yml e .github/; com não vacuidade e controle negativo)
Infrastructure/
  Fixtures/            PostgresContainerFixture, PostgresRoleBootstrap, DatabaseProbe,
                       AppRoleProbe (sonda de privilégios de um papel), CanaryRole (papel descartável)
  Premises/            premissas do isolamento por tenant (P-01 a P-07), com container próprio
  Tenancy/             isolamento (filtro do EF + RLS), interceptors, contrato do esquema, mobilidade;
                       prova do usuário de banco da aplicação nas 3 bases: AppRolePrivilegeTests (DDL/papel/RLS
                       negados e sonda limpa), CatalogReadOnlyTests (só leitura), DedicatedRowLevelSecurityTests,
                       AppRoleProbeCanaryTests (papéis-canário: a sonda acusa o privilégio a mais)
  Catalog/             mapeamento e leitura do catálogo (TenantCatalog)
  Migrations/          adoção de base legada (LegacyDataMigrationTests)
  AccountContext/...            testes de repositório de cada use case (com casos de isolamento)
Migrator/              executor, catálogo, orquestrador, verbos (migrate e seed-dev), códigos de saída;
                       os verbos de homologação: SeedStagingCommandTests (seed-staging: só Staging, idempotente, sem o
                       administrador fixo), VerifyNoSeedAdminCommandTests (verify-no-seed-admin: reprova por qualquer
                       dado fixo, só lê), SeedAdminFingerprintTests (id e e-mail lidos da própria migration SeedAdmin) e
                       StagingTestSupport (ambiente isolado e base contaminada de verdade, sem a SeedAdminPolicy)
Api/                   TokenService, validação do JWT e do tenant, login padrão, autorização,
                       contrato dos 8 endpoints, opções (WebApplicationFactory em Api/Support/),
                       InputLimitsApiTests (400 "Requisição inválida" para campo acima do tamanho máximo),
                       HealthEndpointsTests (/health/live e /health/ready: respostas, cache de 5 s, tempo limite,
                       token ignorado) e OnlyHealthEndpointsAreAnonymousTests (únicos anônimos e sem controller)
Support/               FakeTimeProvider (relógio falso para os testes do limite de login)
```

**Papéis-canário** (`Infrastructure/Fixtures/CanaryRole`): papéis de banco pertencem ao cluster inteiro, não ao banco
isolado em que recebem um privilégio. Por isso cada canário tem nome único (`canary_<tipo>_<Guid:N>`), é `NOLOGIN` e é
removido no `DisposeAsync` (`REASSIGN OWNED`, `DROP OWNED` em cada banco e `DROP ROLE`), e o `DisposeAsync` da
`PostgresContainerFixture` falha a coleção (e o `dotnet test`) se sobrar algum `canary_%`. Nenhum teste altera o
`acs_app` nem o `acs_migrator`.

## Mapa de cobertura de autorização (spec de segurança da autenticação e da sessão)

| O que se prova | Onde |
|---|---|
| Rota sem regra de acesso responde `401`; política de fallback; rota inexistente é `404` | `Api/DefaultAuthorizationTests` (usa a rota de prova `OpenProbeController`) |
| Swagger só em Development, `404` fora dele | `Api/SwaggerTests` |
| **Todo endpoint** do `EndpointCatalog`: `401` sem token, `403` por perfil, `401` de outro tenant, `401` com token antigo depois de troca de senha, de perfil e de desativação | `Api/EndpointAuthorizationTests` (teorias geradas do catálogo) |
| A suíte **falha** se uma rota real não está no catálogo (ou o catálogo cita rota inexistente) | `Api/EndpointCoverageTests` + `Api/Support/EndpointCatalog` |
| Token: emissor, destinatário, validade (30 s de tolerância), HS256, `security_version`, chave de 32 bytes | `Api/JwtValidationTests`, `Api/OptionsValidationTests`, `Api/TokenServiceTests` |
| Sessão: troca de senha ou de perfil, desativação e reativação derrubam o token; e-mail, mesmo perfil e senha em branco não | `Api/SessionInvalidationTests`, `Domain/Entities/OperatorTests`, `Infrastructure/Services/SessionValidatorTests` |
| Login em tempo uniforme (a verificação de hash roda nos três casos de falha; hash fictício com a estrutura de um real) | `Application/.../AuthenticateHandlerTests`, `Infrastructure/AccountContext/Authenticate/PasswordVerifierTests`, `Api/LoginUniformityApiTests` |
| Limite por origem e `X-Forwarded-For` só de proxy conhecido | `Api/LoginRateLimitApiTests`, `Api/ForwardedHeadersTests` |
| Limite por conta: escada 1-2-4-8-15 min, descarte em 15 min, saturação com falha fechada, **admissão atômica** (50 chamadas concorrentes admitem exatamente 5) e **rajada na Api** (50 logins simultâneos, 5 verificações de hash) | `Infrastructure/AccountContext/Authenticate/InMemoryLoginThrottleTests`, `Api/LoginThrottleApiTests` |
| Portas do compose só em `127.0.0.1`, sub-rede e IP do nginx fixos | `Architecture/ComposePortsTests` |
| Saúde da Api: `live` sem consulta; `pronta` com no máximo 1 consulta ao catálogo a cada 5 s por instância (sonda falsa que **conta chamadas**, sem medir tempo), 2 s de tempo limite real, queda e volta refletidas em até 5 s, resposta exata sem detalhe | `Api/HealthEndpointsTests` |
| As rotas de saúde são os **únicos** endpoints anônimos novos e os únicos sem controller; só elas carregam o `HealthEndpointMarker` | `Api/OnlyHealthEndpointsAreAnonymousTests` |
| O `Authorization` (válido ou inválido) é ignorado **só** em `/health/*` (zero consultas de sessão e catálogo) e um endpoint protegido segue aceitando token válido e recusando inválido | `Api/HealthEndpointsTests` (`Authorization_enviado_a_saude…`, `Controle_…`, `Endpoint_protegido_segue…`) |

Para um endpoint novo: acrescente-o ao `EndpointCatalog` (com o acesso certo); as teorias de autorização passam a
cobri-lo e o `EndpointCoverageTests` deixa de falhar.

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
