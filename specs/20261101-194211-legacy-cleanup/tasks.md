# Tarefas: Limpeza de legado

**Entrada**: Documentos de design em `/specs/20261101-194211-legacy-cleanup/`

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Testes**: obrigatórios (Princípio VII, TDD). Em toda fase o teste, ou a verificação que o substitui, vem **antes** e é visto falhando (Red); só então vem a mudança de produção (Green). Nas renomeações, "Red por não compilar" é aceito.

**Organização**: por **fase** (0, A, B1 a B5, C, D, E), cada uma terminando num **checkpoint** com build e testes verdes e os números esperados, para revisão e commit separados. As histórias da spec rotulam as tarefas (`[US1]` a `[US4]`); a fase E não tem rótulo.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[US1]** nomes de repositório e de domínio (P1) · **[US2]** textos do console (P1) · **[US3]** mensagens da API (P2) · **[US4]** sistema sobe sem a `PasswordSaltKey` (P2)
- Caminhos relativos à raiz do repositório. Os números de linha são de 2026-11-01 (antes de qualquer fase); as renomeações trocam o texto na mesma linha, então a numeração se mantém. Se divergir, localize pelo texto citado.
- `TE-nnn` aponta uma linha da tabela **"Testes existentes afetados"** (no fim deste arquivo); `TN-nn`, a lista **"Testes novos"**.

## Regras fixas (valem para todas as tarefas)

1. **Sem commits.** O agente não faz commit nem push. Cada checkpoint é um ponto de revisão e de commit **separado**, feito pelo revisor (sugestões no fim).
2. **Parada em cada checkpoint.** Em cada checkpoint (0, A, B1, B2, B3, B4, B5, C, D, E): parar, reportar os números e a lista de arquivos alterados, e aguardar o commit do revisor antes de seguir. Nunca faça commit. Os "o diff da fase toca só…" dos checkpoints são conferidos com `git status --short` contra o último commit de checkpoint.
3. **Integridade das entregas** (constituição): não apague, pule nem enfraqueça teste. Só mudam os testes da tabela "Testes existentes afetados", e só como ali descrito. Se outro teste precisar mudar, ou qualquer verificação falhar, **pare e reporte a saída real**.
4. **Edição**: use as ferramentas de edição (preservam CRLF e acentos em UTF-8). **Nunca** `sed -i` em arquivo versionado: o `sed` do Git Bash descarta o `CR` dos arquivos CRLF.
5. Sem migration, sem pacote novo ou atualizado, sem editar a constituição (FR-020). O `.env` real do dono não é tocado.
6. **Gates canônicos**:
   - **GATE-BACK**: `cd src/Backend && dotnet build Backend.slnx && dotnet test Backend.slnx` (Docker ligado). Esperado: `0 Aviso(s)`, `0 Erro(s)` e `Aprovado! – Com falha: 0, Aprovado: N, Ignorado: 0, Total: N`.
   - **GATE-FRONT**: `cd src/Frontend && npm run typecheck && npm test && npm run build`. Esperado: `Test Files 26 passed (26)` e `Tests 238 passed (238)`.

## Números esperados em cada checkpoint

| Checkpoint | Backend (N aprovados) | Front (aprovados) | O que muda |
|---|---|---|---|
| 0 (linha de base) | **429** | **238** | nada |
| A | **430** | **238** | +1 teste novo (`UserTests`, FR-007) |
| B1 a B5 | **430** | **238** | só nomes de método |
| C | **430** | **238** | 3 textos de API; 4 asserções (2 aqui, 2 na D) |
| D | **430** | **238** | 17 textos de UI; +2 asserções em testes existentes |
| E | **430** | **238** | documentação e conferência final |

Em todo checkpoint: build com **0 avisos e 0 erros**, 0 falhas, 0 ignorados.

---

## Fase 0: Preparação (linha de base)

**Objetivo**: provar o ponto de partida antes de qualquer mudança. Não há fase fundacional: as fases A a E são independentes entre si (exceto C depois de B).

- [X] T001 Confirmar a branch e a árvore: `git branch --show-current` deve imprimir `20261101-194211-legacy-cleanup`, e `git status --short` pode estar **vazio** (artefatos da spec já commitados) ou listar só `specs/20261101-194211-legacy-cleanup/`. Qualquer outra alteração: parar e reportar.
- [X] T002 Confirmar o Docker ligado (`docker info --format '{{.ServerVersion}}'`) e ver a stack local do dono (`docker ps -a --filter name=acs-`; hoje está parada). Nada é recriado antes da tarefa T097.
- [X] T003 Linha de base do backend: rodar GATE-BACK em `src/Backend`. Esperado: 0 avisos, 0 erros, **429 aprovados**. Registrar a saída.
- [X] T004 [P] Linha de base do front: rodar GATE-FRONT em `src/Frontend`. Esperado: typecheck e build ok, **238 aprovados** em 26 arquivos. Registrar a saída.
- [X] T005 **Checkpoint 0**: números 429 / 238 conferidos. Se forem outros, anotar e usá-los como referência do SC-001 (a diferença +1 da fase A vale sobre o número encontrado). Falha em T003 ou T004: parar. Em qualquer caso, parar e reportar os números (regra 2).

---

## Fase A: `User.Activate()` e remoção da `PasswordSaltKey` (US1 e US4)

**Objetivo**: renomear `User.Active()` com teste antes, e fazer o ambiente subir sem a `PasswordSaltKey`.

**Teste independente**: `UserTests` passa (1 teste); `docker compose --env-file <sem PASSWORD_SALT_KEY> config` passa de exit 1 para exit 0 ([contracts/configuration.md](contracts/configuration.md)).

### A.1: `User.Active()` → `Activate()` (US1, FR-007)

- [X] T006 [US1] (Red) Criar o teste novo **TN-01** em `src/Backend/AccessControlSystem.Tests/Domain/Entities/UserTests.cs`, no estilo de `src/Backend/AccessControlSystem.Tests/Domain/Entities/OperatorTests.cs` (namespace de arquivo `AccessControlSystem.Tests.Domain.Entities;`, `public class UserTests`, `[Fact]`, usings de `AccessControlSystem.Domain.Contexts.UserContext.Entities` e `AccessControlSystem.Domain.Contexts.UserContext.ValueObjects`). Método `Activate_reativa_o_usuario_desativado`: montar `new User(new Name("Maria Silva"), new Telephone(), new PasswordAccess(), new Rg(), new Cpf())` (o `Name` exige entre 3 e 100 caracteres; `Telephone`, `PasswordAccess`, `Rg` e `Cpf` aceitam o construtor padrão vazio; o usuário nasce com `IsActive = true`), chamar `user.Deactivate()`, afirmar `Assert.False(user.IsActive)`, chamar `user.Activate()` e afirmar `Assert.True(user.IsActive)`. Nenhum outro arquivo muda nesta tarefa.
- [X] T007 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve **falhar** com `CS1061` (`User` não contém uma definição para `Activate`). Red por não compilar é aceito. Se compilar, parar: o método já existe ou o teste não foi escrito.
- [X] T008 [US1] (Green) Em `src/Backend/AccessControlSystem.Domain/Contexts/UserContext/Entities/User.cs` (linha 81), trocar `public void Active() {` por `public void Activate() {`. O corpo (`IsActive = true;`) e o par `Deactivate()` ficam como estão. Confirmar que não há chamador: `git grep -nE "\.Active\(\)|void Active\(" -- src` não pode mais listar nada.
- [X] T009 [US1] (Green visto) `cd src/Backend && dotnet test Backend.slnx --filter "FullyQualifiedName~UserTests"` → **1 aprovado**.

### A.2: `PasswordSaltKey` (US4, FR-015 a FR-018)

- [X] T010 [US4] (Red) Rodar o bloco **1b** do [quickstart.md](quickstart.md) (arquivo temporário feito de `.env.example`, nunca do `.env` real): `docker compose --env-file "$TMP/env.sem-salt" config --quiet` deve dar **exit 1** com `required variable PASSWORD_SALT_KEY is missing a value`, e o arquivo `env.com-salt` deve dar exit 0. Registrar. Apagar o diretório temporário no fim.
- [X] T011 [P] [US4] Em `src/Backend/AccessControlSystem.Api/appsettings.json`, remover a linha 11 (`"PasswordSaltKey": ""`) **e** a vírgula final da linha 10 (`"JwtPrivateKey": "",` vira `"JwtPrivateKey": ""`), mantendo o JSON válido. `Secrets:JwtPrivateKey` não muda.
- [X] T012 [P] [US4] Em `docker-compose.yml`, remover a linha 23 (`Secrets__PasswordSaltKey: ${PASSWORD_SALT_KEY:?defina PASSWORD_SALT_KEY no .env}`). O bloco `x-backend-environment` segue com `Secrets__JwtPrivateKey`.
- [X] T013 [P] [US4] Em `.env.example`, remover as linhas 20 e 21 (o comentário `# PASSWORD_SALT_KEY: sem uso no código (legado), mas o compose ainda a exige...` e `PASSWORD_SALT_KEY=`).
- [X] T014 [P] [US4] Em `README.md` (raiz): linha 15, trocar `# preencha JWT_PRIVATE_KEY e PASSWORD_SALT_KEY (veja o arquivo)` por `# preencha JWT_PRIVATE_KEY (veja o arquivo)`; linha 82, remover da lista de variáveis o item `` `PASSWORD_SALT_KEY` (legado, sem uso no código mas ainda exigida), ``, preservando as demais variáveis e a pontuação da frase.
- [X] T015 [US4] (Green visto) Repetir o bloco 1b: **exit 0** nos dois arquivos (a variável sobrando é ignorada); `node -e "JSON.parse(require('fs').readFileSync('src/Backend/AccessControlSystem.Api/appsettings.json','utf8'))"` sem erro; `git grep -nE "PasswordSalt|PASSWORD_SALT" -- . ':!specs' ':!.specify'` → **0 linhas**; `git status --short` não lista `.env` (é gitignorado e não foi tocado).
- [X] T016 [US1] [US4] **Checkpoint A**: GATE-BACK → 0 avisos, 0 erros, **430 aprovados**; `cd src/Frontend && npm test` → **238 aprovados**. `git status --short` (contra o último commit de checkpoint) lista só: `User.cs`, `UserTests.cs` (novo, `??`), `appsettings.json`, `docker-compose.yml`, `.env.example`, `README.md`. Nenhum teste existente foi alterado nesta fase. Parar e aguardar o commit do revisor (regra 2).

