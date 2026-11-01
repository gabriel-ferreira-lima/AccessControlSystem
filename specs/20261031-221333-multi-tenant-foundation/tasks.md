---

description: "Lista de tarefas da fundação multi-tenant"
---

# Tarefas: Fundação Multi-Tenant

**Entrada**: documentos de projeto em `/specs/20261031-221333-multi-tenant-foundation/`

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Testes**: OBRIGATÓRIOS (Princípio VII, não negociável). Em cada fase, as tarefas de teste vêm antes das de implementação, são executadas e **falham** antes de a implementação existir. O template do Spec Kit marca testes como opcionais; a constituição prevalece.

**Organização**: tarefas agrupadas por história de usuário da spec (US1 a US7), com uma fase de premissas, uma de fundação e uma de acabamento.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história da spec (US1 a US7); fases de premissas, fundação e acabamento não têm rótulo
- Todos os caminhos são relativos à raiz do repositório

## Convenções desta lista

- **Esqueleto** (*skeleton*): tipo mínimo que compila e cujos métodos lançam `NotImplementedException`, criado só para o teste compilar e falhar **por comportamento**. É sempre uma tarefa explícita antes dos testes.
- **Falha fechada**: o que não puder ser resolvido (tenant, base, configuração) lança erro. Nunca cai em outro tenant nem em outra base.
- **Idioma**: código e banco em inglês e `snake_case` no banco; comentários, títulos de `#region`, nomes de métodos de teste (frases com underscore) e textos ao usuário em português, com acentuação.
- **Comandos** (de `src/Backend`): `dotnet build Backend.slnx`; `dotnet test Backend.slnx`; só rápidos: `dotnet test Backend.slnx --filter "Category!=Integration"`. Testes de integração: `[Collection(PostgresCollection.Name)]` e `[Trait("Category", "Integration")]`, dados únicos por teste, nunca assumir tabela vazia. Para gerar migrations: `dotnet tool restore` (na raiz) e depois `dotnet ef migrations add`.
- **Integridade das entregas**: é proibido apagar, pular ou enfraquecer um teste, afrouxar threshold, suprimir aviso ou marcar tarefa concluída sem rodar o gate. Diante de falha: corrigir a causa ou parar e reportar a saída real.
- **Tenants nos testes novos**: um `Guid` de tenant por teste (`NewTenantId()`) ou asserções relativas; nunca contar com dados de outros testes. Nos testes de Api, o helper `RegisterTenantAsync` registra o tenant no catálogo (`Guid` e CNPJ únicos).
- **Migrations só pelo Migrator**: nunca `dotnet ef database update` (nem bundle, nem script) numa base nova (a `SeedAdmin` seria executada). `dotnet ef migrations add` para gerar é normal.
- **Commits**: só quando o dono do projeto pedir. Fluxo: PR na `main` com o CI verde.

## Testes existentes afetados (declaração explícita)

| Teste | O que muda | Tarefa | Motivo |
|---|---|---|---|
| `Tests/Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` → `Admin_semeado_pela_migration_existe` | **Mudança de expectativa**: vira `Base_nova_nao_recebe_o_admin_de_seed` e afirma o oposto | T054 | A spec mudou o comportamento (FR-030/FR-031). Nada é apagado nem pulado |
| `Tests/Api/TokenServiceTests.cs` | Constrói o serviço com `IOptions<SecretsOptions>` e `ITenantContext`, em vez de escrever em `Configuration` estático; ganha asserções da claim `tenant` | T074 | O `Configuration` estático é removido (diretriz do plano) |
| `Tests/Infrastructure/Fixtures/PostgresContainerFixture.cs` | Adaptada, não enfraquecida: papéis reais (dono e aplicação) para o RLS valer nos testes existentes. `CreateContext()` continua existindo | T029, T042, T070 | RLS ativo exige papel não dono |
| Helper de `P06SeedAdminHistoryPremiseTests` | Ajustado ao novo construtor do `AppDbContext` | T069 | Mudança de construtor |

---

## Phase 1: Premissas R-14 (executar ANTES de qualquer outra tarefa)

**Decisão do dono do projeto**: estas são as **primeiras tarefas da implementação**, em especial o **P-02** (leituras com RLS). Nada da feature é implementado antes de os sete testes estarem escritos e executados. T001 a T010 são apenas o andaime mínimo para esses testes compilarem e rodarem, mais o manifesto local do `dotnet-ef` (T004) e o `.gitattributes` (T003), que ficam na Fase 1 por serem pré-requisitos do repositório.

**Se uma premissa falhar** (sem contornos, detalhes em [research.md](research.md) R-01 e R-14):
- P-01, P-02, P-03 ou P-05: adotar o **plano B do R-01** (transação curta por comando), com os mesmos testes. Se o plano B também falhar: parar e reportar a falha com a saída real.
- P-04, P-06 ou P-07: parar e reportar a falha, sem contornar.

- [X] T001 [P] Criar `docker/postgres/sql/roles.sql` com os papéis `acs_migrator` (`LOGIN`, `BYPASSRLS`) e `acs_app` (`LOGIN`, `NOBYPASSRLS`; sem `SUPERUSER`, `CREATEDB` nem `CREATEROLE`), **sem senha** (quem executa define com `ALTER ROLE ... PASSWORD`), conforme `contracts/database-isolation.md`
- [X] T002 [P] Criar `docker/postgres/sql/tenant-db-grants.sql` (`GRANT USAGE ON SCHEMA public TO acs_app;` e `ALTER DEFAULT PRIVILEGES FOR ROLE acs_migrator IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO acs_app;`, **sem `DELETE`**) e `docker/postgres/sql/catalog-db-grants.sql` (o mesmo, só com `SELECT`)
- [X] T003 [P] Criar `.gitattributes` na raiz do repositório com a regra `*.sh text eol=lf`: o Git desta máquina converte para CRLF, o que quebraria `docker/postgres/init/01-init.sh` dentro do container Linux. Conferir com `git ls-files --eol` quando o script existir (T133)
- [X] T004 [P] Criar o manifesto local de ferramentas na raiz do repositório (`.config/dotnet-tools.json`): `dotnet new tool-manifest` e `dotnet tool install dotnet-ef --version 10.0.12`. As tarefas de migration (T041, T066 e T068) usam `dotnet tool restore` seguido de `dotnet ef migrations add`. É ferramenta de desenvolvimento (não entra nas imagens de runtime nem aplica migrations), justificada em `plan.md` (Contexto Técnico e Complexity Tracking, Princípio II)
- [X] T005 Em `src/Backend/AccessControlSystem.Tests/AccessControlSystem.Tests.csproj`, adicionar `Microsoft.AspNetCore.Mvc.Testing` versão `10.0.12` (justificado no plan.md, Complexity Tracking) e os três SQL de T001/T002 como `EmbeddedResource` (`..\..\..\docker\postgres\sql\*.sql`, `LogicalName` `sql/roles.sql`, `sql/tenant-db-grants.sql`, `sql/catalog-db-grants.sql`) (depende de T001, T002)
- [X] T006 [P] Criar `src/Backend/AccessControlSystem.Application/SharedContext/Tenancy/ITenantContext.cs` conforme `contracts/internal-interfaces.md` (`bool IsResolved`; `Guid TenantId`, que lança `InvalidOperationException` com mensagem em português quando não resolvido)
- [X] T007 [P] Criar `src/Backend/AccessControlSystem.Infrastructure/Data/ITenantAwareDbContext.cs` (`ITenantContext TenantContext { get; }`), usada pelos interceptors para descobrir o tenant a partir de `eventData.Context` (research R-01, "Interceptors sem estado") (depende de T006)
- [X] T008 Criar os esqueletos: `src/Backend/AccessControlSystem.Infrastructure/Data/Interceptors/TenantTransactionInterceptor.cs` (herda `DbTransactionInterceptor`) e `TenantCommandInterceptor.cs` (herda `DbCommandInterceptor`), ambos **sem estado e sem parâmetros no construtor**, métodos sobrescritos lançando `NotImplementedException`; `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantDbContextOptions.cs` (`public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)`, lançando `NotImplementedException`); e o marcador `src/Backend/AccessControlSystem.Api/Filters/UseDefaultLoginTenantAttribute.cs` (classe `Attribute` vazia, sem lógica) (depende de T007)
- [X] T009 Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Fixtures/PostgresRoleBootstrap.cs`, auxiliar estático usado pelas **duas** fixtures (a das premissas e a `PostgresContainerFixture`) para não repetir o bootstrap: `ApplyRolesAsync(string superuserConnectionString)` (executa o `roles.sql` embutido e define as senhas de teste de `acs_migrator` e `acs_app` com `ALTER ROLE`) e `CreateDatabaseAsync(string superuserConnectionString, string databaseName, bool isCatalog)` (cria o banco com dono `acs_migrator`, aplica `catalog-db-grants.sql` ou `tenant-db-grants.sql` conforme `isCatalog` e devolve as connection strings de aplicação e de migrator) (depende de T005)
- [X] T010 Criar a fixture das premissas em `src/Backend/AccessControlSystem.Tests/Infrastructure/Premises/PremisePostgresFixture.cs` (container `postgres:18` próprio; usa o `PostgresRoleBootstrap` (T009) para os papéis, as senhas de teste e o banco `premises` (dono `acs_migrator`, com `tenant-db-grants.sql`); como `acs_migrator` cria `premise_items (id uuid primary key, name varchar(100) not null, tenant_id uuid not null)` com `ENABLE` + `FORCE ROW LEVEL SECURITY` e a política `premise_items_tenant_isolation` copiada de `contracts/database-isolation.md`; expõe `CreateContext(Guid tenantId, int maxPoolSize = 100)`, `OpenAppConnectionAsync(int maxPoolSize)`, `OpenMigratorConnectionAsync()` e `CreateEmptyDatabaseAsync(string name)`), mais `PremiseDbContext` (implementa `ITenantAwareDbContext`, define `Database.AutoTransactionBehavior = AutoTransactionBehavior.Always`, mapeia `premise_items` com a propriedade sombra `TenantId` em `tenant_id`), `PremiseItem` e `[CollectionDefinition]` `PremiseCollection`, no mesmo diretório (depende de T005, T007, T008, T009)

### Testes das premissas (escrever primeiro; todos em `src/Backend/AccessControlSystem.Tests/Infrastructure/Premises/`)

- [X] T011 [P] P-01: `P01WriteTenantPremiseTests.cs`. Método `Escrita_aplica_o_tenant_e_o_rls_recusa_tenant_diferente`: com tenant A, `SaveChanges` de um `PremiseItem` com `TenantId` de A funciona e só A o enxerga; com tenant A, gravar `TenantId` de B é recusado pelo `WITH CHECK` (`PostgresException`, SqlState `42501`)
- [X] T012 [P] P-02 (**a mais importante**): `P02ReadTenantPremiseTests.cs`. Método `Leitura_aplica_o_tenant_apenas_no_proprio_lote`: consulta sem transação explícita devolve só as linhas do tenant corrente; logo depois, em outra conexão do mesmo pool (`Maximum Pool Size=1`), `select current_setting('app.tenant_id', true)` devolve `NULL` ou vazio (o valor não sobreviveu ao lote); contexto sem tenant resolvido lança **antes** de ir ao banco
- [X] T013 [P] P-03: `P03QueryShapesPremiseTests.cs`. Método `Interceptor_de_leitura_nao_confunde_o_ef` cobre `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `AnyAsync`, `CountAsync`, projeção para `record`, `OrderBy` + `Skip` + `Take`, `ToListAsync` com e sem `AsNoTracking`; método `Comando_que_nao_e_consulta_fora_de_transacao_falha_fechado` (`ExecuteUpdateAsync` e `ExecuteDeleteAsync` lançam `InvalidOperationException`)
- [X] T014 [P] P-04: `P04ServiceProviderPremiseTests.cs`. Método `Contextos_de_bases_diferentes_nao_geram_provedores_demais`: cria mais de 20 contextos com connection strings diferentes (e tenants diferentes) via `TenantDbContextOptions.Configure`, com `ConfigureWarnings(w => w.Throw(CoreEventId.ManyServiceProvidersCreatedWarning))`, e acessa o `Model` de cada um; nenhum lança
- [X] T015 [P] P-05: `P05PoolPremiseTests.cs`. Método `Pool_de_uma_conexao_alternando_tenants_nao_vaza`: `Maximum Pool Size=1`, alterna tenants A e B em leituras e escritas e confere que cada um só vê o seu e que uma conexão crua sem tenant vê 0 linhas
- [X] T016 [P] P-06: `P06SeedAdminHistoryPremiseTests.cs`. Método `Base_nova_nao_recebe_o_admin_de_seed`: em banco novo (`CreateEmptyDatabaseAsync`), usa `IHistoryRepository` (`GetCreateIfNotExistsScript` + `GetInsertScript(new HistoryRow("20260908171522_SeedAdmin", productVersion))`) para pré-marcar a `SeedAdmin`; depois `Database.MigrateAsync()` com o `AppDbContext` atual e `MigrationsAssembly("AccessControlSystem.Api")`; espera 0 linhas em `operators`, coluna `is_active` existente e o histórico igual a `GetMigrations()` do assembly (todas as migrations conhecidas, sem fixar quantas, e a `SeedAdmin` entre elas por ter sido pré-marcada)
- [X] T017 [P] P-07: `P07LoginTokenIgnoredPremiseTests.cs`. Método `Token_enviado_ao_login_e_ignorado`: host de teste mínimo (`Microsoft.AspNetCore.TestHost`) com JwtBearer, dois endpoints (`/login` com `.WithMetadata(new UseDefaultLoginTenantAttribute())` e `/outro`), `OnMessageReceived` chamando `context.NoResult()` quando o endpoint tem o atributo e `OnTokenValidated` incrementando um contador; token válido para `/login` não incrementa, para `/outro` incrementa (prova que os metadados do endpoint estão disponíveis em `OnMessageReceived`)

