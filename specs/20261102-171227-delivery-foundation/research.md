# Pesquisa e Decisões: Base de entrega sem nuvem

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-11-02

Não restou nenhum ponto em aberto: as decisões do `/speckit-clarify` (rulesets, conferência
automática sem credencial, varredura de segredos, verificação pós-merge, definição de "só
documentos", 2 segundos no `/health/ready`) já estão na seção "Clarificações" da spec. Abaixo, o
que foi **verificado** (no código, na API do GitHub e na documentação) e a escolha de cada ponto
técnico, com as alternativas descartadas. Três decisões são as que mais pesam e estão marcadas:
**R-01** (o gate com reusable workflows), **R-12** (o formato do endpoint de regras) e **R-14**
(a proteção contra abuso no `/health/ready`).

## Estado verificado (2026-11-02)

**Workflows**

- Só existem `backend-ci.yml` (job `build-and-test`) e `frontend-ci.yml` (job
  `frontend-build-and-test`). Os dois rodam em `push` e `pull_request` para a `main`, ambos com
  filtro de `paths` (backend: `src/Backend/**`, `docker/postgres/sql/**`, `docker/postgres/init/**`
  e o próprio arquivo; frontend: `src/Frontend/**` e o próprio arquivo). Nenhum tem `schedule`,
  nem `permissions:` explícito, nem `timeout-minutes`.
- **Duração real das execuções com sucesso** (`gh run list`, últimas 11 e 10): backend 43 a 171 s
  (típico 80 a 100 s); frontend 31 a 139 s (típico 35 a 65 s). Base do tempo limite em R-05.
- `PackageAuditConfigTests` (`Tests/Architecture/`) **lê os dois arquivos** e exige, linha a linha
  (já aparada), `run: dotnet restore Backend.slnx` antes do build, e `run: npm audit` depois de
  `run: npm ci`. Isso pesa na decisão R-01: os passos desses arquivos **não podem mudar**.

**Api**

- `Program.cs`: `AddControllersConfiguration`, `AddJwtAuthentication`, `AddRateLimiting` (só a
  política nomeada `login`; **não há limitador global**), `AddForwardedHeaders`; pipeline
  `UseForwardedHeaders` → `UseExceptionHandler` → `UseHttpsRedirection` (só se
  `HttpsRedirection:Enabled`) → `UseCors` → `UseRateLimiter` → **middleware que responde 404 a rota
  sem endpoint** → `UseAuthentication` → `UseAuthorization` → `MapControllers`.
- `AddAuthorization` define `FallbackPolicy` com `RequireAuthenticatedUser`: todo endpoint sem
  `[AllowAnonymous]` exige login. Hoje só o login é anônimo (e o Swagger, só em Development).
- O catálogo é o `CatalogDbContext` (`DbSet<CatalogDatabase> Databases`, tabela `tenant_databases`;
  `DbSet<CatalogTenant> Tenants`), registrado por `AddTenancy` com `ConnectionStrings:Catalog` no
  papel `acs_app` (só `SELECT`). `TimeProvider.System` é registrado com `TryAddSingleton`, e o
  `ApiFactory` dos testes já sabe trocá-lo por relógio falso (`WithFakeTime`) e trocar qualquer
  serviço (`WithSingleton<T>`), configuração (`With`) e a base do catálogo.
- `EndpointCoverageTests` só enumera `RouteEndpoint` **de controller**
  (`ControllerActionDescriptor`); um endpoint de `MapHealthChecks` não entra no catálogo e não
  quebra o teste. `DefaultAuthorizationTests` prova a política de fallback.
- `NoStaticTenantStateTests` varre **Application e Infrastructure** atrás de estado estático com
  dado de tenant. Classe nova na Api, com campos de instância, não é alcançada.
- O projeto da Api é `Microsoft.NET.Sdk.Web`: `AddHealthChecks`/`MapHealthChecks` já vêm no
  framework. **Nenhum pacote novo.**

**Migrator**

- `MigratorApplication.RunAsync` aceita **um** argumento, `migrate` ou `seed-dev`; senão imprime
  "Uso: ..." e devolve `2`. O `seed-dev` é recusado **antes de ler a configuração** (código `3`).
  Códigos hoje: `0`, `1` (base falhou), `2` (configuração), `3` (seed-dev recusado), `4` (trava).
- `SeedDevCommand` registra tenants no catálogo (`InitialTenantRegistrar`), atualiza as bases
  recém-cadastradas (`MigrationOrchestrator`) e grava os operadores pelo `AppDbContext` com
  `TenantScope`, sob RLS, como `acs_app`. Cria 3 tenants (`11111111-…`, `22222222-…` em `shared_01`;
  `33333333-…` em `dedicated_01`) com `admin@admin.com.br` (senha vinda do `.env`) e
  `operador@exemplo.com`.
- **A `SeedAdmin` cria `admin@admin.com.br`** com identificador fixo
  `cc59e349-efbd-4800-9726-f38a325e4d3d` e hash de senha fixo, por `InsertData` na tabela
  `operators` (colunas `id`, `email`, `password`, `role`). É uma classe `public partial` no
  assembly da Api; o Migrator **referencia a Api**.
- **A `SeedAdminPolicy` roda em todo `EfDatabaseMigrationRunner.RunAsync`, em qualquer ambiente**
  (inclusive Development): se a base não tem nenhuma migration aplicada, pré-marca a `SeedAdmin`
  no histórico. O roteiro 05a diz "como o SeedAdminPolicy faz fora de Development"; a leitura
  correta é "em qualquer ambiente". Efeito prático: toda base criada pelo Migrator nasce sem o
  admin fixo; só nasce com ele quem rodar `Database.MigrateAsync()` sem a política (por exemplo,
  `dotnet ef database update`, que o `CLAUDE.md` proíbe).
- `acs_migrator`: dono dos bancos e das tabelas, **`BYPASSRLS`** (`roles.sql`); `acs_app`:
  `NOBYPASSRLS`, sem DDL. Logo, **só o migrator enxerga todos os tenants** de uma base
  compartilhada (o `acs_app` sob `FORCE ROW LEVEL SECURITY` não vê nada sem tenant).

**Testes**

- `PostgresContainerFixture` (Testcontainers, `[Collection(PostgresCollection.Name)]`,
  `[Trait("Category","Integration")]`) oferece `CreateIsolatedCatalogAsync()`,
  `CreateEmptyDatabaseAsync(name)` (banco vazio, dono `acs_migrator`),
  `CreateMigratedTenantDatabaseAsync(name)` (migrado **pelo executor real, com a política**, logo
  sem o admin fixo) e `DatabaseConnectionStrings` (`App`, `Migrator`). `MigratorTestSupport` e
  `SeedDevCommandTests` mostram o padrão de teste de verbo (`MigratorApplication.RunAsync` com
  `AddInMemoryCollection` e `StringWriter`).

**GitHub (verificado agora, somente leitura)**

- `main` com `protected: false` e **0 rulesets**.
- `GET /repos/{dono}/{repo}/rules/branches/main` e `GET .../rulesets` respondem **HTTP 200 sem
  credencial** para este repositório público (hoje `[]`). **O plano não depende disso**: a
  conferência automática chama o endpoint **autenticada** com o `GITHUB_TOKEN` (R-12). Motivo: o
  limite anônimo é de **60 requisições por hora, "associado ao endereço IP de origem, não ao
  usuário"**, e os runners do Actions compartilham IPs; o limite do `GITHUB_TOKEN` é de **1.000 por
  hora, por repositório** (documentação de limites da API REST).
- O app **GitHub Actions** tem `id = 15368` (`GET /apps/github-actions`), e é o `app.id` dos check
  runs reais deste repositório (`build-and-test` e `frontend-build-and-test`, conferidos em um
  commit recente). É o valor do `integration_id` do ruleset (R-10). Observação: o commit atual da
  `main` não tem **nenhum** check run (o último merge foi só de documentos e os filtros de `paths`
  não rodaram nada): é exatamente a armadilha 1 do R-02, e por isso o `apply-protections.sh` só
  age depois de existir um `ci-gate` concluído na `main`.
- **Máquina do dono (Windows)**: o `bash` do PATH resolve **primeiro** para
  `C:\Users\...\AppData\Local\Microsoft\WindowsApps\bash.exe`, o atalho do **WSL** (há um Ubuntu no
  WSL), e não para o Git Bash. Ver R-19.
- `jq`, `gh` e `curl` existem na máquina do dono (no Git Bash); as imagens `ubuntu-latest` do
  Actions trazem `bash`, `jq`, `gh` e `curl`.

## R-01 — O gate: reusable workflows, e não filtros de paths nem jobs duplicados (decisão principal)

**Decisão**: **chamar o backend e o frontend como reusable workflows** a partir de um workflow
novo, `ci-gate.yml`. Os arquivos `backend-ci.yml` e `frontend-ci.yml` **ficam onde estão, com os
mesmos passos e os mesmos nomes de job**; só mudam os gatilhos: saem `push`/`pull_request` com
`paths` e entra `workflow_call` (mais `workflow_dispatch`, para rodar um deles à mão). O
`ci-gate.yml` tem os jobs `changes`, `backend`, `frontend`, `k6-short` e `ci-gate`. O **único**
check exigido pelo ruleset é o job final **`ci-gate`**.

**Por quê**:

1. **Nenhum filtro de `paths` no caminho obrigatório.** O `ci-gate.yml` dispara em todo PR para a
   `main`, então o check exigido sempre aparece (R-02, armadilha 1).
2. **Os passos reais continuam sendo os de hoje.** Como os dois arquivos mantêm os passos, o
   `PackageAuditConfigTests` segue verde **sem nenhuma edição** (Integridade das entregas: nada de
   mexer em teste para a entrega passar). A auditoria de pacotes continua dentro do restore e do
   `npm audit` de cada um.
3. **Um ponto de entrada, uma regra.** Quem lê o CI vê o gate, depois os dois jobs que ele
   chama. Não há dois workflows decidindo por conta própria quando rodar.
4. **A dispensa é uma decisão explícita e auditável**: sai de `changes`, não do estado "pulada"
   (R-02, armadilha 2).
5. **O pós-merge é grátis** (R-06): o mesmo `ci-gate.yml` dispara em `push` para a `main`.

**Alternativas descartadas**:

- **Jobs duplicados dentro do gate** (copiar os passos de backend e frontend para o `ci-gate.yml`
  e apagar os outros dois arquivos): duplicaria 20 linhas de passos, quebraria o
  `PackageAuditConfigTests` (que lê esses arquivos) e obrigaria a alterá-lo, o que a regra de
  Integridade só permite quando a spec muda o comportamento esperado, e aqui não muda.
- **Manter os dois workflows separados, sem `paths`, com `if` por job**: cada um passaria a
  mostrar um check próprio, e o job pulado por `if` conta como verde (armadilha 2). Seriam três
  checks obrigatórios (ou dois que "passam" sem rodar).
- **Um agregador que consulta a API de checks** (`checks: read`, polling): frágil, com espera e
  corrida, e precisa de permissão a mais.
- **Rodar tudo sempre** (sem detecção de áreas): simples, mas contraria a spec (SC-002: PR só de
  documentos em até 5 minutos, sem backend nem frontend) e gasta de 2 a 3 minutos de CI à toa.
- **Ação de terceiros para detectar áreas** (`dorny/paths-filter` e similares): evita escrever 40
  linhas de shell, mas entra código alheio num repositório público que vai ganhar segredos na
  05b. A detecção é `git diff` + um script versionado e testado (R-03).

**Nome do check exigido**: o ruleset identifica o check pelo **nome do job** (`context`). O job se
chama `ci-gate` (definido no `name:` do job, para não depender do identificador). **Renomeá-lo
derruba a exigência**; defesas em três camadas: (1) teste de PR que confere que o nome em
`ci-gate.yml` é igual ao do `main-ruleset.json`; (2) a conferência automática semanal; (3) o
guia. Os jobs chamados por reusable workflow aparecem como `backend / build-and-test`; **esses
não são exigidos**.

**Evento do check**: pela documentação, só `push`, `pull_request` (e alguns outros) geram check
que satisfaz um check exigido; **`workflow_dispatch` não satisfaz**. Por isso o `ci-gate.yml` não
tem `workflow_dispatch`; os arquivos chamados têm, só para depuração.

## R-02 — As duas armadilhas e como o gate as evita

Em linguagem simples, o GitHub tem duas pegadinhas que deixariam o gate mentir:

**Armadilha 1: o check exigido que nunca roda.** Quando um workflow tem filtro de `paths` e a
mudança não toca esses arquivos, **o workflow nem começa**; o check exigido fica "Pending" para
sempre e o PR fica travado ("Waiting for status to be reported"). Foi o que a spec 02 descobriu
(research, item 10). **Defesa**: o `ci-gate.yml` **não tem filtro de paths**; dispara em todo PR
para a `main` e em todo `push` para a `main`. A decisão de "o que rodar" fica dentro dele.

**Armadilha 2: o check "pulado" que o GitHub conta como verde.** Pela documentação: um job pulado
por condição (`if`) **reporta "Success"**; e um job que depende (`needs`) de outro que falhou é
**pulado** e "pode não bloquear". Dessas duas regras sai o pior caso: um job de backend que falha
faz o gate parecer verde. **Defesa**: o job final `ci-gate`

- usa `if: always()`: **nunca é pulado**, mesmo quando um dos anteriores falha, é cancelado ou
  expira;
- lista em `needs` **todos** os jobs que avalia (o `needs` só traz dependências diretas);
- **não confia no estado "skipped"** para dispensar: a dispensa vem das saídas do `changes`
  (`backend`, `frontend`, `docs_only`). O resultado esperado de cada job é calculado a partir
  delas (R-04);
- reprova com qualquer resultado inesperado: `failure`, `cancelled`, `skipped` sem dispensa,
  chave ausente em `needs`, saída de área vazia ou inválida.

| Situação | `needs.<job>.result` | Veredito do gate |
|---|---|---|
| backend aplicável, passou | `success` | aprovada |
| backend aplicável, falhou | `failure` | **reprova** |
| backend aplicável, cancelado (nova mudança no PR, cancelamento à mão) | `cancelled` | **reprova** |
| backend aplicável, estourou o `timeout-minutes` | `cancelled` ou `failure` (a doc não diz qual) | **reprova** (qualquer coisa ≠ `success`) |
| backend aplicável, pulado sem motivo (job anterior falhou) | `skipped` | **reprova** |
| backend dispensado (área sem mudança) | `skipped` | dispensada, **mostrada como tal** |
| `changes` falhou | `failure` | **reprova** (nada mais é confiável) |

**Quando o run inteiro é cancelado** (um novo envio ao PR com `cancel-in-progress`), o check do
commit antigo termina "cancelled", que **não satisfaz** o check exigido, e o novo commit tem o
seu próprio run. **Ausência de resultado** (o workflow não chegou a iniciar): o check exigido
fica pendente e **bloqueia o aceite**; isso é o comportamento seguro.

**A confirmar em PRs descartáveis** (validação do revisor **depois do merge**, ver quickstart V-06): o mapeamento
exato de `needs.<job>.result` quando um job que chama reusable workflow é cancelado ou estoura o
tempo. A documentação lista os quatro valores possíveis mas não diz qual aparece em cada caso; o
gate trata tudo que não seja `success` como reprovação, então a regra vale seja qual for.

## R-03 — Detecção de áreas (`scripts/ci/detect-areas.sh`)

**Decisão**: um script de shell **sem ação de terceiros**, que lê a lista de arquivos alterados
(um caminho por linha, na entrada padrão) e imprime `backend=`, `frontend=` e `docs_only=`
(`true`/`false`). O job `changes` monta a lista com `git diff --name-only` e acrescenta as saídas
a `$GITHUB_OUTPUT`.

**Regras** (spec FR-007/FR-008 e clarify):

1. **Documentos**: `specs/**`, qualquer `*.md` (em qualquer pasta), `.specify/**`, `.claude/**`.
2. **Backend**: `src/Backend/**` (que não seja `*.md`). **Frontend**: `src/Frontend/**` (que não
   seja `*.md`).
3. **Qualquer outro arquivo** (`.github/**`, `docker/**`, `docker-compose.yml`, `scripts/**`,
   `.gitignore`, `.gitattributes` e outros arquivos de raiz que não sejam `*.md`): conta como
   **fora das áreas** e exige **todas** as verificações (fechado por padrão).
4. Backend aplicável = há arquivo de backend **ou** fora das áreas. Frontend aplicável = há
   arquivo de frontend **ou** fora das áreas. `docs_only` = **todos** os arquivos são documentos
   (e há pelo menos um).
5. **Lista vazia** (por exemplo, `before` zerado, merge sem diferença): **todas** aplicáveis,
   `docs_only=false`. Nunca "nenhuma".

`CLAUDE.md` e `README.md` casam com a regra `*.md` e contam como documentos (decisão do clarify).

**Como montar a lista**:

- `pull_request`: o checkout padrão é o **commit de merge** (`refs/pull/N/merge`);
  `git diff --name-only HEAD^1 HEAD` (com `fetch-depth: 2`) dá exatamente o que o PR leva para a
  `main`.
- `push` na `main`: `git diff --name-only <github.event.before> <github.sha>` (com
  `fetch-depth: 0`, para cobrir um merge que traga vários commits). `before` zerado vira lista
  vazia (tudo aplicável).
- Se o `git diff` falhar, o passo falha, o `changes` falha e o gate **reprova** (R-02): nunca
  "passa sem olhar".

**Testes**: `CiScriptsTests` roda o script com uma tabela de listas de arquivos (só documentos,
só frontend, só backend, os dois, `.github/x.yml`, `docker/x`, `docker-compose.yml`, arquivo de
raiz sem extensão, `.md` dentro de `src/Backend`, lista vazia, mistura de documento com arquivo
fora das áreas) e confere as três saídas.

**Alternativas descartadas**: `dorny/paths-filter` (terceiros, ver R-01); `gh api` de arquivos do
PR (precisa de `pull-requests: read` e de paginação, e não existe no `push`); `paths:` nos
workflows (a armadilha 1).

## R-04 — Avaliação final e resumo (`scripts/ci/evaluate-gate.sh`)

**Decisão**: o job `ci-gate` roda um script versionado e testado que recebe `toJSON(needs)` e as
três saídas de área (variáveis de ambiente `NEEDS_JSON`, `AREA_BACKEND`, `AREA_FRONTEND`,
`AREA_DOCS_ONLY`), decide e **escreve a tabela em `$GITHUB_STEP_SUMMARY`** (ou na saída padrão,
fora do Actions). Sai `0` só se todo veredito é "aprovada", "dispensada" ou "não ativada".

**Regras de cada linha**:

| Verificação | Aplicável quando | Aceito |
|---|---|---|
| `changes` | sempre | `success` |
| `backend` | `AREA_BACKEND=true` | só `success`; se `false`, só `skipped` (dispensada) |
| `frontend` | `AREA_FRONTEND=true` | só `success`; se `false`, só `skipped` (dispensada) |
| `k6-short` (nome exibido: **k6 curto (não ativado)**) | sempre | `success`, exibido como **"não ativado"** (R-07) |

Reprova também quando: falta alguma chave em `NEEDS_JSON`; uma área vem vazia ou diferente de
`true`/`false`; `backend=false`, `frontend=false` e `docs_only=false` ao mesmo tempo (estado
incoerente: "nenhuma área" só vale para "só documentos").

**Resumo** (o que o dono vê sem abrir logs, FR-010): uma tabela `Verificação | Estado | Motivo`
com os estados **aprovada**, **reprovada**, **dispensada (área sem mudança)** e **não ativada**, e
uma linha final "Resultado do gate".

**Testes**: `CiScriptsTests` com uma tabela de combinações `needs` × áreas (todos os casos da
tabela do R-02, mais chave ausente e área inválida) conferindo código de saída e texto da linha.

## R-05 — Tempo limite e concorrência

**Decisão**: `timeout-minutes` explícito em **todo** job (hoje nenhum tem; o padrão do GitHub é
360 minutos). Valores a partir da duração real (estado verificado): `backend` (job chamado)
**15 min** (≈ 5× o pior caso de 171 s); `frontend` **10 min** (≈ 4× os 139 s); `changes` **3**;
`k6-short` **2**; `ci-gate` **3**; `package-audit` e `protections-check` **10** e **5**.

`concurrency`: no gate de PR, grupo por número do PR com `cancel-in-progress: true` (um novo
envio cancela a execução velha, que de qualquer forma já não vale para o último commit); no
`push` para a `main`, grupo por `ref` com `cancel-in-progress: false` (cada merge é verificado até
o fim). Fórmula única: `group: ci-gate-${{ github.event.pull_request.number || github.ref }}` e
`cancel-in-progress: ${{ github.event_name == 'pull_request' }}`.

**Revisão**: os valores partem de uma amostra de 10 a 11 execuções; **revisar depois de um mês**
(a spec 06 acrescenta o k6, e o backend pode crescer).

## R-06 — Pós-merge e quem recebe o aviso

**Decisão**: o `ci-gate.yml` tem `on: push: branches: [main]`; roda a mesma detecção (`before..sha`)
e as mesmas verificações. É a rede de segurança que o dono escolheu no clarify (a branch do PR não
precisa estar atualizada com a `main`, então dois PRs verdes isolados podem quebrar juntos).

**Quem recebe o e-mail** (documentação do GitHub, lida hoje):

- **Disparo por `push` (merge)**: a notificação vai para quem **disparou** a execução, ou seja,
  quem aparece como "pushed by". Num merge de PR feito pelo dono, é o dono. A página não diz
  explicitamente quem conta como "pusher" num merge pelo botão do GitHub; **confirmar no primeiro
  merge real** (tarefa do dono, quickstart V-07).
- **Agendados** (R-09): vai para quem **criou o workflow**; se outra pessoa alterar a linha
  `cron`, passa para ela; se o workflow for desativado e reativado, vai para quem reativou. As
  duas páginas da documentação divergem no padrão inicial (criador × último a alterar o `cron`).
  Como o dono é quem aceita os PRs que criam e alteram esses arquivos, nos dois casos ele recebe.
- Depende de a conta do dono ter notificações de Actions ligadas por e-mail (opção de receber só
  de falhas existe). **O guia manda conferir essa configuração.**

## R-07 — Passo k6 curto reservado ("não ativado")

**Decisão**: job de **id** `k6-short` e **`name: "k6 curto (não ativado)"`** no gate, que **roda**
(não é pulado), escreve no `$GITHUB_STEP_SUMMARY` "Passo k6 curto: **NÃO ATIVADO** (cenários de
carga: spec 06)" e termina com sucesso. O `evaluate-gate.sh` o exibe como **"não ativado"**, nunca
como "aprovada", com a mesma linha **"k6 curto (não ativado)"** no resumo.