---

## Fase B1: `GetUserById` → `GetOperatorByIdAsync` (US1)

**Objetivo**: o método que devolve `Operator?` por identificador passa a dizer isso, em 5 use cases: Activate, Deactivate, GetMe, Update, UpdateMe. Assinatura, retorno e corpo não mudam.

**Teste independente**: GATE-BACK verde (430) e a reversão (T030) sem diferença: os testes só mudaram de **nome**.

**Regra dos testes**: trocar `GetUserById` por `GetOperatorByIdAsync` nas chamadas (`Returns`, `Received`, `DidNotReceive`, `repository.`) e nos nomes de teste que começam com `GetUserById_` (o resto da frase fica **exato**). Nenhuma asserção, dado ou cenário muda. Linhas da tabela: as com `B1` na coluna Fase.

- [X] T017 [P] [US1] (Red) Testes de **Activate** (TE-001 a TE-003 e TE-034 a TE-037): `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Activate/ActivateHandlerTests.cs` e `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs`. Renomear os testes `GetUserById_devolve_o_operador` → `GetOperatorByIdAsync_devolve_o_operador` e `GetUserById_null_quando_nao_existe` → `GetOperatorByIdAsync_null_quando_nao_existe`.
- [X] T018 [P] [US1] (Red) Testes de **Deactivate** (TE-004 a TE-006 e TE-038 a TE-041): `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Deactivate/DeactivateHandlerTests.cs` (inclui a ajuda `GivenExistingOperator`) e `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs`. Mesmos 2 nomes de teste de T017.
- [X] T019 [P] [US1] (Red) Testes de **GetMe** (TE-007 a TE-009 e TE-042 a TE-044): `src/Backend/AccessControlSystem.Tests/Application/AccountContext/GetMe/GetMeHandlerTests.cs` e `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs`. Renomear `GetUserById_devolve_o_operador_com_dados_completos` → `GetOperatorByIdAsync_devolve_o_operador_com_dados_completos` e `GetUserById_null_quando_nao_existe` → `GetOperatorByIdAsync_null_quando_nao_existe`. Em `Operador_nao_encontrado_devolve_401` (TE-008) só a chamada muda agora; a asserção do texto vem na fase C.
- [X] T020 [P] [US1] (Red) Testes de **Update**: handler (TE-011, TE-012, TE-013: ajuda `GivenExistingOperator` e testes que chamam `GetUserById`) em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Update/UpdateHandlerTests.cs`; repositório (TE-045, TE-046, TE-049 a TE-052, TE-054) em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs`, onde só os 2 testes `GetUserById_...` mudam de nome. Os usos de `CheckIfEmailIsNotUsed` **ficam** para a fase B5.
- [X] T021 [P] [US1] (Red) Testes de **UpdateMe**: handler (TE-017, TE-018, TE-019) em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs`; repositório (TE-055, TE-056, TE-059 a TE-062, TE-064) em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs`, com os mesmos 2 nomes `GetUserById_...`. `CheckIfEmailIsNotUsed` fica para a B5; a asserção do `401` (TE-019), para a fase C.
- [X] T022 [P] [US1] (Red) Isolamento de tenant (TE-081, TE-082, TE-084, TE-085): em `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantIsolationTests.cs` trocar as chamadas `GetUserById` das linhas 55 e 101; em `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs`, das linhas 49, 50, 73 e 74. As chamadas `GetOperatorList` (linhas 38; 30, 47 e 48) **ficam** para a B3.
- [X] T023 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve **falhar** com `CS1061` (`IRepository` não contém `GetOperatorByIdAsync`) nos arquivos de teste. Se compilar, parar.
- [X] T024 [P] [US1] (Green) **Activate**: trocar `GetUserById` por `GetOperatorByIdAsync` em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Activate/Contracts/IRepository.cs` (linha 6), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Activate/Handler.cs` (linha 24) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Activate/Repository.cs` (linha 14). Só o nome do método muda.
- [X] T025 [P] [US1] (Green) **Deactivate**: mesma troca em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Deactivate/Contracts/IRepository.cs` (linha 6), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Deactivate/Handler.cs` (linha 25) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Deactivate/Repository.cs` (linha 15). `IsLastActiveAdminAsync` **não** muda.
- [X] T026 [P] [US1] (Green) **GetMe**: mesma troca em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/GetMe/Contracts/IRepository.cs` (linha 6), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/GetMe/Handler.cs` (linha 21) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/GetMe/Repository.cs` (linha 13).
- [X] T027 [P] [US1] (Green) **Update**: mesma troca em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Update/Contracts/IRepository.cs` (linha 8), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Update/Handler.cs` (linha 27) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Update/Repository.cs` (linha 17). `CheckIfEmailIsNotUsed` (linhas 10, 52 e 32) **fica** para a B5.
- [X] T028 [P] [US1] (Green) **UpdateMe**: mesma troca em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Contracts/IRepository.cs` (linha 8), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Handler.cs` (linha 27) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/UpdateMe/Repository.cs` (linha 29). `CheckIfEmailIsNotUsed` (linhas 10, 39 e 33) **fica** para a B5.
- [X] T029 [US1] (Green visto) GATE-BACK em `src/Backend`: 0 avisos, 0 erros, **430 aprovados**.
- [X] T030 [US1] Provas da B1: (a) **reversão** do roteiro V2a do [quickstart.md](quickstart.md) (com `BASE` = commit anterior à B1, ou `main` se nada foi commitado desde que a branch saiu dela): nenhuma linha `MUDOU ALÉM DO NOME`; (b) `git grep -n "GetUserById" -- src` → **0 linhas**.
- [X] T031 [US1] **Checkpoint B1**: GATE-BACK **430**, `cd src/Frontend && npm test` **238**. `git status --short` (contra o último commit de checkpoint) lista só os 15 arquivos de produção de T024 a T028 e os 12 arquivos de teste de T017 a T022. Parar e aguardar o commit do revisor (regra 2).

---

## Fase B2: `GetUserByEmailAsync` → `GetOperatorByEmailAsync` (US1)

**Objetivo**: a busca do login por e-mail passa a dizer que devolve um operador. Use case: Authenticate.

**Teste independente**: GATE-BACK verde (430) e reversão sem diferença.

- [X] T032 [US1] (Red) Testes de **Authenticate** (TE-022 a TE-026 e TE-065 a TE-068): em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` trocar as chamadas `GetUserByEmailAsync` por `GetOperatorByEmailAsync`; em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` trocar as chamadas e renomear `GetUserByEmailAsync_null_quando_nao_existe` → `GetOperatorByEmailAsync_null_quando_nao_existe` e `GetUserByEmailAsync_devolve_o_operador_com_a_senha_utilizavel` → `GetOperatorByEmailAsync_devolve_o_operador_com_a_senha_utilizavel`. As asserções (inclusive a do `SeedAdmin` e a de isolamento) não mudam.
- [X] T033 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve falhar com `CS1061` (`IRepository` não contém `GetOperatorByEmailAsync`).
- [X] T034 [US1] (Green) Trocar `GetUserByEmailAsync` por `GetOperatorByEmailAsync` em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Authenticate/Contracts/IRepository.cs` (linha 6), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Authenticate/Handler.cs` (linha 29) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Authenticate/Repository.cs` (linha 13). O hash e a verificação de senha do login **não** mudam.
- [X] T035 [US1] (Green visto) GATE-BACK: 0 avisos, 0 erros, **430 aprovados**.
- [X] T036 [US1] Provas da B2: reversão V2a sem `MUDOU ALÉM DO NOME` (cumulativa desde o início da B1) e `git grep -n "GetUserByEmailAsync" -- src` → **0 linhas**.
- [X] T037 [US1] **Checkpoint B2**: GATE-BACK **430**; `npm test` do front **238**. `git status --short` (contra o último commit de checkpoint) lista só 3 arquivos de produção e 2 de teste. Parar e aguardar o commit do revisor (regra 2).

---

## Fase B3: `GetOperatorList` → `GetOperatorListAsync` (US1)

**Objetivo**: a listagem paginada ganha o sufixo `Async`. Use case: Get. Assinatura (`int page, int size, EActiveFilter isActive, CancellationToken`) e retorno (`Task<PagedResponse<OperatorItem>>`) não mudam.

**Teste independente**: GATE-BACK verde (430) e reversão sem diferença.

