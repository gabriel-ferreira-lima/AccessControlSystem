# Pesquisa: Fundação Multi-Tenant

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-10-31

Não restou nenhum `NEEDS CLARIFICATION` no Contexto Técnico. Este documento registra as decisões
técnicas, as alternativas descartadas e, em cada ponto que depende de comportamento do EF Core ou
do Npgsql que não pôde ser confirmado só lendo código, a **premissa a ser provada por teste antes
de implementar** (R-14 reúne essas premissas).

---

## R-01. Como o tenant chega ao banco (escritas e leituras sem transação explícita)

**Contexto.** O RLS lê `current_setting('app.tenant_id', true)`. A constituição exige que o valor
seja informado **por transação** (`set_config(..., true)`), nunca por sessão, para continuar
correto atrás de PgBouncer em modo transação. Uma conexão de pool é reaproveitada por tenants
diferentes, então um valor de sessão vazaria.

**Problema das leituras.** Uma consulta do EF sem transação roda em autocommit: o `set_config(...,
true)` executado em um comando separado terminaria junto com a transação implícita desse comando e
não valeria para a consulta seguinte. E, no EF Core, um `SaveChanges` de um único comando também
roda sem transação explícita.

**Decisão.** Três mecanismos, todos registrados só nas opções de contexto "com tenant"
(`AddTenancy`), nunca nas opções usadas para aplicar migrations:

1. **Escritas**: `Database.AutoTransactionBehavior = AutoTransactionBehavior.Always` no
   `AppDbContext`, de modo que todo `SaveChanges` abre uma transação do EF. O
   `TenantTransactionInterceptor` (`DbTransactionInterceptor`) executa
   `SELECT set_config('app.tenant_id', @tenant, true)` logo depois de `TransactionStarted(Async)`,
   na própria transação. Isso também cobre quem abrir uma transação explícita
   (`Database.BeginTransactionAsync`).
2. **Leituras fora de transação** (`command.Transaction is null`): o `TenantCommandInterceptor`
   (`DbCommandInterceptor`) em `ReaderExecuting(Async)` antepõe ao texto do comando
   `SELECT set_config('app.tenant_id', @__acs_tenant, true);` (com o valor como parâmetro) e em
   `ReaderExecuted(Async)` avança o leitor com `NextResult(Async)` para pular o resultado do
   `set_config`, devolvendo o mesmo leitor ao EF. Os dois comandos seguem juntos para o servidor
   (uma ida ao banco) e, sem transação explícita, rodam na transação implícita do lote, então o
   valor não sobrevive depois dele e é compatível com PgBouncer em modo transação.
3. **Comandos que não são consulta e não estão em transação** (`ExecuteUpdate`, `ExecuteDelete`,
   SQL cru, escalares): o interceptor **lança** (`InvalidOperationException`, mensagem em
   português) em vez de seguir, porque seguiria sem tenant e, sob RLS, falharia em silêncio (0
   linhas afetadas). Nenhum repositório atual usa esses caminhos.

**Interceptors sem estado.** Os dois interceptors são **instâncias únicas sem estado**
(`static readonly` em `TenantDbContextOptions`, sem DI e sem nenhum dado de tenant, o que o
Princípio I permite) e descobrem o tenant a partir do contexto em uso: `eventData.Context`
implementa `ITenantAwareDbContext` (`ITenantContext TenantContext { get; }`), que o `AppDbContext`
implementa. O tenant nunca fica guardado no interceptor. *Correção feita na execução do P-04:* a
versão anterior deste parágrafo dizia que instâncias novas de interceptor fariam o EF criar um
provedor de serviços por requisição. Isso **não se confirmou** no EF 10.0.12: com instâncias novas por
contexto o P-04 continuou passando (controle negativo), e só falhou quando cada contexto recebeu um
`IMemoryCache` próprio. O que justifica o desenho é o Princípio I e a ausência de alocação por
requisição, não o cache de provedores.

Em todos os caminhos, se o `ITenantContext` não estiver resolvido o interceptor lança **antes** de ir
ao banco (falha fechada). O RLS continua sendo a segunda camada: uma conexão que chegue ao banco
sem `app.tenant_id` não enxerga nem altera linha alguma.

**Por que a política tem `NULLIF(..., '')`.** Depois que uma configuração personalizada é usada
numa sessão, o PostgreSQL passa a devolver string vazia (e não `NULL`) fora da transação em que
foi definida. Um `''::uuid` falharia. Ver R-02.