- [X] T018 Executar `dotnet test Backend.slnx --filter "FullyQualifiedName~Premises"` e **confirmar que P-01 a P-05 falham** (esqueletos). P-06 e P-07 podem passar de primeira (provam primitivas do EF/ASP.NET). Anotar o resultado de cada uma (depende de T011 a T017)

### Implementação mínima dos mecanismos

- [X] T019 Implementar `TenantTransactionInterceptor` em `src/Backend/AccessControlSystem.Infrastructure/Data/Interceptors/TenantTransactionInterceptor.cs`: sobrescrever `TransactionStarted` e `TransactionStartedAsync`, obter o tenant de `eventData.Context` (`ITenantAwareDbContext`; falhar fechado se o contexto for nulo, não implementar a interface ou o tenant não estiver resolvido) e executar `SELECT set_config('app.tenant_id', @tenant, true)` **na própria transação**, valor por parâmetro `text`, nunca concatenado. Deve fazer P-01 passar (depende de T018)
- [X] T020 Implementar `TenantCommandInterceptor` em `src/Backend/AccessControlSystem.Infrastructure/Data/Interceptors/TenantCommandInterceptor.cs` (research R-01): em `ReaderExecuting(Async)`, quando `command.Transaction is null`, antepor `SELECT set_config('app.tenant_id', @__acs_tenant, true);` + quebra de linha ao texto e adicionar o parâmetro `@__acs_tenant` (`text`); em `ReaderExecuted(Async)`, avançar o leitor com `NextResult(Async)` para pular o resultado do `set_config` e devolver o **mesmo** leitor; em `NonQueryExecuting(Async)` e `ScalarExecuting(Async)` sem transação, lançar `InvalidOperationException` (mensagem em português); tenant lido de `ITenantAwareDbContext`, falha fechada. Sem estado. Deve fazer P-02, P-03 e P-05 passarem (depende de T019)
- [X] T021 Implementar `TenantDbContextOptions.Configure` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantDbContextOptions.cs`: `UseNpgsql(connectionString, o => o.MigrationsAssembly("AccessControlSystem.Api"))` e `AddInterceptors` com **instâncias singleton compartilhadas** (campos `static readonly` sem estado de tenant e sem registro no DI: `AddTenancy` não os registra) dos dois interceptors. Deve fazer P-04 passar (depende de T020)
- [X] T022 Executar os sete testes de `Premises/` e confirmar **todos verdes** (depende de T021)
- [X] T023 Registrar o resultado de P-01 a P-07 (vermelho antes, verde depois, ou o motivo da falha) num bloco "Resultado da execução" ao final de R-14 em `specs/20261031-221333-multi-tenant-foundation/research.md` (depende de T022)
- [X] T024 **CONDICIONAL, somente se P-01, P-02, P-03 ou P-05 falhar em T022**: substituir o `TenantCommandInterceptor` pelo **plano B do R-01** (transação curta por comando: `BEGIN`, `set_config(..., true)`, comando e `COMMIT` ao fechar o leitor), mantendo os mesmos testes P-01/P-02/P-03/P-05 **sem alterá-los**. Sem `SET` por sessão, sem remendo, sem afrouxar teste. Se o plano B também falhar, ou se P-04/P-06/P-07 falharem: **parar e reportar** a falha com a saída real. Pular esta tarefa se T022 estiver todo verde — **PULADA**: a condição não ocorreu (T022 todo verde; ver "Resultado da execução" em R-14 do research.md)

**Checkpoint**: premissas provadas (ou plano B adotado). Só então seguir para a Fase 2.

---

## Phase 2: Fundação (bloqueia as histórias)

**Propósito**: catálogo (modelo de persistência na Infrastructure, só lido pela Application), interfaces de tenant, projeto Migrator com o executor de uma base, e a fixture de testes.

**⚠️ CRÍTICO**: nenhuma história começa antes desta fase.

### Interfaces (sem comportamento)

- [X] T025 [P] Criar `src/Backend/AccessControlSystem.Application/SharedContext/Tenancy/ITenantResolver.cs` conforme `contracts/internal-interfaces.md` (`Task<bool> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default)`)
- [X] T026 [P] Criar `src/Backend/AccessControlSystem.Application/SharedContext/Tenancy/ITenantCatalog.cs` (`GetTenantByIdAsync`, `GetActiveTenantListAsync(Guid? afterTenantId, int size, ...)` com `size` de 1 a 100)
- [X] T027 [P] Criar `src/Backend/AccessControlSystem.Application/SharedContext/Tenancy/TenantInfo.cs` com os `record`s `TenantInfo(Guid Id, string Cnpj, string Name, bool IsActive, TenantDatabaseInfo Database)` e `TenantDatabaseInfo(string Name, EDatabaseKind Kind)`; o CNPJ é texto lido, sem validação
- [X] T028 [P] Criar `src/Backend/AccessControlSystem.Application/SharedContext/Tenancy/EDatabaseKind.cs` (`Shared = 1`, `Dedicated = 2`)

### Andaime de testes e projeto Migrator

- [X] T029 Estender **de forma aditiva** `src/Backend/AccessControlSystem.Tests/Infrastructure/Fixtures/PostgresContainerFixture.cs` (fixture F1): após subir o container, usar o `PostgresRoleBootstrap` (T009) para aplicar os papéis e criar o banco `acs_test_catalog` (`isCatalog: true`); expor `CreateEmptyDatabaseAsync(string name)` (delega a `PostgresRoleBootstrap.CreateDatabaseAsync` com `isCatalog: false` e devolve as connection strings de aplicação e de migrator), `CatalogMigratorConnectionString` e `OpenMigratorConnectionAsync()`. **Não alterar** o que os testes existentes usam (`CreateContext()` e o banco atual continuam como estão). Depende de T005 e T009
- [X] T030 Criar o projeto `src/Backend/AccessControlSystem.Migrator/AccessControlSystem.Migrator.csproj` (`net10.0`, `Nullable` e `ImplicitUsings` habilitados, `ProjectReference` para Infrastructure e Api, `Program.cs` que só chama `MigratorApplication.RunAsync(args)` e devolve o código de saída; e `src/Backend/AccessControlSystem.Migrator/MigratorApplication.cs` com `public static class MigratorApplication` e `public static Task<int> RunAsync(string[] args, Action<IConfigurationBuilder>? configure = null, CancellationToken cancellationToken = default)` (esqueleto, lançando `NotImplementedException`). Os testes chamam essa classe **pública** diretamente, **sem `InternalsVisibleTo`**, o que evita o conflito entre o `Program` do Migrator e o `Program` da Api usado em `WebApplicationFactory<Program>`), incluí-lo em `src/Backend/Backend.slnx` e adicionar `ProjectReference` para ele em `AccessControlSystem.Tests.csproj` (depende de T005)
- [X] T031 Criar os esqueletos: modelos `CatalogTenant`, `CatalogDatabase` e `EMigrationStatus` em `src/Backend/AccessControlSystem.Infrastructure/Catalog/Models/` (propriedades conforme `data-model.md`), `CatalogTenantMap`/`CatalogDatabaseMap` vazios em `src/Backend/AccessControlSystem.Infrastructure/Catalog/Mapping/`, `src/Backend/AccessControlSystem.Infrastructure/Data/CatalogDbContext.cs` (só os `DbSet`s), `src/Backend/AccessControlSystem.Infrastructure/Tenancy/Options/CatalogOptions.cs`, `src/Backend/AccessControlSystem.Infrastructure/Services/TenantCatalog.cs` (`ITenantCatalog`, lançando `NotImplementedException`), a assinatura `CreateCatalogContext(bool asMigrator)` em `PostgresContainerFixture.cs` (lançando `NotImplementedException`), e em `src/Backend/AccessControlSystem.Migrator/Orchestration/` o `SeedAdminPolicy.cs`, `IDatabaseMigrationRunner.cs` (`Task<MigrationRunResult> RunAsync(string migrationConnectionString, CancellationToken cancellationToken, Guid? initialTenantId = null)` (o `initialTenantId` é ignorado até T105); `MigrationRunResult(bool Succeeded, string? SchemaVersion, string? Error)`) e `EfDatabaseMigrationRunner.cs` (lançando `NotImplementedException`) (depende de T025 a T030)

### Testes da fundação (escrever primeiro)

- [X] T032 [P] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Catalog/CatalogMappingTests.cs` com: `Tenant_faz_round_trip_completo`, `Cnpj_repetido_e_recusado_pelo_banco` (SqlState `23505`), `Cnpj_e_gravado_como_texto_sem_validacao` (aceita texto com máscara de até 18 caracteres), `Nome_de_base_repetido_e_recusado_pelo_banco`, `Tenant_exige_uma_base_existente` (FK, SqlState `23503`), `Xmin_detecta_conflito_de_edicao` (`DbUpdateConcurrencyException`), `Papel_da_aplicacao_so_le_o_catalogo` (`acs_app` lê mas `INSERT`/`UPDATE` dão `42501`), `Catalogo_nao_tem_colunas_de_senha_nem_connection_string` (FR-004; confere `information_schema.columns` das duas tabelas)
- [X] T033 [P] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Catalog/TenantCatalogTests.cs` com: `GetTenantByIdAsync_devolve_o_tenant_com_a_sua_base`, `GetTenantByIdAsync_devolve_null_quando_nao_existe`, `GetTenantByIdAsync_devolve_tenant_inativo_com_IsActive_falso`, `GetActiveTenantListAsync_devolve_so_ativos_paginados_por_chave`, `GetActiveTenantListAsync_rejeita_size_fora_de_1_a_100`
- [X] T034 [P] Criar `src/Backend/AccessControlSystem.Tests/Migrator/SeedAdminPolicyTests.cs` com: `Base_sem_migrations_aplicadas_recebe_a_seed_admin_pre_marcada`, `Base_com_migrations_aplicadas_nao_e_alterada`, `Politica_e_idempotente`
- [X] T035 [P] Criar `src/Backend/AccessControlSystem.Tests/Migrator/EfDatabaseMigrationRunnerTests.cs` com: `Base_nova_termina_sem_operadores` (FR-031), `Base_nova_tem_todas_as_migrations_no_historico`, `Runner_e_idempotente`, `Falha_de_conexao_vira_resultado_de_falha_sem_a_connection_string`. **Ajuste na execução**: `Base_legada_com_a_seed_admin_continua_com_o_admin` saiu desta tarefa e fica na US3, onde já existe como `Admin_de_seed_legado_e_preservado_no_tenant_inicial`: depois da US1 uma base legada só migra com o tenant inicial informado, então o teste não poderia ficar verde no checkpoint da US1 (a parte "base com histórico não é alterada" segue coberta por `Base_com_migrations_aplicadas_nao_e_alterada` em `SeedAdminPolicyTests`)
- [X] T036 Executar os testes de T032 a T035 e confirmar que **falham** (depende de T032 a T035)

### Implementação da fundação

- [X] T037 [P] Implementar `src/Backend/AccessControlSystem.Infrastructure/Catalog/Mapping/CatalogTenantMap.cs` (`IEntityTypeConfiguration`, `snake_case`): tabela `tenants`; `id` uuid PK com `ValueGeneratedNever` (o id é gerado pela aplicação, nunca pelo banco, FR-002); `cnpj` `varchar(18)` NOT NULL, índice **único** `ix_tenants_cnpj`; `name` `varchar(200)` NOT NULL; `is_active` boolean NOT NULL padrão `true`; `database_id` uuid NOT NULL, FK para `tenant_databases.id` com `ON DELETE RESTRICT` e índice `ix_tenants_database_id`; `created_at` `timestamptz` NOT NULL (preenchido pela aplicação em UTC, `DateTime` com `Kind = Utc`, sem valor padrão no banco); `xmin` como token de concorrência (propriedade sombra); índice parcial `ix_tenants_id_active` em `(id)` com filtro `is_active`
- [X] T038 [P] Implementar `src/Backend/AccessControlSystem.Infrastructure/Catalog/Mapping/CatalogDatabaseMap.cs`: tabela `tenant_databases`; `id` uuid PK com `ValueGeneratedNever` (o id é gerado pela aplicação, nunca pelo banco); `name` `varchar(100)` NOT NULL, índice **único** `ix_tenant_databases_name`; `kind` smallint NOT NULL (`EDatabaseKind`); `schema_version` `varchar(150)` NULL; `migration_status` smallint NOT NULL padrão `1` (`EMigrationStatus`: `1` Pending, `2` Running, `3` Succeeded, `4` Failed); `migration_attempted_at` `timestamptz` NULL; `migration_error` `varchar(1000)` NULL; `created_at` `timestamptz` NOT NULL (preenchido pela aplicação em UTC, `DateTime` com `Kind = Utc`, sem valor padrão no banco); `xmin` como token de concorrência; índice `ix_tenant_databases_migration_status_id` em `(migration_status, id)`
- [X] T039 Implementar `CatalogDbContext` em `src/Backend/AccessControlSystem.Infrastructure/Data/CatalogDbContext.cs` aplicando os dois mapas, e `CatalogOptions` (`ConnectionStrings:Catalog`, obrigatória, para `ValidateOnStart`) (depende de T037, T038)
- [X] T040 Criar `src/Backend/AccessControlSystem.Api/DesignTime/CatalogDbContextFactory.cs` (`IDesignTimeDbContextFactory<CatalogDbContext>`, lê `ConnectionStrings:CatalogMigration` do ambiente) (depende de T039)
- [X] T041 Gerar a migration `InitialCatalog` com `dotnet tool restore` e depois `dotnet ef migrations add InitialCatalog --context CatalogDbContext --project src/Backend/AccessControlSystem.Api --startup-project src/Backend/AccessControlSystem.Api --output-dir Migrations/Catalog` e revisar o SQL: colunas e tipos de `data-model.md`, `xmin` não criado como coluna, índice parcial, FK `ON DELETE RESTRICT` (**não** aplicar com `dotnet ef database update`) (depende de T040)
- [X] T042 Na fixture (`PostgresContainerFixture.cs`), aplicar a `InitialCatalog` em `acs_test_catalog` com o usuário migrator e implementar `CreateCatalogContext(bool asMigrator)` (depende de T041)
- [X] T043 Implementar `TenantCatalog` em `src/Backend/AccessControlSystem.Infrastructure/Services/TenantCatalog.cs` (usa `CatalogDbContext` com `AsNoTracking`; `GetTenantByIdAsync` em **uma** consulta com a base; `GetActiveTenantListAsync` por chave `id > afterTenantId`, `ORDER BY id`, `size` de 1 a 100, senão `ArgumentOutOfRangeException`; devolve os `record`s da Application, nunca os modelos) (depende de T039, T042)
- [X] T044 Implementar `SeedAdminPolicy` em `src/Backend/AccessControlSystem.Migrator/Orchestration/SeedAdminPolicy.cs`: se a base **não tem nenhuma migration aplicada**, criar `__EFMigrationsHistory` se preciso e inserir a linha `20260908171522_SeedAdmin` via `IHistoryRepository` (API pública), usando o `ProductVersion` atual do EF; caso contrário, não faz nada; idempotente (depende de T036)
- [X] T045 Implementar `EfDatabaseMigrationRunner` em `src/Backend/AccessControlSystem.Migrator/Orchestration/EfDatabaseMigrationRunner.cs`: constrói o `AppDbContext` com `UseNpgsql(migrationConnectionString, o => o.MigrationsAssembly("AccessControlSystem.Api"))` **sem** os interceptors de tenant, mantém a conexão aberta, aplica `SeedAdminPolicy`, roda `Database.MigrateAsync`, lê a última migration aplicada e devolve `MigrationRunResult`; qualquer exceção vira `Succeeded = false` com texto **sem connection string nem segredo** (depende de T044)
- [X] T046 Executar os testes de T032 a T035 e a suíte existente (`dotnet test Backend.slnx`): tudo verde (depende de T043, T045)

**Checkpoint**: catálogo e executor de uma base prontos; as histórias podem começar.

---

## Phase 3: User Story 1 - Cada requisição enxerga apenas os dados do seu tenant (Priority: P1) 🎯 MVP

**Objetivo**: `AppDbContext` com tenant, filtro do EF e RLS no banco, nas duas topologias (base compartilhada e dedicada).

**Teste independente**: dois tenants na mesma base e um terceiro numa base dedicada, cada um com operadores (inclusive o mesmo e-mail em dois deles); listagens, consultas por id e alterações só alcançam o próprio tenant, mesmo sem o filtro do EF. Cobre FR-005 a FR-013, FR-019 (cenário de aceitação 1 da US4), FR-032.

### Esqueletos e andaime

- [X] T047 [US1] Criar o esqueleto `src/Backend/AccessControlSystem.Infrastructure/Tenancy/NoTenantContext.cs` (`ITenantContext` nunca resolvido, usado em migrations e design-time) e, em `PostgresContainerFixture.cs`, as assinaturas (lançando `NotImplementedException`) de `CreateContext(Guid tenantId)`, `CreateDedicatedContext(Guid tenantId)`, `OpenAppConnectionAsync()`, `OpenAppConnectionToDedicatedAsync()`, e os auxiliares `NewTenantId()` (um `Guid` novo por chamada, para cada teste ter o seu tenant) e `DefaultTenantId` (o tenant padrão usado por `CreateContext()`); **não** criar constantes de tenant compartilhadas pelos testes novos. Mantém `CreateContext()` como está

### Testes da US1 (escrever primeiro, ver falhar)

- [X] T048 [P] [US1] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/AppDbContextTenantTests.cs`: `SaveChanges_preenche_o_tenant_das_entidades_novas`, `SaveChanges_nao_modifica_o_tenant_de_entidade_alterada`, `SaveChanges_lanca_sem_tenant_resolvido`, `Consulta_lanca_sem_tenant_resolvido`, `Filtro_global_restringe_ao_tenant_corrente`, `Operador_request_e_response_nao_expoem_tenant_id` (reflexão: nenhum tipo do Domain nem `Request`/`Response` do `AccountContext` tem propriedade `TenantId`)
- [X] T049 [P] [US1] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantIsolationTests.cs` (base compartilhada; os tenants A e B são gerados no próprio teste com `NewTenantId()`): `Lista_so_devolve_operadores_do_proprio_tenant`, `Consulta_por_id_de_outro_tenant_devolve_null`, **`Mesmo_email_em_dois_tenants_e_aceito`** (guia o índice `(tenant_id, email)`), `Mesmo_email_no_mesmo_tenant_e_recusado`, `Alteracao_de_outro_tenant_nao_encontra_o_registro`, `Contagem_de_admins_considera_so_o_tenant_corrente`
- [X] T050 [P] [US1] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs`: `Tenant_dedicado_enxerga_so_os_proprios_dados`, `Tenant_dedicado_se_comporta_como_o_compartilhado` (mesmas operações da US1 produzem os mesmos resultados), `Tenants_da_base_compartilhada_nao_enxergam_a_base_dedicada` e `Mesmo_email_em_tres_tenants_e_aceito` (A, B e o dedicado, SC-005)
- [X] T051 [P] [US1] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/RowLevelSecurityTests.cs`: `Rls_bloqueia_mesmo_sem_o_filtro_do_ef` (`IgnoreQueryFilters()` ainda só devolve o tenant corrente), `Conexao_sem_tenant_nao_ve_nem_altera_linhas` (conexão crua como `acs_app`), `Insert_de_outro_tenant_e_recusado_pelo_banco`, `Update_nao_altera_o_tenant_id`, `Valor_do_tenant_nao_sobrevive_a_transacao`
- [X] T052 [P] [US1] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/SchemaContractTests.cs`: `Papel_da_aplicacao_nao_tem_privilegios_alem_de_dml` (sem `DELETE`, sem DDL), `Papel_da_aplicacao_nao_e_dono_e_nao_tem_bypassrls`, `Coluna_tenant_id_e_obrigatoria`, `Indices_do_operador_existem` (`ix_operators_tenant_id_email` único em `(tenant_id, email)`, `ix_operators_tenant_id_id` em `(tenant_id, id)`, e `IX_operators_email` não existe mais), `Toda_tabela_de_tenant_tem_rls_e_politica` (para cada tabela mapeada pelo `AppDbContext`: `relrowsecurity` e `relforcerowsecurity` verdadeiros e política `{tabela}_tenant_isolation` presente), `Email_e_unico_por_tenant` (`23505` no mesmo tenant)
- [X] T053 [P] [US1] Adicionar o caso de isolamento ("dados de outro tenant, na mesma base, não são lidos nem alterados", Princípio VII) em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs`
- [X] T054 [P] [US1] Em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs`: adicionar o caso de isolamento (o mesmo e-mail em outro tenant não é devolvido) e **substituir** `Admin_semeado_pela_migration_existe` por `Base_nova_nao_recebe_o_admin_de_seed`, que afirma que `GetUserByEmailAsync("admin@admin.com.br")` devolve `null` na base de teste recém-migrada. **Mudança de expectativa por mudança de spec (FR-030/FR-031)**, declarada na tabela do início; o teste antigo não é apagado às escondidas nem enfraquecido, é trocado pelo comportamento novo
- [X] T055 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Create/CreateRepositoryTests.cs`
- [X] T056 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs` (inclui `IsLastActiveAdminAsync` contando só o tenant corrente). Atualizar também o comentário das linhas 10 a 12 do arquivo, que cita o "admin semeado pela migration": a base de teste não o tem mais, e os testes existentes continuam válidos porque medem a contagem real de admins antes de agir
- [X] T057 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Get/GetRepositoryTests.cs` (lista e total só do tenant corrente)
- [X] T058 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs` (referência: o `Repository` do `GetMe` não muda)
- [X] T059 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs`
- [X] T060 [P] [US1] Adicionar o caso de isolamento em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs`
- [X] T061 [US1] Executar os testes de T048 a T060 e confirmar que os **novos falham** (os existentes continuam verdes) (depende de T048 a T060)