- [X] T038 [US1] (Red) Testes (TE-027 a TE-030, TE-069 a TE-076, TE-080, TE-083, TE-084): trocar `GetOperatorList` por `GetOperatorListAsync` nas chamadas de `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Get/GetHandlerTests.cs`, `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Get/GetRepositoryTests.cs`, `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/TenantIsolationTests.cs` (linha 38) e `src/Backend/AccessControlSystem.Tests/Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs` (linhas 30, 47 e 48). Nenhum nome de teste cita o método; nada mais muda.
- [X] T039 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve falhar com `CS1061` (`IRepository` não contém `GetOperatorListAsync`).
- [X] T040 [US1] (Green) Trocar `GetOperatorList` por `GetOperatorListAsync` em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Get/Contracts/IRepository.cs` (linha 6), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Get/Handler.cs` (linha 24) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Get/Repository.cs` (linha 15). A consulta (`AsNoTracking`, filtro, paginação, projeção) não muda.
- [X] T041 [US1] (Green visto) GATE-BACK: 0 avisos, 0 erros, **430 aprovados**.
- [X] T042 [US1] Provas da B3: reversão V2a sem diferença e `git grep -nE "GetOperatorList\b" -- src` → **0 linhas** (`GetOperatorListAsync` não casa).
- [X] T043 [US1] **Checkpoint B3**: GATE-BACK **430**; `npm test` do front **238**. `git status --short` (contra o último commit de checkpoint) lista só 3 arquivos de produção e 4 de teste. Parar e aguardar o commit do revisor (regra 2).

---

## Fase B4: `AnyAsync` (Create) → `EmailExistsAsync` (US1)

**Objetivo**: a pergunta "este e-mail já existe?" do cadastro passa ao formato `{Algo}ExistsAsync`. Use case: Create. **Cuidado**: o `AnyAsync` do **EF Core** (sempre com lambda, ex.: `.AnyAsync(x => ...)`) **não muda**: 12 ocorrências, em `ActiveSessionChecker.cs`, `SeedDevCommand.cs`, dentro dos repositórios e em testes ([contracts/repository-renames.md](contracts/repository-renames.md), seção 3). O campo `_Repository` do handler **não** é renomeado (achado F-01).

**Teste independente**: GATE-BACK verde (430) e reversão sem diferença.

- [X] T044 [US1] (Red) Testes de **Create** (TE-031 a TE-033 e TE-077 a TE-079): em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Create/CreateHandlerTests.cs` trocar `_repository.AnyAsync(...)` por `_repository.EmailExistsAsync(...)` nas linhas 18, 45 e 84 (esta última é `Received(1).AnyAsync`); em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Create/CreateRepositoryTests.cs` trocar `repository.AnyAsync(...)` (linhas 31 e 43) e `new Repository(context).AnyAsync(email)` (linha 113) e renomear `AnyAsync_false_quando_o_email_nao_existe` → `EmailExistsAsync_false_quando_o_email_nao_existe` e `AnyAsync_true_depois_de_salvar` → `EmailExistsAsync_true_depois_de_salvar`. **Não** tocar nas linhas 88 e 90 (`.AsNoTracking().AnyAsync(o => ...)`, do EF) nem nos testes `SaveAsync_...`.
- [X] T045 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve falhar com `CS1061` (`IRepository` não contém `EmailExistsAsync`).
- [X] T046 [US1] (Green) Trocar o nome do **método do repositório** (e só dele) em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Create/Contracts/IRepository.cs` (linha 9: `Task<bool> AnyAsync(string email, ...)` → `Task<bool> EmailExistsAsync(string email, ...)`), `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Create/Handler.cs` (linha 39: `var exists = await _Repository.AnyAsync(email, cancellationToken);` → `EmailExistsAsync`; a condição `if (exists)` e o `409` ficam) e `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Create/Repository.cs` (linha 17: só o nome do método; o corpo, que usa o `AnyAsync` do EF na linha 18, **não muda**). `SaveAsync` fica.
- [X] T047 [US1] (Green visto) GATE-BACK: 0 avisos, 0 erros, **430 aprovados**.
- [X] T048 [US1] Provas da B4: reversão V2a sem diferença; `git grep -nE "AnyAsync" -- src/Backend | grep -vE "AnyAsync\([a-z]+ =>"` → **0 linhas**; e `git grep -nE "AnyAsync\([a-z]+ =>" -- src/Backend | wc -l` → **12** (os do EF, intactos).
- [X] T049 [US1] **Checkpoint B4**: GATE-BACK **430**; `npm test` do front **238**. `git status --short` (contra o último commit de checkpoint) lista só 3 arquivos de produção e 2 de teste. Parar e aguardar o commit do revisor (regra 2).

---

## Fase B5: `CheckIfEmailIsNotUsed` → `EmailExistsAsync(email, exceptId)` (US1)

**Objetivo**: o `bool` que hoje se chama "não usado" e devolve `true` quando o e-mail **está** em uso por **outro** operador passa a dizer a verdade. Use cases: Update e UpdateMe. **A condição dos handlers não muda e não ganha `!`.** O parâmetro `id` passa a se chamar `exceptId`.

**Teste independente**: GATE-BACK verde (430), reversão sem diferença e a conferência da condição (T055).

- [X] T050 [P] [US1] (Red) Testes de **Update** (TE-010, TE-014, TE-015 no handler; TE-047, TE-048, TE-053, TE-054 no repositório): em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/Update/UpdateHandlerTests.cs` trocar `CheckIfEmailIsNotUsed` por `EmailExistsAsync` (construtor linha 19, e nos testes das linhas 77 e 108, em `Returns`/`DidNotReceive`); em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` trocar as chamadas e renomear `CheckIfEmailIsNotUsed_true_quando_outro_operador_tem_o_email` → `EmailExistsAsync_true_quando_outro_operador_tem_o_email`, `CheckIfEmailIsNotUsed_false_para_o_proprio_email` → `EmailExistsAsync_false_para_o_proprio_email` e `CheckIfEmailIsNotUsed_considera_so_o_tenant_corrente` → `EmailExistsAsync_considera_so_o_tenant_corrente`. Nenhum `Returns(true/false)` ou asserção muda.
- [X] T051 [P] [US1] (Red) Testes de **UpdateMe** (TE-016, TE-020, TE-021 no handler; TE-057, TE-058, TE-063, TE-064 no repositório): mesma troca em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` (construtor linha 19 e linhas 53 e 84) e em `src/Backend/AccessControlSystem.Tests/Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs`, com os mesmos 3 nomes de teste de T050.
- [X] T052 [US1] (Red visto) `cd src/Backend && dotnet build Backend.slnx` deve falhar com `CS1061` (`IRepository` não contém `EmailExistsAsync` com `(string, Guid, CancellationToken)`).
- [X] T053 [P] [US1] (Green) **Update**: em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Update/Contracts/IRepository.cs` (linha 10) `Task<bool> CheckIfEmailIsNotUsed(string email, Guid id, CancellationToken cancellationToken = default);` → `Task<bool> EmailExistsAsync(string email, Guid exceptId, CancellationToken cancellationToken = default);`; em `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/Update/Repository.cs` (linha 32) o mesmo nome e parâmetro, e no corpo `x.Id != id` → `x.Id != exceptId` (o resto do `AnyAsync` do EF fica); em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Update/Handler.cs` (linha 52) `CheckIfEmailIsNotUsed(newEmail.Address, request.Id, cancellationToken)` → `EmailExistsAsync(newEmail.Address, request.Id, cancellationToken)`.
- [X] T054 [P] [US1] (Green) **UpdateMe**: a mesma troca em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Contracts/IRepository.cs` (linha 10), `src/Backend/AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/UpdateMe/Repository.cs` (linha 33, e `x.Id != id` → `x.Id != exceptId` no corpo) e `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Handler.cs` (linha 39).
- [X] T055 [US1] Conferir que a **condição continua idêntica, sem `!`**: `git diff -U0 -- src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/Update/Handler.cs src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Handler.cs | grep -E '^[-+].*(CheckIfEmailIsNotUsed|EmailExistsAsync)'` deve mostrar, em cada arquivo, um par `-`/`+` iguais além do nome do método (`if (await _repository.… (newEmail.Address, request.Id, cancellationToken)) {`), **sem `!` nas linhas `+`**, e o `return new Response("Email já está em uso", 409);` intacto.
- [X] T056 [US1] (Green visto) GATE-BACK: 0 avisos, 0 erros, **430 aprovados**.
- [X] T057 [US1] Provas finais da fase B: reversão V2a sem nenhuma linha `MUDOU ALÉM DO NOME` (cumulativa B1 a B5); `git grep -nE "GetUserById|GetUserByEmailAsync|CheckIfEmailIsNotUsed|GetOperatorList\b|\.Active\(\)|void Active\(" -- src` → **0 linhas**; `git grep -nE "AnyAsync" -- src/Backend | grep -vE "AnyAsync\([a-z]+ =>"` → **0 linhas**.
- [X] T058 [US1] **Checkpoint B5 (fim da fase B)**: GATE-BACK **430**; `npm test` do front **238**. `git status --short` (contra o último commit de checkpoint) lista só 6 arquivos de produção e 4 de teste. Os 20 testes com nome renomeado batem com a lista de [contracts/repository-renames.md](contracts/repository-renames.md), seção 5. Parar e aguardar o commit do revisor (regra 2).

---

## Fase C: 3 mensagens da API com a grafia corrigida (US3)

**Objetivo**: `Requsição inválida` → `Requisição inválida` (update-me, `400`), `Usuario atualizado com sucesso` → `Usuário atualizado com sucesso` (update-me, `200`) e `Sessão invalida - faça o login novamente` → `Sessão inválida - faça o login novamente` (get-me e update-me, `401`). Status, `notifications` e `data` não mudam ([contracts/api-messages.md](contracts/api-messages.md)). **Depende da fase B** (mesmos handlers e testes de handler).

**Teste independente**: as 3 classes de teste abaixo passam, e o teste de contrato muda **exatamente duas** expectativas.