**Alternativas descartadas.**
- `SET app.tenant_id` por sessão em `ConnectionOpened`: viola o Princípio I (vaza entre tenants
  atrás de pooler em modo transação).
- Transação curta por comando (`BEGIN`, `set_config`, consulta, `COMMIT` ao fechar o leitor):
  correta, mas custa mais idas ao banco e exige coordenar o commit no fechamento do leitor.
  **É o plano B**: adotado, **sem contornos**, se qualquer das premissas P-01, P-02, P-03 ou P-05
  (R-14) falhar. Os testes dessas premissas verificam o comportamento (o tenant vale só na
  operação em curso e não vaza), e não o mecanismo, então valem igual para o plano B.
- Unidade de trabalho por requisição (uma transação aberta até o fim da requisição): muda a
  semântica atual de autocommit, segura a conexão por mais tempo e perde escritas se algo esquecer
  de confirmar. **Não é um plano em vigor**: só pode ser considerado com uma nova decisão do dono
  do projeto, se o plano B também falhar. Até lá, parar e reportar a falha.
- Alterar os 8 repositórios para abrir transação: espalha a regra, e esquecer num repositório
  futuro só seria detectado por teste.

---

## R-02. RLS, papéis e privilégios

**Decisão.**
- Tabela de tenant: `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY`, uma política
  `operators_tenant_isolation` para todos os comandos, com `USING` e `WITH CHECK` iguais:
  `tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid`. Sem tenant informado a
  comparação é `NULL`, e nenhuma linha passa (falha fechada).
- Papéis (cluster): `acs_migrator` (dono dos bancos e das tabelas, `LOGIN`, `BYPASSRLS`) e
  `acs_app` (`LOGIN`, `NOBYPASSRLS`, não dono). O `BYPASSRLS` do migrator existe só para futuras
  migrations de dados que precisem enxergar todos os tenants; a backfill desta spec roda **antes**
  de o RLS ser ligado e não depende dele.
- Privilégios, por banco, via `ALTER DEFAULT PRIVILEGES FOR ROLE acs_migrator`: bases de tenant →
  `acs_app` recebe `SELECT, INSERT, UPDATE` (sem `DELETE`: não há exclusão física de entidade de
  negócio, Princípio IV); catálogo → `acs_app` recebe só `SELECT`. As migrations **não** citam
  papéis, então o mesmo código serve em qualquer ambiente; o provisionamento/IaC fará o equivalente
  em cada base nova.
- Os arquivos SQL de papéis e de privilégios ficam em `docker/postgres/sql/` (fora da pasta de
  `init/`, porque o entrypoint do Postgres executaria qualquer `.sql` dela no banco errado; o
  `docker/postgres/init/01-init.sh` os aplica no banco certo) e são usados tanto
  pelo Postgres do compose quanto, como recurso embutido, pela `PostgresContainerFixture`. Assim os
  testes exercitam exatamente os mesmos papéis do ambiente de desenvolvimento.

**Alternativas descartadas.** `GRANT` dentro das migrations (acopla migration a nome de papel e
quebra onde o papel não existe); usar o superusuário nos testes (superusuário ignora RLS, o teste
não provaria nada); `BYPASSRLS` na aplicação (proibido).

---

## R-03. Resolução do tenant no pipeline JWT e custo por requisição

**Decisão.** Dentro do `OnTokenValidated` já existente (em `BuilderExtensions.AddJwtAuthentication`):

1. extrai `Id` (operador) e `tenant` (Guid); qualquer um ausente ou malformado → `context.Fail`;
2. `ITenantResolver.ResolveAsync(tenantId)` consulta o catálogo (`ITenantCatalog`), recusa tenant
   inexistente ou inativo (`context.Fail`) e preenche o `TenantScope` com tenant e base;
3. só então chama `IActiveSessionChecker.IsActiveAsync(operatorId)`, que usa o `AppDbContext`
   (já apontando para a base do tenant).

Todo `Fail` cai no `OnChallenge` existente: `401` com `"Não autenticado"`, sem revelar o motivo.
Se o tenant existe mas a base não tem connection string configurada, o resolvedor **lança** (erro
de configuração) e a resposta é `500` pelo `GlobalExceptionHandler`, nunca `401` e nunca outra base.

