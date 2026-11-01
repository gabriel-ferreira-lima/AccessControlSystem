# Plano de Implementação: Fundação Multi-Tenant

**Branch**: `20261031-221333-multi-tenant-foundation` | **Data**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da feature em `/specs/20261031-221333-multi-tenant-foundation/spec.md`

## Resumo

Transformar a Api de uma base única em uma Api que atende N tenants, num modelo híbrido (bases
compartilhadas e dedicadas), sem mudar nada do que o operador vê hoje. Abordagem técnica (decisões
e alternativas em [research.md](research.md)):

- **Catálogo** em base própria (`CatalogDbContext`, migrations próprias): `tenants` e
  `tenant_databases`, esta última também guardando o estado da atualização de cada base. O catálogo
  pertence ao sistema de provisionamento: o **Domain não muda**, a Application só o lê por
  `ITenantCatalog` (devolve `record`s) e o modelo de persistência fica na Infrastructure. O CNPJ é
  texto lido, sem validação aqui.
- **Tenant por requisição**: a claim `tenant` do token é validada no `OnTokenValidated` contra o
  catálogo; o `TenantResolver` preenche um `TenantScope` (scoped) que informa o tenant e a base. O
  `AppDbContext` passa a ser criado por requisição com a connection string dessa base.
- **Isolamento em duas camadas independentes**: filtro global do EF por `tenant_id` (shadow
  property) e **RLS** no PostgreSQL (`ENABLE` + `FORCE`, política por `app.tenant_id`). O tenant vai
  ao banco com `set_config(..., true)` (escopo de transação, compatível com PgBouncer): escritas
  usam transação do EF + `DbTransactionInterceptor`; leituras levam o `set_config` no mesmo lote do
  comando (`DbCommandInterceptor`), em uma só ida ao banco.
- **Migrations novas** (sem editar as aplicadas): `AddTenantIdToOperators` (nullable + backfill do
  tenant inicial) e `EnforceTenantIsolationOnOperators` (NOT NULL, índice único `(tenant_id,
  email)`, índice `(tenant_id, id)`, RLS).
- **Atualização das bases fora da Api**: novo projeto `AccessControlSystem.Migrator`, que aplica as
  migrations do catálogo e de cada base de tenant em lotes, retomável, com o estado por base no
  catálogo, e que neutraliza a migration `SeedAdmin` em bases novas. Também traz o comando
  `seed-dev` (só em Development).
- **Configuração tipada**: `Application/Configuration.cs` (estático) é removido; segredos e
  connection strings viram `IOptions<T>` com `ValidateOnStart`.
- **Ambiente de desenvolvimento**: `docker compose up --build` sobe Postgres (catálogo + 2 bases de
  tenant), o migrator (one-shot), e uma Api por tenant de teste (3 portas).

O use case de referência é `AccountContext/UseCases/GetMe`: não muda nenhum arquivo dele
(Request, Response, Specification, Handler, `IRepository`, `Repository`). Só o `AppDbContext` que
ele recebe passa a ser criado por requisição.

## Contexto Técnico

**Linguagem/Versão**: C# / .NET 10 (Api, Application, Domain, Infrastructure, Tests); novo projeto
Migrator também em .NET 10.

**Dependências Principais**: ASP.NET Core, EF Core 10.0.12 + Npgsql EF 10.0.3, JwtBearer, Flunt,
SecureIdentity (Domain). **Nenhum pacote novo em produção previsto**: T081 e T085 conferem se a
Infrastructure precisa de referências explícitas a `Microsoft.Extensions.*` (para `IOptions` e
`ValidateOnStart`) e, **se precisar, elas são justificadas aqui** no mesmo trabalho. Em testes:
`Microsoft.AspNetCore.Mvc.Testing` 10.0.12 (ver Complexity Tracking). **Ferramenta de
desenvolvimento nova**: `dotnet-ef` 10.0.12 em manifesto local (`.config/dotnet-tools.json`), usada só
para gerar migrations, fora das imagens de runtime (ver Complexity Tracking). Redis e OpenTelemetry
**não** entram (fora do escopo da spec).