- [X] T059 [P] [US3] (Red) Em `src/Backend/AccessControlSystem.Tests/Api/ApiContractCompatTests.cs` (TE-086 e TE-087): no cenário `"update-me: 200 conta atualizada"` (linha ~153) trocar `"Usuario atualizado com sucesso"` por `"Usuário atualizado com sucesso"`; no cenário `"update-me: 400 nenhum campo informado"` (linha ~159) trocar `"Requsição inválida"` por `"Requisição inválida"` e substituir o comentário da linha ~158 (`// A mensagem tem um erro de digitação desde sempre ("Requsição"): é o contrato de hoje.`) por `// Grafia corrigida pela spec 03 (legacy-cleanup, FR-012).` (o comentário novo não cita a grafia errada, para a busca `Requsi` da T066 dar 0 linhas). **Nenhum outro cenário** da classe muda.
- [X] T060 [P] [US3] (Red) Em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/GetMe/GetMeHandlerTests.cs`, no teste `Operador_nao_encontrado_devolve_401` (TE-008), acrescentar logo depois de `Assert.Equal(401, response.Status);` a linha `Assert.Equal("Sessão inválida - faça o login novamente", response.Message);` (FR-021). Nenhum teste novo.
- [X] T061 [P] [US3] (Red) Em `src/Backend/AccessControlSystem.Tests/Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs`, no teste `Sessao_sem_operador_correspondente_devolve_401` (TE-019), acrescentar logo depois de `Assert.Equal(401, response.Status);` a mesma asserção `Assert.Equal("Sessão inválida - faça o login novamente", response.Message);` (FR-021).
- [X] T062 [US3] (Red visto) `cd src/Backend && dotnet test Backend.slnx --filter "FullyQualifiedName~ApiContractCompatTests|FullyQualifiedName~GetMeHandlerTests|FullyQualifiedName~UpdateMeHandlerTests"` deve mostrar **exatamente 4 falhas**: `Contrato_dos_8_endpoints_nao_mudou` para `update-me: 200 conta atualizada` e para `update-me: 400 nenhum campo informado`, mais `Operador_nao_encontrado_devolve_401` e `Sessao_sem_operador_correspondente_devolve_401`. Outro número: parar e reportar.
- [X] T063 [P] [US3] (Green) Em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/UpdateMe/Handler.cs`: linha 22 `"Requsição inválida"` → `"Requisição inválida"`; linha 30 `"Sessão invalida - faça o login novamente"` → `"Sessão inválida - faça o login novamente"`; linha 58 `"Usuario atualizado com sucesso"` → `"Usuário atualizado com sucesso"`. Só o texto das 3 strings muda (status `400`/`401`/`200`, argumentos e `ResponseData` ficam).
- [X] T064 [P] [US3] (Green) Em `src/Backend/AccessControlSystem.Application/Context/AccountContext/UseCases/GetMe/Handler.cs`, linha 24: `"Sessão invalida - faça o login novamente"` → `"Sessão inválida - faça o login novamente"` (status `401` e o resto ficam).
- [X] T065 [US3] (Green visto) Repetir o comando de T062: **todos passam**.
- [X] T066 [US3] Provas da C: `git grep -nE "Requsi|Usuario atualizado|Sessão invalida" -- src` → **0 linhas**; `git diff -U0 -- src/Backend/AccessControlSystem.Tests/Api/ApiContractCompatTests.cs` mostra só as 2 linhas de expectativa e o comentário (nenhuma outra linha); `git status --short` (contra o último commit de checkpoint, o B5) não lista nenhum arquivo além dos 5 desta fase.
- [X] T067 [US3] **Checkpoint C**: GATE-BACK **430** (0 avisos, 0 erros); `cd src/Frontend && npm test` **238**. `git status --short` (contra o último commit de checkpoint) lista só 2 arquivos de produção e 3 de teste (as 2 asserções do `401` e as 2 expectativas do contrato; `GetMeHandlerTests` e `UpdateMeHandlerTests` já tinham sido tocados na B1). Parar e aguardar o commit do revisor (regra 2).

---

## Fase D: 17 textos de interface com acentuação correta (US2)

**Objetivo**: todo texto exibido ao operador no front passa a ter acentuação correta ([contracts/ui-texts.md](contracts/ui-texts.md)). **Nenhum** fluxo, rota, `href="#conteudo"` ou `id="conteudo"` muda, e nenhum texto vindo da API é reescrito.

**Teste independente**: `npm test` volta a **238** com os testes acentuados; a varredura (T090) dá 0 linhas.

**Regra dos testes**: só o **texto esperado** muda (e 2 títulos de `it`), nunca a lógica nem a forma da consulta, **exceto** as 2 asserções acrescentadas pelo FR-021 (login e lista). Linhas da tabela: as com `D` na coluna Fase.

### Testes primeiro (Red)

- [X] T068 [P] [US2] `src/Frontend/src/App.test.tsx` (TE-088 a TE-094): linha 14 `"Sua sessao foi encerrada. Faca login novamente."` → `"Sua sessão foi encerrada. Faça login novamente."`; linhas 52 e 160 `"Encerrar sessao"` → `"Encerrar sessão"`; linhas 84, 92, 137, 144, 151 e 158 `name: "Inicio"` → `name: "Início"`; linha 134 `"Pagina nao encontrada"` → `"Página não encontrada"`; linha 152 `"Ir para o conteudo"` → `"Ir para o conteúdo"`; linha 159 `"Navegacao principal"` → `"Navegação principal"`. As linhas 154 e 157 (`"#conteudo"` e `"conteudo"`) **não** mudam.
- [X] T069 [P] [US2] `src/Frontend/src/lib/api-client.test.ts` (TE-095), linha 204: `"Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente."` → `"Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente."`.
- [X] T070 [P] [US2] `src/Frontend/src/components/layout/Sidebar.test.tsx` (TE-096 a TE-102): linha 8 `"Navegacao principal"` → `"Navegação principal"`; **títulos** das linhas 12 e 20 (`...os itens Inicio e...`) → `Início`; linhas 16, 24 e 32 (`toEqual(["Inicio", ...])`), 38 e 46 (`name: "Inicio"`) → `"Início"`; linha 51 `"Operacao"` → `"Operação"`.
- [X] T071 [P] [US2] `src/Frontend/src/components/layout/Topbar.test.tsx` (TE-103), linha 23: `"Encerrar sessao"` → `"Encerrar sessão"`.
- [X] T072 [P] [US2] `src/Frontend/src/pages/LoginPage.test.tsx` (TE-104, TE-105, TE-140): linha 11 `"Sua sessao foi encerrada. Faca login novamente."` → `"Sua sessão foi encerrada. Faça login novamente."`; linha 156 a mensagem de rede → `"Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente."`; e **acrescentar (FR-021)** no teste `mostra o formulário em branco com os campos de e-mail e senha` (linha ~137), depois das asserções existentes: `expect(screen.getByText("O uso deste sistema é limitado a operadores autorizados.")).toBeInTheDocument();`.
- [X] T073 [P] [US2] `src/Frontend/src/pages/HomePage.test.tsx` (TE-106): linha 10 `name: "Inicio"` → `name: "Início"`; linha 14 → `"As informações desta página serão adicionadas conforme os serviços forem disponibilizados."`.
- [X] T074 [P] [US2] `src/Frontend/src/pages/NotFoundPage.test.tsx` (TE-107): linha 10 `"Pagina nao encontrada"` → `"Página não encontrada"`; linha 11 `"O recurso solicitado nao existe ou foi movido."` → `"O recurso solicitado não existe ou foi movido."`.
- [X] T075 [P] [US2] `src/Frontend/src/pages/OperatorsListPage.test.tsx` (TE-108 a TE-115): nas regex `/… pagina X de Y/` das linhas 90, 113, 121, 133, 138, 143, 149, 160 e 170 trocar `pagina` por `página`; `"Proxima"` → `"Próxima"` nas linhas 112, 134, 137, 139, 142, 145, 159, 429, 437 e 444; `"Itens por pagina"` → `"Itens por página"` nas linhas 162, 165, 178 e 435; linha 267 `deixara` → `deixará`; linha 516 a mensagem de rede acentuada (igual à de T069). E **acrescentar (FR-021)** em `carrega a primeira página com o filtro Ativos` (linha 64), depois da asserção do resumo da paginação (linha 90): `expect(screen.getByRole("columnheader", { name: "Ações" })).toBeInTheDocument();`.
- [X] T076 [P] [US2] `src/Frontend/src/pages/EditOperatorPage.test.tsx` (TE-116 a TE-128): `"Salvar alteracoes"` → `"Salvar alterações"` nas linhas 53, 71, 83, 94, 106, 117, 137, 149, 172, 191, 206, 229, 249, 277, 280 e 299.
- [X] T077 [P] [US2] `src/Frontend/src/pages/MyAccountPage.test.tsx` (TE-129 a TE-139): `"Salvar alteracoes"` → `"Salvar alterações"` nas linhas 65, 86, 104, 122, 139, 166, 186, 189, 210, 239, 280 e 297.
- [X] T078 [US2] (Red visto) `cd src/Frontend && npm test`: devem **falhar** os testes que afirmam os textos novos. Anotar a lista de testes que falham: **todo teste que falhar tem de estar na tabela** (linhas `D`); testes da tabela que não falharam (ex.: os que só têm asserção negativa) são anotados e mantidos. Teste fora da tabela que falhe: parar e reportar. O total continua 238 (falhas + aprovados).

### Produção (Green)

