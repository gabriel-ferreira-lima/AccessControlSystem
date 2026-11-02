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
| P9 | Privilégios por tabela exatamente: `SELECT` (catálogo) / `SELECT, INSERT, UPDATE` (tenant) | ✔ | ✔ | ✔ |
| P10 | `INSERT`, `UPDATE` e `DELETE` negados em toda tabela do catálogo (só leitura) | ✔ | — | — |
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
| `canary_delete` | `DELETE` (e `TRUNCATE`) em uma tabela | P3 / P9 |
| `canary_bypass` | atributo `BYPASSRLS` | P6 |
| `canary_owner` | dono de uma tabela | P7 |
| `canary_member` | membro de outro papel com DDL | P7 |
| `canary_catalog_write` | `INSERT` numa tabela do catálogo | P9 / P10 |

Os canários são criados e removidos pelo teste (nomes únicos por execução, papel do cluster
apagado no fim), sem tocar no `acs_app` compartilhado pelo resto da suíte.

## Observações

- Cada instrução proibida roda em **conexão nova**: a transação aborta no erro.
- O SQLSTATE esperado é `42501` (insufficient_privilege). Se o PostgreSQL devolver outro código
  para alguma instrução (ex.: `SET ROLE`), o teste registra o código real e a razão no próprio
  teste, sem afrouxar a exigência de que a instrução **falhe**.
- O catálogo não tem prova de RLS: ele não guarda dados de negócio de tenant (Princípio I).
- Se uma spec futura (ex.: o diretório de operadores, spec 09) der escrita ao `acs_app` em tabelas
  específicas do catálogo, ela ajusta P9/P10 e o canário correspondente.
