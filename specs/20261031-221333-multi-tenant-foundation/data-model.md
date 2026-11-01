# Modelo de Dados: Fundação Multi-Tenant

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Pesquisa**: [research.md](research.md)

Nomes de banco em `snake_case` e inglês; datas em `timestamptz` (UTC); identificadores `Guid`
gerados pela aplicação. Duas camadas de dados: o **catálogo** (uma base) e as **bases de tenant**
(compartilhadas ou dedicadas, todas com o mesmo esquema).

**O Domain não muda.** Não há entidade, value object ou enum novo no Domain, e a classe `Entity`
continua como está. O catálogo pertence ao sistema de provisionamento (Princípio I): aqui ele é
**lido** pela Application por interfaces que devolvem `record`s (`ITenantCatalog`, ver
[contracts/internal-interfaces.md](contracts/internal-interfaces.md)), e o seu modelo de
persistência vive só na Infrastructure.

## 1. Catálogo (base `acs_catalog`)

Modelos de persistência em `Infrastructure/Catalog/Models/`: classes simples, sem regra de domínio,
públicas porque o Migrator as usa. Mapeadas por `CatalogTenantMap` e `CatalogDatabaseMap`
(`IEntityTypeConfiguration`, `snake_case`) no `CatalogDbContext`.

### `tenant_databases` — modelo `CatalogDatabase`

Onde os dados de um ou mais tenants moram, e em que versão da estrutura essa base está.

| Coluna | Tipo | Regras |
|---|---|---|
| `id` | `uuid` | PK, gerado pela aplicação (`ValueGeneratedNever`: nunca pelo banco) |
| `name` | `varchar(100)` | NOT NULL, **único**. É a chave em `TenantDatabases:{name}` da configuração (R-05). **Não** é connection string. Sem validação de formato nesta spec |
| `kind` | `smallint` | NOT NULL; `EDatabaseKind` (Application): `1` Shared, `2` Dedicated |
| `schema_version` | `varchar(150)` | NULL até a primeira atualização; id da última migration aplicada |
| `migration_status` | `smallint` | NOT NULL, padrão `1`; `EMigrationStatus` (Infrastructure, abaixo) |
| `migration_attempted_at` | `timestamptz` | NULL; início da última tentativa |
| `migration_error` | `varchar(1000)` | NULL; motivo curto da última falha, **sem connection string nem segredo** |
| `created_at` | `timestamptz` | NOT NULL; preenchido pela aplicação em UTC (`DateTime` com `Kind = Utc`), sem valor padrão no banco |
| `xmin` | `xid` | token de concorrência otimista (propriedade sombra) |

Índices: `ix_tenant_databases_name` (único) e `ix_tenant_databases_migration_status_id` em
`(migration_status, id)` (lotes do orquestrador, R-07).

### `tenants` — modelo `CatalogTenant`

| Coluna | Tipo | Regras |
|---|---|---|
| `id` | `uuid` | PK; é o valor da claim `tenant`. Gerado pela aplicação (ou informado ao registrar), `ValueGeneratedNever`: nunca pelo banco |
| `cnpj` | `varchar(18)` | NOT NULL, **único**. Texto como veio, **sem validação nesta spec** (a validação é do provisionamento). O tamanho comporta o formato com máscara |
| `name` | `varchar(200)` | NOT NULL |
| `is_active` | `boolean` | NOT NULL, padrão `true` |
| `database_id` | `uuid` | NOT NULL, FK → `tenant_databases.id` (`ON DELETE RESTRICT`) |
| `created_at` | `timestamptz` | NOT NULL; preenchido pela aplicação em UTC (`DateTime` com `Kind = Utc`), sem valor padrão no banco |
| `xmin` | `xid` | token de concorrência otimista (propriedade sombra) |

Índices: `ix_tenants_cnpj` (único; é a garantia de FR-002, uma restrição do banco e não uma
validação de formato), `ix_tenants_database_id`, `ix_tenants_id_active` (parcial `WHERE
is_active`, paginação por chave de `GetActiveTenantListAsync`).

### Quem lê e quem escreve

| Quem | O quê |
|---|---|
| Api / Application | **Só lê**, por `ITenantCatalog` e `ITenantResolver`. O papel `acs_app` tem só `SELECT` no catálogo |
| Migrator (`migrate`) | Escreve o **estado de atualização** em `tenant_databases` (FR-023) e, na adoção, registra a base e o tenant inicial a partir de `InitialTenant` (FR-027) |
| Migrator (`seed-dev`) | Escreve bases e tenants sintéticos, só em Development |
| Provisionamento (fora do escopo) | Dono do restante do catálogo: criar tenants e bases, validar CNPJ, regras entre registros (ex.: base `Dedicated` hospeda um tenant) |

Mover um tenant (História 7) é, no catálogo, atualizar `tenants.database_id`. O procedimento
operacional é da frente de provisionamento/operação; o teste de mobilidade faz essa atualização
diretamente.

### `EMigrationStatus` e transições

