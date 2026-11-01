# Pesquisa: Limpeza de legado

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-11-01

Todos os números abaixo foram **medidos no repositório em 2026-11-01**, antes de qualquer mudança
(buscas no código e execução real da suíte). Não restou nenhuma pendência de clarificação.

## Linha de base (antes de qualquer mudança)

| Verificação | Resultado |
|---|---|
| `dotnet build Backend.slnx` | 0 avisos, 0 erros |
| `dotnet test Backend.slnx` (Docker ligado, com integração) | **429 aprovados**, 0 falhas, 0 ignorados, ~31 s |
| `npm run typecheck` (`src/Frontend`) | ok |
| `npm test` (`src/Frontend`) | **238 aprovados**, 26 arquivos, ~14 s |
| `npm audit --audit-level=high` | 2 vulnerabilidades *high* (`vite`, `launch-editor`); herdadas, spec 04 |
| `dotnet list package --vulnerable --include-transitive` | só `SSH.NET` transitivo no projeto de testes (*high*), já suprimido por `NU1903` com justificativa |
| `docker compose --env-file <sem PASSWORD_SALT_KEY> config` | **falha** (exit 1): `required variable PASSWORD_SALT_KEY is missing a value`. Com a variável: exit 0. É o Red da fase A |
| Nomes antigos no código C# | 162 ocorrências em 42 arquivos (inclui 20 de `SaveAsync`, que **não** muda); cerca de 150 ocorrências a trocar |

Meta depois das fases: backend **430** (429 + o teste do FR-007), front **238**, build 0 avisos.

## 1. Estratégia de renomeação dos repositórios

- **Decisão**: renomear **um símbolo por vez** (B1 a B5), à mão, com o compilador como guia: primeiro
  os testes (a solução deixa de compilar = Red), depois contrato (`IRepository`), implementação
  (`Repository`) e handler (Green). Cada passo termina com `dotnet build` e `dotnet test` verdes.
- **Por quê**: cada use case tem o seu próprio `IRepository` (Princípio III), então o impacto de cada
  símbolo é fechado e verificável. O compilador acusa qualquer chamada esquecida, inclusive os
  `Substitute` do NSubstitute, que usam o método por nome.
- **Alternativas**:
  - Busca e troca global de texto: **rejeitada**. `AnyAsync` também é o método do EF Core (em
    `ActiveSessionChecker.cs:17`, `SeedDevCommand.cs:149`, dentro de `Create`, `Update` e `UpdateMe`
    `Repository.cs`, e em testes como `CreateRepositoryTests.cs:88`). Trocá-lo mudaria consulta.
  - Manter o nome antigo como método `[Obsolete]` que repassa ao novo: **rejeitada**, contraria
    SC-002 (nenhuma ocorrência do nome antigo) e deixa o padrão errado para a IA copiar.
  - Ferramenta de refactor da IDE: não está disponível ao agente.
- **Ordem B1 a B5**: os símbolos são independentes entre si; a ordem do plano vai do maior ao menor
  volume e deixa os dois `bool` por último. B1 (`GetUserById`, 5 use cases, 10 testes renomeados) é
  a maior; B2 e B3 são pequenas; B4 e B5 são as que exigem conferir a condição dos handlers.

## 2. Como provar que os testes só mudaram de nome

- **Decisão**: **verificação por reversão**. Para cada arquivo de teste alterado numa fase de
  renomeação, aplicar o mapa **novo → antigo** (`sed`) ao arquivo **depois** e comparar com a versão
  do `HEAD`: a diferença tem de ser **vazia**. Se sobrar qualquer linha, houve mudança além do nome
  (asserção, dado ou cenário) e a fase não passa. O roteiro está em [quickstart.md](quickstart.md), V2.
- **Por quê**: a regra "nas renomeações os testes só mudam de nome, nunca de asserção" precisa de
  prova mecânica, não de leitura de diff com 150 linhas. Funciona porque o mapa é injetivo no
  contexto (o novo nome não existia antes no mesmo arquivo).