**O nome é de propósito**: a lista de checks do PR mostra o `name:` do job, então o dono vê
"k6 curto (não ativado)" ao lado do ✓, e não um ✓ com nome neutro que pareceria uma aprovação. O
**id** continua `k6-short` porque as chaves de `needs` são ids, não nomes (renomear o `name:` não
mexe no `evaluate-gate.sh`). Para a spec 06 ativá-lo, troca-se o passo pelo k6 real, o `name:` para
"k6 curto" e a linha da tabela de avaliação passa a exigir `success` de verdade (a regra e a saída
`k6_active` já existem). **Teste**: `CiWorkflowsConfigTests` confere que o `name:` do job
`k6-short` é exatamente "k6 curto (não ativado)" **enquanto ele é o reservado** (sem passo de k6
real); a spec 06 troca essa asserção junto com o nome.

**Por quê**: um job pulado conta como verde sem aviso (armadilha 2) e some do resumo; um job que
roda e diz em voz alta que não está ativo não engana ninguém. Custo: ~10 s de runner.

**Alternativa descartada**: `if: false` no job (fica "pulado", exatamente o estado que o gate
aprendeu a desconfiar).

## R-08 — Fork, permissões e ações de terceiros

**Decisão**: todo workflow declara `permissions: contents: read` no topo (a documentação diz que,
ao declarar qualquer permissão, as não declaradas viram `none`). Nenhum usa `secrets.*`. O gatilho
é `pull_request`, **nunca** `pull_request_target`, e nenhum passo faz checkout de código de PR
com permissão elevada.

