# Modelo de Dados: Base de entrega sem nuvem

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Pesquisa**: [research.md](research.md)

Esta feature **não cria nem altera tabela, coluna ou migration** e não toca dado de negócio real.
Os "dados" abaixo são conceitos que ela governa: classificação de arquivos, estados do gate, a
identidade fixa da `SeedAdmin`, os dados sintéticos de homologação, os códigos de saída e as regras
do ruleset.

## 1. Áreas e classificação de arquivos (`detect-areas.sh`)

Um arquivo alterado cai em **uma** classe; as áreas aplicáveis saem do conjunto de classes.

| Classe | Casa com | Efeito |
|---|---|---|
| **documento** | `specs/**`, qualquer `*.md`, `.specify/**`, `.claude/**` | não exige backend nem frontend |
| **backend** | `src/Backend/**` que não seja `*.md` | exige `backend` |
| **frontend** | `src/Frontend/**` que não seja `*.md` | exige `frontend` |
| **fora das áreas** | todo o resto (`.github/**`, `docker/**`, `docker-compose.yml`, `scripts/**`, `.gitignore`, `.gitattributes`, outros arquivos de raiz não `*.md`) | exige **backend e frontend** (fechado por padrão) |

Ordem de precedência: documento → backend → frontend → fora das áreas.

Saídas: `backend` e `frontend` (`true`/`false`) e `docs_only`.

| Conjunto de arquivos alterados | `backend` | `frontend` | `docs_only` |
|---|---|---|---|
| só documentos | `false` | `false` | `true` |
| só backend | `true` | `false` | `false` |
| só frontend | `false` | `true` | `false` |
| backend e frontend | `true` | `true` | `false` |
| algum arquivo fora das áreas (com ou sem os outros) | `true` | `true` | `false` |
| **lista vazia** | `true` | `true` | `false` |
| documentos + backend | `true` | `false` | `false` |

Estado incoerente (reprova no gate): `backend=false`, `frontend=false` e `docs_only=false`.

## 2. Estados de cada verificação no resultado do gate

| Estado | Quando | Conta para o aceite |
|---|---|---|
| **aprovada** | área aplicável e `needs.<job>.result = success` | sim |
| **reprovada** | área aplicável e resultado ≠ `success` (`failure`, `cancelled`, `skipped` sem dispensa), chave ausente, saída inválida | **bloqueia** |
| **dispensada** | área sem mudança e resultado `skipped` | sim (explícita) |
| **não ativada** | passo k6 curto enquanto não houver cenários (spec 06) | sim (não bloqueia) e **nunca** aparece como aprovada |

O passo k6 curto tem **id** `k6-short` (chave de `needs`) e **nome exibido**
**`k6 curto (não ativado)`** (o `name:` do job, que é o que a lista de checks do PR mostra, para o ✓
nunca parecer uma aprovação). A linha do resumo do gate usa o mesmo texto.

Resultado do gate: aprovado só se nenhuma linha é "reprovada" e o `changes` terminou com `success`.

## 3. Identidade fixa da `SeedAdmin` (`SeedAdminFingerprint`)

Lida **da própria migration** (`new SeedAdmin().UpOperations`), sem duplicar valores à mão.

| Campo | Origem | Valor hoje |
|---|---|---|
| `Table` | `InsertDataOperation.Table` | `operators` |
| `IdColumn`, `EmailColumn` | posição de `id` e `email` em `InsertDataOperation.Columns` | `id`, `email` |
| `Id` | valor da coluna `id` | `cc59e349-efbd-4800-9726-f38a325e4d3d` |
| `Email` | valor da coluna `email` | `admin@admin.com.br` |