- **Cuidado com `EmailExistsAsync`** (dois nomes antigos viram o mesmo novo): a reversão de
  `EmailExistsAsync` depende do arquivo (em `Create*Tests` volta a `AnyAsync`; em `Update*Tests` e
  `UpdateMe*Tests` volta a `CheckIfEmailIsNotUsed`). O roteiro aplica a reversão por pasta.
- **Alternativa rejeitada**: revisão manual do `git diff`. Frágil com ~150 linhas e 20 nomes de
  teste.

## 3. `EmailExistsAsync`: nome, assinatura e semântica

- **Decisão**: `CheckIfEmailIsNotUsed(string email, Guid id, CancellationToken)` →
  `EmailExistsAsync(string email, Guid exceptId, CancellationToken)`; `AnyAsync(string email,
  CancellationToken)` (Create) → `EmailExistsAsync(string email, CancellationToken)`. O corpo do
  método **não muda**: continua o `AnyAsync` do EF Core; só o nome do parâmetro acompanha
  (`x.Id != id` vira `x.Id != exceptId` em `Update` e `UpdateMe`).
- **Semântica conferida no código**: hoje `CheckIfEmailIsNotUsed` devolve `true` quando **outro**
  operador tem o e-mail (`AnyAsync(x => x.Email.Address == email && x.Id != id)`), e os handlers
  fazem `if (await _repository.CheckIfEmailIsNotUsed(...)) return 409`. Com o nome novo a condição
  fica **idêntica, sem `!`**: `if (await _repository.EmailExistsAsync(...)) return 409`. O
  `CreateHandler` usa `var exists = await ...AnyAsync(email, ...); if (exists) 409`, que continua igual.
- **Três interfaces com o mesmo nome**: `Create`, `Update` e `UpdateMe` passam a ter, cada uma no seu
  `IRepository`, um método `EmailExistsAsync` (assinaturas diferentes). Não há `IRepository`
  compartilhado entre use cases, então não há conflito nem sobrecarga ambígua (Princípio III).
- **Alternativas**: `IsEmailInUseByAnotherOperatorAsync` (mais explícito, mas o roteiro e a
  constituição usam o formato `{Algo}ExistsAsync`); `EmailExistsAsync` sem o `exceptId` (mudaria a
  pergunta, e o "outro operador" é parte do comportamento).

## 4. `User.Active()` → `Activate()` e o teste do FR-007

- **Decisão**: criar `Tests/Domain/Entities/UserTests.cs` com `Activate_reativa_o_usuario_desativado`
  **antes** (Red por não compilar: `User` não tem `Activate`), depois renomear o método (Green).
  Nenhum chamador existe hoje (busca em `src/Backend`), então a mudança é só a declaração.
- **Como montar um `User` válido**: todos os value objects do contexto aceitam o valor padrão
  vazio (`new Telephone()`, `new PasswordAccess()`, `new Rg()`, `new Cpf()`), e `Name` exige 3 a 100
  caracteres. Basta `new User(new Name("Maria Silva"), new Telephone(), new PasswordAccess(), new Rg(),
  new Cpf())`. O usuário nasce ativo (`IsActive = true`): o teste desativa, reativa e afirma `IsActive`.
- **Por quê um teste e não dois**: o FR-007 pede a reativação. `Deactivate()` serve só de preparação.
- **Alternativa rejeitada**: renomear sem teste (a mudança não teria proteção nenhuma).

## 5. Remoção da `PasswordSaltKey`

- **Fato verificado**: nenhuma classe C# a lê. `SecretsOptions` só tem `JwtPrivateKey`; nenhum teste
  a referencia (`ApiFactory` só define `Secrets:JwtPrivateKey`); nenhum workflow a cita. Ocorrências
  vigentes: `appsettings.json:11`, `docker-compose.yml:23`, `.env.example:20-21` e `README.md:15` e
  `:82`. `appsettings.Development.json` e user-secrets de exemplo **não** a têm.
- **Decisão**: remover dos 4 arquivos (`appsettings.json`, `docker-compose.yml`, `.env.example` e
  `README.md`, este em 2 linhas), 6 linhas no total (ver
  [contracts/configuration.md](contracts/configuration.md)). Ao tirar a linha do `appsettings.json`,
  o `JwtPrivateKey` vira o último item de `Secrets`, sem vírgula sobrando.