**Armazenamento**: PostgreSQL 18, três papéis de dados: catálogo (base própria), bases
compartilhadas e bases dedicadas (mesmo esquema de tenant). RLS ativo nas tabelas de tenant.

**Testes**: xUnit, NSubstitute, Testcontainers.PostgreSql (já existentes). TDD obrigatório
(Princípio VII).

**Plataforma Alvo**: containers Linux (Docker Compose local nesta spec; AWS fica para a spec de
ambientes).

**Tipo de Projeto**: web-service (Api REST) + ferramenta de linha de comando (Migrator). O
frontend não muda.

**Metas de Desempenho** (Princípio XIII, sem números novos): endpoints comuns p95 ≤ 300 ms e
p99 ≤ 1 s; taxa de 5xx < 0,1%. **A medição fica para a spec 06** (ver Complexity Tracking): nesta
spec não há baseline, e nenhum número foi estimado.

**Restrições**: contrato HTTP de `v1/account/*` inalterado (exceto exigir novo login para tokens
sem `tenant`); `AppDbContext` por requisição, sem estado estático ou cache de dados de tenant;
usuário de banco da aplicação sem DDL, sem DELETE e sem `BYPASSRLS`; migrations aplicáveis a N
bases de forma idempotente.

**Escala/Escopo**: de algumas centenas a 150 mil tenants (Princípio XIII), crescendo por bases
compartilhadas e dedicadas. O número de bases não é fixado aqui; o orquestrador processa em lotes
com concorrência configurável.

## Verificação da Constituição

*GATE: passa antes da Fase 0; reavaliado após o design da Fase 1. Detalhes das exceções em
Complexity Tracking.*