- [X] T079 [P] [US2] `src/Frontend/src/lib/api-client.ts`, linha 10 (constante `NETWORK_ERROR`): `"Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente."` → `"Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente."`.
- [X] T080 [P] [US2] `src/Frontend/src/pages/LoginPage.tsx`: linha 80 `title="Sua sessao foi encerrada. Faca login novamente."` → `title="Sua sessão foi encerrada. Faça login novamente."`; linha 118 `O uso deste sistema e limitado a operadores autorizados.` → `O uso deste sistema é limitado a operadores autorizados.`.
- [X] T081 [P] [US2] `src/Frontend/src/pages/HomePage.tsx`: linha 5 `Inicio` → `Início`; linhas 10 e 11 `As informacoes desta pagina serao adicionadas conforme os servicos forem` / `disponibilizados.` → `As informações desta página serão adicionadas conforme os serviços forem` / `disponibilizados.` (mantendo a quebra de linha).
- [X] T082 [P] [US2] `src/Frontend/src/pages/NotFoundPage.tsx`: linha 8 `Pagina nao encontrada` → `Página não encontrada`; linha 10 `O recurso solicitado nao existe ou foi movido.` → `O recurso solicitado não existe ou foi movido.`.
- [X] T083 [P] [US2] `src/Frontend/src/components/layout/Sidebar.tsx`: linha 23 `label: "Operacao"` → `label: "Operação"`; linha 24 `label: "Inicio"` → `label: "Início"`; linha 74 `aria-label="Navegacao principal"` → `aria-label="Navegação principal"`.
- [X] T084 [P] [US2] `src/Frontend/src/components/layout/AppShell.tsx`, linha 9: `Ir para o conteudo` → `Ir para o conteúdo`. As linhas 8 (`href="#conteudo"`) e 14 (`id="conteudo"`) **não** mudam.
- [X] T085 [P] [US2] `src/Frontend/src/components/layout/Topbar.tsx`, linha 31: `Encerrar sessao` → `Encerrar sessão`.
- [X] T086 [P] [US2] `src/Frontend/src/pages/OperatorsListPage.tsx`: linha 158 `Acoes` → `Ações`; linha 208 `pagina {currentPage} de{" "}` → `página {currentPage} de{" "}` (o `&middot;` e a estrutura ficam); linha 214 `Itens por pagina` → `Itens por página`; linha 247 `Proxima` → `Próxima`; linha 264 `A conta deixara de conseguir acessar o sistema.` → `A conta deixará de conseguir acessar o sistema.`.
- [X] T087 [P] [US2] `src/Frontend/src/pages/EditOperatorPage.tsx`, linha 191: `Salvar alteracoes` → `Salvar alterações`.
- [X] T088 [P] [US2] `src/Frontend/src/pages/MyAccountPage.tsx`, linha 153: `Salvar alteracoes` → `Salvar alterações`.
- [X] T089 [US2] (Green visto) GATE-FRONT em `src/Frontend`: typecheck ok, `Tests 238 passed (238)`, build ok.
- [X] T090 [US2] Varreduras (SC-004 e FR-009): (a) **formas conhecidas**: `git grep -nE "Nao foi poss|Sua sessao|Faca login|e limitado|>Inicio<|\"Inicio\"|informacoes desta|Pagina nao|nao existe ou foi|Operacao\"|Navegacao principal|o conteudo|Encerrar sessao|>Acoes<|por pagina|pagina \{|Proxima|deixara de|Salvar alteracoes" -- 'src/Frontend/src/*.ts' 'src/Frontend/src/*.tsx'` → **0 linhas** (testes incluídos); (b) **radicais**, para a completude (acha um texto sem acento que não esteja no inventário): `git grep -nEi "\b(pagina|paginas|nao|sao|voce|servico|servicos|informac[a-z]*|operac[a-z]*|sessao|conexao|alterac[a-z]*|confirmac[a-z]*|autenticac[a-z]*|permissao|acao|acoes|inicio|conteudo|obrigatori[oa]s?|invalid[oa]s?|proxima|ultima|unico|numero|titulo|codigo|descricao|deixara|serao|navegac[a-z]*|exclusao|criacao|edicao|seguranca|configurac[a-z]*|possivel|esta|ja|ate|tambem)\b" -- 'src/Frontend/src/*.ts' 'src/Frontend/src/*.tsx' ':!*.test.ts' ':!*.test.tsx' ':!src/Frontend/src/test/**' | grep -vE ":[0-9]+:[[:space:]]*(//|\*|/\*)" | grep -vE 'href="#conteudo"|id="conteudo"'` → só pode sobrar `src/Frontend/src/main.tsx:9` (mensagem técnica, fora da correção) e comentários: as linhas de comentário saem pelo primeiro `grep -v`, e o `href`/`id` `conteudo` do `AppShell` (identificadores), pelo segundo. Qualquer outra linha é um texto de interface sem acento (corrigir) ou um comentário ao fim de uma linha de código (aceitar e anotar no relatório). Conferir `git status --short -- src/Frontend` (contra o último commit de checkpoint): só os 10 arquivos de produção e os 10 de teste.
- [X] T091 [US2] **Checkpoint D**: GATE-FRONT **238 aprovados**; GATE-BACK **430** (inalterado). Os 17 textos estão corrigidos e todos passam a ser afirmados por teste (os 2 que não eram, pelas asserções de T072 e T075). Parar e aguardar o commit do revisor (regra 2).

---

## Fase E: Documentação e varredura final

**Objetivo**: manter a documentação coerente com o que mudou e provar o conjunto.

- [X] T092 [P] `src/Backend/AccessControlSystem.Tests/README.md`, linha 8 da árvore: `  Entities/            OperatorTests` → `  Entities/            OperatorTests, UserTests`.
- [X] T093 [P] `specs/20261101-164600-frontend-testing/contracts/cobertura-cenarios.md` (mapa de rastreabilidade da spec 02, em sincronia com os testes): (a) as linhas 20 e 21 (nota "Os títulos de `describe`/`it` levam acentuação correta; só os textos de interface afirmados nos testes (ex.: `Pagina nao encontrada`) ficam sem acento, como estão hoje.") passam a dizer que os títulos e os textos de interface afirmados levam acentuação correta, corrigida pela spec 03 (legacy-cleanup); (b) na linha 70, os dois títulos do `Sidebar.test.tsx` passam a `...os itens Início e Operadores...` e `...os itens Início e Minha conta...`; (c) na linha 124, `Ir para o conteudo` → `Ir para o conteúdo`. Ler o arquivo antes de editar.
- [X] T094 Varreduras finais, todas com **0 linhas** (exceto a de radicais do T090, que só pode listar `src/Frontend/src/main.tsx:9`): `git grep -nE "PasswordSalt|PASSWORD_SALT" -- . ':!specs' ':!.specify'`; `git grep -nE "GetUserById|GetUserByEmailAsync|CheckIfEmailIsNotUsed|GetOperatorList\b|\.Active\(\)|void Active\(" -- . ':!specs' ':!.specify'`; `git grep -nE "AnyAsync" -- src/Backend | grep -vE "AnyAsync\([a-z]+ =>"`; `git grep -nE "Requsi|Usuario atualizado|Sessão invalida" -- src`; e as duas varreduras de T090 (formas conhecidas e radicais).
- [X] T095 Integridade (FR-019, SC-007): `git diff --name-status main | grep -E "Tests\.cs$|\.test\.tsx?$"` deve listar **só** os 19 arquivos de teste do backend e os 10 do front da tabela "Testes existentes afetados", todos com status `M`, mais `src/Backend/AccessControlSystem.Tests/Domain/Entities/UserTests.cs` com status `A` (novo; se ainda não commitado, aparece como `??` em `git status --short`). Nenhuma linha `D` ou `R` (teste apagado ou movido). A reversão V2a só vale até a fase B (a C acrescenta asserções a `GetMeHandlerTests.cs` e `UpdateMeHandlerTests.cs`); por isso ela é exigida em T030, T036, T042, T048 e T057, e não aqui. Qualquer arquivo de teste fora dessa lista: parar e reportar.
- [X] T096 Gates finais (V6 do [quickstart.md](quickstart.md)): GATE-BACK (**430**, 0 avisos, 0 erros); GATE-FRONT (**238**); `cd src/Backend && dotnet list Backend.slnx package --vulnerable --include-transitive` e `cd src/Frontend && npm audit --audit-level=high` **iguais à linha de base** (só o `SSH.NET` transitivo do projeto de testes, já suprimido por `NU1903`; 2 *high* do `vite`/`launch-editor`, spec 04). `git diff --stat main -- '*.csproj' src/Frontend/package*.json` deve imprimir **0 linhas**.
- [X] T097 Ambiente local sobe sem `PASSWORD_SALT_KEY` e o login funciona (US4, V7 do [quickstart.md](quickstart.md)): copiar o `.env` para um arquivo temporário **sem** a linha `PASSWORD_SALT_KEY=`, criado com `TMP=$(mktemp -d)` (diretório temporário do sistema, **fora do repositório**) e com `trap 'rm -rf "$TMP"' EXIT` logo em seguida, para o arquivo (que tem segredos reais) ser removido **mesmo se o `up` falhar**; rodar `docker compose --env-file <temporário> up --build -d`, conferir `ps` (migrator concluído; `backend`, `backend-b`, `backend-dedicated` e `frontend` no ar), fazer `POST http://localhost:<BACKEND_PORT>/v1/account/login` com `admin@admin.com.br` e o `DEV_ADMIN_PASSWORD` do `.env` e esperar `"status": 200`, `"isSuccess": true` e um token; com o token do login, ver `PUT /v1/account/update-me` com corpo `{}` devolver `400` com `"message": "Requisição inválida"`; depois `docker compose --env-file <temporário> down` (**sem** `-v`); a remoção do temporário fica por conta do `trap`. Se o `up` falhar, rodar o `down` (sem `-v`) com o `.env` normal. Repetir o `up` com o `.env` normal (que ainda tem a linha): também sobe.
- [X] T098 Estado final do repositório: `git status --short` lista só os arquivos previstos no [plan.md](plan.md) (nenhum `.env`, nenhum `bin/`/`obj/`, nenhuma migration, nenhum `.csproj`/`package.json`); **nenhum commit foi feito pelo agente**: todo commit da branch desde a `main` é de checkpoint (mensagens da seção "Commits sugeridos") ou dos artefatos da spec; prova: `git log --oneline main..HEAD` lista só commits desses dois tipos.
- [X] T099 **Checkpoint E (entrega)**: GATE-BACK **430**, GATE-FRONT **238**, todas as varreduras com 0 linhas (a de radicais só deixa `main.tsx:9`), T097 ok. Reportar ao dono: (a) os números de cada checkpoint; (b) a lista de testes que falharam no Red de T062 e T078; (c) que a emenda da constituição (retirar de "Legado conhecido": nomes de repositório do `AccountContext`, `User.Active()`, `Secrets:PasswordSaltKey` e, no front, os textos sem acento) é do dono, via `/speckit-constitution`, depois do merge. Parar e aguardar o commit do revisor (regra 2).