**Quantas idas ao banco.** Duas por requisição autenticada: uma ao catálogo e uma à base do tenant
(a verificação de sessão). Hoje há uma (a da sessão), então esta spec acrescenta uma. Não dá para
ser uma só: são bancos diferentes, e o Princípio I proíbe cache em memória de dado de tenant. O
cache com TTL ≤ 60 s no Redis é da spec de fundação de escala. Registrado em Complexity Tracking.
O `GetTenantByIdAsync` já traz tenant e base em uma única consulta (duas chaves primárias).

**Alternativas descartadas.** Colocar a situação do tenant dentro do token (um tenant inativado
continuaria entrando até o token expirar, contra FR-016); validar o tenant em um middleware
separado (duplicaria o ponto de rejeição e o envelope de erro).

---

## R-04. Base escolhida por requisição

**Decisão.** `AddDbContext<AppDbContext>((serviceProvider, options) => ...)` lê o `TenantScope`
(scoped) e chama `UseNpgsql(connectionString)` com a connection string da base do tenant,
resolvida por `TenantDatabaseConnectionProvider` a partir de `IOptions<TenantDatabasesOptions>`
(chave = nome da base no catálogo). Se o escopo não estiver resolvido quando o contexto for
criado, a criação lança (falha fechada). O `TenantScope.Set` só aceita ser chamado **uma vez** por
escopo: um segundo tenant na mesma requisição é erro de programação.

**Pools.** O Npgsql mantém um pool por connection string, então cada base ocupa o seu pool. O
tamanho máximo do pool de cada base é configurável (`Maximum Pool Size`, `Connection Idle
Lifetime` na connection string) para o total de conexões abertas não crescer com o número de
bases. Em produção o PgBouncer fica entre a Api e as bases; isso é da spec de ambientes.

**Cache de provedor de serviços do EF.** A connection string não entra no que o EF usa para
decidir se reaproveita o provedor interno de serviços. A premissa P-04 (R-14) é provada por teste
(criar contextos para três bases e não receber `ManyServiceProvidersCreatedWarning`).

**Alternativas descartadas.** `NpgsqlDataSource` por base guardado em dicionário singleton (é
estado estático na Api e cresce com o número de bases); alterar a connection string no
`ConnectionOpening` de um contexto único (esconde a ordem de resolução e vira bug silencioso).

---

## R-05. Catálogo

**Decisão.** Base própria, `CatalogDbContext` com migrations próprias no assembly da Api
(`Migrations/Catalog/`). Duas tabelas (ver [data-model.md](data-model.md)): `tenants` e
`tenant_databases`. O estado da atualização de cada base fica em `tenant_databases` (versão,
situação, data da última tentativa, motivo da falha), o que atende FR-003 e FR-023 sem uma tabela
de histórico.

**O catálogo pertence ao sistema de provisionamento, então o Domain não muda.** Não há `Tenant`,
`TenantDatabase` nem `Cnpj` no Domain, e a classe `Entity` fica intacta. O modelo de persistência
do catálogo (`CatalogTenant`, `CatalogDatabase`, `EMigrationStatus`) vive em
`Infrastructure/Catalog/Models/`, como classes simples sem regra de domínio, com `xmin` como token
de concorrência (propriedade sombra), mapeadas por `CatalogTenantMap` e `CatalogDatabaseMap`. O
enum `EDatabaseKind` fica na Application porque aparece no `record` devolvido ao código de tenant.
O CNPJ é **texto lido**: a coluna comporta o formato com máscara, a única garantia é a
**unicidade** (FR-002), por índice do banco, e não há validação de formato nem de dígitos
verificadores nesta spec.

A Application enxerga o catálogo **só** por `ITenantCatalog` (leitura, devolve `record`s) e
`ITenantResolver`; o código de tenant NÃO lê tabela do catálogo (Princípio I). Quem escreve no
catálogo nesta spec é apenas o Migrator, e só em três casos: o estado de atualização por base
(FR-023), o registro do tenant inicial na adoção (FR-027) e os dados sintéticos do `seed-dev`.
O resto do catálogo é do provisionamento. `ITenantDirectory` (e-mail → tenant) não nasce aqui.

**A base é identificada por nome, e o segredo vem de configuração.** `tenant_databases.name` é a
referência (`shared_01`, `dedicated_01`); `TenantDatabases:{name}:ConnectionString` (Api, usuário
`acs_app`) e `...:MigrationConnectionString` (só Migrator, usuário `acs_migrator`) vêm de
variáveis de ambiente/segredos. Limite conhecido: com milhares de bases, configurar uma a uma não
escala; o provedor de connection string é uma interface de Infrastructure e a implementação por
Secrets Manager é da spec de ambientes/provisionamento.