**Verificado na documentação**: no `pull_request` de fork o `GITHUB_TOKEN` é somente leitura e os
segredos não são passados ao runner (exceto o próprio token). Por padrão, **todo contribuidor de
primeira viagem precisa de aprovação** para rodar workflows, em Settings → Actions → General.
`pull_request_target` roda no contexto da branch padrão e **ignora** essa aprovação, por isso é
proibido. Dependabot é tratado como fork.

**Ações usadas**: só as oficiais `actions/*` que os workflows atuais já usam (`checkout`,
`setup-dotnet`, `setup-node`) e chamadas locais (`./.github/workflows/...`). Nenhuma ação de
terceiros. **Teste**: `CiWorkflowsConfigTests` falha se aparecer `pull_request_target`, `secrets.`,
um `uses:` fora de `actions/` e `./`, um job sem `timeout-minutes` ou um workflow sem
`permissions:`.

## R-09 — Auditoria agendada (`package-audit.yml`)

**Decisão**: workflow novo com `schedule` semanal (`cron: '23 9 * * 1'`, segunda às 09:23 UTC; fora
da hora cheia, que a documentação diz ser a de maior atraso) e `workflow_dispatch`. Dois jobs
independentes: **backend** (`dotnet restore Backend.slnx` em `src/Backend`) e **frontend**
(`npm ci` e `npm audit` em `src/Frontend`). Os critérios são **os mesmos da 04b por construção**:
o NuGet Audit está em `Directory.Build.props` (todo restore o aplica, qualquer severidade, e
`CI=true` torna `NU1900` erro) e `npm audit` é sem `--audit-level`. **Não repete build nem testes**:
uma falha de teste não pode virar alarme de vulnerabilidade.