Regras de reprovação (basta **uma**): `id = Id` **ou** `lower(email) = lower(Email)`. Se a
migration mudar de forma e a extração falhar, o verbo **falha** (código `8`, "não foi possível
verificar"), nunca aprova.
O hash da senha não é usado na busca. Teste fixa os dois literais **de forma independente**.

### Locais verificados

| Local | O que se procura |
|---|---|
| Cada base de tenant (`MigrationConnectionString`) | tabela `operators` (se a tabela não existe: "não contém") com `id` fixo ou e-mail fixo |
| Catálogo (`ConnectionStrings:CatalogMigration`) | no esquema `public`: colunas de texto com `email` no nome, e colunas `uuid` chamadas `operator_id`, com o e-mail fixo ou o identificador fixo |

Resultado de uma base: **limpa**, **contaminada** ou **não verificável** (inacessível, ou
divergência entre o catálogo e a configuração).

## 4. Dados fictícios de homologação (`seed-staging`)

Configuração (seção `StagingSeed`, variável de ambiente `StagingSeed__<Chave>`; nunca no
repositório): `AdminPassword`, `OperatorPasswordTenantA`, `OperatorPasswordTenantB`. Cada uma
passa pela regra de senha do `Password` (mínimo 8, maiúscula, minúscula, número, símbolo) antes
de qualquer escrita.

| Tenant | Id (estável) | CNPJ sintético | Nome | Base (referência no catálogo) | Tipo |
|---|---|---|---|---|---|
| A | `aaaaaaaa-0000-4000-8000-000000000001` | `99999999000101` | Tenant A (homologação) | `shared_01` | compartilhada |
| B | `aaaaaaaa-0000-4000-8000-000000000002` | `99999999000102` | Tenant B (homologação) | `shared_01` | compartilhada |
| D | `aaaaaaaa-0000-4000-8000-000000000003` | `99999999000103` | Tenant D (homologação) | `dedicated_01` | dedicada |

(Os CNPJs são inválidos de propósito e o catálogo não os valida; os ids são distintos dos de
desenvolvimento para nunca se confundirem.)

| Operador | Tenants | Role | Senha |
|---|---|---|---|
| `admin@homologacao.test` | A, B, D | `Admin` | `StagingSeed:AdminPassword` |
| `operador@homologacao.test` | A | `Operator` | `StagingSeed:OperatorPasswordTenantA` |
| `operador@homologacao.test` | B | `Operator` | `StagingSeed:OperatorPasswordTenantB` (diferente da de A) |

Regras: domínio `.test` (reservado, RFC 2606); **nenhum** operador usa `admin@admin.com.br`;
idempotente (consulta o e-mail no tenant antes de criar; não troca senha); escrita pelo
`AppDbContext` com `TenantScope`, sob RLS, como `acs_app`. O login desses operadores segue a
decisão da spec sobre instâncias da Api (até a spec 09 cada Api só entra no seu tenant padrão).

## 5. Códigos de saída do Migrator

| Código | Significado | Origem |
|---|---|---|
| `0` | Tudo certo | existente |
| `1` | Pelo menos uma base terminou em `Failed` | existente |
| `2` | Configuração ou argumentos inválidos | existente |
| `3` | `seed-dev` recusado (ambiente ≠ Development) | existente |
| `4` | Tempo de espera pela trava global esgotado | existente |
| **`5`** | `seed-staging` recusado (ambiente ≠ Staging) | **novo** |
| **`6`** | `verify-no-seed-admin` recusado (ambiente = Development) | **novo** |
| **`7`** | **Administrador fixo encontrado** em pelo menos uma base (reprovou) | **novo** |
| **`8`** | **Não foi possível verificar** (base inacessível, ou catálogo e configuração divergem) | **novo** |

Se houver `7` e `8` na mesma execução, sai `7` e a saída lista as duas coisas.

## 6. Respostas de saúde

| Rota | Estado | Código | Corpo |
|---|---|---|---|
| `/health/live` | processo atendendo | `200` | `Healthy` |
| `/health/ready` | catálogo acessível e com esquema | `200` | `Healthy` |
| `/health/ready` | catálogo fora, sem esquema, sem credencial válida ou acima de 2 s | `503` | `Unhealthy` |

Estado interno do verificador da pronta (por instância, em memória): `ultimoResultado`
(booleano) e `validoAte` (instante); **nenhum dado de tenant**. TTL de 5 s para sucesso e falha.

## 7. Regras do ruleset e da conferência

Detalhe em [contracts/repository-protections.md](contracts/repository-protections.md). Fonte única
do nome do check, do `integration_id` e das regras: `scripts/github/protections/main-ruleset.json`,
lida pelo `apply-protections.sh`, pelo `check-rules.sh` e pelo teste de configuração.

| Dado fixo | Valor | Conferido por |
|---|---|---|
| nome do check exigido | `ci-gate` | teste (= `name:` do job em `ci-gate.yml`); `check-rules.sh` |
| `integration_id` do check | `15368` (app GitHub Actions) | `GET /apps/github-actions`; check runs reais do repositório; `apply-protections.sh` (antes de aplicar); `check-rules.sh`; teste de configuração |

## 8. Localização do `bash` pelos testes de script (`BashLocator`)

| Sistema | Variáveis | Resultado |
|---|---|---|
| Windows | `ACS_BASH` definida e o arquivo existe | usa `ACS_BASH` |
| Windows | `ACS_BASH` definida e o arquivo **não** existe | **falha** (configuração explícita errada) |
| Windows | `ACS_BASH` ausente e `C:\Program Files\Git\bin\bash.exe` existe | usa o Git Bash |
| Windows | `ACS_BASH` ausente e o Git Bash não existe | **falha**: "bash não encontrado" |
| Windows | caminho resolvido contém `\WindowsApps\` ou é o `bash.exe` de `System32` (atalho/lançador do WSL), venha de onde vier | **recusado** com mensagem sobre o WSL |
| Linux e macOS | — | `bash` do PATH; se não achar, **falha** |
| qualquer | `jq` não encontrado **dentro** do `bash` escolhido | **falha** com mensagem clara |

Nunca há `Skip`: falta de `bash` ou `jq` é falha de teste.