**Alternativas descartadas.** Guardar host/porta/senha no catálogo (viola FR-004 e o Princípio
IX); entidades de catálogo no Domain (`Tenant`, `TenantDatabase`, `Cnpj`): traria conceitos de
plataforma para o Domain, exigiria mudar `Entity` para aceitar um `Id` informado e duplicaria
regras (validação de CNPJ, regras entre registros) que são do provisionamento; validar o CNPJ
aqui (a validação pertence a quem cadastra o tenant).

---

## R-06. Migrations do `AppDbContext` (tenant_id, índices, RLS)

**Decisão.** Duas migrations novas (as três antigas ficam intactas), em ordem expand → contract:

1. `AddTenantIdToOperators`: `ADD COLUMN tenant_id uuid NULL` e backfill
   `UPDATE operators SET tenant_id = NULLIF(current_setting('acs.initial_tenant_id', true), '')::uuid
   WHERE tenant_id IS NULL`. A configuração `acs.initial_tenant_id` é definida pelo Migrator na
   mesma conexão da migration (ver R-07). Se existirem linhas e a configuração estiver ausente, as
   linhas ficam `NULL` e a migration seguinte falha no `NOT NULL` com erro claro (falha fechada, não
   atribui linhas a um tenant adivinhado). Em tabela vazia, passa.
2. `EnforceTenantIsolationOnOperators`: `tenant_id SET NOT NULL`; remove o índice único global
   `IX_operators_email`; cria `ix_operators_tenant_id_email` (único) e `ix_operators_tenant_id_id`;
   liga `ENABLE` + `FORCE ROW LEVEL SECURITY` e cria a política (R-02).

**Como gerar com `Designer` correto.** Scaffold em dois passos: primeiro com a propriedade
sombra `TenantId` opcional (gera a 1ª), depois com `IsRequired`, índice `(TenantId, Id)` e sem o
índice único de e-mail no mapeamento (gera a 2ª). Em cada uma, o `Up` é completado à mão com o
SQL acima. A `ModelSnapshot` fica consistente com o modelo.

**Índice composto com o e-mail.** `Email` é *owned type* em *table splitting*; o EF não aceita um
índice com propriedades do dono e do owned type. Por isso `ix_operators_tenant_id_email` existe
só na migration (`CreateIndex` com as colunas `tenant_id`, `email`), e a regra é protegida por
teste de integração (mesmo e-mail em dois tenants passa; no mesmo tenant dá `23505` →
`ConflictException`). O `OperatorMap` deixa de declarar o índice único global do e-mail.

**Sobre expand/contract.** O conteúdo segue o padrão, mas as duas migrations são aplicadas no
mesmo passo do Migrator, e a Api antiga não consegue conviver com o RLS. Exceção única e
justificada em Complexity Tracking; vale para esta migration fundadora e para nenhuma outra.

**Alternativas descartadas.** Uma única migration (perde o ponto de retomada entre expandir e
impor); DEFAULT no `tenant_id` (deixaria linhas esquecidas num tenant errado em vez de falhar);
`DELETE` do admin de seed na migration (R-08).

---

## R-07. Aplicação das migrations: Migrator, lotes, retomada, trava

**Decisão.** Novo projeto de console `AccessControlSystem.Migrator`, imagem própria, com os
verbos `migrate` e `seed-dev` (R-11). `migrate` faz, nesta ordem:

1. aplica as migrations do **catálogo** (`CatalogDbContext`, usuário migrator);
2. se `InitialTenant` estiver configurado, registra de forma idempotente a base e o tenant
   inicial no catálogo (FR-027/FR-028: CNPJ, nome e base vêm de configuração);
3. toma a **trava global** (`pg_advisory_lock` no banco do catálogo, em conexão dedicada que dura
   a execução toda; uma segunda execução espera até o timeout configurado e depois reavalia o que
   falta);
4. lista, em **lotes por chave** (`id > @after`, tamanho configurável), as bases cujo
   `schema_version` ≠ última migration ou cuja situação ≠ `Succeeded`, e processa cada lote com
   **concorrência limitada** (`MaxDegreeOfParallelism` configurável). O Migrator não é um job
   em background da Api, é um passo único do pipeline de entrega, então a trava global é o
   `pg_advisory_lock` do próprio Postgres (o lock no Redis do Princípio XIII vale para jobs da Api;
   o Redis também está fora do escopo);