| Princípio | Pré-pesquisa | Pós-design | Como o plano atende |
|---|---|---|---|
| I. Isolamento e Catálogo (NÃO NEGOCIÁVEL) | ✅ | ✅ | `tenant_id` shadow property em toda tabela de tenant; RLS `ENABLE`+`FORCE` e filtro do EF; `set_config(..., true)`; índices começando por `tenant_id`; `AppDbContext` scoped; `Configuration.cs` estático removido; catálogo só por `ITenantCatalog`; claim `tenant` obrigatória; `SeedAdmin` neutralizado; mover tenant = copiar linhas. Diretório de operadores e login multi-tenant ficam para specs próprias |
| II. Clean Architecture | ✅ | ⚠️ justificado | **Domain não muda** (nem a classe `Entity`); Application só ganha interfaces e `record`s (incluindo `EDatabaseKind`); EF/Npgsql em Infrastructure/Api. Novo projeto Migrator (raiz de composição) referencia a Api para enxergar as migrations |
| III. Vertical slice | ✅ | ✅ | Nenhum use case novo ou alterado; assinaturas do `AccountContext` intactas. O catálogo é infraestrutura de plataforma, não um contexto de negócio: modelos e mapeamentos em `Infrastructure/Catalog/` |
| IV. Domínio rico | ✅ | ✅ | Nada novo no Domain. `Operator` não ganha `TenantId`. Os modelos de persistência do catálogo (que pertence ao provisionamento) ficam na Infrastructure, sem regra de domínio |
| V. Contrato de use case | ✅ | ✅ | Sem use case novo. O filtro de login devolve o mesmo envelope `Response` (`401`, `"Credenciais inválidas"`) |
| VI. Api fina | ✅ | ✅ | Controller só ganha o atributo `[UseDefaultLoginTenant]`. Sem rota nova; contrato inalterado |
| VII. Test-First (NÃO NEGOCIÁVEL) | ✅ | ✅ | Testes primeiro, falhando (ver "Estratégia de testes"). Um teste existente é **substituído por mudança de spec**, de forma explícita |
| VIII. Frontend só apresentação | ✅ | ✅ | Sem mudança no frontend |
| IX. Segurança (NÃO NEGOCIÁVEL) | ✅ | ✅ | Usuário da aplicação sem DDL, sem DELETE, sem `BYPASSRLS`; migrator com usuário próprio; mensagem 401 genérica; strings parametrizadas. Itens legados do IX (iss/aud, versão de segurança, rate limit, KnownProxies, Swagger) **não** pertencem a esta spec e não pioram |
| X. LGPD | ✅ | ✅ | Nenhum dado pessoal novo; dados de dev sintéticos; seed só em Development |
| XI. Auditoria | ⚠️ | ⚠️ legado | Sem use case de escrita novo, então nenhum evento novo. A lacuna legada (`AccountContext` sem auditoria) continua e não é tratada aqui |
| XII. Permissões e roles | ✅ N/A | ✅ N/A | Nenhuma permissão nova; autorização por role legada permanece |
| XIII. Escalabilidade (NÃO NEGOCIÁVEL) | ❌ | ⚠️ justificado | Api sem estado ✅; consultas indexadas ✅; passo de migrations em lotes, retomável e com estado por base ✅. **Exceções**: 2 idas ao banco por requisição autenticada (catálogo + base do tenant), migração fundadora sem compatibilidade com a Api anterior, e k6 adiado para a spec 06. **Lock de jobs**: o Migrator não é um job em background da Api, é um passo único do pipeline de entrega, então o `pg_advisory_lock` no catálogo fica (o lock no Redis do Princípio XIII vale para jobs da Api e não se aplica a este passo) |
| Padrões técnicos | ✅ | ✅ | Código em inglês, comentários/regiões/testes em português; `snake_case`; mapeamento por `IEntityTypeConfiguration`; `timestamptz`; `xmin` nas entidades editáveis do catálogo; segredos fora do `appsettings.json` |
| Operação | ⚠️ | ⚠️ legado | `/health/*`, logs com `tenantId` e OpenTelemetry são legado de outras specs. Backup/RPO fica para a spec de ambientes |
| Quality gates e entrega | ✅ | ✅ | `dotnet build/test` já rodam no CI; Migrator entra na solução e no CI. `dotnet list package --vulnerable` roda localmente antes do PR (o CI atual não tem; o scan de vulnerabilidades no CI é escopo da spec 04, security-hardening) |

**Resultado do gate**: aprovado, com 9 itens em Complexity Tracking.

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261031-221333-multi-tenant-foundation/
├── plan.md              # Este arquivo
├── research.md          # Fase 0: decisões, justificativas e alternativas
├── data-model.md        # Fase 1: catálogo, tabelas de tenant, estados
├── quickstart.md        # Fase 1: roteiro de validação ponta a ponta
├── contracts/
│   ├── api-compat.md            # Contrato HTTP e do token
│   ├── configuration.md         # Chaves de configuração da Api e do Migrator
│   ├── database-isolation.md    # Contrato SQL: RLS, papéis, privilégios
│   ├── internal-interfaces.md   # Interfaces da Application (tenant e catálogo)
│   └── migrator-cli.md          # Comandos, códigos de saída, estados por base
└── tasks.md             # Fase 2 (/speckit-tasks, não criado aqui)
```

### Código-fonte (raiz do repositório)

```text
docker-compose.yml                 # reescrito: db, migrator, 3 Apis, frontend
.env.example                       # variáveis novas (usuários de banco, seed de dev)
.config/dotnet-tools.json          # NOVO: manifesto local com o dotnet-ef 10.0.12 (só para gerar migrations)
.gitattributes                     # NOVO: `*.sh text eol=lf` (o Git desta máquina converte para CRLF)
docker/postgres/sql/               # NOVO: papéis e privilégios (também embutidos nos testes)
├── roles.sql
├── tenant-db-grants.sql
└── catalog-db-grants.sql
docker/postgres/init/01-init.sh    # NOVO: cria papéis (senhas do ambiente) e bancos e aplica os SQL acima
README.md, CLAUDE.md               # comandos de migrations e do ambiente de dev atualizados

