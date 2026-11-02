# Plano de Implementação: Base de entrega sem nuvem

**Branch**: `20261102-171227-delivery-foundation` | **Data**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `/specs/20261102-171227-delivery-foundation/spec.md`

## Resumo

Esta é a spec 05a: tudo o que dá para fazer e provar de graça, sem conta AWS, para proteger a
`main` e tornar o CI confiável. Entrega-se em **4 fases de código (A a D)**, cada uma com build e
testes verdes ao final, para revisão e commit separados (o agente não faz commits), mais uma
**fase E de tarefas manuais** (a validação do gate com PRs descartáveis, que é do revisor, e depois a
aplicação e os ensaios, que são do dono), que só começa depois do merge. Cada fase segue
Red → Green. Decisões e alternativas em [research.md](research.md).

Abordagem técnica:

- **Gate** (`ci-gate.yml`): um workflow que **sempre** roda em todo PR para a `main` e em todo `push`
  para a `main`, **sem filtro de paths**. Detecta as áreas tocadas por `git diff` (script
  `scripts/ci/detect-areas.sh`, sem ação de terceiros) e **chama `backend-ci.yml` e `frontend-ci.yml`
  como reusable workflows** (`workflow_call`) só nas áreas aplicáveis. Um job final, `ci-gate`
  (`if: always()`, o **único** check exigido), confere o resultado real de cada um com
  `scripts/ci/evaluate-gate.sh`: falha, cancelamento, timeout, "skipped" sem dispensa e ausência de
  resultado **reprovam**. Os dois workflows chamados mantêm **os mesmos passos** (só trocam o
  gatilho), então o `PackageAuditConfigTests` segue verde sem edição (R-01, R-02).
- **Proteção**: um **ruleset** na `main` (PR obrigatório, `ci-gate` exigido **e preso ao
  `integration_id` do GitHub Actions, 15368**, de modo que um status externo com o mesmo nome não
  vale; sem bypass nem para admin, sem envio forçado nem exclusão, sem revisão humana, sem exigir
  branch atualizada), criado por `scripts/github/apply-protections.sh` (o dono roda, no Git Bash,
  depois de o gate existir na `main`), mais varredura de segredos e proteção de envio. A
  **conferência** tem duas partes: automática (`protections-check.yml`, semanal e sob demanda,
  **autenticada com o `GITHUB_TOKEN`**, só leitura, nunca anônima, lendo
  `GET /repos/{dono}/{repo}/rules/branches/main`) e à mão (`check-protections-manual.sh`) para o que
  esse endpoint não mostra (R-10 a R-12).
- **Testes de script no Windows**: os testes que rodam scripts localizam o `bash` por um auxiliar
  (`ScriptShell`): no Windows a variável `ACS_BASH` ou, senão, `C:\Program Files\Git\bin\bash.exe`
  (**nunca** o `bash` do PATH, que lá é o atalho do WSL); no Linux, o `bash` do PATH. Sem `bash` ou
  sem `jq`, **falham com mensagem clara** (nunca pulam) (R-19).
- **Auditoria agendada** (`package-audit.yml`): semanal e sob demanda, `dotnet restore` (NuGet
  Audit) e `npm audit`, mesmos critérios da 04b (R-09).
- **Saúde da Api**: `/health/live` (sem consulta) e `/health/ready` (catálogo, 2 s, **resultado em
  cache por 5 s com uma só consulta em voo**, proteção contra abuso), anônimos, resposta mínima, por
  `MapHealthChecks` com uma `ICatalogProbe` como costura de teste (R-14).
- **Migrator**: `seed-staging` (só `Staging`, idempotente, 3 tenants, senhas de fora, caminho de
  escrita compartilhado com o `seed-dev` por um `SyntheticTenantSeeder`; **como o `seed-dev`,
  registra as bases e os tenants no catálogo e atualiza a estrutura delas pelo orquestrador do
  próprio Migrator, com a `SeedAdminPolicy`**, porque o `migrate` só atualiza bases já
  registradas; se qualquer base falhar, sai com `1` e **não cria nenhum operador**, e o registro
  no catálogo pode ficar, por ser idempotente) e `verify-no-seed-admin`
  (só leitura, recusado em Development, lê os dados fixos **da própria migration**, reprova por
  **qualquer** dado fixo, base inacessível ou divergência), com os códigos de saída `5` a `8`
  (R-15, R-16).