5. para cada base: situação `Running` + data da tentativa (transições feitas pelo `CatalogRegistry`
   do Migrator); aplica a `SeedAdminPolicy` (R-08);
   abre a conexão e define `acs.initial_tenant_id` se for a base do tenant inicial; roda
   `Database.MigrateAsync`; grava `schema_version` (última migration aplicada) e `Succeeded`, ou
   `Failed` + motivo curto **sem connection string nem segredo**;
6. ao final imprime o resumo e sai com `0` (tudo certo) ou `1` (alguma base falhou).

**Retomada.** O estado fica no catálogo, então uma nova execução reprocessa só as bases
`Pending`, `Failed`, `Running` (travada por queda do processo, já que a trava global garante que
ninguém mais está rodando) ou com `schema_version` defasada. As já atualizadas não são refeitas
(SC-006). Uma base falha sem interromper as outras (FR-024). Uma base nova cadastrada no catálogo
entra como `Pending` e é atualizada na próxima execução, sem mudar o passo (História 5, cenário 5).
O `EF Core` já protege cada base de duas execuções concorrentes com a sua própria trava de
migration; a trava global evita dois orquestradores.

**Bundle do EF (`dotnet ef migrations bundle`) vs. em processo.** A diretriz preferia o bundle. A
decisão é **em processo**, pelos motivos: (a) a política da `SeedAdmin`, a configuração
`acs.initial_tenant_id` e o registro de estado precisam rodar na mesma sessão e no mesmo
processo; um bundle só recebe `--connection`; (b) um bundle é um processo por base, o que é caro
com muitas bases; (c) o orquestrador (lotes, retomada, falha, trava) fica testável com
Testcontainers e com uma implementação falsa de `IDatabaseMigrationRunner`, sem gerar executável
nem instalar o SDK do `dotnet ef` na imagem; (d) em runtime só é preciso o runtime .NET, não o
SDK. Registrado em Complexity Tracking.

**Migrations só são aplicadas pelo Migrator.** Nenhuma outra forma de aplicar migrations é
suportada: nem `dotnet ef database update` numa base nova, nem bundle, nem script gerado por
`dotnet ef migrations script`, porque qualquer uma executaria a `SeedAdmin` e criaria o admin de
senha fixa (FR-031). Gerar migrations (`dotnet ef migrations add`) segue normal. O quickstart e o
README repetem essa regra.

**Ferramentas do EF em tempo de design.** Como o `AppDbContext` passa a receber `ITenantContext`,
`dotnet ef migrations add` precisa de `IDesignTimeDbContextFactory` para os dois contextos
(`Api/DesignTime/`).

**Alternativas descartadas.** Api com modo `--migrate` (ver Complexity Tracking no `plan.md`); migrations em Hosted Service
da Api (Api com DDL, N instâncias disputando); script SQL gerado com `dotnet ef migrations script`
(sem estado por base nem retomada).

---

## R-08. `SeedAdmin`

**Decisão.** Não editar a migration. A `SeedAdminPolicy` do Migrator, **antes** de migrar uma
base, consulta o histórico: se a base **não tem nenhuma migration aplicada**, grava na tabela
`__EFMigrationsHistory` (criando-a se preciso) a linha da `20260908171522_SeedAdmin` como já
aplicada, usando a API pública `IHistoryRepository` (`GetCreateIfNotExistsScript`,
`GetInsertScript(new HistoryRow(...))`). O EF então não executa o `Up` dela. Bases que já a
aplicaram (as de desenvolvimento antigas) não são tocadas: o admin delas é preenchido com o tenant
inicial pelo backfill.

O comportamento vale para **qualquer** ambiente, o que atende FR-030/FR-031 de forma mais forte
que "só rodar em Development": nenhuma base nova recebe o admin pela migration. Em desenvolvimento,
o admin com valores fixos nasce pelo `seed-dev` (R-11), que só roda em Development.

**Consequência.** A `SeedAdmin` fica como código morto para bases novas e só serve a bases
legadas de desenvolvimento. Uma spec futura pode consolidar as migrations quando não houver mais
base legada. O teste `Admin_semeado_pela_migration_existe` muda de expectativa (plan.md).

**Alternativas descartadas.** Filtrar a migration com um `IMigrationsAssembly` customizado (usa
tipos internos do EF, `EF1001`); apagar o admin numa migration seguinte (a credencial existiria
por instantes e atrapalharia o `NOT NULL` do `tenant_id`); condicionar o `Up` da `SeedAdmin` ao
ambiente (edita migration aplicada).