### Implementação da US1

- [X] T062 [US1] Criar `src/Backend/AccessControlSystem.Infrastructure/Data/TenantModelBuilderExtensions.cs` com `ApplyTenantIsolation(this ModelBuilder modelBuilder, AppDbContext context)`: para cada entidade que não é *owned*, criar a propriedade sombra `TenantId` mapeada para a coluna `tenant_id` (`uuid`, obrigatória), o filtro global `EF.Property<Guid>(e, "TenantId") == context.CurrentTenantId` (a expressão referencia a propriedade da **instância** do contexto, para o EF avaliá-la por contexto e não congelar o tenant no modelo em cache) e o índice `(TenantId, Id)` de nome `ix_{tabela}_tenant_id_id`. O teste `Filtro_global_restringe_ao_tenant_corrente` (T048) deve usar dois contextos de tenants diferentes no mesmo processo para provar isso
- [X] T063 [US1] Alterar `src/Backend/AccessControlSystem.Infrastructure/Data/AppDbContext.cs`: construtor `(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)`, implementar `ITenantAwareDbContext`, `CurrentTenantId` (o tenant quando resolvido, senão `Guid.Empty`), `Database.AutoTransactionBehavior = AutoTransactionBehavior.Always`, chamar `ApplyTenantIsolation` em `OnModelCreating` e sobrescrever `SaveChanges`/`SaveChangesAsync` (as duas sobrecargas relevantes): entidades `Added` recebem o tenant, entidades `Modified` têm `TenantId` marcado como **não modificado**, e sem tenant resolvido lança antes de ir ao banco (depende de T062)
- [X] T064 [US1] Implementar `src/Backend/AccessControlSystem.Infrastructure/Tenancy/NoTenantContext.cs` (`IsResolved = false`; `TenantId` lança) (depende de T047)
- [X] T065 [US1] Criar `src/Backend/AccessControlSystem.Api/DesignTime/AppDbContextFactory.cs` (`IDesignTimeDbContextFactory<AppDbContext>`, `NoTenantContext`, `MigrationsAssembly("AccessControlSystem.Api")`, connection string de `ConnectionStrings:Migration` do ambiente) (depende de T063, T064)
- [X] T066 [US1] Gerar a migration `AddTenantIdToOperators` (passo 1 de R-06): declarar **temporariamente** em `TenantModelBuilderExtensions` a propriedade `TenantId` como opcional e sem o índice `(TenantId, Id)`, rodar `dotnet tool restore` e depois `dotnet ef migrations add AddTenantIdToOperators --context AppDbContext --project src/Backend/AccessControlSystem.Api --startup-project src/Backend/AccessControlSystem.Api`, e completar o `Up` à mão com o backfill `UPDATE operators SET tenant_id = NULLIF(current_setting('acs.initial_tenant_id', true), '')::uuid WHERE tenant_id IS NULL` (coluna `tenant_id uuid` **NULL**, nome em `snake_case`). O diff do scaffold só pode conter o `AddColumn` de `tenant_id` (nullable): se houver `DropIndex` ou qualquer outra operação, parar e revisar o modelo antes de seguir (o `OperatorMap` ainda mantém o índice único do e-mail neste ponto). **Não editar** as três migrations antigas (depende de T065)
- [X] T067 [US1] Alterar `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/Mapping/OperatorMap.cs`: remover o `b.HasIndex(x => x.Address).IsUnique()` do e-mail (o índice composto vive só na migration, research R-06). **Não** alterar nenhuma outra coluna. **Depende de T066**: o scaffold de `AddTenantIdToOperators` precisa nascer com o índice único global do e-mail ainda no modelo, então esta alteração é feita **logo antes de gerar a T068**
- [X] T068 [US1] Restaurar `TenantId` obrigatório e o índice `(TenantId, Id)` e gerar a migration `EnforceTenantIsolationOnOperators` (passo 2 de R-06); completar o `Up` à mão: `tenant_id SET NOT NULL`; `DROP INDEX "IX_operators_email"`; `CreateIndex` único `ix_operators_tenant_id_email` em `(tenant_id, email)`; `ix_operators_tenant_id_id` em `(tenant_id, id)`; `ALTER TABLE operators ENABLE ROW LEVEL SECURITY` e `FORCE ROW LEVEL SECURITY`; `CREATE POLICY operators_tenant_isolation ON operators USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)`. O diff do scaffold deve conter `AlterColumn` (NOT NULL), `CreateIndex` de `ix_operators_tenant_id_id` e `DropIndex` de `IX_operators_email`. Nenhuma migration cita papéis nem cria operador (depende de T067, T066)
- [X] T069 [US1] Corrigir a compilação dos pontos afetados pelo novo construtor: `EfDatabaseMigrationRunner` (usa `NoTenantContext`) e o helper de contexto de `src/Backend/AccessControlSystem.Tests/Infrastructure/Premises/P06SeedAdminHistoryPremiseTests.cs`/`PremisePostgresFixture.cs`. **Corrigir apenas a construção do contexto**: nenhuma asserção de teste muda aqui (a de P-06 já compara com `GetMigrations()`) (depende de T063, T064)
- [X] T070 [US1] Fixture F2 em `src/Backend/AccessControlSystem.Tests/Infrastructure/Fixtures/PostgresContainerFixture.cs`: criar `acs_test_shared` e `acs_test_dedicated` com `CreateEmptyDatabaseAsync`, migrar as duas com `EfDatabaseMigrationRunner` (usuário migrator), trocar o banco único antigo por `acs_test_shared`; `CreateContext()` passa a devolver um contexto de um tenant padrão da fixture como `acs_app` via `TenantDbContextOptions.Configure`; implementar `CreateContext(Guid)`, `CreateDedicatedContext(Guid)`, `OpenAppConnectionAsync()`, `OpenAppConnectionToDedicatedAsync()`. **Os testes existentes continuam chamando `CreateContext()` sem alteração** (depende de T068, T069)
- [X] T071 [US1] Executar `dotnet build Backend.slnx` e `dotnet test Backend.slnx`: **tudo verde** (premissas, fundação, novos da US1 e a suíte existente). Se algo falhar, corrigir a causa (depende de T070)