- **Red e Green**: `docker compose --env-file <temporário sem a variável> config` falha hoje (exit 1,
  confirmado) e deve passar depois (exit 0). Complementa: `up --build` com esse arquivo e login com
  operador sintético (V7). O arquivo temporário nasce de `.env.example`; o `.env` real do dono não é
  lido nem alterado.
- **`.env` antigo**: quem ainda tem `PASSWORD_SALT_KEY` no `.env` não é afetado, porque o compose só
  interpola as variáveis que referencia; a sobrando não vai para o container.
- **Alternativa rejeitada**: deixar a chave "opcional" no compose (`:-`): manteria a configuração viva
  e a confusão que a spec quer eliminar.

## 6. Mensagens da API (FR-012, FR-013, FR-021)

- **Onde estão** (todas em `Application`): `UpdateMe/Handler.cs:22` (`400`), `:30` (`401`), `:58`
  (`200`) e `GetMe/Handler.cs:24` (`401`). Nenhuma outra cópia no repositório (busca em código,
  documentação, front e testes).
- **Testes que as afirmam hoje**: `ApiContractCompatTests` afirma o `400` (linha 159) e o `200`
  (linha 153), ambos **com o erro**; ninguém afirma o texto do `401`. Os handlers de `GetMe` e
  `UpdateMe` já têm um teste do caso `401` (`Operador_nao_encontrado_devolve_401` e
  `Sessao_sem_operador_correspondente_devolve_401`), que só confere o status.
- **Decisão**: (1) mudar as 2 expectativas do teste de contrato e o comentário da linha 158 (que
  chama o erro de "contrato de hoje"); (2) **acrescentar a asserção** `Assert.Equal("Sessão inválida -
  faça o login novamente", response.Message)` nos 2 testes de handler existentes, em vez de criar
  testes novos; (3) só então corrigir as 4 linhas nos 2 handlers. Os 4 testes falham antes e passam
  depois.
- **Por quê não um cenário novo de `401` no teste de contrato**: seria teste novo fora do que a spec
  autoriza (FR-019); o teste de handler já fixa o texto na camada certa.
- **Alternativa rejeitada**: não afirmar o `401` (violaria o FR-021). O mesmo princípio vale para os
  2 textos de interface sem teste (item 7).

## 7. Textos do front (FR-008 a FR-011)

- **Varredura** em todo o código de produção de `src/Frontend/src` (telas, componentes, contextos,
  `lib`): **17 textos de interface** sem acento (os 16 da spec mais o resumo da paginação, apontado
  pelo dono). O resto que a busca encontra são **comentários** e a mensagem técnica de `main.tsx`
  (excluídos pela spec). Detalhe com arquivo e linha em [contracts/ui-texts.md](contracts/ui-texts.md).
- **TDD**: ajustar os 10 arquivos de teste que afirmam esses textos para a grafia correta, acrescentar
  as 2 asserções do FR-021 (abaixo), rodar `npm test` e **guardar a lista de falhas**: ela é
  exatamente o conjunto de testes afetados para o `tasks.md`. Depois corrigir os 10 arquivos de
  produção até ficar tudo verde.
- **Dois textos que nenhum teste afirmava** ("O uso deste sistema é limitado a operadores
  autorizados." em `LoginPage.tsx:118` e o cabeçalho só para leitor de tela "Ações" em
  `OperatorsListPage.tsx:158`) **não podem ser corrigidos sem teste** (Princípio VII). **Decisão**
  (dono, na revisão do plano): acrescentar a asserção do texto acentuado a um teste **existente**,
  sem criar teste novo, e ver falhar antes de corrigir (FR-021). Os escolhidos, por já renderizarem a
  parte da tela em que o texto aparece:
  - `pages/LoginPage.test.tsx`, `"mostra o formulário em branco com os campos de e-mail e senha"`
    (renderiza o login sem enviar nada): `expect(screen.getByText("O uso deste sistema é limitado a
    operadores autorizados.")).toBeInTheDocument()`;
  - `pages/OperatorsListPage.test.tsx`, `"carrega a primeira página com o filtro Ativos"` (já confere
    a tabela e o resumo da paginação): `expect(screen.getByRole("columnheader", { name: "Ações"
    })).toBeInTheDocument()`. O `<th scope="col">` expõe o texto do `sr-only` como nome acessível (o
    jsdom não carrega o CSS do app, então o texto está no DOM).
  - **Alternativas rejeitadas**: corrigir sem teste (viola o Princípio VII); criar teste novo (o
    total deixaria de ser 430 / 238 e o FR-019 só admite o teste do FR-007). Com isso, **todos os 17
    textos** passam a ser afirmados por teste, e o que era o achado F-03 deixou de existir: virou
    tarefa.