---

## R-09. Configuração tipada

**Decisão.** Remover `Application/Configuration.cs` e os usos estáticos:

| Antes | Depois |
|---|---|
| `Configuration.Secrets.JwtPrivateKey` | `SecretsOptions` (Api), `Required`, `ValidateOnStart` |
| `Configuration.Database.ConnectionString` | `CatalogOptions` + `TenantDatabasesOptions` (Infrastructure), `ValidateOnStart` |
| `Configuration.Secrets.PasswordSaltKey` | **sem uso no código**; chave mantida em `appsettings.json` e no compose até a spec que a remove (Legado conhecido) |

`TokenService` e a validação do JWT passam a receber `IOptions<SecretsOptions>`. A exigência de
256 bits na chave é do Princípio IX e fica para a spec de segurança; aqui basta "presente" para
não quebrar chaves de desenvolvimento existentes. `Login:DefaultTenantId` é `Guid?` sem
`ValidateOnStart`: ausente, o login falha com `401` e o problema vai para o log (caso de borda da
spec).

**Teste.** `TokenServiceTests` constrói o serviço com `Options.Create(new SecretsOptions { ... })`
e um `ITenantContext` falso; deixa de mexer em estado estático.

---

## R-10. Login com tenant padrão

**Decisão.** Um filtro de **recurso** (`IAsyncResourceFilter`), aplicado ao `Login` pelo atributo
`[UseDefaultLoginTenant]`, resolve `Login:DefaultTenantId` pelo `ITenantResolver` **antes** do
*model binding*. A ordem importa: os parâmetros `[FromServices] IHandler<...>` são criados no
binding, e a criação do repositório cria o `AppDbContext`, que já precisa saber a base. Um filtro
de ação rodaria tarde demais. Falhando (não configurado, inexistente, inativo), o filtro responde
`401` com o mesmo envelope e a mesma mensagem do handler (`"Credenciais inválidas"`) e registra o
motivo no log (sem expor).

**Token enviado ao login é ignorado.** No `JwtBearerEvents.OnMessageReceived`, se o endpoint tem o
atributo, chama `context.NoResult()`. Sem isso, um token válido de outro tenant faria o
`OnTokenValidated` fixar a base do tenant A, e o login (que resolve o tenant padrão D) tentaria
resolver de novo na mesma requisição. Hoje um token inválido no login já é ignorado; o
comportamento visível é o mesmo.

**Alternativas descartadas.** Resolver o tenant padrão em middleware por caminho (`/v1/account/login`
fixo, frágil); mudar a assinatura do `Authenticate.Handler` (proibido pela diretriz); resolver o
tenant dentro do `TokenService` (tarde demais: o handler já consultou o banco).

---

## R-11. Ambiente de desenvolvimento

**Decisão.** `docker compose up --build` sobe:

- `db`: **um** servidor `postgres:18` com três bancos, `acs_catalog`, `acs_shared_01` (2 tenants)
  e `acs_dedicated_01` (1 tenant), e os papéis `acs_migrator`/`acs_app` criados pelos scripts de
  `docker/postgres/init/01-init.sh` e `docker/postgres/sql/` (R-02). Um servidor basta para provar o isolamento nos dois casos; a
  separação física é do ambiente real. Volume novo `pg-data` (o antigo `db-data` guarda um banco
  de formato legado e os scripts de inicialização só rodam em volume vazio).
- `migrator`: uso único (`restart: "no"`); roda `migrate` e, em Development, `seed-dev`. Cada
  `backend*` depende dele com `service_completed_successfully`.
- `backend` (tenant A, `5290`), `backend-b` (tenant B, `5291`), `backend-dedicated` (tenant
  dedicado, `5292`): mesma imagem, cada um com `Login__DefaultTenantId` de um tenant diferente
  (resposta da clarificação 2). O serviço `backend` mantém o nome porque o nginx do frontend o usa.
- `frontend`: igual a hoje.
- `ASPNETCORE_ENVIRONMENT` passa a ter padrão `Development` no compose; `.env.example` é
  atualizado (hoje diz `Production`), e há um aviso de que o compose é só de desenvolvimento.