**Checkpoint**: isolamento por filtro do EF + RLS provado nas duas topologias, com os testes existentes passando.

---

## Phase 4: User Story 2 - O token identifica o tenant e tokens inválidos são rejeitados (Priority: P1)

**Objetivo**: tenant resolvido no pipeline JWT, `AppDbContext` por requisição na base do tenant, claim `tenant` nos tokens, login pelo tenant padrão, `401` genérico para token inválido.

**Teste independente**: login com credenciais válidas do tenant padrão devolve token com a claim `tenant` e o fluxo é o de hoje; tokens sem tenant, malformado, inexistente e inativo recebem `401` com a mensagem genérica. Cobre FR-005, FR-006, FR-013 a FR-018, FR-037.

### Esqueletos e andaime

- [X] T072 [US2] Criar os esqueletos: `src/Backend/AccessControlSystem.Api/Options/SecretsOptions.cs`, `LoginOptions.cs`; `src/Backend/AccessControlSystem.Infrastructure/Tenancy/Options/TenantDatabasesOptions.cs`; `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantScope.cs`, `TenantDatabaseConnectionProvider.cs`, `TenantResolver.cs`, `TenancyServiceCollectionExtensions.cs` (`AddTenancy`, lançando `NotImplementedException`); `ClaimPrincipalExtensions.TenantId()` em `src/Backend/AccessControlSystem.Api/Extensions/ClaimPrincipalExtensions.cs`; alterar a assinatura do `TokenService` (`IOptions<SecretsOptions>`, `ITenantContext`); e `public partial class Program { }` em `src/Backend/AccessControlSystem.Api/Program.cs`
- [X] T073 [US2] Criar o andaime de testes da Api em `src/Backend/AccessControlSystem.Tests/Api/Support/`: `ApiFactory.cs` (`WebApplicationFactory<Program>` com configuração em memória apontando `ConnectionStrings:Catalog`, `TenantDatabases:*:ConnectionString`, `Secrets:JwtPrivateKey` e `Login:DefaultTenantId` para as bases da fixture; permite trocar por teste o tenant padrão e as connection strings das bases, inclusive para uma base legada, e o helper `RegisterTenantAsync(...)`, que cria no catálogo um tenant com `Guid` e CNPJ únicos por teste, apontando para a base compartilhada ou para a dedicada) e `TestTokens.cs` (gera JWT assinado com a mesma chave: com/sem `tenant`, `tenant` malformado, `Id` de um tenant e `tenant` de outro, role Admin/Operator) (depende de T071, T072)