**Falha de rede** na consulta: o restore falha com `NU1900` (erro com `CI=true`) e o `npm audit`
sai com código ≠ 0 sem acesso ao registro; os dois reprovam. A exceção justificada do `SSH.NET`
continua nos `NuGetAuditSuppress` do projeto de testes, que o restore da solução aplica.

**Aviso**: e-mail do GitHub para falha de workflow agendado (R-06). **Desativação por inatividade**:
a documentação diz que, num repositório público, agendamentos são desativados "quando não houve
atividade no repositório em 60 dias"; reativa-se quando alguém com permissão de escrita faz um
commit que mude a linha `cron`, ou manualmente em Actions → o workflow → Enable. O guia explica
como notar (a aba Actions mostra o workflow como desativado) e reativar.

## R-10 — Rulesets e o script de proteções

**Decisão**: **ruleset** (não proteção de branch clássica), criado e mantido por um script
versionado que o dono roda com o `gh` autenticado:
`scripts/github/apply-protections.sh`, com o corpo em
`scripts/github/protections/main-ruleset.json`.

**Corpo** (campos conferidos no esquema oficial do GitHub; ver
[contracts/repository-protections.md](contracts/repository-protections.md)):

| Campo | Valor | Motivo |
|---|---|---|
| `name` | `protect-main` | identifica o ruleset para o script atualizar sem duplicar |
| `target` / `enforcement` | `branch` / `active` | `evaluate` só existe no Enterprise |
| `bypass_actors` | `[]` | ninguém fura, nem admin (o guia manda conferir `current_user_can_bypass`) |
| `conditions.ref_name.include` | `["refs/heads/main"]` | a regra é da `main`, não "a branch padrão" |
| regra `pull_request` | contagem de aprovações `0`; os outros quatro parâmetros obrigatórios `false` | PR obrigatório, **sem revisão humana** (dono único) |
| regra `required_status_checks` | `context: "ci-gate"` com **`integration_id: 15368`** (app GitHub Actions); `strict_required_status_checks_policy: false` | exige o gate **vindo do Actions**; **não** exige a branch atualizada (clarify) |
| regra `non_fast_forward` | — | sem envio forçado |
| regra `deletion` | — | sem exclusão |

`required_approving_review_count` aceita 0 (o esquema diz mínimo 0), e os quatro booleanos do
`pull_request` são **obrigatórios**: vão explícitos como `false`.

**O `integration_id` é fixado em `15368` (o app GitHub Actions).** O esquema oficial o descreve como
"the optional integration ID that this status check must originate from". Com ele, **só um check do
Actions** satisfaz o `ci-gate`: um status externo (a API de commit status, ou outro app) com o mesmo
nome **não vale**. O valor foi conferido de três formas: `GET /apps/github-actions` devolve
`id 15368`; os check runs reais deste repositório vêm de `app.id 15368` (`github-actions`); e o
esquema do ruleset aceita o campo inteiro. O risco de um valor errado (**travar todos os PRs**) é
tratado **no script**: antes de aplicar, o `apply-protections.sh` confere que o check run real do
`ci-gate` na `main` tem `app.id` **igual** ao `integration_id` do `main-ruleset.json`; se for
diferente, **para** e explica. O `RepositoryProtectionsConfigTests` cobre o campo (R-12).

**O script**: `apply-protections.sh` (1) exige `gh` autenticado e `jq`; (2) **verifica a ordem de
ativação**: só prossegue se existir um check `ci-gate` concluído na `main` **e se o `app.id` desse
check for o `integration_id` do corpo** (a documentação diz
que o check exigido "precisa ter concluído com sucesso no repositório nos últimos 7 dias"); (3)
`--dry-run` **lê** o estado e imprime o corpo e as chamadas, mas **nunca escreve** (nenhum `POST`,
`PUT` ou `PATCH`); os scripts chamam `"$GH_BIN"` (padrão `gh`), e **só os testes** trocam `GH_BIN`
por um `gh` falso; o implementador **nunca** roda o script com o `gh` do dono (o `--dry-run` real é
do dono, quickstart V-07); (4) cria o ruleset se não existir ou atualiza (`PUT`) o de mesmo nome
(**idempotente**); (5) liga a varredura de segredos (R-11); (6) roda a conferência (R-12) e
imprime a parte manual. Subcomandos `--disable` e `--enable` mudam só `enforcement` para a
emergência (o guia explica; a plataforma registra quem mudou).

**Alternativas descartadas**: proteção de branch clássica (o roteiro e a decisão do dono mandam
rulesets; no clássico "incluir administradores" é opcional e a conferência por endpoint é mais
pobre); configuração só manual (o repositório pode voltar a privado e a configuração precisa ser
refeita); Terraform/provider do GitHub (ferramenta nova para uma configuração de um ruleset).

## R-11 — Varredura de segredos e proteção de envio

**Decisão**: o script liga, por `PATCH /repos/{dono}/{repo}` com
`security_and_analysis.secret_scanning.status = enabled` e
`security_and_analysis.secret_scanning_push_protection.status = enabled`. Verificado na
documentação: exige permissão de administrador (ou ser o dono); **ler** o estado também exige
administrador (`GET /repos/{dono}/{repo}` só devolve o bloco `security_and_analysis` a quem é
admin), por isso essa conferência é **à mão**, com o `gh` do dono (R-12).

**Não verificado**: o estado padrão em repositório público e se o `PATCH` é necessário. A página
de proteção de envio fala em Settings → Advanced Security; a de REST traz o exemplo oficial só
para `advanced_security`. A forma do corpo acima é **inferida** da estrutura documentada. O
script, portanto, **lê antes** (`gh api repos/... --jq .security_and_analysis`), só envia o
`PATCH` se algo estiver desligado e **relê para confirmar**; se o GitHub recusar o campo, o
script mostra o passo manual da interface e sai com falha (nunca finge sucesso).

## R-12 — Formato do endpoint de regras e a conferência automática (decisão principal)

**O que foi verificado** (três fontes):

1. **Página da API REST** ("Get rules for a branch"): é um **array**; "Returns all active rules
   that apply to the specified branch"; regras de ruleset em `evaluate` ou `disabled` **não são
   devolvidas**; "the branch does not need to exist". **Não consta** nessa página o formato de
   cada item, nem permissão de token, nem se funciona sem credencial.
