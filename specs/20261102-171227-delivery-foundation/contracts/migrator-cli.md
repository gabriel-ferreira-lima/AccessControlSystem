# Contrato: Migrator, verbos novos (`seed-staging` e `verify-no-seed-admin`)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-15, R-16 | **Spec**: FR-024 a FR-035 | **Base**:
[../../20261031-221333-multi-tenant-foundation/contracts/migrator-cli.md](../../20261031-221333-multi-tenant-foundation/contracts/migrator-cli.md)

Estende o contrato da spec 01. Os verbos `migrate` e `seed-dev` **não mudam**. A Api continua sem
aplicar migrations e sem receber connection string com DDL.

## Comandos

```text
AccessControlSystem.Migrator migrate                  # inalterado
AccessControlSystem.Migrator seed-dev                 # inalterado (só Development)
AccessControlSystem.Migrator seed-staging             # NOVO: só Staging
AccessControlSystem.Migrator verify-no-seed-admin     # NOVO: somente leitura; recusado em Development
```

Sem argumento ou com verbo desconhecido: texto de uso listando os quatro verbos e código `2`
(inalterado).

## `seed-staging`

- **Guarda de ambiente (antes de ler a configuração)**: só roda com `DOTNET_ENVIRONMENT=Staging`
  (comparação sem diferenciar maiúsculas). Qualquer outro valor, **inclusive ausente** (que vale
  `Production`), recusa com o código **`5`**, mensagem clara e **nada escrito**.
- **Configuração**: as de sempre do Migrator (`ConnectionStrings:CatalogMigration`,
  `TenantDatabases:{nome}:ConnectionString` e `:MigrationConnectionString` para `shared_01` e
  `dedicated_01`) mais a seção `StagingSeed` (`AdminPassword`, `OperatorPasswordTenantA`,
  `OperatorPasswordTenantB`), todas **de fora do repositório**. Senha ausente ou fora das regras de
  senha: código `2`, **nada criado**, **valor nunca impresso**.
- **Fluxo** (o do `seed-dev`, pelo `SyntheticTenantSeeder`): (1) catálogo atualizado e tenants e
  bases **registrados** (idempotente); (2) **atualização da estrutura** das bases recém-cadastradas
  pelo orquestrador do próprio Migrator, que as faz **nascer sem o admin fixo** (`SeedAdminPolicy`);
  se **qualquer** uma falhar, código `1` e **nenhum operador criado** (o registro no catálogo pode
  ficar, por ser idempotente, e uma nova execução, com a causa corrigida, conclui); (3) operadores
  pelo `AppDbContext` com `TenantScope`, sob RLS, como `acs_app`. **Por que o comando registra e
  atualiza**: o verbo `migrate` só atualiza bases **já registradas** no catálogo (FR-024, FR-030).
- **Dados**: os de [../data-model.md](../data-model.md), seção 4 (3 tenants: 2 em `shared_01`,
  1 em `dedicated_01`; ids estáveis; e-mails em `.test`; nenhum com `admin@admin.com.br`).
- **Idempotente**: reexecutar não duplica tenants, bases nem operadores e não troca senha.
- **Saída**: nomes das bases e contagens; nunca connection string, nunca senha.

## `verify-no-seed-admin`

- **Guarda de ambiente (antes de ler a configuração)**: com `DOTNET_ENVIRONMENT=Development`,
  recusa com o código **`6`** e explica que não se aplica a esse ambiente. Roda em qualquer outro
  (Staging, Production, ...).
- **Somente leitura**: cada conexão abre **transação somente leitura** (`BEGIN READ ONLY`); nada é
  alterado em nenhuma base (escrever falharia com `25006`). Pooling desligado.
- **Configuração**: `ConnectionStrings:CatalogMigration` e `TenantDatabases:{nome}:MigrationConnectionString`
  (o **migrator**, porque só ele tem `BYPASSRLS` e enxerga todos os tenants). Concorrência
  limitada por `Migrator:MaxDegreeOfParallelism`; leitura do catálogo paginada por chave
  (`Migrator:BatchSize`).
