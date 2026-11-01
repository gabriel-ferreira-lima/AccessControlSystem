# Contrato: isolamento no banco (RLS, papéis, privilégios)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-01, R-02 | **Spec**: FR-007 a FR-011

## Configurações do PostgreSQL usadas

| Nome | Quem define | Escopo | Uso |
|---|---|---|---|
| `app.tenant_id` | Api, via `set_config('app.tenant_id', @tenant, true)` | **transação/lote** (nunca sessão) | lido pela política de RLS |
| `acs.initial_tenant_id` | Migrator, via `set_config(..., false)` na conexão do migrator | sessão do migrator (sem pooler) | usada só pelo backfill das migrations `AddTenantIdToOperators` e `EnforceTenantIsolationOnOperators` (esta repete o backfill para a execução poder ser retomada, R-06) |

O valor sempre vai por **parâmetro**, nunca concatenado ao SQL.

## Política (padrão para toda tabela de tenant)

```sql
ALTER TABLE operators ENABLE ROW LEVEL SECURITY;
ALTER TABLE operators FORCE ROW LEVEL SECURITY;

CREATE POLICY operators_tenant_isolation ON operators
    USING      (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
```

`NULLIF(..., '')` evita erro de conversão quando a configuração existe mas está vazia (comportamento do
PostgreSQL fora da transação em que foi definida). Sem tenant informado o resultado é `NULL` e nenhuma
linha passa.

## Papéis e privilégios

| Papel | Atributos | Em bases de tenant | No catálogo |
|---|---|---|---|
| `acs_migrator` | `LOGIN`, `BYPASSRLS`, **dono** dos bancos e das tabelas | DDL e DML completos | DDL e DML completos |
| `acs_app` | `LOGIN`, `NOBYPASSRLS`, **não dono**, sem DDL | `SELECT`, `INSERT`, `UPDATE` (sem `DELETE`) | `SELECT` |

Os privilégios são concedidos por `ALTER DEFAULT PRIVILEGES FOR ROLE acs_migrator` em cada banco,
nos arquivos `docker/postgres/sql/` (`roles.sql`, `tenant-db-grants.sql`,
`catalog-db-grants.sql`), aplicados no banco certo por `docker/postgres/init/01-init.sh`. As migrations não citam papéis. Criar uma base nova (provisionamento/IaC)
exige executar o equivalente desses arquivos.

## Garantias (cada uma tem teste de integração)

| ID | Garantia | Teste |
|---|---|---|
| G1 | Com `app.tenant_id` = A, `SELECT` só vê linhas de A, mesmo com `IgnoreQueryFilters()` | `Rls_bloqueia_mesmo_sem_o_filtro_do_ef` |
| G2 | Sem `app.tenant_id`, `SELECT` devolve 0 linhas e `INSERT`/`UPDATE` são recusados | `Conexao_sem_tenant_nao_ve_nem_altera_linhas` |
| G3 | `INSERT` com `tenant_id` ≠ tenant corrente é recusado (`WITH CHECK`) | `Insert_de_outro_tenant_e_recusado_pelo_banco` |
| G4 | `UPDATE` não consegue trocar o `tenant_id` de uma linha | `Update_nao_altera_o_tenant_id` |
| G5 | `acs_app` não é dono das tabelas, não tem `BYPASSRLS`, não tem `DELETE` nem DDL | `Papel_da_aplicacao_nao_tem_privilegios_alem_de_dml` |
| G6 | O valor de `app.tenant_id` não sobrevive à transação/lote nem vaza entre requisições na mesma conexão | `Pool_de_uma_conexao_alternando_tenants_nao_vaza` |
| G7 | Mesmo e-mail em tenants diferentes é aceito; duplicado no mesmo tenant dá `23505` | `Email_e_unico_por_tenant` |
| G8 | Toda entidade do `AppDbContext` tem RLS `ENABLE`+`FORCE` e política no banco | `Toda_tabela_de_tenant_tem_rls_e_politica` |

## Exigência para tabelas de tenant futuras

1. Coluna `tenant_id uuid NOT NULL` (propriedade sombra no EF; nunca em entidade de domínio).
2. Índices começando por `tenant_id`.
3. Migration com `ENABLE` + `FORCE ROW LEVEL SECURITY` e a política acima.
4. Migrations compatíveis com a versão anterior da Api (expand/contract), a menos de nova exceção
   justificada.
