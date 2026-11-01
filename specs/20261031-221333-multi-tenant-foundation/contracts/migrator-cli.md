# Contrato: Migrator (linha de comando)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-07, R-08, R-11 | **Spec**: FR-020 a FR-026, FR-030, FR-031, FR-034

Executável `AccessControlSystem.Migrator`, imagem própria. A Api **não** aplica migrations e não
recebe connection string com DDL. Chaves de configuração em [configuration.md](configuration.md).

## Comandos

```text
AccessControlSystem.Migrator migrate     # atualiza o catálogo e todas as bases de tenant
AccessControlSystem.Migrator seed-dev    # só em Development: tenants, bases e operadores sintéticos
```

### `migrate`

1. Aplica as migrations do `CatalogDbContext` no catálogo.
2. Se `InitialTenant` estiver configurado, registra (idempotente) a base e o tenant inicial.
3. Toma a trava global no catálogo (espera até `Migrator:LockTimeoutSeconds`).
4. Em lotes (`Migrator:BatchSize`, paginação por `id`), com concorrência limitada
   (`Migrator:MaxDegreeOfParallelism`), para cada base **elegível**:
   - marca `Running` e a data da tentativa;
   - aplica a `SeedAdminPolicy` (base sem nenhuma migration aplicada → pré-marca a `SeedAdmin`);
   - define `acs.initial_tenant_id` se for a base de `InitialTenant:DatabaseName`;
   - executa `Database.MigrateAsync` com o usuário migrator;
   - grava `schema_version` e `Succeeded`, ou `Failed` com o motivo (sem segredos).
5. Imprime o resumo (bases atualizadas, ignoradas por já estarem na versão, falhas) e sai.

**Base elegível**: `schema_version` ≠ última migration **ou** `migration_status` ≠ `Succeeded`.

**Garantias**: retomável (só reprocessa o que falta); idempotente; a falha de uma base não impede
as outras; duas execuções simultâneas não atualizam a mesma base ao mesmo tempo; base nova no
catálogo é atualizada sem mudar o comando; nenhuma etapa cria operador (FR-026).

### `seed-dev`

- Recusa rodar quando `DOTNET_ENVIRONMENT` não é `Development`: sai com código `3` **sem escrever
  nada**.
- Registra no catálogo as bases `shared_01` e `dedicated_01` e os três tenants de
  [data-model.md](../data-model.md) (seção 5); cria os operadores sintéticos em cada tenant
  **pelo `AppDbContext`, sob RLS**, com o papel `acs_app`.
- Idempotente: reexecutar não duplica tenants, bases nem operadores.
- No compose roda depois do `migrate`, no mesmo container one-shot.

## Códigos de saída

| Código | Significado |
|---|---|
| `0` | Tudo certo |
| `1` | Pelo menos uma base terminou em `Failed` (as demais foram tentadas) |
| `2` | Configuração ou argumentos inválidos |
| `3` | `seed-dev` recusado por ambiente diferente de Development |
| `4` | Tempo de espera pela trava global esgotado |

## Saída e logs

Estruturados, em português. Mostram o nome da base (nunca a connection string), o estado e o
motivo curto da falha. Nenhum segredo, token ou senha é escrito.

## Estados por base

Definidos em [data-model.md](../data-model.md) (`EMigrationStatus`, na Infrastructure). As
transições são feitas pelo `CatalogRegistry` do Migrator. O registro é a única fonte da verdade
sobre "em que versão cada base está" (FR-023).

## Migrations só pelo Migrator

Nenhuma outra forma de aplicar migrations é suportada: nem `dotnet ef database update` numa base
nova, nem `dotnet ef migrations bundle`, nem um script de `dotnet ef migrations script`. Qualquer
uma executaria a `SeedAdmin` e criaria o admin de senha fixa (FR-031). Gerar migrations
(`dotnet ef migrations add`) segue normal.