| Fase | Escopo | Red (como falha primeiro) | Green |
|---|---|---|---|
| **A** | Saúde da Api (FR-019 a FR-023) | `HealthEndpointsTests` e o teste de "únicos endpoints sem controller" falham (`/health/*` responde `404`) | `ICatalogProbe`, `CatalogReadinessHealthCheck`, `MapHealthEndpoints`, registro no `Program.cs` e no `BuilderExtensions` |
| **B** | Verbos do Migrator (FR-024 a FR-035) | os testes dos dois verbos falham (`MigratorApplication` devolve `2`: verbo desconhecido) | `SyntheticTenantSeeder` (extraído do `SeedDevCommand`), `SeedStagingCommand`, `SeedAdminFingerprint`, `VerifyNoSeedAdminCommand`, códigos `5` a `8`, roteamento dos verbos |
| **C** | Workflows e scripts de CI (FR-006 a FR-018, FR-013a) | `CiWorkflowsConfigTests` e `CiScriptsTests` falham (arquivos e scripts ausentes) | `BashLocator`/`ScriptShell` (acham o `bash`), `main-ruleset.json` (fonte única lida pelo `check-rules.sh`, por isso nasce aqui), `ci-gate.yml`, `scripts/ci/{detect-areas,evaluate-gate}.sh`, gatilhos novos nos dois workflows, `package-audit.yml`, `protections-check.yml`, `scripts/github/check-rules.sh` |
| **D** | Script de proteções + guia (FR-001 a FR-005a, FR-036 a FR-038) | `RepositoryProtectionsConfigTests` falha (scripts e guia ausentes; o corpo do ruleset já passa desde a C) | `FakeGh` (o `gh` falso dos testes, `GH_BIN`), `apply-protections.sh`, `check-protections-manual.sh`, `docs/protecoes-do-repositorio.md`, ajuste do `src/Frontend/README.md`, `README.md` e `CLAUDE.md` |
| **E** | **Manual, depois do merge**: **revisor** valida o gate com PRs descartáveis partindo da `main` (V-06, **antes** do ruleset); **dono** aplica, confere e ensaia | n/a | n/a (ver [quickstart.md](quickstart.md) V-06 a V-09) |

**Um pull request** reúne as fases A a D (4 commits, revistos um a um). **A fase E só começa depois
do merge e depois de o `ci-gate` ter rodado verde na `main`**. O PR desta spec entra **sem**
proteção (a `main` ainda não tem ruleset) e é o último que entra assim.

**Rastreabilidade dos requisitos da spec**

| Requisitos | Fase | Onde é entregue e provado |
|---|---|---|
| FR-001, FR-002, FR-003, FR-004, FR-005, FR-005a | C (corpo do ruleset), D (script) e E (aplicação) | `main-ruleset.json` (nasce na **C**, com `integration_id` do Actions), `apply-protections.sh` (**D**); conferências; ensaios (V-07, V-09) |
| FR-006, FR-007, FR-008, FR-009, FR-010, FR-011 | C | `ci-gate.yml`, `detect-areas.sh`, `evaluate-gate.sh`; `CiScriptsTests`, `CiWorkflowsConfigTests`; PRs descartáveis (V-06) |
| FR-012 | C | job `k6-short` com `name: "k6 curto (não ativado)"` (aparece assim na lista de checks do PR) e a linha "não ativado" do resumo |
| FR-013 | A a D | regra de Integridade; `PackageAuditConfigTests` inalterado; linha de base (V-01) |
| FR-013a | C | `push` na `main` no `ci-gate.yml` |
| FR-014, FR-015 | C | `permissions: contents: read`, sem segredo, `pull_request` apenas; `CiWorkflowsConfigTests` |
| FR-016, FR-017, FR-018 | C | `package-audit.yml`; e-mail padrão do GitHub (R-06, R-09) |
| FR-019, FR-020, FR-021, FR-022, FR-023 | A | `Api/Health/*`, `HealthEndpointsTests` |
| FR-024, FR-025, FR-026, FR-027, FR-028, FR-029, FR-030 | B | `seed-staging`, `SyntheticTenantSeeder`, `SeedStagingCommandTests` |
| FR-031, FR-032, FR-033, FR-034, FR-035 | B | `verify-no-seed-admin`, `SeedAdminFingerprint`, `VerifyNoSeedAdminCommandTests` |
| FR-036, FR-037, FR-037a, FR-038 (SC-015: nome **e** origem do check) | C (conferência automática), D (conferência à mão e guia) | `protections-check.yml` (autenticada com o `GITHUB_TOKEN`), `check-rules.sh`, `check-protections-manual.sh`, `docs/protecoes-do-repositorio.md` (inclui "rode no Git Bash") |
| FR-039 | A, B e C | `OnlyHealthEndpointsAreAnonymousTests` e a suíte existente sem edição |

Linha de base: a **primeira tarefa do `tasks.md`** mede e anota os totais de backend e de front
antes de qualquer mudança (SC-013: nenhum teste anterior é apagado, pulado ou enfraquecido).

**Glossário** (os mesmos termos em todos os artefatos desta spec)

| Termo | Significa |
|---|---|
| **gate** | o workflow `ci-gate.yml` (todos os jobs dele) |
| **verificação agregadora** / **`ci-gate`** | o **job final** de nome `ci-gate`, o único check exigido pelo ruleset |
| **verificações de área** | os jobs `backend` e `frontend` (reusable workflows) e o `k6-short` reservado |
| **dispensada** / **não ativada** | estados de uma verificação no resumo do gate: área sem mudança; passo de carga ainda sem cenários |
| **ruleset** | o conjunto de regras da plataforma aplicado à `main` (corpo em `main-ruleset.json`) |
| **conferência automática** | o `protections-check.yml` (semanal e sob demanda, `GITHUB_TOKEN`, lê as regras) |
| **conferência à mão** | o `check-protections-manual.sh`, rodado pelo dono (bypass e varredura de segredos) |
| **validação do revisor** (V-06) | os PRs descartáveis que o revisor abre contra o GitHub real, depois do merge e antes do ruleset |
| **pronta** / **viva** | `/health/ready` (catálogo) e `/health/live` (sem consulta) |
| **administrador fixo** | o operador de id, e-mail e senha fixos criado pela migration `SeedAdmin` |