---

## Dependências e ordem de execução

### Dependências entre fases

A implementação é **sequencial**: **0 → A → B1 → B2 → B3 → B4 → B5 → C → D → E**. Cada fase termina no seu checkpoint, onde o agente para e aguarda o commit do revisor (regra 2) antes da seguinte. As dependências abaixo explicam por que a ordem é essa; **fases não rodam em paralelo**.

- **Fase 0** (T001 a T005): sem dependências; começa imediatamente e **bloqueia** o resto (a linha de base é a referência).
- **Fase A** e **Fase D**: dependem só da Fase 0 e não dependem de nenhuma outra fase.
- **Fase B1 a B5**: cada passo exige o checkpoint do anterior (B1 → B2 → B3 → B4 → B5), porque arquivos como `UpdateMeHandlerTests.cs` e `UpdateMeRepositoryTests.cs` entram em mais de um passo. Independentes da A e da D.
- **Fase C**: depende da **B** (mesmos handlers `GetMe/Handler.cs` e `UpdateMe/Handler.cs` e mesmos testes de handler).
- **Fase E**: depende de A, B, C e D completas.

### Dentro de cada fase

- O teste (ou a verificação) **antes**, visto falhando; depois a mudança de produção; depois o Green e as provas; por último o checkpoint.
- Os checkpoints são os pontos de **revisão e commit separados** (sugestões abaixo).

### Oportunidades de paralelismo

O paralelismo vale **só dentro de uma fase** (tarefas `[P]` em arquivos diferentes); fases nunca rodam em paralelo.

- Fase 0: T003 e T004 (backend e front).
- A.2: T011 a T014 (4 arquivos diferentes).
- B1: T017 a T022 (testes) e T024 a T028 (produção) são de arquivos diferentes.
- B5: T050 e T051; T053 e T054.
- C: T059 a T061; T063 e T064.
- D: T068 a T077 (10 arquivos de teste) e T079 a T088 (10 arquivos de produção).
- E: T092 e T093.

### Exemplo de execução paralela: Fase D

```text
# Testes primeiro (10 arquivos independentes), depois T078 (Red visto):
Task: "T068 App.test.tsx"        Task: "T069 api-client.test.ts"      Task: "T070 Sidebar.test.tsx"
Task: "T071 Topbar.test.tsx"     Task: "T072 LoginPage.test.tsx"      Task: "T073 HomePage.test.tsx"
Task: "T074 NotFoundPage.test.tsx"  Task: "T075 OperatorsListPage.test.tsx"
Task: "T076 EditOperatorPage.test.tsx"  Task: "T077 MyAccountPage.test.tsx"
# Produção (10 arquivos independentes), depois T089 (Green visto):
Task: "T079 api-client.ts" ... "T088 MyAccountPage.tsx"
```

---

## Estratégia de implementação

### Núcleo primeiro (US1, a mais arriscada)

1. Fase 0 → **Fase A** (checkpoint A) → **Fases B1 a B5** (checkpoints B1 a B5). Para e valida: os nomes enganosos (`CheckIfEmailIsNotUsed`) já saíram, com a prova mecânica de que os testes só mudaram de nome.
2. Cada checkpoint é entregável sozinho: o repositório compila e passa em qualquer um deles.

### Entrega incremental

1. **A** (US1 + US4) → revisar e commitar.
2. **B1 a B5** (US1), um commit por passo (ou um só para a B, a critério do revisor).
3. **C** (US3) → revisar e commitar.
4. **D** (US2) → revisar e commitar.
5. **E** → conferência final.

### Rastreio dos requisitos

| Requisito | Tarefas |
|---|---|
| FR-001 a FR-005 (nomes de repositório e de domínio) | T006 a T009, T017 a T058 |
| FR-006 (nomes de teste acompanham) | T017 a T021, T032, T044, T050, T051 |
| FR-007 (teste de `Activate()`) | T006 a T009 |
| FR-008 a FR-010 (textos do front) | T079 a T090 |
| FR-011 (testes ajustados primeiro) | T068 a T078 |
| FR-012 a FR-014 (mensagens da API) | T059, T063, T064, T066 |
| FR-013 (contrato: 2 expectativas) | T059, T066 |
| FR-015 a FR-018 (`PasswordSaltKey`) | T010 a T015, T097 |
| FR-019 (integridade) | T095, T098 |
| FR-020 (sem migration, pacote ou constituição) | T096, T098 |
| FR-021 (4 asserções acrescentadas) | T060, T061, T072, T075 |

### Commits sugeridos (feitos pelo revisor, um por checkpoint)

| Checkpoint | Mensagem sugerida |
|---|---|
| A | `refactor(spec-003): phase A - User.Activate and remove PasswordSaltKey` |
| B1 a B5 | `refactor(spec-003): phase B1 - GetOperatorByIdAsync` … `phase B5 - EmailExistsAsync(email, exceptId)` |
| C | `fix(spec-003): phase C - accented API messages (update-me, get-me)` |
| D | `fix(frontend): phase D - accented UI texts` |
| E | `docs(spec-003): phase E - docs sync and final sweep` |

---

## Testes existentes afetados

Tabela exigida pela regra de **Integridade das entregas**: cada teste **existente** alterado, um por linha, com o arquivo, o nome e o motivo. Qualquer alteração em teste existente que **não** esteja aqui deve ser tratada como erro (T095). Os caminhos do backend são relativos a `src/Backend/AccessControlSystem.Tests/`; os do front, a `src/Frontend/src/`.

- **Fase**: em qual fase o teste é editado. Uma linha pode ter mais de uma (ex.: `B1, C`); então a mesma linha é tocada em fases diferentes.
- **Motivo** (apenas estes): `renomeação de método` (o nome do método do repositório mudou; nomes de teste e chamadas acompanham, sem mudar asserção, dado ou cenário), `texto esperado mudou por esta spec` (só o texto afirmado muda) e `asserção acrescentada pelo FR-021` (uma asserção a mais num teste existente, antes da correção; não enfraquece nada).
- **Linhas `(construtor)`, `(ajuda)` e `(constante ou ajuda compartilhada)`** são membros compartilhados da classe (construtor, método auxiliar ou constante) que o teste usa; o corpo de cada teste que os usa não muda por causa deles.

**Totais**: 140 linhas = 87 de backend (19 arquivos) + 53 de front (10 arquivos). Por motivo: **renomeação de método** 85 linhas (2 delas também com asserção do FR-021), **texto esperado mudou** 54 linhas (1 delas também com asserção do FR-021), **asserção acrescentada pelo FR-021** 4 linhas (TE-008, TE-019, TE-108 e TE-140). 20 testes tiveram o **nome** renomeado (FR-006).