src/Backend/
├── AccessControlSystem.Domain/                           # SEM MUDANÇAS (nem Entity)
├── AccessControlSystem.Application/
│   ├── Configuration.cs                                  # REMOVIDO
│   └── SharedContext/Tenancy/
│       ├── ITenantContext.cs, ITenantResolver.cs, ITenantCatalog.cs
│       ├── TenantInfo.cs (+ TenantDatabaseInfo)          # records devolvidos ao código de tenant
│       └── EDatabaseKind.cs
├── AccessControlSystem.Infrastructure/
│   ├── Data/
│   │   ├── AppDbContext.cs                               # ALTERADO: ITenantContext, filtro, tenant_id
│   │   ├── CatalogDbContext.cs                           # NOVO
│   │   ├── TenantModelBuilderExtensions.cs               # NOVO: shadow tenant_id, filtro, índice
│   │   ├── ITenantAwareDbContext.cs                      # NOVO: expõe o ITenantContext aos interceptors
│   │   └── Interceptors/TenantTransactionInterceptor.cs, TenantCommandInterceptor.cs   # instâncias `static readonly` sem estado
│   ├── Catalog/                                          # NOVO: persistência do catálogo (só Infrastructure)
│   │   ├── Models/CatalogTenant.cs, CatalogDatabase.cs, EMigrationStatus.cs
│   │   └── Mapping/CatalogTenantMap.cs, CatalogDatabaseMap.cs
│   ├── Context/AccountContext/Mapping/OperatorMap.cs     # ALTERADO: sem índice único de e-mail no modelo
│   ├── Services/TenantCatalog.cs                         # NOVO (ITenantCatalog)
│   ├── Services/ActiveSessionChecker.cs                  # sem mudança de código
│   └── Tenancy/
│       ├── TenantScope.cs, NoTenantContext.cs, TenantResolver.cs, TenantDatabaseConnectionProvider.cs
│       ├── TenantDbContextOptions.cs                     # configuração única do contexto com tenant (Api, Migrator, testes)
│       ├── Options/CatalogOptions.cs, TenantDatabasesOptions.cs
│       └── TenancyServiceCollectionExtensions.cs         # AddTenancy(...) usado pela Api (e pelos testes de Api); Migrator e fixture usam TenantDbContextOptions
├── AccessControlSystem.Api/
│   ├── Program.cs                                        # sem ApplyMigrations; partial Program
│   ├── Extensions/BuilderExtensions.cs                   # ALTERADO: IOptions, AddTenancy, OnTokenValidated
│   ├── Extensions/MigrationExtensions.cs                 # REMOVIDO
│   ├── Extensions/ClaimPrincipalExtensions.cs            # + TenantId()
│   ├── Filters/UseDefaultLoginTenantAttribute.cs         # NOVO (resource filter do login)
│   ├── Options/SecretsOptions.cs, LoginOptions.cs        # NOVO
│   ├── Services/AccountContext/TokenService.cs           # ALTERADO: IOptions + claim "tenant"
│   ├── Controllers/AccountContextController.cs          # só o atributo no Login
│   ├── DesignTime/AppDbContextFactory.cs, CatalogDbContextFactory.cs   # NOVO (ferramentas do EF)
│   ├── Migrations/                                       # existentes intactas + 2 novas (AppDbContext)
│   ├── Migrations/Catalog/                               # NOVO: migrations do CatalogDbContext
│   └── appsettings.json, appsettings.Development.json    # ALTERADO (o Dockerfile da Api não muda)
├── AccessControlSystem.Migrator/                         # NOVO (ver Complexity Tracking)
│   ├── Program.cs                                        # verbos: migrate, seed-dev
│   ├── Commands/MigrateCommand.cs, SeedDevCommand.cs
│   ├── Orchestration/MigrationOrchestrator.cs, IDatabaseMigrationRunner.cs, EfDatabaseMigrationRunner.cs
│   ├── Orchestration/SeedAdminPolicy.cs, CatalogRegistry.cs, InitialTenantRegistrar.cs   # estado por base; tenant inicial
│   ├── Options/MigratorOptions.cs, InitialTenantOptions.cs, DevSeedOptions.cs
│   └── Dockerfile
├── Backend.slnx                                          # + Migrator
└── AccessControlSystem.Tests/
    ├── Application/SharedContext/Tenancy/                # TenantResolver (unitário)
    ├── Architecture/                                     # NoStaticTenantStateTests (FR-013, reflexão)
    ├── Infrastructure/Fixtures/PostgresContainerFixture.cs  # ADAPTADA (papéis, RLS, por tenant)
    ├── Infrastructure/Fixtures/PostgresRoleBootstrap.cs  # NOVO: papéis/bancos de teste, usado pelas duas fixtures
    ├── Infrastructure/Premises/                          # premissas P-01 a P-07 (R-14), permanentes
    ├── Infrastructure/Tenancy/                           # isolamento, RLS, interceptors, mobilidade
    ├── Infrastructure/Catalog/                           # TenantCatalog (leitura), mapeamentos, CNPJ único, xmin
    ├── Infrastructure/Migrations/                        # dados atuais → tenant inicial; base nova
    ├── Migrator/                                         # orquestrador: lotes, retomada, falha, trava
    └── Api/                                              # TokenServiceTests (alterado), JwtTenantValidationTests,
                                                          # EndpointAuthorizationTests, DefaultLoginTenantTests,
                                                          # ApiContractCompatTests