- **O que procura** ([../data-model.md](../data-model.md), seção 3): em cada base de tenant, na
  tabela da própria migration, `id` fixo **ou** `lower(email)` fixo (**qualquer um basta**); no
  catálogo, o e-mail fixo e o identificador fixo nas colunas de e-mail e `operator_id` do esquema
  `public`. Os dados fixos saem de `new SeedAdmin().UpOperations`, nunca de literais duplicados.
- **Reprova** (código `7`) se **qualquer** base contiver o administrador fixo.
- **Não consegue verificar** (código `8`): base cadastrada no catálogo sem connection string de
  migração; connection string configurada para base que não está no catálogo; base inacessível;
  ou a leitura do administrador fixo na própria migration `SeedAdmin` falha (a migration mudou de
  forma): nunca se aprova sem saber o que procurar. Se houver `7` e `8`, sai `7` e a saída lista ambos.
- **"Não contém"** (aprova a base): base vazia ou sem as tabelas de operadores.
- **Saída**: contagem de bases verificadas e, nas falhas, o **nome lógico** da base e o motivo
  curto. **Nunca** connection string, e-mail de pessoa, identificador de operador ou senha.
- **Quando roda**: depois do `migrate` (a spec 05b o encaixa no pipeline); aqui é só o verbo.

## Códigos de saída

| Código | Significado |
|---|---|
| `0` | Tudo certo (inclusive: verificação sem administrador fixo em nenhuma base) |
| `1` | Pelo menos uma base terminou em `Failed` (as demais foram tentadas) |
| `2` | Configuração ou argumentos inválidos (inclusive senha de `StagingSeed` ausente/inválida) |
| `3` | `seed-dev` recusado por ambiente diferente de Development |
| `4` | Tempo de espera pela trava global esgotado |
| **`5`** | `seed-staging` recusado por ambiente diferente de Staging |
| **`6`** | `verify-no-seed-admin` recusado em Development |
| **`7`** | Administrador fixo encontrado (reprovou) |
| **`8`** | Não foi possível verificar (base inacessível ou divergência) |

## Garantias (cada uma tem teste, com Testcontainers)

1. `seed-staging` em Development, em Production e **sem** `DOTNET_ENVIRONMENT`: código `5`, nenhuma
   escrita.
2. `seed-staging` em Staging cria 3 tenants (2 compartilhada, 1 dedicada) com operadores em `.test`,
   nenhum com o e-mail fixo; reexecutar não muda a contagem de tenants nem de operadores.
3. Senha ausente ou inválida: código `2`, nada criado, valor ausente da saída.
3a. **Base com falha na atualização** (connection string de migração inválida numa base): código
   `1`, **nenhum operador criado em nenhuma base**, saída com o nome lógico da base que falhou; uma
   segunda execução, com a causa corrigida, conclui sem duplicar nada (FR-030).
3b. **Nenhuma senha fictícia literal** no código de produção, nos `appsettings*.json`, nos
   `docker-compose*.yml` nem em `.github/` (SC-010; `FictitiousPasswordsNotInRepoTests`: os testes
   e o `.env.example` ficam fora do escopo).
4. `seed-staging` seguido de `verify-no-seed-admin` em Staging: código `0` (ponta a ponta).
5. `verify-no-seed-admin`: base limpa → `0`; base **contaminada de verdade** (migrada **sem** a
   política, o que executa a `SeedAdmin`) → `7`; **e-mail alterado com identificador fixo** → `7`;
   **identificador trocado com e-mail fixo** (inclusive em outras maiúsculas e minúsculas) → `7`;
   catálogo contaminado → `7`; base sem tabelas → aprova; base inacessível → `8`; divergência
   catálogo × configuração → `8`; Development → `6`; a transação é somente leitura (uma escrita na
   mesma conexão falha).
6. `SeedAdminFingerprint` extrai `operators`, `id`, `email`, `cc59e349-efbd-4800-9726-f38a325e4d3d`
   e `admin@admin.com.br` (literais independentes no teste); falha alto se a migration mudar de
   forma.
7. `seed-dev` e `migrate` seguem passando **sem nenhuma edição** nos testes existentes.