### Testes da US2 (escrever primeiro, ver falhar)

- [X] T074 [P] [US2] Atualizar `src/Backend/AccessControlSystem.Tests/Api/TokenServiceTests.cs` (mudança declarada na tabela do início): construir o serviço com `Options.Create(new SecretsOptions { JwtPrivateKey = TestKey })` e um `ITenantContext` falso (NSubstitute), mantendo os três testes existentes; adicionar `Token_carrega_a_claim_tenant_do_contexto` e `Gerar_token_sem_tenant_resolvido_lanca`
- [X] T075 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantScopeTests.cs`: `Set_resolve_o_contexto`, `Set_duas_vezes_lanca`, `TenantId_lanca_quando_nao_resolvido`
- [X] T076 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantDatabaseConnectionProviderTests.cs`: `Devolve_a_connection_string_da_base`, `Lanca_quando_a_base_nao_esta_configurada`, `Mensagem_de_erro_nao_inclui_a_connection_string`
- [X] T077 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Application/SharedContext/Tenancy/TenantResolverTests.cs` (NSubstitute em `ITenantCatalog`): `Resolve_tenant_ativo`, `Devolve_false_para_tenant_inexistente`, `Devolve_false_para_tenant_inativo`, `Lanca_quando_a_base_nao_tem_connection_string`
- [X] T078 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Api/OptionsValidationTests.cs`: `Api_nao_inicia_sem_a_chave_do_jwt`, `Api_nao_inicia_sem_a_connection_string_do_catalogo`, `Api_inicia_sem_tenant_padrao_do_login`, `Api_nao_altera_a_estrutura_de_nenhuma_base_ao_iniciar` (FR-020: sobe contra uma base vazia e, depois do início, ela continua sem tabelas)
- [X] T079 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Api/JwtTenantValidationTests.cs` (`ApiFactory`): `Token_sem_claim_tenant_retorna_401`, `Token_com_tenant_malformado_retorna_401`, `Token_de_tenant_inexistente_retorna_401`, `Token_de_tenant_inativo_retorna_401`, `Tenant_inativado_depois_do_login_retorna_401_na_requisicao_seguinte`, `Token_com_id_de_um_tenant_e_tenant_de_outro_retorna_401`, `Token_antigo_sem_tenant_retorna_401`, `Mensagem_do_401_e_a_generica_de_hoje` (corpo com `"Não autenticado"` e status `401` em todos os casos acima, sem revelar o motivo), `Token_valido_le_get_me_com_os_dados_do_proprio_tenant`, `Qualquer_instancia_atende_qualquer_tenant` (Api com tenant padrão A atende token do tenant B), `Base_do_tenant_sem_connection_string_retorna_500`, `Base_do_tenant_inacessivel_retorna_500_e_nunca_cai_em_outra_base`, `Operador_de_outro_tenant_por_id_retorna_404_como_inexistente` (get-me não se aplica; activate, deactivate e update com o id de um operador de outro tenant, FR-012)
- [X] T080 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Api/DefaultLoginTenantTests.cs` (`ApiFactory`): `Login_usa_o_tenant_padrao_e_o_token_carrega_a_claim_tenant`, `Login_mantem_campos_status_e_mensagem_de_hoje`, `Login_com_tenant_padrao_ausente_retorna_401_credenciais_invalidas`, `Login_com_tenant_padrao_inexistente_retorna_401`, `Login_com_tenant_padrao_inativo_retorna_401`, `Token_enviado_ao_login_e_ignorado`, `Login_nao_revela_operadores_de_outros_tenants`
- [X] T081 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Api/EndpointAuthorizationTests.cs` (Princípio VII, tabela dos 8 endpoints de `v1/account/*`): `Sem_token_retorna_401`, `Role_Operator_em_endpoint_de_admin_retorna_403` (create, get-list, update, activate, deactivate) e `Token_de_outro_tenant_retorna_401`
- [X] T082 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Api/ApiContractCompatTests.cs` (`ApiFactory`, depende de T073): `Contrato_dos_8_endpoints_nao_mudou`, teoria com um caso por endpoint de `v1/account/*` (login, create, get-me, get-list, update-me, update/{id}, activate/{id}, deactivate/{id}) que confere o status do caminho feliz e dos erros documentados, os nomes das propriedades JSON do envelope (`message`, `status`, `notifications`, `data`) e de `data`, e as mensagens (ex.: `"Credenciais inválidas"`, `"Requisição inválida"`, `"E-mail já está em uso"`, `"Não autenticado"`). Os valores esperados vêm do código ATUAL dos handlers, de `Response`/`ResponseData` e dos comentários XML do controller (nenhum deles muda nesta feature): qualquer diferença é regressão de contrato (SC-003, FR-037)
- [X] T083 [P] [US2] Criar `src/Backend/AccessControlSystem.Tests/Architecture/NoStaticTenantStateTests.cs` (FR-013, Princípio I): `Nenhum_campo_estatico_guarda_dado_de_tenant_em_application_e_infrastructure`, que percorre por reflexão todos os tipos dos assemblies Application e Infrastructure e falha se achar campo ou propriedade **estática** que não seja constante nem gerada pelo compilador e que esteja fora da lista permitida. A lista contém apenas os dois campos `static readonly` de interceptors em `TenantDbContextOptions` (instâncias sem estado). Qualquer outra exceção exige justificativa em comentário no próprio teste e aprovação do dono do projeto. Fica vermelho enquanto o `Configuration` estático existir e passa quando ele é removido (T097)
- [X] T084 [US2] Executar os testes de T074 a T083 e confirmar que **falham** (depende de T074 a T083)

### Implementação da US2

- [X] T085 [P] [US2] Implementar `SecretsOptions` (`JwtPrivateKey` obrigatória) e `LoginOptions` (`Guid? DefaultTenantId`, **sem** `ValidateOnStart`) em `src/Backend/AccessControlSystem.Api/Options/`
- [X] T086 [P] [US2] Implementar `TenantDatabasesOptions` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/Options/TenantDatabasesOptions.cs` (mapa `{name}` → `ConnectionString` e `MigrationConnectionString`; validação: cada entrada configurada tem `ConnectionString` não vazia) conforme `contracts/configuration.md`. Ao concluir, conferir `AccessControlSystem.Infrastructure.csproj`: se foi preciso acrescentar `PackageReference` (ex.: `Microsoft.Extensions.Options`), justificar em `plan.md` (Contexto Técnico e Complexity Tracking, Princípio II) no mesmo trabalho
- [X] T087 [US2] Implementar `TenantScope` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantScope.cs` (`ITenantContext` scoped; `Set(Guid tenantId, string databaseName)` aceita **uma** chamada por escopo, a segunda lança) (depende de T085, T086)
- [X] T088 [US2] Implementar `TenantDatabaseConnectionProvider` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantDatabaseConnectionProvider.cs`: devolve a connection string de `TenantDatabases:{name}`; base sem configuração lança erro **sem** incluir a connection string (depende de T086)
- [X] T089 [US2] Implementar `TenantResolver` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenantResolver.cs` (`ITenantResolver`): consulta `ITenantCatalog`, devolve `false` para inexistente/inativo, valida que a base tem connection string (senão lança) e preenche o `TenantScope` (depende de T087, T088)
- [X] T090 [US2] Implementar `AddTenancy` em `src/Backend/AccessControlSystem.Infrastructure/Tenancy/TenancyServiceCollectionExtensions.cs`: `AddOptions<CatalogOptions>` e `TenantDatabasesOptions` com `ValidateOnStart`; `CatalogDbContext` (connection de `ConnectionStrings:Catalog`); `ITenantCatalog`; `TenantScope` scoped registrado também como `ITenantContext`; `TenantDatabaseConnectionProvider`; `ITenantResolver`; `AddDbContext<AppDbContext>((sp, options) => ...)` usando o `TenantScope` (**lança se não resolvido**, falha fechada) e `TenantDbContextOptions.Configure`; sem estado estático nem cache de dado de tenant (depende de T089). Se o `ValidateOnStart` exigir `PackageReference` nova na Infrastructure (ex.: `Microsoft.Extensions.Hosting.Abstractions`), justificar em `plan.md` no mesmo trabalho (Princípio II)
- [X] T091 [P] [US2] Implementar `TenantId(this ClaimsPrincipal)` em `src/Backend/AccessControlSystem.Api/Extensions/ClaimPrincipalExtensions.cs` (claim `tenant`, texto vazio quando ausente)
- [X] T092 [US2] Implementar o `TokenService` em `src/Backend/AccessControlSystem.Api/Services/AccountContext/TokenService.cs`: chave de `IOptions<SecretsOptions>`, claim `tenant` = `ITenantContext.TenantId` (lança se não resolvido); `Id`, `Name`, `Role` e validade de 8 horas como hoje (depende de T085)
- [X] T093 [US2] Completar `UseDefaultLoginTenantAttribute` em `src/Backend/AccessControlSystem.Api/Filters/UseDefaultLoginTenantAttribute.cs` como filtro de **recurso** (`IAsyncResourceFilter`, roda antes do *model binding*): resolve `Login:DefaultTenantId` via `ITenantResolver`; ausente, inexistente ou inativo ⇒ `401` com o envelope e a mensagem de hoje (`Authenticate.Response("Credenciais inválidas", 401)`) e motivo só no log (sem tenant, token, e-mail ou segredo) (depende de T090)
- [X] T094 [US2] Alterar `src/Backend/AccessControlSystem.Api/Extensions/BuilderExtensions.cs`: `AddConfiguration` passa a registrar `SecretsOptions` (com `ValidateOnStart`) e `LoginOptions`; `AddDatabase` passa a chamar `AddTenancy`; em `AddJwtAuthentication`, usar `IOptions<SecretsOptions>` para a chave, `OnMessageReceived` chamando `context.NoResult()` quando o endpoint tem `UseDefaultLoginTenantAttribute`, e `OnTokenValidated` que (1) exige `Id` e `tenant` válidos (`context.Fail`), (2) chama `ITenantResolver.ResolveAsync` (`Fail` se `false`), (3) só então chama `IActiveSessionChecker.IsActiveAsync`. Todo `Fail` cai no `OnChallenge` existente (`401`, `"Não autenticado"`) (depende de T091, T093)
- [X] T095 [US2] Em `src/Backend/AccessControlSystem.Api/Controllers/AccountContextController.cs`, adicionar **apenas** `[UseDefaultLoginTenant]` à ação `Login` (o atributo, sem lógica no controller; comentários XML inalterados) (depende de T093)
- [X] T096 [US2] Em `src/Backend/AccessControlSystem.Api/Program.cs`, remover `app.ApplyMigrations()` e **apagar** `src/Backend/AccessControlSystem.Api/Extensions/MigrationExtensions.cs` (a Api deixa de aplicar migrations ao iniciar) (depende de T094)
- [X] T097 [US2] **Apagar** `src/Backend/AccessControlSystem.Application/Configuration.cs` e confirmar por busca que não resta nenhuma referência a `Configuration.Secrets` nem `Configuration.Database` (depende de T092, T094)
- [X] T098 [P] [US2] Atualizar `src/Backend/AccessControlSystem.Api/appsettings.json` (remover `ConnectionStrings:DefaultConnection`; incluir `ConnectionStrings:Catalog`, `TenantDatabases` e `Login:DefaultTenantId` vazios; manter `Secrets:PasswordSaltKey` sem uso, legado) e `appsettings.Development.json` (sem segredos) conforme `contracts/configuration.md`
- [X] T099 [US2] Executar `dotnet build Backend.slnx` e `dotnet test Backend.slnx`: tudo verde (depende de T096, T097, T098)

**Checkpoint**: US1 + US2 funcionam. Escopo mínimo entregável: o sistema atende vários tenants com o isolamento provado e o token validado.

---

## Phase 5: User Story 3 - Os dados existentes passam a pertencer a um tenant inicial (Priority: P1)

**Objetivo**: adoção de uma base legada sem perda, registrando o tenant inicial no catálogo. Cobre FR-027 a FR-029.

**Teste independente**: base no formato atual (`v1` + `SeedAdmin` + `AddIsActiveToOperator`) com operadores recebe as duas migrations novas com o tenant inicial configurado; mesma contagem de linhas, todas no tenant inicial, login com as credenciais de antes.

- [X] T100 [US3] Criar os esqueletos: `src/Backend/AccessControlSystem.Migrator/Options/InitialTenantOptions.cs`, `src/Backend/AccessControlSystem.Migrator/Orchestration/InitialTenantRegistrar.cs` (lançando `NotImplementedException`) (o parâmetro opcional `initialTenantId` de `IDatabaseMigrationRunner.RunAsync` já existe desde T031, com a assinatura fixa `RunAsync(string migrationConnectionString, CancellationToken cancellationToken, Guid? initialTenantId = null)`, e é ignorado até T105)

### Testes da US3 (escrever primeiro, ver falhar)

- [X] T101 [P] [US3] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Migrations/LegacyDataMigrationTests.cs` (banco legado criado migrando só até `20260910225125_AddIsActiveToOperator`, com operadores inseridos, inclusive o admin de seed legado): `Dados_legados_passam_a_pertencer_ao_tenant_inicial_sem_perda` (mesma contagem, nenhum campo alterado além de `tenant_id`), `Operador_legado_faz_login_com_as_credenciais_de_antes` (via `ApiFactory`, tenant padrão = tenant inicial, `200`), `Sem_tenant_inicial_a_migration_falha_no_not_null_e_nao_atribui_tenant`, `Admin_de_seed_legado_e_preservado_no_tenant_inicial`
- [X] T102 [P] [US3] Criar `src/Backend/AccessControlSystem.Tests/Migrator/InitialTenantRegistrarTests.cs`: `Registra_a_base_e_o_tenant_inicial_ativo`, `Registro_e_idempotente`, `Recusa_cnpj_ja_usado_por_outro_tenant`, `Grava_o_cnpj_como_informado_sem_validar`
- [X] T103 [US3] Executar os testes de T101 e T102 e confirmar que **falham** (depende de T101, T102)

### Implementação da US3

- [X] T104 [US3] Implementar `InitialTenantOptions` (`Id`, `Cnpj`, `Name`, `DatabaseName`, `DatabaseKind` padrão `Shared`) em `src/Backend/AccessControlSystem.Migrator/Options/InitialTenantOptions.cs`; valores vêm de configuração, nunca fixos no código (FR-028)
- [X] T105 [US3] No `EfDatabaseMigrationRunner`, quando `initialTenantId` for informado, executar `SELECT set_config('acs.initial_tenant_id', @id, false)` **na mesma conexão aberta** antes de `MigrateAsync` (valor por parâmetro) (depende de T104)
- [X] T106 [US3] Implementar `InitialTenantRegistrar` em `src/Backend/AccessControlSystem.Migrator/Orchestration/InitialTenantRegistrar.cs`: com o usuário migrator, registrar de forma idempotente a base (`tenant_databases`) e o tenant (`tenants`, `is_active = true`) a partir de `InitialTenantOptions`; CNPJ gravado **como informado**; unicidade garantida só pelo índice do banco (depende de T104). O `Id` vem de `InitialTenantOptions.Id` e `created_at` é `DateTime.UtcNow` (UTC), ambos definidos pela aplicação
- [X] T107 [US3] Executar os testes de T101 e T102 e `dotnet test Backend.slnx`: tudo verde (depende de T105, T106)

**Checkpoint**: um ambiente legado pode ser adotado sem perda.

---

## Phase 6: User Story 4 - E-mail do operador único dentro do tenant (Priority: P2)

**Objetivo**: e-mail único por tenant, `409` mantido dentro do tenant. O índice `(tenant_id, email)` (T068) e a remoção do índice global (T067) já guiaram o teste vermelho `Mesmo_email_em_dois_tenants_e_aceito` (T049); aqui ficam os testes de aceitação do cenário completo, no nível de repositório e de Api. Cobre FR-019.

**Teste independente**: cadastrar o mesmo e-mail em dois tenants (sucesso) e duas vezes no mesmo tenant (conflito).

- [ ] T108 [P] [US4] Em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Create/CreateRepositoryTests.cs`, adicionar `SaveAsync_aceita_o_mesmo_email_em_outro_tenant` e `SaveAsync_no_mesmo_tenant_lanca_ConflictException`
- [ ] T109 [P] [US4] Em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs`, adicionar `UpdateAsync_aceita_email_que_existe_so_em_outro_tenant`, `UpdateAsync_para_email_do_mesmo_tenant_lanca_ConflictException` e `CheckIfEmailIsNotUsed_considera_so_o_tenant_corrente`
- [ ] T110 [P] [US4] Em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs`, adicionar os mesmos três casos para o `UpdateMe`
- [ ] T111 [P] [US4] Criar `src/Backend/AccessControlSystem.Tests/Api/EmailPerTenantApiTests.cs` (`ApiFactory`): `Cadastro_de_email_de_outro_tenant_retorna_201`, `Cadastro_de_email_do_mesmo_tenant_retorna_409_com_a_mensagem_de_hoje`, `Alterar_email_ou_situacao_em_um_tenant_nao_afeta_o_outro`
- [ ] T112 [US4] Executar os testes de T108 a T111. Se algum falhar, **corrigir a causa** (índice, mapa ou repositório), nunca o teste (depende de T108 a T111)
- [ ] T113 [US4] Em `src/Backend/AccessControlSystem.Api/Controllers/AccountContextController.cs`, ajustar apenas os comentários XML dos `409` de `create`, `update/{id}` e `update-me` para "Já existe conta cadastrada com esse e-mail neste tenant." e conferir que o Swagger continua gerando (contrato HTTP inalterado, `api.ts` não muda) (depende de T112)

**Checkpoint**: e-mail único por tenant provado.

---

## Phase 7: User Story 5 - Atualização de todas as bases por um passo separado (Priority: P2)

**Objetivo**: Migrator que atualiza o catálogo e todas as bases em lotes, retomável, com estado por base e trava global. Cobre FR-020 a FR-026.

**Teste independente**: várias bases em versões diferentes; forçar a falha de uma; rodar de novo e conferir que só o que faltava foi feito e que o registro de versão por base está correto.

- [ ] T114 [US5] Criar os esqueletos em `src/Backend/AccessControlSystem.Migrator/`: `Options/MigratorOptions.cs`, `Orchestration/CatalogRegistry.cs`, `Orchestration/MigrationOrchestrator.cs`, `Commands/MigrateCommand.cs` (lançando `NotImplementedException`); `MigratorApplication.cs` com o roteamento dos verbos e dos códigos de saída de `contracts/migrator-cli.md` (esqueleto; o `Program.cs` só delega a ela)

### Testes da US5 (escrever primeiro, ver falhar)

- [ ] T115 [P] [US5] Criar `src/Backend/AccessControlSystem.Tests/Migrator/CatalogRegistryTests.cs`: `MarkRunning_grava_situacao_e_data_da_tentativa`, `MarkSucceeded_grava_a_versao`, `MarkFailed_grava_o_motivo_truncado_em_1000_caracteres`, `Base_elegivel_quando_versao_defasada_ou_situacao_diferente_de_Succeeded`, `Listagem_em_lotes_por_chave`
- [ ] T116 [P] [US5] Criar `src/Backend/AccessControlSystem.Tests/Migrator/MigrationOrchestratorTests.cs` (catálogo real da fixture e `IDatabaseMigrationRunner` falso via NSubstitute): `Processa_em_lotes_do_tamanho_configurado`, `Respeita_a_concorrencia_maxima`, `Atualiza_todas_as_bases_e_registra_a_versao`, `Falha_de_uma_base_nao_impede_as_outras`, `Segunda_execucao_so_reprocessa_pendentes_e_falhas`, `Base_Running_de_queda_e_reprocessada`, `Base_nova_no_catalogo_e_atualizada_sem_mudar_o_passo`, `Duas_execucoes_simultaneas_nao_atualizam_a_mesma_base_ao_mesmo_tempo`, `Motivo_da_falha_nao_contem_connection_string`, `Base_sem_connection_string_de_migracao_vira_Failed`, `Mil_bases_com_uma_falha_atualizam_as_outras_999_e_a_reexecucao_processa_so_a_que_falhou` (SC-006, executor falso)
- [ ] T117 [P] [US5] Criar `src/Backend/AccessControlSystem.Tests/Migrator/MigrationOrchestratorIntegrationTests.cs` (executor real): `Duas_bases_reais_sao_atualizadas_e_registradas`, `Base_com_connection_string_invalida_falha_e_a_outra_conclui`, `Reexecucao_retoma_so_a_base_que_falhou`
- [ ] T118 [P] [US5] Criar `src/Backend/AccessControlSystem.Tests/Migrator/MigratorProgramTests.cs` (chamam `MigratorApplication.RunAsync` diretamente: sem subir processo e sem `InternalsVisibleTo`): `Sai_com_0_quando_tudo_foi_atualizado`, `Sai_com_1_quando_alguma_base_falhou`, `Sai_com_2_com_configuracao_invalida`, `Sai_com_4_quando_a_trava_global_esgota_o_tempo`
- [ ] T119 [US5] Executar os testes de T115 a T118 e confirmar que **falham** (depende de T115 a T118)

### Implementação da US5

- [ ] T120 [US5] Implementar `MigratorOptions` em `src/Backend/AccessControlSystem.Migrator/Options/MigratorOptions.cs`: `Migrator:BatchSize` (padrão inicial `10`), `Migrator:MaxDegreeOfParallelism` (padrão inicial `2`), `Migrator:LockTimeoutSeconds` (padrão inicial `600`), valores iniciais **não medidos**, validados (maiores que zero) com `ValidateOnStart`
- [ ] T121 [US5] Implementar `CatalogRegistry` em `src/Backend/AccessControlSystem.Migrator/Orchestration/CatalogRegistry.cs`: `MarkRunning`, `MarkSucceeded`, `MarkFailed` (motivo truncado em 1000 caracteres, sem segredo), listagem de bases elegíveis por chave `id > @after` em lotes, usando o índice `ix_tenant_databases_migration_status_id` (depende de T120)
- [ ] T122 [US5] Implementar `MigrationOrchestrator` em `src/Backend/AccessControlSystem.Migrator/Orchestration/MigrationOrchestrator.cs` conforme research R-07: trava global `pg_advisory_lock` no catálogo em conexão dedicada com espera até `LockTimeoutSeconds`; lotes por chave com `Parallel.ForEachAsync` limitado; por base `MarkRunning` → `IDatabaseMigrationRunner.RunAsync` (com `initialTenantId` quando for a base de `InitialTenant:DatabaseName`) → `MarkSucceeded`/`MarkFailed`; uma falha não interrompe as demais; devolve o resumo (atualizadas, já na versão, falhas) (depende de T121)
- [ ] T123 [US5] Implementar `MigrateCommand` em `src/Backend/AccessControlSystem.Migrator/Commands/MigrateCommand.cs`: aplica as migrations do `CatalogDbContext` (usuário migrator), executa o `InitialTenantRegistrar` se `InitialTenant` estiver configurado, depois o orquestrador; imprime o resumo estruturado em português (nome da base, estado, motivo curto; nunca connection string) (depende de T122)
- [ ] T124 [US5] Completar `MigratorApplication.RunAsync` em `src/Backend/AccessControlSystem.Migrator/MigratorApplication.cs` (o `Program.cs` só delega a ela e devolve o código de saída; o parâmetro `configure` permite aos testes injetar configuração): host genérico com `DOTNET_ENVIRONMENT`, `ConnectionStrings:CatalogMigration`, `TenantDatabases` e `MigratorOptions`, verbos `migrate` (e `seed-dev` na US6) e códigos de saída `0` (ok), `1` (alguma base falhou), `2` (configuração/argumentos inválidos), `3` (`seed-dev` recusado), `4` (trava global esgotada) (depende de T123)
- [ ] T125 [US5] Criar `src/Backend/AccessControlSystem.Migrator/Dockerfile`: multi-stage com contexto `src/Backend` (copia os `.csproj` de Api, Application, Domain, Infrastructure e Migrator para o *restore*), publica o Migrator e usa a imagem `mcr.microsoft.com/dotnet/aspnet:10.0` (o Migrator referencia a Api) com `libgssapi-krb5-2`, usuário não root, `ENTRYPOINT ["dotnet", "AccessControlSystem.Migrator.dll"]` (depende de T124)
- [ ] T126 [P] [US5] Em `.github/workflows/backend-ci.yml`, acrescentar o passo `dotnet publish src/Backend/AccessControlSystem.Migrator/AccessControlSystem.Migrator.csproj --configuration Release` após o publish da Api. Incluir também `docker/postgres/sql/**` e `docker/postgres/init/**` nos `paths` dos gatilhos `push` e `pull_request` (os testes embutem esses SQL, e mudar só eles precisa disparar o CI)
- [ ] T127 [US5] Executar os testes de T115 a T118 e `dotnet test Backend.slnx`: tudo verde (depende de T124, T125)

**Checkpoint**: atualização de todas as bases pelo Migrator, retomável e registrada.

---

## Phase 8: User Story 6 - Ambiente de desenvolvimento com os dois cenários de base (Priority: P2)

**Objetivo**: um comando sobe catálogo, base compartilhada (2 tenants), base dedicada (1 tenant), migrator e uma Api por tenant de teste; o admin de seed só existe em Development. Cobre FR-030, FR-031, FR-034 a FR-036.

**Teste independente**: em máquina limpa, `docker compose up --build` e, sem passo manual extra, autenticar nos três tenants (cada um na sua Api) e confirmar o isolamento.

- [ ] T128 [US6] Criar os esqueletos `src/Backend/AccessControlSystem.Migrator/Options/DevSeedOptions.cs` e `src/Backend/AccessControlSystem.Migrator/Commands/SeedDevCommand.cs` (lançando `NotImplementedException`)

### Testes da US6 (escrever primeiro, ver falhar)

- [ ] T129 [P] [US6] Criar `src/Backend/AccessControlSystem.Tests/Migrator/SeedDevCommandTests.cs`: `Recusa_fora_de_Development_sem_escrever_nada` (código `3`, catálogo e bases intactos), `Cria_bases_tenants_e_operadores_sob_rls` (grava pelo `AppDbContext` como `acs_app`, não como migrator), `E_idempotente`, `Mesmo_email_em_tenants_diferentes_com_senhas_diferentes` (`admin@admin.com.br` nos três tenants; `operador@exemplo.com` em A e B com senhas diferentes), `Usa_os_ids_e_cnpjs_sinteticos_de_data_model` (dados da seção 4 de `data-model.md`)
- [ ] T130 [US6] Executar os testes de T129 e confirmar que **falham** (depende de T129)

### Implementação da US6

- [ ] T131 [US6] Implementar `DevSeedOptions` (`AdminPassword`, `OperatorPasswordTenantA`, `OperatorPasswordTenantB`) em `src/Backend/AccessControlSystem.Migrator/Options/DevSeedOptions.cs`. O e-mail do admin de dev é fixo (`admin@admin.com.br`, FR-030); a senha vem da configuração (`DevSeed:AdminPassword`, preenchida pelo `.env`), sem senha fixa no código
- [ ] T132 [US6] Implementar `SeedDevCommand` em `src/Backend/AccessControlSystem.Migrator/Commands/SeedDevCommand.cs` e ligar o verbo `seed-dev` em `MigratorApplication.cs`: recusar (código `3`) quando `DOTNET_ENVIRONMENT` não for `Development`; registrar no catálogo as bases `shared_01` (Shared) e `dedicated_01` (Dedicated) e os três tenants de `data-model.md` seção 4 (ids `11111111-...`, `22222222-...`, `33333333-...`, CNPJs sintéticos `11222333000181`, `11444777000161`, `11222333000262`); criar os operadores sintéticos em cada tenant pelo `AppDbContext` com RLS (tenant via `TenantScope`, conexão como `acs_app`), consultando antes de criar (idempotente) (depende de T131). Os ids são os fixos de `data-model.md` seção 4 (definidos pela aplicação, nunca pelo banco) e `created_at` é `DateTime.UtcNow` (UTC)
- [ ] T133 [US6] Criar `docker/postgres/init/01-init.sh` (bash, executável): usando `psql` com as senhas do ambiente (`ACS_MIGRATOR_PASSWORD`, `ACS_APP_PASSWORD`), aplicar `/docker/sql/roles.sql`, `ALTER ROLE ... PASSWORD`, criar os bancos `acs_catalog`, `acs_shared_01` e `acs_dedicated_01` com dono `acs_migrator` e aplicar `catalog-db-grants.sql` em `acs_catalog` e `tenant-db-grants.sql` nos outros dois, sempre com `-d` do banco certo
- [ ] T134 [US6] Reescrever `docker-compose.yml`: serviço `db` (`postgres:18`, volume novo `pg-data`, montando `./docker/postgres/init` em `/docker-entrypoint-initdb.d` e `./docker/postgres/sql` em `/docker/sql`); serviço `migrator` de uso único (`restart: "no"`, build com `src/Backend/AccessControlSystem.Migrator/Dockerfile`, `DOTNET_ENVIRONMENT=Development`, usuário migrator, comando que roda `migrate` e depois `seed-dev`); serviços `backend` (5290, tenant A), `backend-b` (5291, tenant B) e `backend-dedicated` (5292, tenant dedicado), mesma imagem, cada um com o seu `Login__DefaultTenantId`, `ConnectionStrings__Catalog` e `TenantDatabases__shared_01__ConnectionString`/`TenantDatabases__dedicated_01__ConnectionString` como `acs_app`, e `depends_on` do `migrator` com `service_completed_successfully`; `frontend` como hoje (o nginx continua apontando para `backend`); `ASPNETCORE_ENVIRONMENT` padrão `Development`. Manter `PASSWORD_SALT_KEY` (legado) (depende de T125, T132, T133)
- [ ] T135 [P] [US6] Atualizar `.env.example` conforme `contracts/configuration.md` (`POSTGRES_PASSWORD`, `ACS_MIGRATOR_PASSWORD`, `ACS_APP_PASSWORD`, `JWT_PRIVATE_KEY`, `PASSWORD_SALT_KEY`, `DEV_ADMIN_PASSWORD` e senhas de operador, `BACKEND_PORT`, `BACKEND_B_PORT`, `BACKEND_DEDICATED_PORT`, `FRONTEND_PORT`, `DB_PORT`, `ASPNETCORE_ENVIRONMENT=Development`) com a senha de dev (`DEV_ADMIN_PASSWORD` e as de operador) vindo do `.env` e um valor padrão de desenvolvimento no `.env.example` (o `.env` real não é versionado), e remover `POSTGRES_USER`/`POSTGRES_DB`; avisar que o compose é só de desenvolvimento
- [ ] T136 [P] [US6] Atualizar `README.md`: o comando `docker compose up --build` sobe catálogo, bases, migrator e três Apis; a Api **não aplica migrations ao iniciar**; as portas e o tenant de cada Api; `docker compose down -v` na primeira vez (volume antigo em formato legado); a regra "migrations só pelo Migrator" (nunca `dotnet ef database update` numa base nova)
- [ ] T137 [P] [US6] Atualizar `CLAUDE.md` (seção Comandos): acrescentar os comandos do Migrator e a regra "migrations só pelo Migrator"; remover a menção de que a Api aplica migrations
- [ ] T138 [P] [US6] Atualizar `src/Backend/AccessControlSystem.Tests/README.md`: nova estrutura de pastas (`Premises/`, `Architecture/`, `Tenancy/`, `Catalog/`, `Migrations/`, `Migrator/`), a fixture com papéis reais e a explicação de que as bases de teste não recebem o admin de seed
- [ ] T139 [US6] Executar os testes de T129 e `dotnet test Backend.slnx`: tudo verde. Depois, com Docker ligado, `docker compose up --build` e os cenários V1, V2, V3, V4, V5, V8, V11, V12, V13 e V14 de `quickstart.md`, anotando o resultado (depende de T134 a T138)

**Checkpoint**: ambiente de desenvolvimento completo.

---

## Phase 9: User Story 7 - Mover um tenant entre bases sem transformar dados (Priority: P3)

**Objetivo**: provar que o modelo permite mover um tenant copiando as linhas dele. A propriedade vem do modelo (Guid gerado pela aplicação, `tenant_id` como único vínculo); o procedimento operacional fica fora da spec. Cobre FR-032 e FR-033.

**Teste independente**: copiar as linhas de um tenant da base compartilhada para uma dedicada vazia e verificar que o tenant funciona de forma idêntica.

- [ ] T140 [US7] Criar `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantMobilityTests.cs` (tenants gerados no teste com `NewTenantId()`): `Tenant_copiado_para_base_dedicada_funciona_de_forma_identica` (lê as linhas do tenant na base compartilhada e as insere na dedicada vazia **sem transformar nenhum dado**, atualiza `tenants.database_id` no catálogo e confere login, listagem e alteração com os mesmos ids), `Outros_tenants_da_base_de_origem_nao_percebem_a_mudanca`, `Tenant_movido_de_volta_para_base_com_outros_tenants_nao_gera_conflito_de_id`. Como a propriedade vem do modelo, o teste pode passar de primeira: se falhar, é bug do modelo de dados e a **causa** deve ser corrigida (depende de T071, T043)
- [ ] T141 [US7] Executar T140 e `dotnet test Backend.slnx`: tudo verde (depende de T140)

---

## Phase 10: Acabamento e transversais

**Propósito**: gates finais e conferências que atravessam as histórias.

- [ ] T142 Executar os gates de `src/Backend`: `dotnet build Backend.slnx` sem erros nem avisos novos e `dotnet test Backend.slnx` (Docker ligado) com tudo verde. Nenhum teste apagado, pulado ou enfraquecido
- [ ] T143 Executar `dotnet list Backend.slnx package --vulnerable --include-transitive`: nenhum pacote vulnerável novo; **não** suprimir alertas
- [ ] T144 Conferir por busca que não restam `Configuration.Secrets`, `Configuration.Database`, `ApplyMigrations` nem estado estático com dado de tenant em `src/Backend/**/*.cs` (conferência complementar: a regra é verificada automaticamente pelo teste T083; a única exceção permitida são os dois interceptors `static readonly` de `TenantDbContextOptions`, que não guardam dado de tenant), e que nenhum segredo foi parar em `appsettings*.json`, `docker-compose.yml` ou logs
- [ ] T145 Conferir a conformidade com a constituição: nomes em inglês no código e `snake_case` no banco; comentários, `#region` e nomes de teste em português; comentários XML dos endpoints preservados; `AsNoTracking` nas leituras novas; `timestamptz` nas colunas de data; nenhum use case do `AccountContext` com assinatura alterada (`GetMe` sem nenhuma mudança em Request/Response/Specification/Handler/`IRepository`/`Repository`); Domain sem nenhuma mudança (`git diff --stat src/Backend/AccessControlSystem.Domain` vazio)
- [ ] T146 Confirmar que o adiamento do cenário k6 para a spec 06 continua registrado em Complexity Tracking do `plan.md` e que **nenhum** número de desempenho foi inventado (SC-010 não é verificado nesta spec). Conferir também que SC-010 está anotado na spec como "verificado na spec 06; fora desta entrega"
- [ ] T147 Executar `quickstart.md` por inteiro (seções 2 a 7) e registrar o resultado de V1 a V14; cenários automatizados (H3, H5, H7) conferidos pelos testes correspondentes
- [ ] T148 Registrar na descrição do PR as exceções constitucionais desta entrega, com o texto de Complexity Tracking do `plan.md`: (1) cenário k6 e **SC-010 verificados na spec 06, fora desta entrega**, sem nenhum número de desempenho inventado; (2) 2 idas ao banco por requisição autenticada (catálogo + base do tenant); (3) migração fundadora sem compatibilidade com a Api anterior (depende de T146)
- [ ] T149 Sugerir ao dono do projeto rodar `/speckit-analyze` e, depois da implementação, `/speckit-converge`; abrir o PR na `main` com o CI verde (a branch `20261031-221333-multi-tenant-foundation` não tem commits até o dono pedir)