| ID | Fase | Arquivo | Teste | O que muda | Motivo |
|---|---|---|---|---|---|
| TE-001 | B1 | `Application/AccountContext/Activate/ActivateHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_tocar_no_banco` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-002 | B1 | `Application/AccountContext/Activate/ActivateHandlerTests.cs` | `Operador_inexistente_devolve_404` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-003 | B1 | `Application/AccountContext/Activate/ActivateHandlerTests.cs` | `Caminho_feliz_reativa_e_persiste` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-004 | B1 | `Application/AccountContext/Deactivate/DeactivateHandlerTests.cs` | (ajuda) GivenExistingOperator | uso: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-005 | B1 | `Application/AccountContext/Deactivate/DeactivateHandlerTests.cs` | `Autodesativacao_devolve_400_sem_tocar_no_banco` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-006 | B1 | `Application/AccountContext/Deactivate/DeactivateHandlerTests.cs` | `Operador_inexistente_devolve_404` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-007 | B1 | `Application/AccountContext/GetMe/GetMeHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_tocar_no_banco` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-008 | B1, C | `Application/AccountContext/GetMe/GetMeHandlerTests.cs` | `Operador_nao_encontrado_devolve_401` | chamada: GetUserById→GetOperatorByIdAsync; acrescenta `Assert.Equal("Sessão inválida - faça o login novamente", response.Message)` | renomeação de método (B1); asserção acrescentada pelo FR-021 (C) |
| TE-009 | B1 | `Application/AccountContext/GetMe/GetMeHandlerTests.cs` | `Devolve_200_com_os_dados_do_operador` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-010 | B5 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | (construtor) | uso: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-011 | B1 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | (ajuda) GivenExistingOperator | uso: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-012 | B1 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_tocar_no_banco` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-013 | B1 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | `Operador_inexistente_devolve_404` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-014 | B5 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | `Email_em_uso_por_outro_devolve_409` | chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-015 | B5 | `Application/AccountContext/Update/UpdateHandlerTests.cs` | `Email_igual_ao_atual_nao_checa_duplicidade` | chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-016 | B5 | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | (construtor) | uso: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-017 | B1 | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | (ajuda) GivenExistingOperator | uso: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-018 | B1 | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_tocar_no_banco` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-019 | B1, C | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | `Sessao_sem_operador_correspondente_devolve_401` | chamada: GetUserById→GetOperatorByIdAsync; acrescenta `Assert.Equal("Sessão inválida - faça o login novamente", response.Message)` | renomeação de método (B1); asserção acrescentada pelo FR-021 (C) |
| TE-020 | B5 | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | `Email_em_uso_por_outro_devolve_409` | chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-021 | B5 | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs` | `Email_igual_ao_atual_nao_checa_duplicidade` | chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-022 | B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_consultar_o_banco` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-023 | B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` | `Usuario_inexistente_devolve_401_sem_gerar_token` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-024 | B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` | `Senha_errada_devolve_401_sem_gerar_token` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-025 | B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` | `Credenciais_corretas_devolvem_200_com_token_e_dados` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-026 | B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs` | `Gera_o_token_para_o_operador_autenticado` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-027 | B3 | `Application/AccountContext/Get/GetHandlerTests.cs` | `Requisicao_invalida_devolve_400_sem_consultar_o_banco` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-028 | B3 | `Application/AccountContext/Get/GetHandlerTests.cs` | `Devolve_200_com_a_pagina_e_os_metadados` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-029 | B3 | `Application/AccountContext/Get/GetHandlerTests.cs` | `Repassa_page_e_size_da_requisicao_pro_repositorio` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-030 | B3 | `Application/AccountContext/Get/GetHandlerTests.cs` | `Repassa_o_filtro_isActive_pro_repositorio` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-031 | B4 | `Application/AccountContext/Create/CreateHandlerTests.cs` | (construtor) | uso: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-032 | B4 | `Application/AccountContext/Create/CreateHandlerTests.cs` | `Email_ja_existente_devolve_409_sem_persistir` | chamada: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-033 | B4 | `Application/AccountContext/Create/CreateHandlerTests.cs` | `Checa_existencia_usando_o_email_normalizado` | chamada: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-034 | B1 | `Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs` | `GetUserById_devolve_o_operador` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-035 | B1 | `Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs` | `GetUserById_null_quando_nao_existe` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-036 | B1 | `Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs` | `ActivateAsync_persiste_o_estado_ativo` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-037 | B1 | `Infrastructure/AccountContext/Activate/ActivateRepositoryTests.cs` | `Isolamento_nao_le_nem_altera_operador_de_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-038 | B1 | `Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs` | `GetUserById_devolve_o_operador` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-039 | B1 | `Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs` | `GetUserById_null_quando_nao_existe` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-040 | B1 | `Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs` | `DeactivateAsync_persiste_o_estado_inativo` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-041 | B1 | `Infrastructure/AccountContext/Deactivate/DeactivateRepositoryTests.cs` | `Isolamento_so_considera_operadores_do_tenant_corrente` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-042 | B1 | `Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs` | `GetUserById_devolve_o_operador_com_dados_completos` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-043 | B1 | `Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs` | `GetUserById_null_quando_nao_existe` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-044 | B1 | `Infrastructure/AccountContext/GetMe/GetMeRepositoryTests.cs` | `Isolamento_nao_devolve_operador_de_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-045 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `GetUserById_devolve_o_operador` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-046 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `GetUserById_null_quando_nao_existe` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-047 | B5 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `CheckIfEmailIsNotUsed_true_quando_outro_operador_tem_o_email` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-048 | B5 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `CheckIfEmailIsNotUsed_false_para_o_proprio_email` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-049 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `UpdateAsync_persiste_as_mudancas` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-050 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `UpdateAsync_com_email_colidindo_lanca_ConflictException` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-051 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `UpdateAsync_aceita_email_que_existe_so_em_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-052 | B1 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `UpdateAsync_para_email_do_mesmo_tenant_lanca_ConflictException` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-053 | B5 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `CheckIfEmailIsNotUsed_considera_so_o_tenant_corrente` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-054 | B1, B5 | `Infrastructure/AccountContext/Update/UpdateRepositoryTests.cs` | `Isolamento_nao_enxerga_operador_nem_email_de_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync; CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-055 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `GetUserById_devolve_o_operador` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-056 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `GetUserById_null_quando_nao_existe` | nome do teste e chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-057 | B5 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `CheckIfEmailIsNotUsed_true_quando_outro_operador_tem_o_email` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-058 | B5 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `CheckIfEmailIsNotUsed_false_para_o_proprio_email` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-059 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `UpdateAsync_persiste_as_mudancas` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-060 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `UpdateAsync_com_email_colidindo_lanca_ConflictException` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-061 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `UpdateAsync_aceita_email_que_existe_so_em_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-062 | B1 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `UpdateAsync_para_email_do_mesmo_tenant_lanca_ConflictException` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-063 | B5 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `CheckIfEmailIsNotUsed_considera_so_o_tenant_corrente` | nome do teste e chamada: CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-064 | B1, B5 | `Infrastructure/AccountContext/UpdateMe/UpdateMeRepositoryTests.cs` | `Isolamento_nao_enxerga_operador_nem_email_de_outro_tenant` | chamada: GetUserById→GetOperatorByIdAsync; CheckIfEmailIsNotUsed→EmailExistsAsync | renomeação de método |
| TE-065 | B2 | `Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` | `GetUserByEmailAsync_null_quando_nao_existe` | nome do teste e chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-066 | B2 | `Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` | `GetUserByEmailAsync_devolve_o_operador_com_a_senha_utilizavel` | nome do teste e chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-067 | B2 | `Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` | `Base_nova_nao_recebe_o_admin_de_seed` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-068 | B2 | `Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` | `Isolamento_nao_devolve_o_mesmo_email_de_outro_tenant` | chamada: GetUserByEmailAsync→GetOperatorByEmailAsync | renomeação de método |
| TE-069 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Devolve_a_pagina_com_o_total_do_banco` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-070 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Itens_vem_ordenados_por_id` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-071 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Paginas_diferentes_nao_repetem_itens` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-072 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Projeta_id_email_e_role` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-073 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Filtro_Active_nao_traz_operadores_inativos` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-074 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Filtro_Inactive_traz_so_operadores_inativos` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-075 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Filtro_All_traz_ativos_e_inativos` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-076 | B3 | `Infrastructure/AccountContext/Get/GetRepositoryTests.cs` | `Isolamento_lista_e_total_so_do_tenant_corrente` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-077 | B4 | `Infrastructure/AccountContext/Create/CreateRepositoryTests.cs` | `AnyAsync_false_quando_o_email_nao_existe` | nome do teste e chamada: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-078 | B4 | `Infrastructure/AccountContext/Create/CreateRepositoryTests.cs` | `AnyAsync_true_depois_de_salvar` | nome do teste e chamada: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-079 | B4 | `Infrastructure/AccountContext/Create/CreateRepositoryTests.cs` | `Isolamento_nao_enxerga_email_de_outro_tenant` | chamada: AnyAsync→EmailExistsAsync | renomeação de método |
| TE-080 | B3 | `Infrastructure/Tenancy/TenantIsolationTests.cs` | `Lista_so_devolve_operadores_do_proprio_tenant` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-081 | B1 | `Infrastructure/Tenancy/TenantIsolationTests.cs` | `Consulta_por_id_de_outro_tenant_devolve_null` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-082 | B1 | `Infrastructure/Tenancy/TenantIsolationTests.cs` | `Alteracao_de_outro_tenant_nao_encontra_o_registro` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-083 | B3 | `Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs` | `Tenant_dedicado_enxerga_so_os_proprios_dados` | chamada: GetOperatorList→GetOperatorListAsync | renomeação de método |
| TE-084 | B1, B3 | `Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs` | `Tenant_dedicado_se_comporta_como_o_compartilhado` | chamada: GetOperatorList→GetOperatorListAsync; GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-085 | B1 | `Infrastructure/Tenancy/DedicatedTenantIsolationTests.cs` | `Tenants_da_base_compartilhada_nao_enxergam_a_base_dedicada` | chamada: GetUserById→GetOperatorByIdAsync | renomeação de método |
| TE-086 | C | `Api/ApiContractCompatTests.cs` | `Contrato_dos_8_endpoints_nao_mudou("update-me: 200 conta atualizada")` | expectativa (linha ~153): `Usuario atualizado com sucesso`→`Usuário atualizado com sucesso` | texto esperado mudou por esta spec |
| TE-087 | C | `Api/ApiContractCompatTests.cs` | `Contrato_dos_8_endpoints_nao_mudou("update-me: 400 nenhum campo informado")` | expectativa (linha ~159): `Requsição inválida`→`Requisição inválida`; comentário da linha ~158 reescrito para `// Grafia corrigida pela spec 03 (legacy-cleanup, FR-012).` | texto esperado mudou por esta spec |
| TE-088 | D | `App.test.tsx` | (constante ou ajuda compartilhada) | aviso de sessão encerrada | texto esperado mudou por esta spec |
| TE-089 | D | `App.test.tsx` | `apaga a sessão e vai ao login, sem o aviso de sessão encerrada, ao encerrar a sessão` | `Encerrar sessao`→`Encerrar sessão` | texto esperado mudou por esta spec |
| TE-090 | D | `App.test.tsx` | `redireciona o Operador de %s para o início` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-091 | D | `App.test.tsx` | `redireciona o Administrador de /minha-conta para o início` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-092 | D | `App.test.tsx` | `mostra a página não encontrada para um endereço inexistente e volta ao início pelo link` | `Pagina nao encontrada`→`Página não encontrada`; `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-093 | D | `App.test.tsx` | `redireciona /login para o início quando já há sessão` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-094 | D | `App.test.tsx` | `mostra o menu lateral e o topo junto do conteúdo da rota, com link para pular ao conteúdo` | `Inicio`→`Início`; `Ir para o conteudo`→`Ir para o conteúdo`; `Navegacao principal`→`Navegação principal`; `Encerrar sessao`→`Encerrar sessão` | texto esperado mudou por esta spec |
| TE-095 | D | `lib/api-client.test.ts` | `lança ApiError com status 0 e a mensagem de falha de comunicação quando o servidor está inacessível` | mensagem de falha de comunicação | texto esperado mudou por esta spec |
| TE-096 | D | `components/layout/Sidebar.test.tsx` | (constante ou ajuda compartilhada) | `Navegacao principal`→`Navegação principal` | texto esperado mudou por esta spec |
| TE-097 | D | `components/layout/Sidebar.test.tsx` | `mostra para o Administrador os itens Inicio e Operadores, sem Minha conta` | título do `it` e expectativas: `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-098 | D | `components/layout/Sidebar.test.tsx` | `mostra para o Operador os itens Inicio e Minha conta, sem Operadores` | título do `it` e expectativas: `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-099 | D | `components/layout/Sidebar.test.tsx` | `mostra só os itens públicos quando não há sessão` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-100 | D | `components/layout/Sidebar.test.tsx` | `aponta cada item para a sua rota` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-101 | D | `components/layout/Sidebar.test.tsx` | `marca como atual o link da rota aberta` | `Inicio`→`Início` | texto esperado mudou por esta spec |
| TE-102 | D | `components/layout/Sidebar.test.tsx` | `não mostra os grupos que ficam sem itens para o perfil` | `Operacao`→`Operação` | texto esperado mudou por esta spec |
| TE-103 | D | `components/layout/Topbar.test.tsx` | `apaga a sessão e vai ao login ao encerrar a sessão` | `Encerrar sessao`→`Encerrar sessão` | texto esperado mudou por esta spec |
| TE-104 | D | `pages/LoginPage.test.tsx` | (constante ou ajuda compartilhada) | aviso de sessão encerrada | texto esperado mudou por esta spec |
| TE-105 | D | `pages/LoginPage.test.tsx` | `mostra a mensagem de falha de comunicação quando o servidor está inacessível` | mensagem de falha de comunicação | texto esperado mudou por esta spec |
| TE-106 | D | `pages/HomePage.test.tsx` | `mostra o título, a descrição e o aviso de que as informações serão adicionadas` | `Inicio`→`Início`; texto de apoio `informacoes/pagina/servicos`→acentuado | texto esperado mudou por esta spec |
| TE-107 | D | `pages/NotFoundPage.test.tsx` | `mostra o título, a descrição e o link de volta ao início` | `Pagina nao encontrada`→`Página não encontrada`; descrição `...nao existe...`→`...não existe...` | texto esperado mudou por esta spec |
| TE-108 | D | `pages/OperatorsListPage.test.tsx` | `carrega a primeira página com o filtro Ativos` | resumo `pagina X de Y`→`página X de Y`; acrescenta `expect(screen.getByRole("columnheader", { name: "Ações" })).toBeInTheDocument()` | texto esperado mudou por esta spec; asserção acrescentada pelo FR-021 |
| TE-109 | D | `pages/OperatorsListPage.test.tsx` | `pede Inativos e Todos e volta à página 1 ao trocar o filtro` | `Proxima`→`Próxima`; resumo `pagina X de Y`→`página X de Y` | texto esperado mudou por esta spec |
| TE-110 | D | `pages/OperatorsListPage.test.tsx` | `navega entre páginas e desabilita Anterior na primeira e Próxima na última` | resumo `pagina X de Y`→`página X de Y`; `Proxima`→`Próxima` | texto esperado mudou por esta spec |
| TE-111 | D | `pages/OperatorsListPage.test.tsx` | `pede o novo tamanho e volta à página 1 ao mudar os itens por página` | `Proxima`→`Próxima`; resumo `pagina X de Y`→`página X de Y`; `Itens por pagina`→`Itens por página` | texto esperado mudou por esta spec |
| TE-112 | D | `pages/OperatorsListPage.test.tsx` | `oferece os tamanhos de página 10, 25, 50 e 100` | `Itens por pagina`→`Itens por página` | texto esperado mudou por esta spec |
| TE-113 | D | `pages/OperatorsListPage.test.tsx` | `abre a confirmação com o e-mail, chama a api, fecha e recarrega ao desativar` | `deixara`→`deixará` | texto esperado mudou por esta spec |
| TE-114 | D | `pages/OperatorsListPage.test.tsx` | `desabilita os controles enquanto a lista carrega, o que impede trocar filtro ou página no meio de uma carga` | `Proxima`→`Próxima`; `Itens por pagina`→`Itens por página` | texto esperado mudou por esta spec |
| TE-115 | D | `pages/OperatorsListPage.test.tsx` | `mostra a mensagem de falha de comunicação no lugar da tabela quando o servidor está inacessível` | mensagem de falha de comunicação | texto esperado mudou por esta spec |
| TE-116 | D | `pages/EditOperatorPage.test.tsx` | `envia apenas o e-mail quando só o e-mail mudou` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-117 | D | `pages/EditOperatorPage.test.tsx` | `envia apenas a senha quando só a senha foi digitada` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-118 | D | `pages/EditOperatorPage.test.tsx` | `envia apenas o perfil quando só o perfil mudou` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-119 | D | `pages/EditOperatorPage.test.tsx` | `envia um corpo vazio quando nada mudou e deixa a api decidir` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-120 | D | `pages/EditOperatorPage.test.tsx` | `não envia o e-mail quando ele foi apagado` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-121 | D | `pages/EditOperatorPage.test.tsx` | `codifica o identificador na URL da requisição` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-122 | D | `pages/EditOperatorPage.test.tsx` | `envia o perfil mesmo sem alteração quando acessada direto` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-123 | D | `pages/EditOperatorPage.test.tsx` | `envia e-mail e perfil quando acessada direto e o e-mail foi digitado` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-124 | D | `pages/EditOperatorPage.test.tsx` | `mostra e-mail e perfil atualizados e usa-os como nova referência ao editar novamente` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-125 | D | `pages/EditOperatorPage.test.tsx` | `mostra message e notifications, com erro por campo junto do campo, quando a api recusa` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-126 | D | `pages/EditOperatorPage.test.tsx` | `volta e-mail e perfil ao valor da lista, esvazia a senha e some com o erro ao acionar Limpar` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-127 | D | `pages/EditOperatorPage.test.tsx` | `não mostra sucesso nem erro e mantém o formulário quando a api responde sucesso sem dados` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-128 | D | `pages/EditOperatorPage.test.tsx` | `mostra o estado de carregamento enquanto envia` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-129 | D | `pages/MyAccountPage.test.tsx` | `envia só o e-mail quando só o e-mail mudou` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-130 | D | `pages/MyAccountPage.test.tsx` | `envia só a senha quando só a senha foi digitada` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-131 | D | `pages/MyAccountPage.test.tsx` | `envia e-mail e senha quando os dois mudaram` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-132 | D | `pages/MyAccountPage.test.tsx` | `envia um corpo vazio quando nada mudou e deixa a api decidir` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-133 | D | `pages/MyAccountPage.test.tsx` | `ignora um e-mail que só tem espaços` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-134 | D | `pages/MyAccountPage.test.tsx` | `mostra a mensagem da api, limpa a senha, recarrega os dados e atualiza o e-mail da sessão` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-135 | D | `pages/MyAccountPage.test.tsx` | `não mostra sucesso nem erro e mantém a senha digitada quando a api responde sucesso sem dados` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-136 | D | `pages/MyAccountPage.test.tsx` | `mostra o estado de carregamento enquanto envia` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-137 | D | `pages/MyAccountPage.test.tsx` | `mostra message e notifications, com erro por campo junto do campo, quando o envio é recusado` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-138 | D | `pages/MyAccountPage.test.tsx` | `volta o e-mail ao atual, esvazia a senha e some com o erro ao acionar Limpar` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-139 | D | `pages/MyAccountPage.test.tsx` | `some com a mensagem de sucesso ao acionar Limpar` | `Salvar alteracoes`→`Salvar alterações` | texto esperado mudou por esta spec |
| TE-140 | D | `pages/LoginPage.test.tsx` | `mostra o formulário em branco com os campos de e-mail e senha` | acrescenta `expect(screen.getByText("O uso deste sistema é limitado a operadores autorizados.")).toBeInTheDocument()` | asserção acrescentada pelo FR-021 |

---

## Testes novos

Esta spec cria **um único teste novo**. O FR-021 **não** cria teste: só acrescenta asserções a testes existentes (as 4 linhas `TE-008`, `TE-019`, `TE-108` e `TE-140` acima).

| ID | Fase | Arquivo | Teste | Motivo |
|---|---|---|---|---|
| TN-01 | A | `src/Backend/AccessControlSystem.Tests/Domain/Entities/UserTests.cs` (arquivo novo) | `Activate_reativa_o_usuario_desativado` | FR-007: `User.Active()` não tinha chamador nem teste; a renomeação para `Activate()` ganha um teste de caracterização, escrito antes e visto falhando (T006 e T007) |

Contagem: backend **429 → 430** (+1: TN-01); front **238 → 238**.

---

## Notas

- **[P]** = arquivos diferentes e sem dependência de tarefa incompleta. Dentro de um mesmo arquivo, as tarefas são sequenciais.
- O **Red** de cada fase é parte da tarefa, não opcional: sem ver falhar (ou não compilar, nas renomeações), a mudança de produção não começa.
- Se um Red não acontecer (o teste passou antes da mudança), **pare**: o teste não está protegendo o que deveria.
- Os comandos de varredura (`git grep`) só enxergam arquivos **versionados** ou novos já adicionados ao índice; o `UserTests.cs` novo, enquanto não commitado, aparece em `git status --short` (`??`) e não em `git diff`; depois de commitado, aparece como `A` em `git diff --name-status main`.
- Os números de linha são pontos de partida; o **texto citado** é a referência. Não renumerar nem reformatar código ao redor.
- Registros históricos (specs 01 e 02, exceto o mapa de rastreabilidade da T093) e a constituição **não** são editados.
- Achados fora do escopo (F-01 a F-04, em [research.md](research.md)) **não** são corrigidos aqui.
