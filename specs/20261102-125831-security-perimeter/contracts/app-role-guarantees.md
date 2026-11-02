# Contrato: garantias do usuário de banco da aplicação (`acs_app`)

**Spec**: FR-019 a FR-023 | **Plano**: Fase B | **Princípios**: I, IX ("Banco")

Esta spec **não muda** os privilégios; ela fixa o que precisa ser verdade e prova por teste de
integração, executado com o `acs_app` (nunca com o migrator), contra o PostgreSQL real do
Testcontainers. As garantias G1 a G8 da spec 01 (`contracts/database-isolation.md`) continuam
valendo; este contrato acrescenta as de abuso abaixo.

## Garantias por base

| # | Garantia | Catálogo | Compartilhada | Dedicada |
|---|---|:-:|:-:|:-:|
| P1 | `CREATE TABLE`, `CREATE INDEX`, `CREATE SCHEMA` negados (`42501`) | ✔ | ✔ | ✔ |
| P2 | `ALTER TABLE` (adicionar/remover coluna), `DROP TABLE`, `DROP INDEX` negados | ✔ | ✔ | ✔ |
| P3 | `TRUNCATE` e `DELETE` negados | ✔ | ✔ | ✔ |
| P4 | `ALTER TABLE … DISABLE/NO FORCE ROW LEVEL SECURITY` e `DROP POLICY` negados | — | ✔ | ✔ |
| P5 | `ALTER ROLE acs_app …`, `CREATE ROLE`, `SET ROLE acs_migrator`, `SET SESSION AUTHORIZATION acs_migrator` negados | ✔ | ✔ | ✔ |
| P6 | Sem atributos perigosos: superusuário, `CREATEDB`, `CREATEROLE`, `REPLICATION`, `BYPASSRLS` | ✔ | ✔ | ✔ |
| P7 | Não é dono do banco, do esquema `public` nem de nenhuma tabela; não é membro de `acs_migrator` | ✔ | ✔ | ✔ |
| P8 | Sem `CREATE` no esquema `public` | ✔ | ✔ | ✔ |
| P9 | Nenhum privilégio de tabela fora do perfil além de `DELETE`/`TRUNCATE` (P3) e de escrita no catálogo (P10): ex.: `REFERENCES`, `TRIGGER`. Perfil: `SELECT` (catálogo) / `SELECT, INSERT, UPDATE` (tenant) | ✔ | ✔ | ✔ |
| P10 | `INSERT` e `UPDATE` negados em toda tabela do catálogo (só leitura; o `DELETE` é coberto por P3) | ✔ | — | — |
| R1 | Com o tenant A informado, leitura da tabela inteira sem filtro devolve só o tenant A | — | ✔ (spec 01) | ✔ |
| R2 | Sem tenant informado, leitura devolve 0 linhas e `UPDATE` afeta 0 | — | ✔ (spec 01) | ✔ |
| R3 | Com o tenant A informado, `INSERT`/`UPDATE` com `tenant_id` do tenant B é recusado | — | ✔ (spec 01) | ✔ |

"spec 01" = já provado por `RowLevelSecurityTests`/`SchemaContractTests`; não se duplica. Em
`SchemaContractTests` fica o que é único dele (matriz do `operators`, índices, `tenant_id` NOT NULL).

## Prova de que a prova falha (FR-023)

`AppRoleProbe` concentra as consultas de P6 a P9 e devolve uma **lista de violações** para um papel
em um banco. A suíte a exercita de dois lados:

- contra o `acs_app` real das três bases → lista vazia;
- contra **papéis-canário** criados só para o teste, em banco isolado, cada um com um privilégio
  indevido → a lista traz exatamente aquela violação.

| Canário | Privilégio indevido | Violação esperada |
|---|---|---|
| `canary_ddl` | `CREATE` no esquema `public` | P8 |
| `canary_delete` | `DELETE` e `TRUNCATE` em uma tabela | **só** P3 (nunca P9) |
| `canary_extra` | `REFERENCES` e `TRIGGER` em uma tabela | **só** P9 |
| `canary_bypass` | atributo `BYPASSRLS` | P6 |
| `canary_owner` | dono de uma tabela | P7 |
| `canary_member` | membro de `acs_migrator` | P7 |
| `canary_catalog_write` | `INSERT` e `UPDATE` numa tabela do catálogo | **só** P10 (nunca P9) |

**Um privilégio, um código** (classificação da sonda, nesta ordem): `DELETE`/`TRUNCATE` → só `P3`, em
qualquer perfil; `INSERT`/`UPDATE` fora do perfil (só no catálogo) → só `P10`; qualquer outro
privilégio fora do perfil (`REFERENCES`, `TRIGGER`) → só `P9`.

Os bancos isolados dos canários recebem as **migrations reais** (catálogo por
`CreateIsolatedCatalogAsync`, tenant por `CreateMigratedTenantDatabaseAsync`), nunca `CREATE TABLE` à
mão, para o canário valer contra o esquema e os privilégios padrão reais.

Papéis são do **cluster inteiro**, não do banco isolado. Os canários têm nome único por execução
(`canary_<tipo>_<Guid:N>`), são `NOLOGIN` e são removidos no `DisposeAsync`, mesmo se a asserção
falhar: `REASSIGN OWNED BY` e `DROP OWNED BY` em cada banco onde receberam algo, depois `DROP ROLE`.
Cada teste confere que o seu papel sumiu de `pg_roles`, e o `DisposeAsync` da fixture confere, antes
de descartar o contêiner, que nenhum `canary_%` sobrou na suíte inteira. Nada disso toca o `acs_app`
compartilhado pelo resto da suíte.

## Observações

- Cada instrução proibida roda em **conexão nova**: a transação aborta no erro.
- O SQLSTATE esperado é `42501` (insufficient_privilege). Se o PostgreSQL devolver outro código
  para alguma instrução (ex.: `SET ROLE`), o teste registra o código real e a razão no próprio
  teste, sem afrouxar a exigência de que a instrução **falhe**.
- O catálogo não tem prova de RLS: ele não guarda dados de negócio de tenant (Princípio I).
- Se uma spec futura (ex.: o diretório de operadores, spec 09) der escrita ao `acs_app` em tabelas
  específicas do catálogo, ela ajusta P9/P10 e o canário correspondente.