---

## Dependências e ordem de execução

### Dependências entre fases

- **Premissas (Fase 1)**: sem dependências. **Bloqueia tudo.** Falha de premissa segue as regras da própria fase.
- **Fundação (Fase 2)**: depende da Fase 1. **Bloqueia todas as histórias.**
- **US1 (Fase 3)**: depende da Fase 2. **Bloqueia US2, US3, US4, US5, US6 e US7**, porque entrega o `AppDbContext` com tenant, as migrations `AddTenantIdToOperators`/`EnforceTenantIsolationOnOperators` e a fixture F2.
- **US2 (Fase 4)**: depende de US1.
- **US3 (Fase 5)**: depende de US1; o teste de login usa o `ApiFactory` da US2.
- **US4 (Fase 6)**: depende de US1 e de US2 (testes de Api).
- **US5 (Fase 7)**: depende da Fase 2, de US1 (migrations reais) e de US3 (a implementação do `initialTenantId`, cujo teste de login usa o `ApiFactory` da US2). **Só começa depois da US3.**
- **US6 (Fase 8)**: depende de US2 (Api) e US5 (Migrator).
- **US7 (Fase 9)**: depende de US1 e do catálogo (Fase 2).
- **Acabamento (Fase 10)**: depende de todas as histórias desejadas.