```

**Decisão de estrutura**: manter as quatro camadas existentes e acrescentar **um** projeto
(`Migrator`), porque o passo de atualização tem ciclo de vida, imagem e credenciais diferentes
da Api (Princípio IX). Migrations continuam no assembly da Api (constituição); o Migrator as
enxerga por referência de projeto.

## Consultas novas e índices (Princípio XIII)

| Consulta | Onde | Índice que a atende |
|---|---|---|
| Tenant por id, com a sua base (1 consulta, 2 chaves primárias) | catálogo, a cada requisição autenticada | `PK_tenants`, `PK_tenant_databases` |
| Tenants ativos, paginação por chave (`id > @after`, `LIMIT`) | catálogo (`GetActiveTenantListAsync`) | `ix_tenants_id_active` (parcial, `WHERE is_active`) |
| Bases pendentes de atualização, em lotes | catálogo, no Migrator | `ix_tenant_databases_migration_status_id` |
| Operador por e-mail (login), `Any` de e-mail em uso | tenant | `ix_operators_tenant_id_email` (único, nova) |
| Operador por id (`GetMe`, `Update`, `Activate`, `Deactivate`, sessão ativa) | tenant | `ix_operators_tenant_id_id` (nova); a PK `(id)` continua existindo |
| Lista paginada e contagem de operadores | tenant | `ix_operators_tenant_id_id` |
| Contagem de administradores ativos (último admin) | tenant | `ix_operators_tenant_id_id` (varre só as linhas do tenant) |

Nenhuma consulta em laço e nenhuma carga sem limite. O filtro de RLS e o filtro do EF acrescentam
`tenant_id = ...` a todas elas, e o primeiro campo dos índices novos é `tenant_id`.

## Segurança e dados pessoais

**Permissões novas do catálogo (Princípio XII)**: nenhuma. Não há endpoint novo.

**Matriz de autorização (endpoint × exigência)**: as exigências de role de hoje não mudam. Muda
o requisito de entrada: **todo** endpoint autenticado passa a exigir token com tenant válido.

| Endpoint | Role (inalterada) | Tenant no token | 401 | 403 |
|---|---|---|---|---|
| `POST v1/account/login` | anônimo | ignora o token; usa `Login:DefaultTenantId` | tenant padrão ausente/inválido, credenciais | n/a |
| `GET v1/account/get-me` | autenticado | obrigatório e ativo | sem token, sem tenant, tenant inexistente/inativo, conta desativada | n/a |
| `PUT v1/account/update-me` | autenticado | obrigatório e ativo | idem | n/a |
| `POST v1/account/create` | Admin | obrigatório e ativo | idem | role sem Admin |
| `GET v1/account/get-list` | Admin | obrigatório e ativo | idem | role sem Admin |
| `PUT v1/account/update/{id}` | Admin | obrigatório e ativo | idem | role sem Admin |
| `PUT v1/account/activate/{id}` | Admin | obrigatório e ativo | idem | role sem Admin |
| `PUT v1/account/deactivate/{id}` | Admin | obrigatório e ativo | idem | role sem Admin |

**Casos de abuso considerados**
- Token sem `tenant`, com `tenant` malformado, de tenant inexistente ou inativo: `401` genérico
  (`"Não autenticado"`), sem revelar o motivo (FR-015).
- Token do tenant A com o `Id` de um operador do tenant B: o operador não existe no tenant A, a
  sessão é rejeitada (`401`). Acesso por id a registro de outro tenant: `404`.
- Esquecer o filtro do EF em uma consulta: o RLS devolve só as linhas do tenant corrente (teste
  com `IgnoreQueryFilters`).
- Gravar linha de outro tenant: recusada pelo `WITH CHECK` da política.
- Vazamento do tenant entre requisições na mesma conexão do pool: o `set_config` é local à
  transação/lote e não sobrevive a ela. Teste com pool de uma conexão alternando tenants.
- Conexão sem tenant: o interceptor falha antes de ir ao banco e a política devolve 0 linhas.
- Injeção por `set_config`: o valor vai por parâmetro, nunca concatenado.
- Token enviado ao `login` tentando fixar outro tenant: o token é ignorado nessa rota.
- Chave de assinatura global comprometida permite forjar a claim `tenant`: risco conhecido do
  desenho (constituição); a validação no catálogo só impede tenants inexistentes/inativos.
- Credenciais: o catálogo guarda só uma referência à base (nome); connection strings vêm de
  configuração/segredos. A Api nunca recebe a connection string de migrations (usuário com DDL).
- Admin de seed: nenhuma base nova o recebe; o `seed-dev` recusa rodar fora de Development.

**Dados pessoais tocados**: e-mail do operador e hash de senha (já existentes, só ganham o
vínculo ao tenant). O catálogo guarda CNPJ e nome da empresa cliente, que são dados da empresa e
não de pessoa natural. Nenhum dado pessoal novo é coletado.

**Eventos de auditoria gerados**: nenhum. Não há use case de escrita novo; a lacuna do
Princípio XI no `AccountContext` é legada e segue fora desta spec. O estado de atualização por
base (`tenant_databases`) é registro operacional, não evento de auditoria de tenant.

## Estratégia de testes (TDD)

Os testes de cada item são escritos e executados falhando antes da implementação (Princípio VII).
Lista de alto nível; o `tasks.md` detalha a ordem.

**Ordem obrigatória no `tasks.md` (decisão do dono do projeto).** Os testes das sete premissas
P-01 a P-07 de [research.md](research.md) (R-14) são as **primeiras tarefas da implementação**,
antes de qualquer outra, em especial o **P-02** (leituras com RLS). Se alguma falhar:
- P-01, P-02, P-03 ou P-05: adotar o **plano B do R-01** (transação curta por comando), **sem
  contornos**; se o plano B também falhar, parar e reportar a falha com a saída real;
- P-04, P-06 ou P-07: parar e reportar a falha, sem contornar.

0. **Premissas P-01 a P-07** (acima), antes de tudo.
1. **Isolamento (integração, Testcontainers)**: dois tenants na mesma base com o mesmo e-mail;
   tenant em base dedicada; lista, consulta por id, `Update`, `Activate`, `Deactivate`.
2. **RLS sem o filtro do EF**: `IgnoreQueryFilters()` ainda devolve só o tenant corrente; conexão
   crua sem `app.tenant_id` vê 0 linhas; `INSERT` com outro `tenant_id` é recusado; o papel da
   aplicação não é dono, não tem `BYPASSRLS` nem `DELETE`.
3. **Interceptors**: leitura e escrita aplicam o tenant; pool de 1 conexão alternando tenants
   não vaza; contexto sem tenant lança antes de ir ao banco; comando sem transação que não seja
   consulta falha fechado.
4. **Pipeline de autenticação (`WebApplicationFactory`)**: `401` sem claim, claim malformada,
   tenant inexistente, tenant inativo, tenant inativado depois do login, token de outro tenant,
   token antigo sem `tenant`; login com tenant padrão ausente; token ignorado no login; `403` por
   role nos endpoints de Admin.
5. **Migração dos dados atuais**: base no formato legado (v1 + SeedAdmin + AddIsActive) com
   operadores recebe as duas migrations novas e fica com todas as linhas no tenant inicial, sem
   perda, com login funcionando.
6. **Base nova**: `SeedAdminPolicy` pré-marca a `SeedAdmin` como aplicada, e a base termina com 0
   operadores (FR-031), com a história de migrations completa.
7. **Orquestrador**: lotes, retomada só das pendentes, falha de uma base não para as outras,
   trava contra execução simultânea, registro de versão por base, base nova no catálogo é
   atualizada, `seed-dev` recusa fora de Development e é idempotente.
8. **Catálogo (só Infrastructure)**: `TenantCatalog` devolve `record`s corretos (tenant ativo,
   inativo, inexistente, base compartilhada e dedicada, paginação por chave dos ativos); o banco
   recusa CNPJ repetido; `xmin` detecta conflito de edição. Nenhum teste de validação de CNPJ: o
   texto é lido como está.
9. **Mobilidade**: copiar as linhas de um tenant da base compartilhada para uma dedicada vazia,
   sem transformação, e o tenant funciona de forma idêntica (e na volta, sem conflito de `id`).
10. **Compatibilidade**: o conjunto atual de testes de handler e de repositório continua
    passando, com a `PostgresContainerFixture` adaptada; e o `ApiContractCompatTests` confere o
    contrato dos 8 endpoints de `v1/account/*` (status, nomes de campo e mensagens) contra o
    comportamento atual (SC-003, FR-037).

**Testes existentes afetados (declaração explícita, Integridade das entregas)**
- `AuthenticateRepositoryTests.Admin_semeado_pela_migration_existe` **muda de expectativa**
  porque a spec mudou o comportamento (FR-030/FR-031: base nova não recebe o admin de seed).
  Passa a ser `Base_nova_nao_recebe_o_admin_de_seed`, que afirma o oposto. Nenhum teste é apagado
  ou pulado, e o `tasks.md` DEVE registrar essa troca.
- `TokenServiceTests` é atualizado: deixa de escrever em `AppConfiguration.Secrets` (o
  `Configuration` estático é removido) e passa a construir o serviço com `IOptions` e
  `ITenantContext`, ganhando asserções da claim `tenant`.
- `PostgresContainerFixture` é adaptada, não enfraquecida: passa a usar papéis reais (dono e
  aplicação) para que o RLS esteja ativo nos testes existentes. `CreateContext()` continua
  existindo e devolve um contexto de um tenant padrão da fixture.

## Acompanhamento de Complexidade

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| **Novo projeto `AccessControlSystem.Migrator`** (2º ponto de composição, referencia Infrastructure e Api) | A atualização de N bases deve rodar fora da Api, com usuário de banco com DDL, imagem e ciclo de entrega próprios (Princípio IX), em lotes e retomável (XIII). O mesmo projeto traz o `seed-dev`, que precisa do mesmo catálogo e do mesmo `AppDbContext` | Um modo `--migrate` dentro da Api misturaria credenciais com DDL na imagem de runtime e manteria a Api como orquestradora de bases. Mover as migrations para outro assembly contraria a constituição (migrations ficam no assembly da Api) |
| **Migrator aplica migrations em processo (`Database.MigrateAsync`), em vez de `dotnet ef migrations bundle`** (a diretriz preferia o bundle) | A política da `SeedAdmin` (pré-marcar no histórico), o `acs.initial_tenant_id` na mesma conexão e o registro de estado por base precisam rodar na mesma sessão. Em processo, o orquestrador é testável com Testcontainers, sem o SDK do `dotnet ef` na imagem | Bundle: um processo por base (custo de inicialização × número de bases), sem como aplicar a política da `SeedAdmin` e sem testar o orquestrador sem gerar o executável. Ver R-07 |
| **Custo por requisição: 2 idas ao banco** (catálogo + base do tenant) no lugar de no máximo 1 (Princípio XIII) | Catálogo e base do tenant são bancos diferentes; não existe consulta única entre eles. Cache e Redis estão fora do escopo (spec de fundação de escala) e o Princípio I proíbe cache em memória de dado de tenant | Cache em memória viola o Princípio I. Guardar `is_active` do tenant na base do tenant duplicaria a verdade do catálogo. A spec de escala resolve com cache no Redis (TTL ≤ 60 s) |
| **Gate de carga k6 adiado para a spec 06** (Princípio XIII, quality gates) | A diretriz da feature registra que o k6 só passa a existir na spec 06, que mede a baseline cobrindo este pipeline. Esta spec altera um caminho crítico (autenticação), então SC-010 **não será verificado aqui** | Criar um cenário k6 agora exigiria inventar a carga de referência. Nenhum threshold é afrouxado: as metas continuam as da constituição e passam a ser exigidas na spec 06 |
| **Migração fundadora sem compatibilidade com a Api anterior** (Princípio XIII, expand/contract) | O RLS torna as linhas invisíveis à Api antiga, então ela não pode conviver com as bases novas. Não existe ambiente fora de desenvolvimento, logo nenhuma instância antiga fica no ar. Mesmo assim o conteúdo segue expand/contract em duas migrations (adiciona e preenche; depois impõe) | Manter o RLS desligado até um segundo deploy adiaria o isolamento exigido por esta spec |
| **Pré-marcar a `SeedAdmin` em `__EFMigrationsHistory` de bases novas** | A migration aplicada não pode ser editada (constituição) e não pode criar operador em bases novas (FR-031). A política só pré-marca quando a base não tem nenhuma migration aplicada, e só afeta bases novas | Editar a migration viola a constituição. Apagar o operador depois de criá-lo deixaria a credencial existindo por instantes e quebraria o `NOT NULL` do `tenant_id` |
| **Índice único `(tenant_id, email)` criado só na migration, fora do modelo do EF** | O e-mail é um *owned type* em *table splitting*, e o EF não permite índice que misture propriedades do dono e do owned type | Remapear `Email` como *complex type* mudaria o mapeamento do `Operator` e arriscaria todos os testes de repositório. O comportamento é protegido por teste de integração |
| **Pacote de teste `Microsoft.AspNetCore.Mvc.Testing`** | Testar o `OnTokenValidated`, o filtro de login e os `401`/`403` de verdade exige subir o pipeline da Api (`WebApplicationFactory`) | Testar só classes isoladas não prova a ordem do pipeline nem o envelope de erro. Pacote oficial da Microsoft, mesma versão do ASP.NET |
| **Ferramenta local `dotnet-ef` 10.0.12** (manifesto `.config/dotnet-tools.json`) | Gerar as migrations do `AppDbContext` e do `CatalogDbContext` (`dotnet ef migrations add`) de forma reproduzível, na mesma versão do EF Core do projeto | Depender de uma instalação global de cada pessoa gera migrations diferentes por máquina. É ferramenta de desenvolvimento: não entra nas imagens de runtime e não aplica migrations (quem aplica é o Migrator) |