**`seed-dev`.** Recusa rodar se o ambiente não for `Development` (código de saída ≠ 0, sem
escrever nada). Registra no catálogo as duas bases e os três tenants, com GUIDs e CNPJs sintéticos
fixos (sintéticos, sem validação) e cria, **pelo `AppDbContext` com RLS** (papel `acs_app`),
operadores em cada tenant: o admin `admin@admin.com.br` em todos (e-mail repetido entre tenants,
FR-035) e um operador comum com o mesmo e-mail em dois tenants, com senhas diferentes. Idempotente
(consulta antes de criar).

**Alternativas descartadas.** Três servidores Postgres (mais pesado sem provar mais); uma só Api
trocando o tenant padrão por variável (passo manual, derrota SC-008); ferramenta de emissão de
token só em dev (caminho de emissão fora do login, risco de vazar para outro ambiente).

---

## R-12. Estratégia de testes

- **`PostgresContainerFixture` adaptada.** Sobe o Postgres, executa os mesmos SQLs de papéis e
  privilégios do compose (R-02), cria bancos de teste, aplica as migrations **com o orquestrador
  real** (usuário migrator) e entrega contextos pelo usuário `acs_app`, usando a mesma
  configuração de `TenantDbContextOptions.Configure`. `CreateContext()` devolve um contexto de um tenant
  padrão da fixture (os testes existentes seguem sem alteração de chamada); `CreateContext(Guid
  tenantId)` e `CreateConnectionAs...` servem aos novos testes. Há uma segunda base na mesma
  fixture para o teste de tenant dedicado e de mobilidade.
- **Dados legados.** Para provar a migração dos dados atuais, o teste cria uma base, aplica só
  `v1`, `SeedAdmin` e `AddIsActiveToOperator` (`IMigrator.MigrateAsync("...AddIsActiveToOperator")`),
  insere operadores e então roda o orquestrador com `InitialTenant`.
- **Pipeline da Api.** `WebApplicationFactory<Program>` (exige `public partial class Program`)
  com o Postgres da fixture; tokens gerados pelo próprio `TokenService` e, para os casos inválidos,
  assinados no teste com a mesma chave.
- **Orquestrador.** `IDatabaseMigrationRunner` é uma interface: testes de lote/retomada/falha usam
  uma implementação falsa (NSubstitute), e um teste de integração usa a real.
- **Nomes e organização** seguem a constituição (frases em português com underscore;
  `[Collection(PostgresCollection.Name)]` + `[Trait("Category", "Integration")]`).

---

## R-13. Mobilidade do tenant (FR-032/FR-033, História 7)

**Decisão.** A propriedade vem do modelo: `id` é Guid gerado pela aplicação, o único vínculo com o
tenant é a coluna `tenant_id`, e não há FK entre bancos nem identidade sequencial. Mover é copiar as
linhas com o `tenant_id`, apontar o catálogo para a base nova e funcionar igual. O **procedimento
operacional** de mover em produção (janela, verificação, troca no catálogo) é de outra frente
(provisionamento/operação). Esta spec entrega a **prova** em teste: copia as linhas de um tenant de
uma base compartilhada para uma dedicada vazia (leitura e `INSERT` com o papel migrator, sem
transformar dado), atualiza `tenants.database_id` e verifica login, listagem e alteração; depois
copia de volta para uma compartilhada que já tem outros tenants, sem conflito de `id`.

---

## R-14. Premissas a provar antes de implementar (spikes em forma de teste)

**Ordem (decisão do dono do projeto).** Os testes destas sete premissas são as **primeiras tarefas
da implementação**, antes de qualquer outra (em especial o **P-02**, das leituras com RLS). Nada
da feature é implementado antes de todos estarem escritos e executados. Cada um é um teste de
integração escrito primeiro, que falha antes e passa só com a implementação mínima do mecanismo.
P-04, P-06 e P-07 se provam contra as próprias primitivas do EF e do ASP.NET (banco de teste,
`IHistoryRepository`, um host de teste mínimo com um endpoint marcado), portanto não dependem do
Migrator nem do filtro de login para existir.

**Se uma premissa falhar.**
- P-01, P-02, P-03 ou P-05 (o caminho do tenant até o banco): adotar o **plano B do R-01** (transação
  curta por comando), **sem contornos**: nada de `SET` por sessão, de afrouxar ou pular o teste, nem
  de remendo no interceptor só para o teste passar. Os mesmos testes passam a valer para o plano B.
  Se o plano B também falhar: parar e reportar a falha com a saída real (Integridade das entregas).
- P-04, P-06 ou P-07 (fora do que o plano B substitui, e para as quais este documento não define
  plano alternativo): parar e reportar a falha, sem contornar. Seguir só com uma nova decisão.