## Contexto Técnico

**Linguagem/Versão**: C# / .NET 10 (Api, Migrator, testes); GitHub Actions (YAML); shell (`bash`) e
`jq` para os scripts de CI. TypeScript e o frontend **não mudam** (só um parágrafo do README).
Os testes de script exigem `bash` e `jq` e os localizam explicitamente (ver "Testes").

**Dependências Principais**: o que já existe: ASP.NET Core (`MapHealthChecks` já vem no framework da
Api), EF Core + Npgsql (Migrator e Api), `actions/checkout`, `actions/setup-dotnet`,
`actions/setup-node`. **Nenhum pacote NuGet ou npm novo e nenhuma ação de terceiros** (Princípio II;
R-19). Os scripts usam `bash`, `jq`, `gh` e `curl`, presentes no `ubuntu-latest` e no **Git Bash** do
dono (no Windows do dono o `bash` do PATH é o atalho do WSL, que **não** serve).

**Armazenamento**: PostgreSQL, **sem nenhuma mudança de esquema e sem migration**. O
`seed-staging` grava operadores pelo `AppDbContext` (como o `seed-dev`) e registra tenants e bases
no catálogo; o `verify-no-seed-admin` só lê. O `CLAUDE.md` segue valendo: migrations só pelo
Migrator.