`EMigrationStatus` mora em `Infrastructure/Catalog/Models/`. As transições são feitas pelo
Migrator (`CatalogRegistry`: `MarkRunning`, `MarkSucceeded`, `MarkFailed`), não por métodos do
modelo.

| Valor | Nome | Significado |
|---|---|---|
| `1` | `Pending` | Cadastrada, ainda não atualizada (estado inicial) |
| `2` | `Running` | Em atualização; se aparecer numa nova execução, é resto de uma queda e é reprocessada |
| `3` | `Succeeded` | `schema_version` = última migration aplicada |
| `4` | `Failed` | Última tentativa falhou; `migration_error` tem o motivo |

```text
Pending ──MarkRunning──► Running ──MarkSucceeded──► Succeeded
                            │
                            └──MarkFailed──► Failed ──MarkRunning──► Running
Succeeded ──(nova migration no código: schema_version ≠ última)──► reprocessada via MarkRunning
```

O texto do motivo é truncado em 1000 caracteres. Uma base é **elegível** quando
`schema_version` ≠ última migration **ou** `migration_status` ≠ `Succeeded`.

## 2. Bases de tenant (compartilhadas e dedicadas)

### `operators` (única tabela de tenant hoje)

| Coluna | Tipo | Observação |
|---|---|---|
| `id` | `uuid` | PK (`PK_operators`), sem mudança |
| `email` | `varchar(255)` | sem mudança; **deixa de ser único no sistema** |
| `password` | `varchar(255)` | hash, sem mudança |
| `role` | `smallint` | sem mudança |
| `is_active` | `boolean` | sem mudança |
| `tenant_id` | `uuid` | **NOVA**, NOT NULL ao fim da migration 2; *shadow property* `TenantId` no EF; **não existe** em `Operator`, `Request` nem `Response` |

Índices ao fim das migrations:

| Índice | Colunas | Observação |
|---|---|---|
| `PK_operators` | `(id)` | mantido |
| `ix_operators_tenant_id_email` | `(tenant_id, email)` **único** | substitui `IX_operators_email`; criado na migration, fora do modelo do EF (R-06) |
| `ix_operators_tenant_id_id` | `(tenant_id, id)` | lookup por id, lista paginada, contagens (declarado no modelo) |

Política de RLS (contrato SQL em [contracts/database-isolation.md](contracts/database-isolation.md)):
`ENABLE` + `FORCE ROW LEVEL SECURITY`, política `operators_tenant_isolation` com `USING` e `WITH
CHECK` por `tenant_id`.

### Regra para toda tabela de tenant futura

`TenantModelBuilderExtensions.ApplyTenantIsolation` percorre as entidades do `AppDbContext` que
não são *owned*, e para cada uma: cria a propriedade sombra `TenantId` (`tenant_id uuid NOT NULL`),
o filtro global `EF.Property<Guid>(e, "TenantId") == CurrentTenantId` e o índice `(tenant_id, id)`.
Toda tabela nova precisa, além disso, da sua migration com `tenant_id`, RLS `ENABLE`+`FORCE` e a
política (teste de arquitetura: toda entidade do `AppDbContext` tem a política no banco).

### Preenchimento de `tenant_id`

No `SaveChanges` do `AppDbContext`: entidades `Added` recebem o tenant do `ITenantContext`;
entidades `Modified` têm `tenant_id` marcado como **não modificado** (nunca é atualizado). Sem
tenant resolvido, o `SaveChanges` lança antes de ir ao banco. O `WITH CHECK` do RLS é a segunda
camada.

## 3. Estado em memória na Api (por requisição, não é dado persistido)

| Tipo | Escopo | Conteúdo |
|---|---|---|
| `TenantScope` (`ITenantContext`) | scoped | `IsResolved`, `TenantId`, nome da base. Preenchido uma única vez pelo `TenantResolver`; um segundo `Set` lança |
| `AppDbContext` | scoped | criado com a connection string da base do tenant (R-04) |

Nada disso é estático, singleton ou cache (Princípio I).

## 4. Dados sintéticos de desenvolvimento (`seed-dev`, só em Development)

| Tenant | Id | CNPJ (sintético) | Base | Operadores |
|---|---|---|---|---|
| Tenant A | `11111111-1111-1111-1111-111111111111` | `11222333000181` | `shared_01` (Shared) | `admin@admin.com.br` (Admin), `operador@exemplo.com` (Operator) |
| Tenant B | `22222222-2222-2222-2222-222222222222` | `11444777000161` | `shared_01` (Shared) | `admin@admin.com.br` (Admin), `operador@exemplo.com` (Operator, **senha diferente da do A**) |
| Tenant D | `33333333-3333-3333-3333-333333333333` | `11222333000262` | `dedicated_01` (Dedicated) | `admin@admin.com.br` (Admin) |

O e-mail repetido entre tenants exercita FR-019 e FR-035. O tenant padrão de cada Api de
desenvolvimento é A (`backend`), B (`backend-b`) e D (`backend-dedicated`).