### Dentro de cada história

- Esqueletos → testes (devem falhar) → implementação → executar tudo.
- Fundação e esqueletos antes dos testes que os referenciam.
- Migrations só pelo Migrator; `dotnet ef migrations add` apenas para gerar.

### Oportunidades de paralelismo

- Fase 1: T001/T002/T003/T004/T006/T007 em paralelo; os sete testes T011 a T017 em paralelo.
- Fase 2: T025 a T028 em paralelo; T032 a T035 em paralelo; T037 e T038 em paralelo.
- US1: testes T048 a T060 em paralelo (arquivos diferentes).
- US2: testes T074 a T083 em paralelo; T085, T086 e T091 em paralelo.
- Depois da US1: US2 e US7 em paralelo; US5 só depois da US3.

### Exemplo de paralelismo: Fase 1 (testes das premissas)

```bash
# Depois de T010, os sete testes em paralelo (arquivos diferentes):
Task: "P-01 em Tests/Infrastructure/Premises/P01WriteTenantPremiseTests.cs"
Task: "P-02 em Tests/Infrastructure/Premises/P02ReadTenantPremiseTests.cs"
Task: "P-03 em Tests/Infrastructure/Premises/P03QueryShapesPremiseTests.cs"
Task: "P-04 em Tests/Infrastructure/Premises/P04ServiceProviderPremiseTests.cs"
Task: "P-05 em Tests/Infrastructure/Premises/P05PoolPremiseTests.cs"
Task: "P-06 em Tests/Infrastructure/Premises/P06SeedAdminHistoryPremiseTests.cs"
Task: "P-07 em Tests/Infrastructure/Premises/P07LoginTokenIgnoredPremiseTests.cs"
```