**Testes**: backend xUnit + NSubstitute + Testcontainers.PostgreSql (os verbos e a saúde); testes de
configuração dos workflows e de comportamento dos scripts em xUnit (`Architecture/`), no estilo do
`PackageAuditConfigTests`. Os que **rodam scripts** usam o auxiliar `ScriptShell` (`Support/`), que
acha o `bash` assim: **Windows** → a variável de ambiente `ACS_BASH` (se definida; se apontar para
arquivo inexistente, falha), senão `C:\Program Files\Git\bin\bash.exe`; **Linux/macOS** → `bash` do
PATH. Recusa o atalho do WSL (`\WindowsApps\` ou `System32\bash.exe`), procura o `jq` **dentro** do
`bash` escolhido e, sem `bash` ou sem `jq`, **falha com mensagem clara** (nunca `Skip`). Os
arquivos temporários dos testes de script (o `gh` falso e o arquivo do `$GITHUB_STEP_SUMMARY`) ficam
em `src/Backend/AccessControlSystem.Tests/obj/script-tests/<Guid>/`: caminho **relativo à raiz**,
entendido igual pelo Git Bash e pelo .NET, e **ignorado pelo git** (o `.gitignore` do backend ignora
`obj/`; o `obj/` da raiz **não** é ignorado). **Prova local da falha da auditoria** (sem GitHub): um
projeto temporário **fora do repositório**, com um pacote de vulnerabilidade conhecida, em que
`dotnet restore` falha (NuGet Audit como erro) e `npm audit` sai com código diferente de zero. **Teste
de arquitetura das senhas fictícias** (SC-010), com controle negativo. Nenhuma biblioteca nova. Validação no GitHub real: **revisor** (antes do merge, só o PR da feature verde e "nada aplicado"; **depois do merge e antes do ruleset**, os PRs descartáveis partindo da `main`) e **dono** (fase E).

**Plataforma de Destino**: GitHub Actions (`ubuntu-latest`); Api e Migrator em Linux/containers; o
ambiente local segue o `docker compose`. Nuvem: spec 05b.

**Tipo de Projeto**: web-service (Api REST) + ferramenta de linha de comando (Migrator) +
configuração de CI e de repositório.

**Metas de Desempenho** (Princípio XIII, as do projeto): endpoints comuns p95 ≤ 300 ms e p99 ≤ 1 s;
polling de aparelho p95 ≤ 150 ms; taxa de erro (5xx) < 0,1%. Os endpoints de saúde **não têm meta
de latência própria medida em teste** (um teste de tempo seria frágil e não prova o que importa):
o que se prova é o **custo ao catálogo**. A `viva` não faz consulta; a `pronta` faz **no máximo
uma consulta a cada 5 s por instância**, qualquer que seja o tráfego (SC-008, provado por teste com
uma sonda falsa que **conta as chamadas**, sem medir tempo), e responde em até 2 s com o catálogo
fora (SC-007; o estado reflete a queda ou a volta em até 5 s). Assim a spec **não acrescenta carga
ao pipeline de autenticação** nem ao catálogo sob inundação (R-14).

**Restrições**: nenhum teste, threshold ou aviso pode ser apagado, pulado ou suprimido para a
entrega passar (Integridade das entregas); **o implementador não executa nada contra o GitHub
real** (a aplicação do ruleset e os ensaios são tarefas do dono); nenhum segredo no repositório
(público); nenhuma ação de terceiros; o código novo segue o estilo K&R do arquivo vizinho,
comentários em português, identificadores em inglês.

**Escala/Escopo**: até 150 mil tenants (Princípio XIII). O que muda aqui é independente de tenant,
exceto a verificação do admin fixo, que **percorre todas as bases** (operacional, por entrega).
Alcance: 1 endpoint de saúde (2 rotas), 2 verbos do Migrator, 1 classe extraída, 3 workflows novos
e 2 alterados, 5 scripts de shell, 1 corpo de ruleset, 1 guia, 10 classes de teste novas (uma delas
é o teste de arquitetura das senhas fictícias, SC-010) e 4 auxiliares de teste (`BashLocator`,
`ScriptShell`, `FakeGh`, `StagingTestSupport`).

## Verificação da Constituição

*GATE: passou antes da pesquisa; reavaliada depois do design (resultado abaixo).*

| Princípio | Avaliação |
|---|---|
| **I. Isolamento e catálogo** | Passa, com uma nota. Nenhuma tabela, coluna ou migration. O `seed-staging` grava pelo `AppDbContext` com `TenantScope`, **sob RLS**, como a Api e o `seed-dev`. O catálogo continua sem senha nem hash. **Nota**: o `verify-no-seed-admin` lê **todos os tenants** com o papel migrator (`BYPASSRLS`). É um **processo operacional separado** (como o `migrate`, que o `roles.sql` descreve como o motivo do `BYPASSRLS`), **não** é código interno de tenant, só lê em transação somente leitura e **nunca devolve dado de tenant** (só o nome lógico da base e a contagem). Registrado no Rastreamento de Complexidade. |
| **II. Clean Architecture** | Passa. Nada novo em Domain ou Application. A verificação de saúde fica na **Api** (composição), que referencia a Infrastructure; `SyntheticTenantSeeder` e os verbos ficam no Migrator, como o `SeedDevCommand`. **Nenhum pacote NuGet ou npm novo**: a justificativa exigida é "nenhum" (`MapHealthChecks` é do framework). |
| **III. Vertical slice** | Passa. Nenhum caso de uso novo: saúde é infraestrutura de operação (não regra de negócio) e os verbos são ferramentas, como o `seed-dev`. Sem `IRepository`, `Handler` nem `Specification`. |
| **IV. Domínio rico** | Passa, sem mudança. Os operadores fictícios nascem pelo construtor `Operator(new Email(...), new Password(...), role)`, que já valida (a senha fraca falha **antes** de escrever). |
| **V. Contrato de use case** | Passa. Não é caso de uso. O `503` da saúde não usa o envelope: a constituição só o impõe a 401, 403, 429 e 500 (R-14). |
| **VI. Api fina e autorização** | ⚠ 1 exceção justificada. **Sem controller**: `MapHealthChecks` não tem lógica de Api. **Rota fora de `v1/`**: a seção "Operação" nomeia `/health/live` e `/health/ready`, e a regra específica prevalece. **Comentário XML**: não há `action` para documentar, então o comentário fica no método `MapHealthEndpoints()` (FR-023) e no contrato [health-endpoints.md](contracts/health-endpoints.md). Ver Rastreamento de Complexidade. |
| **VII. Test-First** | Passa, com uma ressalva. Todos os comportamentos de código têm teste que falha primeiro (tabela de fases). **Ressalva**: os endpoints de saúde são anônimos; em vez dos testes 401/403/"outro tenant" (que não se aplicam), provam-se: anônimo responde, `Authorization` inválido não muda o resultado, **os demais endpoints continuam exigindo autenticação** (já coberto) e os **únicos** endpoints sem controller são os de saúde. Os testes de banco do `verify-no-seed-admin` têm o caso de isolamento análogo (base contaminada × limpa, catálogo × tenant). Nomes em português com underscore; xUnit, NSubstitute e Testcontainers apenas. |
| **VIII. Front só apresentação** | Passa. O front **não muda** (só o parágrafo do README sobre o CI e uma linha da tabela de endpoints, exigida pela regra de mudança de rota). |
| **IX. Segurança** | **É o objeto da spec.** Fechado por padrão: o **único** `[AllowAnonymous]` novo são as duas rotas de saúde, já previstas na constituição (justificativa na seção abaixo). Rede: `/health/*` **não** passa pelo nginx. **Autenticação**: o JWT **ignora o cabeçalho `Authorization` só nas duas rotas de saúde** (T011), para que um token válido ou inválido não dispare consulta de autenticação nem mude o resultado (ver "Casos de abuso"); o resto da Api segue como antes. Logs: a falha da pronta registra só o **tipo** da exceção; senhas do seed nunca vão à saída. Banco: a aplicação segue sem DDL; o verificador usa o migrator **somente leitura**. Dependências: a auditoria passa a rodar **também em agenda**. Segredos: senhas do seed só por configuração/variável de ambiente; sem segredo em workflow; varredura de segredos ligada. |
| **X. LGPD** | Passa. **Só dado sintético**, gerado por script: nomes genéricos, e-mails em domínio reservado `.test`, CNPJs inválidos. O `seed-staging` recusa Production e Development. Nenhum dado pessoal real entra, é guardado ou logado. |
| **XI. Auditoria** | Passa. Nenhum caso de uso de escrita novo. O `seed-staging` é ferramenta operacional como o `seed-dev` (não gera evento; a trilha é da spec 07), e a verificação e a saúde só leem. |
| **XII. Permissões** | Passa. Nenhuma permissão nova do catálogo; nenhum endpoint de negócio. Os operadores fictícios usam as roles atuais (`ERole`, legado até a spec 08). |
| **XIII. Escalabilidade** | Passa, com uma exceção operacional. **Api sem estado**: o cache da pronta guarda só um booleano e um instante, **por instância de propósito** (readiness é por instância), sem dado de tenant. **Custo por requisição**: a `viva` zero consultas; a `pronta` ≤ 1 a cada 5 s por instância. **Consultas novas e índices**: ver a tabela abaixo. **Jobs**: nenhum job novo na Api; o verificador processa as bases em **lotes paginados por chave**, com concorrência limitada, uma base com erro **não interrompe as outras**. **Testes de carga**: a spec não cria nem altera caminho crítico (login, pipeline de autenticação, aparelhos, listas, jobs), então não exige cenário k6; o passo k6 curto fica **reservado** como "não ativado" (spec 06). |
| **Operação** (seção) | **Cumpre parcialmente**: `/health/live` e `/health/ready` (catálogo; as bases de tenant não entram), anônimos, sem detalhe interno. **O Redis fica de fora do `ready`**, porque ainda não existe (spec 06): a seção "Operação" o lista como dependência da pronta. Logs estruturados, OpenTelemetry e `xmin` seguem para as specs 06 e 10. Ver Rastreamento de Complexidade. |
| **Fluxo e Quality Gates** | Passa. Mudança de rota: **comentário XML** no mapeamento e **linha na tabela de endpoints** do `src/Frontend/README.md` (marcada como "fora do console"); `types/api.ts` não muda (o front não usa a saúde). O CI continua repetindo build, testes e auditoria. |
| **Branches, ambientes e entrega** | **Implementa**: "Push direto na `main` é proibido (branch protection)", "a `main` DEVE estar sempre pronta" (verificação pós-merge, FR-013a), "só dados sintéticos fora de produção" (`seed-staging`). Nada de AWS, deploy, promoção ou rollback: spec 05b. |
| **Idioma/estilo** | Passa. Artefatos em português; código e banco em inglês; mensagens ao usuário com acento. |

**Consultas novas e índices (Princípio XIII)**

| Consulta | Onde | Índice | Frequência |
|---|---|---|---|
| `SELECT 1 FROM tenant_databases LIMIT 1` (sonda do `ready`) | Api, catálogo, papel `acs_app` | qualquer (leitura de 1 linha; chave primária) | ≤ 1 a cada 5 s por instância |
| `SELECT … FROM operators WHERE id = @fixo OR lower(email) = lower(@fixo) LIMIT 1` | Migrator, cada base de tenant, papel migrator, transação somente leitura | `id` tem a chave primária; **`lower(email)` não tem índice** (o índice único é `(tenant_id, email)`) → varredura de `operators` | uma vez por execução da verificação (por entrega, na 05b) |
| Varredura do esquema `public` do catálogo (`information_schema.columns`) e `SELECT 1 … WHERE lower(<coluna_email>) = …` nas colunas achadas | Migrator, catálogo | tabelas do catálogo são minúsculas (uma linha por base/tenant) | uma vez por execução |
| Leitura paginada de `tenant_databases` (todas as bases) | Migrator, catálogo | chave primária (paginação por `id`, como o `migrate`) | uma vez por execução |

A segunda linha é a exceção registrada em Rastreamento de Complexidade.

**Legado conhecido que esta spec resolve** (a retirada do texto da constituição é por emenda do
dono, fora desta spec): "`main` sem proteção" (parte do item "Entrega") e "Api: sem health checks"
(só os health checks; logs estruturados, OpenTelemetry e `xmin` seguem abertos para as specs 06 e
10).

**Reavaliação depois do design (Fase 1)**: sem violações novas. As ressalvas de Rastreamento de
Complexidade seguem justificadas (rota e documentação da saúde; testes 401/403 que não se aplicam;
o Redis fora do `ready` até a spec 06; leitura entre tenants do verificador; consulta sem índice de
ferramenta operacional).

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261102-171227-delivery-foundation/
├── plan.md              # Este arquivo (saída do /speckit-plan)
├── research.md          # Fase 0: verificações e decisões (R-01 a R-21)
├── data-model.md        # Fase 1: áreas, estados do gate, dados fictícios, identidade fixa, códigos de saída
├── quickstart.md        # Fase 1: validação ponta a ponta e passos manuais do dono
├── contracts/
│   ├── health-endpoints.md           # rotas, respostas, cache, tempo limite, anonimato
│   ├── migrator-cli.md               # os dois verbos novos, configuração e códigos de saída 5 a 8
│   ├── ci-gate.md                    # gatilhos, jobs, regras de avaliação, resumo, nomes
│   ├── repository-protections.md     # corpo do ruleset, scripts, conferência (formato da resposta)
│   └── scheduled-workflows.md        # auditoria de pacotes e conferência automática
├── checklists/requirements.md
└── tasks.md             # Fase 2 (saída do /speckit-tasks, NÃO criado aqui)
```

### Código-fonte (raiz do repositório)

Casos de referência mais parecidos já implementados: **`Migrator/Commands/SeedDevCommand.cs`** (verbo
com recusa por ambiente, validação de senha, escrita pelo `AppDbContext` sob RLS) e
**`SeedDevCommandTests`** (padrão de teste de verbo com Testcontainers); **`DefaultAuthorizationTests`**
e **`ApiFactory`** (testes da Api com `WithSingleton`/`WithFakeTime`); **`PackageAuditConfigTests`**
(testes que leem arquivos de CI).

```text
.github/workflows/
├── ci-gate.yml                        # NOVO: changes, backend, frontend, k6-short ("k6 curto (não ativado)"), ci-gate (PR e push na main)
├── backend-ci.yml                     # gatilho: workflow_call (+ workflow_dispatch); passos INALTERADOS; permissions e timeout
├── frontend-ci.yml                    # idem
├── package-audit.yml                  # NOVO: schedule semanal + workflow_dispatch
└── protections-check.yml              # NOVO: schedule semanal + workflow_dispatch (autenticada com o GITHUB_TOKEN, só leitura)

scripts/                               # NOVO (raiz); *.sh em LF pelo .gitattributes
├── ci/
│   ├── detect-areas.sh                # lista de arquivos (stdin) → backend=, frontend=, docs_only=
│   └── evaluate-gate.sh               # NEEDS_JSON + áreas → tabela no resumo; 0 ou 1
└── github/
    ├── apply-protections.sh           # cria/atualiza o ruleset, liga a varredura de segredos (--dry-run, --disable, --enable)
    ├── check-rules.sh                 # conferência automática: JSON do endpoint de regras (stdin) → 0 ou 1 (nome E integration_id do check)
    ├── check-protections-manual.sh    # conferência à mão: bypass_actors e varredura de segredos
    └── protections/main-ruleset.json  # corpo do ruleset (fonte única do nome do check, do integration_id e das regras)

docs/
└── protecoes-do-repositorio.md        # NOVO: o guia curto (R-17)

src/Backend/
├── AccessControlSystem.Api/
│   ├── Program.cs                                  # + app.MapHealthEndpoints()
│   ├── Extensions/BuilderExtensions.cs             # + AddHealth() (sonda e verificador); OnMessageReceived do JWT ignora o Authorization nas rotas de saúde (HealthEndpointMarker)
│   └── Health/                                     # NOVO
│       ├── ICatalogProbe.cs                        # costura de teste (interna)
│       ├── CatalogDbProbe.cs                       # SELECT 1 FROM tenant_databases LIMIT 1
│       ├── CatalogReadinessHealthCheck.cs          # cache 5 s, uma consulta em voo, tempo limite 2 s
│       ├── HealthEndpointMarker.cs                 # metadado que faz o JWT ignorar o Authorization nas rotas de saúde
│       └── HealthEndpointExtensions.cs             # MapHealthEndpoints(): rotas, GET/HEAD, AllowAnonymous
├── AccessControlSystem.Migrator/
│   ├── MigratorApplication.cs                      # + verbos, códigos 5 a 8, guardas por ambiente, uso
│   ├── Commands/
│   │   ├── SeedDevCommand.cs                       # passa a delegar ao SyntheticTenantSeeder (comportamento igual)
│   │   ├── SeedStagingCommand.cs                   # NOVO
│   │   └── VerifyNoSeedAdminCommand.cs             # NOVO
│   ├── Options/StagingSeedOptions.cs               # NOVO (seção StagingSeed)
│   └── Orchestration/
│       ├── SyntheticTenantSeeder.cs                # NOVO (extraído do SeedDevCommand)
│       ├── SeedAdminFingerprint.cs                 # NOVO: lê id, e-mail, tabela e colunas de new SeedAdmin().UpOperations
│       ├── FixedAdminVerifier.cs                   # NOVO: leitura somente leitura por base + varredura do catálogo
│       └── CatalogRegistry.cs                      # + listagem de todas as bases (paginada por chave)
└── AccessControlSystem.Tests/
    ├── Api/HealthEndpointsTests.cs                 # NOVO: viva, pronta, 503, tempo limite, cache, anonimato, resposta mínima
    ├── Api/OnlyHealthEndpointsAreAnonymousTests.cs # NOVO: únicos endpoints sem controller
    ├── Migrator/
    │   ├── SeedStagingCommandTests.cs              # NOVO
    │   ├── SeedAdminFingerprintTests.cs            # NOVO
    │   ├── VerifyNoSeedAdminCommandTests.cs        # NOVO
    │   ├── StagingTestSupport.cs                   # NOVO: ambiente isolado e base contaminada de verdade (MigrateAsync sem a política)
    │   └── MigratorProgramTests.cs                 # + casos de uso/verbos novos (sem alterar os existentes)
    ├── Support/
    │   ├── FakeGh.cs                               # NOVO: gh falso (GH_BIN) que registra chamadas e devolve respostas enlatadas
    │   ├── BashLocator.cs                          # NOVO: acha o bash (ACS_BASH, Git Bash, PATH no Linux); recusa o WSL
    │   └── ScriptShell.cs                          # NOVO: roda script (cwd = raiz, caminho relativo, stdin, env); exige bash e jq; NewScratchDirectory() → obj/script-tests/<Guid>/ do projeto de testes (ignorado pelo git)
    └── Architecture/
        ├── FictitiousPasswordsNotInRepoTests.cs    # NOVO (SC-010): nenhuma senha fictícia literal em código de produção, appsettings*.json, docker-compose*.yml e .github/ (os testes ficam de fora)
        ├── BashLocatorTests.cs                     # NOVO: regras de localização do bash (função pura, tabela)
        ├── CiWorkflowsConfigTests.cs               # NOVO: permissions, timeouts, gatilhos, sem terceiros, nomes do gate e do k6
        ├── CiScriptsTests.cs                       # NOVO: detect-areas, evaluate-gate, check-rules (tabelas)
        └── RepositoryProtectionsConfigTests.cs     # NOVO: corpo do ruleset (inclui integration_id), scripts, guia

README.md / CLAUDE.md                               # + verbos novos, health checks, link do guia
src/Frontend/README.md                              # texto do CI (linha 84) e linha da tabela de endpoints
```

**Decisão de estrutura**: a base de código existente é reaproveitada (Princípio III); o que é novo
e fora do `src/` (scripts de CI e de repositório, guia) ganha duas pastas de raiz, `scripts/` e
`docs/`, porque não pertence nem ao backend nem ao frontend e o guia precisa viver onde alguém o
procura quando a proteção quebrar.

## Segurança e dados pessoais

**Permissões novas do catálogo**: nenhuma.

**Matriz de autorização (endpoint × permissão)**

| Endpoint | Autenticação | Permissão | Justificativa |
|---|---|---|---|
| `GET /health/live` (e `HEAD`) | **anônimo** (`AllowAnonymous`) | nenhuma | ver abaixo |
| `GET /health/ready` (e `HEAD`) | **anônimo** (`AllowAnonymous`) | nenhuma | ver abaixo |
| os 8 de `v1/account` e os demais | como hoje (login anônimo; o resto exige autenticação e a permissão do `EndpointCatalog`) | inalterada | nada muda |

**Justificativa dos endpoints anônimos (Princípio IX, "Fechado por padrão")**

A constituição determina que "endpoint anônimo só com `[AllowAnonymous]` explícito e justificado no
`plan.md` (hoje: login e health checks)" e que a Api "DEVE expor `/health/live` e `/health/ready`,
anônimos e sem detalhes internos". As duas rotas são anônimas porque **quem as chama não tem
credencial**: o balanceador de carga e o deploy da 05b (verificam se a versão nova está saudável
antes de mandar tráfego), o `docker compose` e as verificações de funcionamento. Exigir token
tornaria a saúde inútil justamente quando o login está quebrado. O risco é aceito porque:

1. **Nada interno vaza**: a resposta é só `Healthy`/`Unhealthy` com `200`/`503` (sem versão, nome
   de servidor ou de base, texto de exceção, lista de verificações ou dado de tenant). Testado
   (resposta exata).
2. **Não dá para usá-las contra a Api**: a `viva` não consulta nada; a `pronta` consulta o catálogo
   **no máximo 1 vez a cada 5 s por instância** (cache com uma só consulta em voo), então uma
   inundação anônima não esgota o pool de conexões nem derruba usuários reais (R-14).
3. **Superfície mínima**: só `GET` e `HEAD`, rotas fixas, sem corpo, sem parâmetro; o nginx do
   console não as encaminha; **um teste garante que são os únicos endpoints anônimos sem controller**
   (FR-039), de modo que nenhum outro endpoint nasça aberto por engano.
4. **A política de fallback continua fechando todo o resto**: o teste existente
   (`DefaultAuthorizationTests`) segue passando.
5. **O JWT ignora o `Authorization` só nessas duas rotas** (`HealthEndpointMarker`, T011): sem isso,
   um token qualquer enviado a `/health/live` faria o pipeline de autenticação consultar o
   catálogo e o banco (a `viva` deixaria de ser "sem dependência") e um token inválido poderia
   derrubar a `pronta` com `401`. A mudança é **restrita por metadado de endpoint** (só as duas
   rotas) e provada por teste: token válido ou inválido em `/health/*` não muda a resposta nem
   consulta nada, e um endpoint protegido segue aceitando token válido e recusando inválido, como
   antes.

**Casos de abuso considerados**

| Abuso | Defesa nesta spec |
|---|---|
| Inundação anônima de `/health/ready` para esgotar o pool do catálogo | cache de 5 s, uma consulta em voo, tempo limite de 2 s; custo por instância constante (R-14) |
| Ler informação interna pela resposta de saúde | resposta mínima; sem JSON; teste da resposta exata; log só com o tipo da exceção |
| `Authorization` (válido ou inválido) enviado a `/health/*` para forçar consulta de autenticação ou mudar o resultado | o JWT ignora o cabeçalho **só nas duas rotas** (`HealthEndpointMarker`, T011); testes: token válido ou inválido em `/health/*` não muda nada e não consulta nada; endpoint protegido segue aceitando válido e recusando inválido |
| Verificação "verde" porque um job foi pulado/cancelado/expirou | job `ci-gate` com `if: always()` confere o resultado real; só `success` ou dispensa explícita passam (R-02, R-04) |
| PR "esperando para sempre" ou verde sem rodar | gate sem filtro de paths; fechado por padrão para arquivo fora das áreas; tempo limite em todo job |
| PR de fork roubando segredos ou alterando o repositório | `pull_request` (nunca `_target`), `permissions: contents: read`, nenhum segredo, sem ação de terceiros; teste de configuração |
| Alguém (inclusive o dono) enviando direto, forçando ou apagando a `main` | ruleset sem bypass; conferência automática semanal; conferência à mão (`bypass_actors`) |
| Renomear o `ci-gate` e a exigência cair em silêncio | teste de PR (nome do job × `main-ruleset.json`); conferência automática; guia |
| Segredo enviado por engano ao repositório público | varredura de segredos com bloqueio no envio (script, guia, conferência à mão) |
| Administrador de senha fixa presente em homologação/produção | `verify-no-seed-admin` (qualquer dado fixo, base inacessível ou divergência reprova); o `seed-staging` não usa o e-mail fixo |
| `seed-staging` rodando em produção ou desenvolvimento | recusa **antes** de ler a configuração, código `5`, nada escrito |
| Senha fictícia fraca ou vazada em log/saída | validada pelo value object `Password` antes de escrever; o valor nunca vai à saída; teste |
| Agendamento (auditoria e conferência) parando em silêncio | guia explica a desativação por 60 dias e como reativar |
| Falha de rede na auditoria passando como "sem vulnerabilidades" | `NU1900` como erro no CI e `npm audit` sem acesso reprovam |
| Ensaio de proteção alterando a `main` de verdade | só depois da conferência; SHA anotado antes; mudança desfazível (R-13) |

**Dados pessoais tocados**: **nenhum real**. Os dados fictícios usam nomes genéricos, e-mails em
`.test` (domínio reservado) e CNPJs inválidos (Princípio X). As mensagens do verificador mostram o
nome lógico da base e a contagem, nunca e-mail, identificador de operador nem senha.

**Eventos de auditoria gerados**: nenhum. Nenhum caso de uso de escrita novo: o `seed-staging` é
ferramenta operacional (como o `seed-dev`), e a saúde e o verificador só leem. (A trilha de
auditoria é a spec 07.)

## Rastreamento de Complexidade

> Violações da constituição que precisam de justificativa.

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| **VI**: endpoint **sem controller**, **fora de `v1/`** e **sem comentário XML de `action`** | A seção "Operação" nomeia as duas rotas exatas; `MapHealthChecks` é o mecanismo do framework (sem pacote novo) e não tem `action`. O comentário de documentação vai no `MapHealthEndpoints()` e no contrato | Um `HealthController` com `v1/health/...` quebraria o nome que a constituição manda e exigiria entradas no `EndpointCatalog` e testes 401/403 que não se aplicam a rota anônima |
| **VII**: os endpoints de saúde não têm os testes 401/403/"outro tenant" | A rota é anônima por exigência da constituição; o par desses testes é "anônimo responde", "token inválido não muda o resultado" e "únicos endpoints anônimos sem controller" | Forçar 401/403 contradiz o requisito de a saúde ser anônima |
| **Operação**: o `ready` **não** verifica o Redis (cumprimento parcial da seção "Operação") | O Redis ainda não existe no projeto (spec 06); verificar algo que não existe seria inventar uma dependência. A pronta cobre o que existe hoje: o catálogo | Incluir uma sonda de Redis agora exigiria subir um Redis só para ela e deixaria a pronta sempre indisponível. **Revisão**: spec 06 acrescenta o Redis à `ICatalogProbe`/pronta junto com o próprio Redis |
| **I**: o `verify-no-seed-admin` lê todos os tenants (papel migrator, `BYPASSRLS`) | É a **única** forma de conferir **todas** as bases (o `acs_app` sob RLS não vê nada sem tenant). É processo operacional separado (como o `migrate`), só lê em transação somente leitura e nunca devolve dado de tenant | Rodar como `acs_app` não enxerga os operadores; criar um papel novo só para isso adiciona privilégio e bootstrap na 05b sem ganho |
| **XIII**: consulta sem índice (`lower(email)`) na verificação | Ferramenta operacional por entrega, fora do caminho de requisição, com `statement_timeout`, paralelismo limitado e transação somente leitura; o índice de `(tenant_id, email)` não atende busca entre tenants | Criar índice funcional global em `operators` mudaria o esquema (migration) e custaria escrita no caminho quente por uma verificação que roda por entrega. **Revisão**: spec 06, se as bases compartilhadas crescerem |