2. **Descrição OpenAPI oficial** (`github/rest-api-description`, baixada e lida hoje): a resposta
   200 é `array` de `repository-rule-detailed`. Cada item é a regra (`type` e, quando existe,
   `parameters`) **mais** `ruleset_source_type` (`Repository` ou `Organization`), `ruleset_source`
   (nome do dono do ruleset) e `ruleset_id` (inteiro). Para as quatro regras que importam:
   `pull_request` (parâmetros obrigatórios: `dismiss_stale_reviews_on_push`,
   `require_code_owner_review`, `require_last_push_approval`, `required_approving_review_count`,
   `required_review_thread_resolution`), `required_status_checks` (`parameters.required_status_checks`,
   lista de objetos com `context` obrigatório e `integration_id` opcional ("the optional
   integration ID that this status check must originate from"), e
   `strict_required_status_checks_policy`), `non_fast_forward` (sem parâmetros) e `deletion` (sem
   parâmetros). Exemplo **montado a partir do esquema** (não capturado, porque ainda não existe
   ruleset): [contracts/repository-protections.md](contracts/repository-protections.md).
3. **Chamada real, somente leitura** (hoje): `HTTP 200` sem credencial, corpo `[]`; cabeçalho
   `X-RateLimit-Limit: 60` (limite anônimo por hora). **Serve só de prova de que o endpoint
   existe e é público; o plano não o usa assim** (ver abaixo).

**A conferência chama o endpoint AUTENTICADA, não anônima** (decisão do dono, confirmada):

- **Por que não anônima**: o limite anônimo é de **60 requisições por hora e vale por IP**
  ("associado ao endereço IP de origem, não ao usuário ou ao aplicativo"); os runners hospedados
  do Actions **compartilham IPs** com outros repositórios, então a chamada poderia receber `403` por
  culpa de terceiros e a conferência falharia sem nada ter mudado. O `GITHUB_TOKEN` tem limite de
  **1.000 por hora, por repositório** (documentação de limites da API REST), sem custo e sem ser
  uma credencial guardada: é gerado a cada job e expira com ele.
- **Permissão confirmada na documentação** (dados de `github/docs` que alimentam as páginas de
  permissões, `server-to-server-permissions.json`): `GET /repos/{owner}/{repo}/rules/branches/{branch}`
  aparece **somente** sob **"Repository permissions for 'Metadata'"**, acesso **`read`**,
  `server-to-server: true` (o `GITHUB_TOKEN` é um **token de instalação** do app do Actions, ou
  seja, server-to-server). `GET .../rulesets` e `GET .../rulesets/{id}` também estão em Metadata
  read. **Nenhuma outra permissão** é exigida.
- **O que a documentação NÃO afirma, e fica como prova real**: a tabela de chaves de
  `permissions:` de um workflow **não lista `metadata`** (só `contents`, `actions`, `checks`...), e
  nenhuma página que li diz textualmente que o `GITHUB_TOKEN` "sempre" tem Metadata read (a
  afirmação do dono é a leitura esperada, mas **não está escrita na documentação consultada**). A
  doc diz que as permissões **não declaradas** em `permissions:` viram `none`; `metadata` não é uma
  das chaves dessa lista. Por isso a **prova** é a primeira execução real do `protections-check`
  (`workflow_dispatch`), tarefa do dono (quickstart V-07), e não uma suposição. Se der `403`, a
  conferência **falha** (nunca aprova) e o ajuste é na spec.
- **O workflow**: `GH_TOKEN: ${{ github.token }}` e `permissions: contents: read`. O script
  `check-rules.sh` **não** tem caminho anônimo: sem `GH_TOKEN` nem `gh` autenticado, falha.
- **Se o repositório voltar a ser privado**: rulesets em repositório privado exigem plano pago, e a
  conferência (a) **precisa ser reavaliada** (a permissão necessária continua sendo Metadata read, mas
  isso não foi confirmado para repositório privado). Está no guia e na spec.

**A conferência automática** (`scripts/github/check-rules.sh`, usado pelo workflow
`protections-check.yml` e pelo `apply-protections.sh`): lê o JSON da entrada padrão e confere, nesta
ordem:

1. a entrada é um **array JSON** (resposta de erro, `{}`, vazio de rede, limite excedido: tudo isso
   **reprova**; "não conseguir ler" nunca é aprovar);
2. existe regra `pull_request`;
3. existe regra `required_status_checks` com algum item cujo `context` é **igual ao nome do check
   do gate** **e** cujo `integration_id` é **igual ao do corpo do ruleset** (os dois lidos do mesmo
   `main-ruleset.json` que o script de aplicação usa, nunca duplicados à mão). Item com o nome certo
   e **sem** `integration_id` (ou com outro) **reprova**: qualquer status externo com o mesmo nome
   passaria a valer;
4. existe regra `non_fast_forward`;
5. existe regra `deletion`.

Qualquer item ausente: sai com código 1 e uma linha por falta ("falta: ..."). O workflow
`protections-check.yml` roda por `schedule` semanal e `workflow_dispatch` (nunca por PR), com
`permissions: contents: read`, **sem segredo**. O aviso por falha é o e-mail padrão (R-06).

**O que o endpoint não mostra** (conferência **à mão**, `scripts/github/check-protections-manual.sh`,
rodado pelo dono): `bypass_actors` e a situação da varredura de segredos. Para o primeiro, o script
usa `GET /repos/{dono}/{repo}/rulesets` (autenticado), que devolve `bypass_actors` e
`current_user_can_bypass` (valores `always`, `pull_requests_only`, `never`, `exempt`; o esperado é
`never`); para o segundo, `GET /repos/{dono}/{repo}` (`security_and_analysis`, só admin).

**Alternativas descartadas**: token pessoal guardado como segredo para ler o ruleset completo
(credencial de longa duração num repositório público; o dono escolheu "sem credencial"); só a
conferência à mão (esquecimento: a regra pode ser desligada e ninguém ver por semanas).

## R-13 — Ensaios das proteções (SC-001)

Sem proteção, o ensaio de "envio direto" **alteraria a `main` de verdade**. Por isso (spec,
clarify):

1. **Só depois** de a conferência automática (disparo manual) **e** a manual confirmarem que as
   proteções estão ativas.
2. **Antes de qualquer ensaio**: anotar o SHA atual da `main` (`git rev-parse origin/main`) e
   confirmar que ele também aparece no GitHub. Esse SHA recria a branch se um ensaio de exclusão a
   apagasse (`gh api repos/{dono}/{repo}/git/refs -f ref=refs/heads/main -f sha=<SHA>`).
3. **Envio direto**: um commit trivial e desfazível (por exemplo, uma linha num arquivo de teste
   que depois se reverte por PR), enviado com `git push origin HEAD:main`. Esperado: recusa.
4. **Envio forçado**: `git push --force-with-lease origin <SHA-anterior>:main` partindo de um
   commit que **recoloca a `main` no mesmo SHA anotado** se for aceito. Esperado: recusa.
5. **Exclusão**: `git push origin :main`. Esperado: recusa. (Se a exclusão passasse, recria-se pelo
   SHA anotado.)
6. Registrar o resultado no guia. **São tarefas manuais do dono** no `tasks.md`: o implementador
   não executa nada contra o GitHub real.

## R-14 — Health checks: forma, resposta e proteção contra abuso (decisão principal)

**Forma**: `MapHealthChecks` (recurso do framework, sem pacote novo), **não** um controller. Os
endpoints de saúde não são casos de uso (Princípio III), não têm regra e não recebem corpo; a
constituição os nomeia na seção "Operação" (`/health/live`, `/health/ready`, anônimos, sem
detalhe), e esse texto específico prevalece sobre o padrão `v1/{contexto}/{acao}` do Princípio VI.
Mapeados por um método de extensão `MapHealthEndpoints()` com **comentário XML** (o "comentário de
documentação" do FR-023: não há `action` de controller para documentar, e o Swagger só existe em
Development). **Fora do `v1/`**, `AllowAnonymous()` explícito, só os métodos `GET` e `HEAD`.

**Resposta mínima**: texto puro `Healthy` ou `Unhealthy` (o escritor padrão do framework, que não
imprime nada além do estado), `200` ou `503`, `Cache-Control` **contendo `no-store`** (o
middleware padrão do framework envia `no-store, no-cache`). **Sem JSON, sem lista de
verificações, sem tempo, sem versão, sem texto de exceção.** Não usa o envelope `ErrorResponse`:
quem consome (balanceador, `curl`, o script da 05b) lê o código, e a constituição só impõe o
envelope aos erros 401, 403, 429 e 500.

**Viva** (`/health/live`): `Predicate = _ => false`; nenhuma verificação roda; responde `200`
enquanto o processo atende. Custa zero consultas.

**Pronta** (`/health/ready`): uma verificação, `catalog`, que roda
`SELECT 1 FROM tenant_databases LIMIT 1` pelo `CatalogDbContext` (papel `acs_app`). Prova três
coisas: o catálogo **alcança**, a credencial **vale** e o **esquema existe** (um catálogo ainda sem
migration deixa o `ready` vermelho, o que é o desejado para um deploy que chegou antes da
atualização das bases). **As bases de tenant não entram** (são muitas). Tempo limite de **2
segundos** (`CancellationTokenSource` com `TimeProvider`). **Testabilidade**: o `FakeTimeProvider`
dos testes só controla `GetUtcNow()`/`Advance` (o `TimeProvider.CreateTimer` base usa timer real),
então o **TTL do cache** (calculado com `GetUtcNow()`) é testado com relógio falso, mas o **tempo
limite de 2 s corre em tempo real**: o teste da sonda que nunca termina espera ≈2 s reais.

**A proteção contra abuso** (a decisão que o roteiro pediu) — o problema: o `ready` é **anônimo**
e **consulta o catálogo**; o catálogo é também consultado a cada requisição autenticada
(`ITenantResolver`). Uma inundação anônima de `/health/ready` poderia esgotar o pool de conexões e
derrubar os usuários reais.

| Opção | Efeito | Veredito |
|---|---|---|
| **A. Resultado em cache por 5 s, com uma única consulta em voo (escolhida)** | no máximo **1 consulta a cada 5 s por instância**, qualquer que seja a taxa de chamadas; ~40 linhas; testável com relógio falso e contador | **escolhida** |
| B. Sem proteção (uma consulta por chamada) | amplificação direta: N chamadas anônimas = N consultas ao catálogo | descartada |
| C. Limite por IP (`RateLimiter`) no `/health/*` | devolve `429` ao balanceador, que tem **um** IP e consulta com frequência fixa; mistura política de abuso com sondagem de saúde; e o limitador de login é por instância e será trocado na spec 06 | descartada |
| D. Verificação periódica em segundo plano (`IHealthCheckPublisher`), endpoint só lê o último estado | carga constante e independente do tráfego | descartada **por ora**: serviço de fundo, estado "desconhecido" na partida e mais código; vale rever se o custo do 5 s incomodar |

**Detalhes da opção A**: o verificador é um **singleton** com `SemaphoreSlim(1,1)`; chamada com
cache válido não toca o banco; com cache vencido, **uma** chamada consulta e as concorrentes
esperam e leem o resultado novo; o TTL de **5 s** vale para saúde e para falha (a recuperação
aparece em até 5 s, aceitável para balanceador e deploy). O cache guarda **só um booleano e um
instante**: nenhum dado de tenant (Princípio I), e é **por instância de propósito** (cada
instância precisa reportar o próprio estado; o Princípio XIII exige Redis para estado que deve ser
coerente entre instâncias, e este não deve). Um `ICatalogProbe` (interface interna da Api, uma
implementação com o `CatalogDbContext`) é a **costura de teste**: o `ApiFactory.WithSingleton`
troca a sonda por uma que conta chamadas, trava ou falha.

**O que se garante e como se prova** (o critério de desempenho é o **custo ao catálogo**, não a
latência): (1) **FR-022 / SC-008**: no máximo **1 consulta a cada 5 s por instância**, sob qualquer
volume, provado por teste com a **sonda falsa que conta as chamadas** (1000 chamadas, em sequência
e concorrentes, dentro de 5 s = 1 consulta), **sem medir tempo**; um teste de latência p95 seria
frágil e não prova o que importa (a carga no catálogo); (2) **FR-020 / SC-007**: o estado reflete a
queda **e a volta** do catálogo em até 5 s, provado com `Advance(5 s)` nos dois sentidos (falha →
passa e passa → falha), e cada chamada responde em até 2 s (o tempo limite corre em tempo real);
(3) **a mudança do JWT** (ignorar o `Authorization` só nas duas rotas de saúde, por metadado de
endpoint, nunca por prefixo de caminho) tem **dois testes**: token válido ou inválido em
`/health/*` não muda a resposta e **não dispara nenhuma consulta** (zero chamadas ao
`ISessionValidator` e à sonda), e **um endpoint protegido segue aceitando token válido e recusando
inválido**, como antes (prova de que a exceção não vazou para o resto da Api).

**Segurança do que fica de fora**: log em caso de falha só com o **tipo** da exceção, nunca a
mensagem (poderia trazer servidor e porta) nem connection string (Princípio IX, "Logs"). O header
`Authorization` **é ignorado** nas rotas de saúde, como o login já faz: o `OnMessageReceived` do JWT
chama `context.NoResult()` quando o endpoint tem o metadado `HealthEndpointMarker` (mesmo desenho do
`UseDefaultLoginTenantAttribute`). Sem isso, um token **válido** enviado à `live` dispararia, no
`OnTokenValidated`, a consulta ao catálogo e à sessão, e a `live` deixaria de ser "zero consultas"
(e a `ready` perderia a proteção do cache). Token inválido ou válido não muda o resultado, e como
são `AllowAnonymous` a autenticação não os bloqueia.

**Quem os alcança**: o nginx do console só encaminha `/v1/`, então `/health/*` **não** fica
exposto pelo console. O balanceador e o deploy (05b) chegam ao container direto, pela rede
privada. Em containers, `HttpsRedirection:Enabled=false` (o TLS termina antes): sem isso a sonda
do balanceador receberia um redirecionamento.

## R-15 — Dados fictícios de homologação (`seed-staging`)

**Verbo**: `seed-staging`, aceito **somente** com `DOTNET_ENVIRONMENT=Staging` (sem a variável, o
padrão é `Production`: recusa). Recusa em Development, Production e qualquer outro valor com o
**código de saída `5`**, **antes** de ler a configuração, sem escrever nada (mesmo desenho do
`seed-dev`).

**Reaproveitamento**: o caminho de escrita é o do `seed-dev` (referência:
`Commands/SeedDevCommand.cs`, `SeedOperatorsAsync`): extrai-se um **`SyntheticTenantSeeder`** no
Migrator (registro no catálogo → atualização das bases recém-cadastradas → operadores pelo
`AppDbContext` sob RLS), e os dois comandos passam a delegar a ele, mantendo guarda, validação de
senha e mensagens próprias. Os `SeedDevCommandTests` existentes **não mudam** e provam que a
extração não alterou o comportamento (nenhum teste precisa ser editado).

**Dados** (detalhe em [data-model.md](data-model.md)): 3 tenants, dois em `shared_01` e um em
`dedicated_01`, com identificadores **estáveis de homologação** (`aaaaaaaa-0000-4000-8000-00000000000N`,
distintos dos de desenvolvimento), CNPJs sintéticos **inválidos** (`99999999000101` a `103`, não
validados pelo catálogo), nomes "Tenant X (homologação)". Operadores: `admin@homologacao.test`
(todos os 3 tenants) e `operador@homologacao.test` (A e B, com **senhas diferentes**, para exercitar
o "mesmo e-mail em tenants diferentes" do Princípio I). Domínio `.test` é reservado
(RFC 2606). **Nenhum** usa `admin@admin.com.br`; um teste confere isso.

**Senhas**: seção `StagingSeed` (`AdminPassword`, `OperatorPasswordTenantA`,
`OperatorPasswordTenantB`), lidas de configuração/variável de ambiente (`StagingSeed__AdminPassword`
etc.; na 05b virão do Secrets Manager sem mudar o código). Validadas pelo value object `Password`
(mínimo 8, maiúscula, minúscula, número e símbolo) **antes de escrever**; senha ausente ou inválida
sai com `2` e nada é criado; **o valor nunca vai para a saída**.

**Idempotência**: consulta antes de criar, como o `seed-dev`; reexecutar não duplica nem troca
senha. **Bases sem estrutura (FR-030, decisão confirmada)**: o `seed-staging`, como o `seed-dev`,
**registra as bases e os tenants no catálogo** (idempotente) e **atualiza a estrutura delas pelo
orquestrador do próprio Migrator, com a `SeedAdminPolicy`** (que garante que nasçam sem o admin
fixo), antes de gravar qualquer operador. Isso é necessário porque o verbo `migrate` só atualiza
bases **já registradas**: sem o registro feito pelo próprio comando, uma base nova nunca seria
atualizada. Se **qualquer** base falhar, o comando sai com `1` e **não cria nenhum operador**; o
registro no catálogo **pode ficar** (é idempotente), e uma nova execução, com a causa corrigida,
conclui o trabalho sem duplicar nada. (Texto da spec alinhado: FR-024, FR-030, cenários 1 e 8 da
História 6.)

**Alternativas descartadas**: parametrizar o `seed-dev` por ambiente (mistura dois contratos e
afrouxa a recusa do `seed-dev` em Production); copiar o código (duplicação).

## R-16 — Verificação do administrador fixo (`verify-no-seed-admin`)

**Verbo**: `verify-no-seed-admin`, **somente leitura**, recusado em Development (código `6`, antes
de ler a configuração: lá o administrador de desenvolvimento existe de propósito).

**De onde vêm os dados fixos** (nada duplicado à mão): `new SeedAdmin().UpOperations` devolve as
operações da própria migration (`Migration.UpOperations` é público); a `InsertDataOperation`
traz a **tabela** (`operators`), as **colunas** (`id`, `email`, ...) e os **valores** (o
`Guid` e o e-mail). Um `SeedAdminFingerprint` extrai `(Table, IdColumn, EmailColumn, Id, Email)`
dessa operação; se a migration mudar de forma e a extração falhar, o verbo **falha alto** (não
aprova sem saber o que procurar). A migration **não é editada** (o `CLAUDE.md` e a constituição
proíbem editar migration aplicada). Um teste fixa, **com literais independentes**, o
`cc59e349-efbd-4800-9726-f38a325e4d3d` e o `admin@admin.com.br`, para pegar uma mudança acidental.

**Como lê todos os tenants**: com a **connection string do migrator** (`BYPASSRLS`), porque o
`acs_app` sob RLS não vê nada sem tenant. Cada leitura roda em **transação somente leitura**
(`BEGIN READ ONLY`): qualquer escrita falha com `25006`; um teste prova isso. Pooling desligado,
como no executor de migrations.

**O que confere** (cada local, com `lower()` no e-mail):

- **Base de tenant**: se a tabela `operators` existe (`to_regclass`), procura
  `id = <fixo> OR lower(email) = lower(<fixo>)`; **sem a tabela** conta como "não contém". Basta
  **um** dado fixo para reprovar (FR-031).
- **Catálogo**: hoje não tem tabela de operadores; mas a spec 09 criará o diretório (e-mail →
  tenant). Para pegar **qualquer** referência, a verificação varre o esquema `public` do catálogo
  atrás de colunas de texto cujo nome contenha `email` e de colunas `uuid` chamadas `operator_id`, e
  procura o e-mail fixo e o identificador fixo nelas. Identificadores vêm de
  `information_schema` e são **citados** (`QuoteIdentifier`), nunca concatenados. O teste de
  "catálogo contaminado" cria uma tabela com essas colunas de propósito.

**Quais bases**: catálogo (`ConnectionStrings:CatalogMigration`) e **todas** as de
`tenant_databases` (`CatalogRegistry`, paginado por chave como o `migrate`), cada uma com a
`MigrationConnectionString` configurada. **Divergência** (base no catálogo sem connection string de
migração, ou connection string configurada para uma base que não está no catálogo) **reprova**
com código `8`; base inacessível também. Paralelismo limitado, reaproveitando
`Migrator:MaxDegreeOfParallelism`.

**Códigos de saída novos** (somam-se a 0 a 4): `5` seed-staging recusado; `6` verificação recusada
em Development; `7` **admin fixo encontrado** (reprovou); `8` **não foi possível verificar**
(base inacessível ou divergência). Se houver as duas coisas, sai `7` (a pior) e a saída lista
ambas. A saída traz o **nome lógico** das bases e a **contagem** verificada; nunca connection
string, e-mail de pessoa, identificador de operador nem senha.

**Consulta e índice (Princípio XIII)**: `lower(email)` **não tem índice** (o índice único é por
`tenant_id` + e-mail), então cada base lê `operators` por varredura. É uma **ferramenta
operacional** que roda por entrega (05b) e não no caminho de requisição; mitigação:
`statement_timeout` configurado na conexão, paralelismo limitado e leitura em transação
somente leitura. Revisar quando as bases compartilhadas crescerem (spec 06). Registrado em
Rastreamento de Complexidade.

**Testes** (Testcontainers, padrão do `SeedDevCommandTests`): base limpa aprova; base
**contaminada de verdade** (`Database.MigrateAsync()` direto, **sem** a política, que executa a
`SeedAdmin`); e-mail alterado com identificador fixo reprova; identificador trocado com e-mail
fixo em outras maiúsculas reprova; catálogo contaminado reprova; base sem tabelas aprova; base
inacessível reprova (`8`); divergência reprova (`8`); Development recusa (`6`); a transação é
somente leitura.

## R-17 — O guia e o quickstart

**Decisão**: o guia permanente é **`docs/protecoes-do-repositorio.md`** (pasta nova `docs/` na
raiz), em português, curto, com: as proteções e o motivo de cada uma; como refazê-las (script) e
conferi-las (as duas partes); a ordem de ativação; a emergência (desligar e religar); o cuidado dos
ensaios (SHA anotado, mudança desfazível); o repositório privado; onde ver o resultado do gate, da
auditoria e da conferência; como notar e reativar agendamento desativado; a configuração de
notificação por e-mail; **e onde rodar os scripts no Windows** (no **Git Bash**, não no PowerShell
nem no `bash` do WSL; `ACS_BASH` e `jq` para os testes, ver R-19). O `quickstart.md` desta spec é o
**roteiro de validação** (como provar que cada fase funciona e os passos manuais do dono), e aponta
para o guia.

**Por quê**: `specs/` registra a história de cada spec; o guia precisa viver onde alguém o procura
quando a proteção quebrar. A spec 05b acrescentará o guia de operação da nuvem na mesma pasta.

## R-18 — Fases, PRs e ordem de ativação

**Fases** (um commit por fase, revisado antes da próxima; **o agente não faz commits**):

| Fase | Escopo | Red | Green |
|---|---|---|---|
| **A** | Health checks (FR-019 a FR-023) | testes de `/health/*` falham (rota inexistente, `404`) | `ICatalogProbe`, verificador, `MapHealthEndpoints`, registro |
| **B** | Verbos do Migrator (FR-024 a FR-035) | testes dos dois verbos falham (verbo desconhecido, código `2`) | `SyntheticTenantSeeder`, `seed-staging`, `SeedAdminFingerprint`, `verify-no-seed-admin`, códigos 5 a 8 |
| **C** | Workflows (FR-006 a FR-018, FR-013a) | testes de configuração e de scripts falham (arquivos ausentes) | `BashLocator`/`ScriptShell`, `main-ruleset.json` (nasce na C: o `check-rules.sh` o lê), `ci-gate.yml`, scripts `scripts/ci/*`, gatilhos novos, `package-audit.yml`, `protections-check.yml` e `check-rules.sh` |
| **D** | Script de proteções + guia (FR-001 a FR-005a, FR-036 a FR-038) | testes dos scripts de proteção e do guia falham (o corpo do ruleset já passa desde a C) | `FakeGh` (`GH_BIN`, só para teste), `apply-protections.sh`, `check-protections-manual.sh`, `docs/` |
| **E** | **Tarefas manuais, depois do merge**: validação do gate pelo **revisor** (V-06, antes do ruleset) e, depois, o **dono** (aplicar, conferir, ensaiar) | n/a | n/a |

**Um pull request** reúne as fases A a D (4 commits). **A fase E só começa depois do merge** e
**depois de o `ci-gate` ter rodado verde na `main`** (a ordem de ativação: exigir um check que ainda
não existe trava todos os PRs, inclusive o que o cria). O PR desta spec entra **sem** a proteção
ativa (a `main` ainda não tem ruleset), e é o último que entra assim.

**O que o revisor valida no GitHub real** (o implementador não roda nada lá). **Antes do merge**, só
duas coisas: o **PR da feature verde** no `ci-gate` (V-06a: o diff traz todas as áreas, então o gate
roda tudo) e **nada aplicado** no GitHub (V-06b). **Depois do merge e antes de aplicar o ruleset**
(V-06, tarefa da Fase E), em PRs descartáveis **partindo da `main`** e abertos já contra ela: só
documentos (passa sem backend e frontend); só frontend; só backend; teste quebrado (reprova);
execução cancelada (reprova); arquivo fora das áreas (exige tudo); mapeamento de `cancelled` em
reusable workflow (R-02). Fecham-se com `gh pr close --delete-branch`.

**Por que não antes do merge**: o `ci-gate.yml` só existe na branch da feature; um PR descartável
teria de partir dela e o diff contra a `main` incluiria todos os arquivos da feature (backend,
frontend e `.github`), dando sempre "roda tudo". **Por que antes do ruleset**: se um caso falhar, a
correção do gate entra por PR pequeno **ainda sem proteção**; com o ruleset ativo, a correção
também teria de passar pelo gate que se quer corrigir.

## R-19 — Pacotes e dependências

**Nenhum pacote NuGet ou npm novo** (Princípio II): `MapHealthChecks` é do framework; o Migrator já
tem EF Core e Npgsql; os scripts usam `bash`, `jq`, `gh` e `curl`. **Nenhuma ação de terceiros** nos
workflows.

**Como os testes acham o `bash` (decisão, por causa do Windows).** No Windows, "bash" no PATH
resolve primeiro para `...\AppData\Local\Microsoft\WindowsApps\bash.exe`, o **atalho do WSL**: ele
roda o Ubuntu do WSL, que não vê o `jq` nem o `gh` do Windows e enxerga os caminhos de outro jeito.
Por isso **nenhum teste usa o `bash` do PATH no Windows**. Os testes que rodam scripts usam um
auxiliar único, `ScriptShell` (em `Tests/Support/`), com esta regra:

| Sistema | Qual `bash` |
|---|---|
| Windows | a variável de ambiente **`ACS_BASH`**, **se estiver definida**; **senão** `C:\Program Files\Git\bin\bash.exe` |
| Linux e macOS | o `bash` do PATH |

Regras do auxiliar (todas com teste, `BashLocatorTests`, sobre uma função pura que recebe o sistema,
as variáveis e a existência de arquivos):

1. **`ACS_BASH` definida mas apontando para um arquivo que não existe**: falha (uma configuração
   explícita errada nunca cai em silêncio no padrão).
2. **Caminho que passa por `\WindowsApps\` ou é o `bash.exe` do `System32`** (o atalho/lançador do
   WSL), mesmo vindo de `ACS_BASH`: **recusado**, com mensagem dizendo que é o WSL e que o certo é o
   Git Bash.
3. **Não achou o `bash`** (Windows sem Git Bash em `Program Files` e sem `ACS_BASH`): **falha** com
   mensagem clara: "bash não encontrado; instale o Git para Windows ou defina ACS_BASH com o caminho
   do bash.exe do Git Bash".
4. **`jq` ausente**: o auxiliar roda `command -v jq` **dentro do `bash` escolhido** (não no PATH do
   Windows) e **falha** com mensagem clara se não achar.
5. **Nunca pula**: nenhum teste de script usa `Skip`, `[SkippableFact]` ou retorno antecipado quando
   falta o `bash` ou o `jq`; ele **falha** (a regra de Integridade proíbe pular teste).
6. Os scripts são chamados com o **diretório de trabalho na raiz do repositório e caminho
   relativo** (`scripts/ci/detect-areas.sh`), para não depender da conversão de caminhos
   Windows/POSIX; entrada pela entrada padrão; variáveis por ambiente; `*.sh` em LF
   (`.gitattributes` já garante).

No Actions (`ubuntu-latest`) o `bash` e o `jq` do PATH existem. **No guia e no quickstart**: os
comandos de script que o **dono** roda (`apply-protections.sh` etc.) são rodados no **Git Bash**
(não no PowerShell e não no `bash` do WSL, que não enxerga o `gh` do Windows), e quem roda a suíte
com `dotnet test` num Windows com WSL só precisa do Git para Windows instalado ou de `ACS_BASH`.

## R-20 — Testes existentes afetados e linha de base

**Nenhum teste existente precisa mudar.** Conferido:

- `PackageAuditConfigTests`: continua verde porque `backend-ci.yml` e `frontend-ci.yml` mantêm os
  passos (R-01). **Vigiar**: se alguém mover esses passos, o teste falha por esse motivo.
- `MigratorProgramTests.Sai_com_2_com_configuracao_invalida` (`verbo_desconhecido`, `sem_verbo`):
  seguem devolvendo `2` (só muda o texto de uso, que não é afirmado).
- `SeedDevCommandTests` e `SeedAdminPolicyTests`: seguem verdes depois da extração do
  `SyntheticTenantSeeder`.
- `EndpointCoverageTests`, `DefaultAuthorizationTests`, `SwaggerTests`: não enumeram endpoints de
  saúde; nada muda. Um teste **novo** garante que os únicos endpoints sem controller são os de
  saúde (e, em Development, o Swagger).
- `src/Frontend/README.md`: texto sobre o CI (linha 84) é atualizado (documento).

**Linha de base**: a **primeira tarefa do `tasks.md`** mede e anota os totais de backend e de
frontend antes de qualquer mudança (SC-013: nenhum teste anterior é apagado, pulado ou
enfraquecido).

**Tabela "Testes existentes afetados"** (vai para o `tasks.md`, mesmo que vazia): hoje **vazia**, com
a conferência acima como justificativa. Se algum teste existente precisar mudar durante a
implementação, a mudança só vale se estiver nessa tabela, com o motivo (a spec mudou o
comportamento esperado).

## R-21 — Riscos e pontos de atenção

| Risco | Tratamento |
|---|---|
| Renomear o job `ci-gate` derruba a exigência sem aviso | teste de PR compara com `main-ruleset.json`; conferência semanal; guia |
| `GITHUB_TOKEN` com `contents: read` pode não ler o endpoint de regras | a documentação lista o endpoint só em Metadata read (token de instalação); a conferência é **autenticada** e **nunca anônima** (limite de 60/h por IP compartilhado); a primeira execução real é a prova (tarefa do dono); se der `403`, a conferência falha, nunca aprova |
| `integration_id` errado no ruleset travaria todos os PRs | `15368` conferido por `GET /apps/github-actions` e pelos check runs reais; o `apply-protections.sh` confere o `app.id` do check `ci-gate` na `main` antes de aplicar e para se divergir |
| Status externo com o nome `ci-gate` burlaria o gate | o `required_status_checks` é preso ao `integration_id` do Actions; a conferência automática reprova item sem ele |
| `bash` do PATH no Windows é o atalho do WSL | os testes localizam o `bash` por `ScriptShell` (`ACS_BASH`, senão o Git Bash; nunca o PATH; recusa o WSL); o guia manda rodar no Git Bash |
| Aviso por e-mail não chega (notificação desligada) | a falha fica no histórico de Actions; o guia manda conferir a configuração |
| Agendamento desativado após 60 dias sem atividade | guia explica como notar e reativar; vale para os dois workflows agendados |
| PR que afrouxa o próprio `ci-gate.yml` se aprova sozinho | limite aceito (Premissas da spec): o dono revisa o diff dos arquivos de CI; a regra de Integridade proíbe afrouxar |
| Verificação do admin fixo lenta em bases grandes (varredura de `operators`) | ferramenta operacional por entrega, com `statement_timeout`; revisar na spec 06 |
| `jq`/`bash` ausentes numa máquina de dev | os testes de scripts falham com mensagem clara (nunca são pulados), com o `jq` procurado **dentro** do `bash` escolhido; o guia lista os requisitos |
| Merge direto antes de a proteção estar ativa | por definição só até o fim da fase E; a ordem de ativação está no guia e no `tasks.md` |
| Senha fictícia literal escapando para um repositório **público** (SC-010) | teste de arquitetura `FictitiousPasswordsNotInRepoTests` sobre código de produção, `appsettings*.json`, `docker-compose*.yml` e `.github/` (os testes e o `.env.example` ficam de fora), com checagem de não vacuidade e controle negativo; varredura de segredos do GitHub como segunda rede |
| Auditoria que "passa" sem nunca ter reprovado | prova **local** da falha (pacote vulnerável conhecido em pasta temporária: `dotnet restore` e `npm audit` reprovam; `npm audit` sem rede também) e, opcional, ensaio no GitHub por `workflow_dispatch` numa branch descartável (T066, caso 8) |
| Pasta temporária dos testes de script entrando no commit | `ScriptShell.NewScratchDirectory()` usa `src/Backend/AccessControlSystem.Tests/obj/script-tests/<Guid>/`: relativo à raiz (igual para Git Bash e .NET) e ignorado pelo `[Oo]bj/` de `src/Backend/.gitignore`; o `obj/` da **raiz não** é ignorado, por isso não serve; `git check-ignore` confere (T062) |
| PR que nunca recebe o `ci-gate` (conflito de merge; fork de primeiro contribuinte) | a plataforma não inicia as verificações nesses dois casos: o aceite fica **bloqueado** (nunca aprovado) até resolver o conflito ou o dono aprovar a execução; exceção explícita no FR-006 e no guia |