| ID | Premissa | Teste que prova |
|---|---|---|
| P-01 | `AutoTransactionBehavior.Always` faz até o `SaveChanges` de um comando abrir transação, e o `set_config(..., true)` do `TransactionStarted` vale para o `INSERT/UPDATE` | `Escrita_aplica_o_tenant_e_o_rls_recusa_tenant_diferente` |
| P-02 | Comando com o `set_config` na frente, enviado em um lote pelo Npgsql, roda em uma transação implícita, o valor vale para a consulta seguinte do lote e **não** sobrevive ao lote | `Leitura_aplica_o_tenant_apenas_no_proprio_lote` (lê `current_setting` fora do lote e espera vazio) |
| P-03 | `ReaderExecuted(Async)` pode avançar o leitor com `NextResult` e devolver o mesmo leitor sem confundir o EF (consultas simples, `Any`, `Count`, projeções, paginação) | os testes de repositório existentes continuam passando |
| P-04 | A connection string não faz o EF criar um provedor de serviços novo por base | `Contextos_de_bases_diferentes_nao_geram_provedores_demais` |
| P-05 | Com pool de **uma** conexão e tenants alternando, nada vaza entre requisições | `Pool_de_uma_conexao_alternando_tenants_nao_vaza` |
| P-06 | `IHistoryRepository` insere a linha da `SeedAdmin` numa base nova e o `Migrate` seguinte a pula | `Base_nova_nao_recebe_o_admin_de_seed` |
| P-07 | `OnMessageReceived` enxerga os metadados do endpoint (roteamento já executado) | `Token_enviado_ao_login_e_ignorado` |

### Resultado da execução (Fase 1 do `tasks.md`, 2026-10-31)

Ambiente: .NET SDK 10.0.402, EF Core 10.0.12, Npgsql EF 10.0.3, PostgreSQL 18 (Testcontainers,
Docker 29.9.2), papéis reais `acs_migrator` e `acs_app`.

| ID | Antes (T018, esqueletos) | Depois (T022, implementação mínima) | Observação |
|---|---|---|---|
| P-01 | vermelho (`NotImplementedException`) | **verde** | `AutoTransactionBehavior.Always` + `set_config` no `TransactionStarted`; o `WITH CHECK` devolve `42501` para tenant diferente |
| P-02 | vermelho (2 métodos) | **verde** | Leitura sem transação recebe o tenant no mesmo lote; na **mesma conexão física** (pool de 1) o `app.tenant_id` está vazio depois do lote |
| P-03 | vermelho (2 métodos) | **verde** | `FirstOrDefault`, `SingleOrDefault`, `Any`, `Count`, projeção, `Skip`/`Take`, com e sem `AsNoTracking`; `ExecuteUpdate`/`ExecuteDelete` fora de transação lançam |
| P-04 | vermelho | **verde** | 30 contextos com connection strings e tenants diferentes, sem `ManyServiceProvidersCreatedWarning`. Controle negativo feito (ver abaixo) |
| P-05 | vermelho | **verde** | Pool de 1 conexão alternando tenants em leituras e escritas: sem vazamento |
| P-06 | **verde de primeira** | **verde** | `IHistoryRepository` pré-marca a `SeedAdmin`; 0 operadores; `is_active` existe; histórico igual a `GetMigrations()` |
| P-07 | **verde de primeira** | **verde** | O `OnMessageReceived` enxerga os metadados do endpoint; o token enviado ao login não chega ao `OnTokenValidated` |

- **Plano B do R-01: não acionado** (nenhuma das premissas P-01, P-02, P-03 e P-05 falhou). A T024 foi
  pulada pela condição da própria tarefa.
- **Controles negativos do P-04** (para provar que o teste não é vazio): (1) trocar os interceptors
  compartilhados por instâncias novas a cada contexto **não** fez o teste falhar, o que mostra que, no
  EF 10.0.12, instâncias de interceptor não entram na chave do provedor de serviços (a justificativa
  do R-01 foi corrigida); (2) dar um `IMemoryCache` próprio a cada contexto fez o teste falhar com
  `ManyServiceProvidersCreatedWarning`, então ele detecta o problema que vigia. Os dois controles foram
  revertidos.
- **Regressão**: depois da Fase 1, `dotnet build Backend.slnx` com 0 avisos e 0 erros, e
  `dotnet test Backend.slnx` com 189 testes aprovados e 0 falhas (inclui os 9 métodos de `Premises/`).