- **Títulos de `it`** que citam o texto antigo: 2 em `Sidebar.test.tsx` (linhas 12 e 20, "Inicio").
  Passam a "Início", para o teste não ensinar a grafia errada. Isso altera
  `contracts/cobertura-cenarios.md` da spec 02, que cita esses títulos **literalmente**; o mapa é
  mantido em sincronia (fase E, 3 trechos), porque é rastreabilidade viva, não registro histórico.
- **Ficam como estão**: `href="#conteudo"` e `id="conteudo"` (o teste afirma `"#conteudo"`; é
  identificador, não texto), rotas (`/minha-conta`, `/operadores`), `&middot;` e comentários.
- **Cuidado nos testes com regex**: `/pagina 2 de 3/` vira `/página 2 de 3/` (a letra acentuada
  casa normalmente; não há `\b` envolvido). O `getByRole(..., { name })` compara texto exato, então
  `"Proxima"` precisa virar `"Próxima"` em todas as 10 ocorrências, ou o teste falha.

## 8. Fases e independência

- **Decisão**: A (User + PasswordSaltKey), B (renomeações), C (mensagens), D (front), E (docs e
  varredura). Cada uma compila e passa sozinha; A e D não dependem de nenhuma; **C depois de B**
  porque `GetMe/Handler.cs`, `UpdateMe/Handler.cs`, `GetMeHandlerTests.cs` e
  `UpdateMeHandlerTests.cs` entram nas duas (B troca o nome do método, C troca o texto/asserção), e
  fazê-las em ordem evita conflito.
- **Por quê pequenas**: cada PR/commit fica revisável; uma regressão se localiza na fase.
- **Alternativa rejeitada**: uma fase única (diff grande, difícil de revisar e de reverter).

## 9. Documentação a atualizar

- `AccessControlSystem.Tests/README.md`: a árvore **já lista todos os use cases** ("AccountContext/...
  testes de Specification e Handler de cada use case"); a premissa do pedido (só Create e
  Authenticate) está superada pela spec 01. Falta só acrescentar `UserTests` em
  `Domain/Entities/` (hoje cita só `OperatorTests`).
- `README.md` da raiz: 2 linhas da `PasswordSaltKey` (fase A).
- Nenhuma outra documentação vigente cita os nomes antigos (busca em `*.md` fora de `specs/` e da
  constituição). Os registros históricos de specs anteriores não são reescritos (spec, Premissas).

## Achados fora do escopo (registrados, não corrigidos)

| # | Achado | Onde | Por que fica |
|---|---|---|---|
| F-01 | Campo privado `_Repository` com maiúscula (convenção: `_camelCase`) | `Create/Handler.cs:11` | Fora da lista do pedido; renomear campo privado não é nome de método nem de texto |
| F-02 | Redação divergente: `"Email já está em uso"` (handlers de Create, Update e UpdateMe) contra `"E-mail já está em uso"` (exceções nos repositórios) | handlers e repositórios | Não é erro de acento; unificar mudaria a mensagem de um `409` |
| F-03 | Rotas e id de âncora do front em português (`/minha-conta`, `/operadores`, `#conteudo`) | `App.tsx`, `AppShell.tsx` | Mudariam URL (comportamento); excluído pela spec |
| F-04 | Erros de digitação em comentários de `#region` (ex.: "Vefifica Role") | `Update/Handler.cs` | Comentário pode ficar sem correção (constituição) |

(O antigo F-03, "2 textos de interface sem teste que os afirme", deixou de ser achado: virou a
tarefa de acrescentar as 2 asserções do FR-021, na fase D.)