### Exemplo de paralelismo: US1 (isolamento dos repositórios)

```bash
Task: "Caso de isolamento em Tests/Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs"
Task: "Caso de isolamento em Tests/Infrastructure/AccountContext/Get/GetRepositoryTests.cs"
Task: "Caso de isolamento em Tests/Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs"
Task: "Caso de isolamento em Tests/Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs"
```

---

## Estratégia de implementação

### Premissas primeiro (obrigatório)

1. Fase 1 inteira, com os sete testes verdes, ou plano B / parar e reportar conforme as regras da fase.
2. Só então a Fase 2.

### Escopo mínimo entregável (US1 + US2)

1. Fase 1, Fase 2.
2. Fase 3 (US1): **parar e validar** o isolamento nas duas topologias.
3. Fase 4 (US2): validar token, `401` genérico e login pelo tenant padrão.
4. US3 também é P1: sem ela não há como adotar uma base com dados, então a entrega para um ambiente com dados só fecha com a US3.

### Entrega incremental

1. Fundação → US1 → US2 → US3 (P1 completo).
2. US4 e US5 (P2); US6 (P2) fecha o ambiente de desenvolvimento.
3. US7 (P3) prova a mobilidade.
4. Acabamento e PR.

---

## Notas

- [P] = arquivos diferentes, sem dependência de tarefa incompleta.
- O rótulo [Story] liga a tarefa à história para rastreio.
- Verificar que os testes falham antes de implementar; esqueletos servem para falhar por comportamento, não por erro de compilação.
- Evitar: tarefas vagas, conflito no mesmo arquivo, contornar teste que falha.
- O cenário k6 e a medição de SC-010 ficam para a spec 06 (Complexity Tracking do plan.md).
