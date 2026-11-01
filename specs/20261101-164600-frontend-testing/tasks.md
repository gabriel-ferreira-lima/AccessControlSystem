---

description: "Lista de tarefas da base de testes do frontend"
---

# Tarefas: Base de testes do frontend

**Entrada**: Documentos de design em `/specs/20261101-164600-frontend-testing/`

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Testes**: nesta feature os **testes são a própria entrega** (testes de caracterização). Por isso
as tarefas de cada história são tarefas de teste, e não há "tarefas de implementação" de telas.

**Organização**: agrupadas por história de usuário (US1…US7), para cada uma poder ser entregue e
validada isoladamente.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[USn]**: história da spec a que a tarefa pertence
- Todos os caminhos são relativos à raiz do repositório; `FRONT` abaixo significa `src/Frontend`

## Regras desta entrega (valem para TODAS as tarefas)

1. **Não faça commits** nem abra PR. O revisor confere e faz os commits, o push e o PR.
2. **Nenhuma linha de código de produção muda** (FR-015, SC-007): nada em `FRONT/src` que não seja
   `*.test.ts`, `*.test.tsx` ou `FRONT/src/test/`. Se um teste só passar alterando código de produção,
   **pare** e registre o caso como achado em `research.md` (seção "Achados de caracterização"), sem
   corrigir.
3. **Teste de caracterização passa desde o início.** Se um teste que descreve o comportamento atual
   falhar, a causa é o teste (leitura errada do código) ou um achado. É **proibido** apagar, pular
   (`it.skip`, `it.todo`, `xit`) ou enfraquecer um teste para o CI passar (Constituição, "Integridade
   das entregas").
4. **Textos como estão hoje** (sem acento): os testes afirmam os textos atuais, por exemplo
   `Sua sessao foi encerrada. Faca login novamente.`. A correção dos acentos é da spec 03.
5. Consultar por `getByRole` / `getByLabelText` / `getByText`; interagir com `user-event`; descrever
   cada `describe` e `it` em português **com acentuação correta** (ex.: `it("mostra as notifications quando a api devolve 400")`, `it("não chama a api ao cancelar")`); só os textos de interface afirmados nos testes ficam sem acento, como estão hoje (`Pagina nao encontrada`). Nas telas, passar ao `renderWithProviders` a rota real da tela em `route` (ex.: `"/login"`, `"/operadores"`). Responder à
   API só com as fábricas de `FRONT/src/test/handlers.ts`; nunca mockar `FRONT/src/api/` nem o `fetch`.
6. Dados sintéticos (`@example.test`), sem dado pessoal real (Princípio X).
7. Não marque uma tarefa como concluída sem rodar o que ela pede. Cada tarefa de teste roda o próprio
   arquivo (`npm test -- <arquivo>`) e ele deve passar antes de a tarefa ser marcada. Cada
   **Checkpoint** de fase roda os três portões (`npm run typecheck`, `npm test`, `npm run build`),
   que devem passar antes de a fase ser dada por concluída. Os portões de T052 repetem isso no fim.
8. Todos os comandos `npm` rodam dentro de `src/Frontend`.

### Testes existentes afetados

O frontend não tinha testes antes desta spec, então nenhum teste de tela foi alterado. Há **uma**
alteração de teste já existente dentro da própria entrega, declarada aqui por força da regra de
Integridade das entregas (a spec não mudou o comportamento esperado; o que mudou foi a correção de um
defeito da infraestrutura de teste):

| Teste | Criado em | Alterado em | O que mudou | Motivo |
|---|---|---|---|---|
| `src/Frontend/src/test/server.test.ts` › "registra a chamada que não tem handler" | Fase A (T004) | Fase C | Em vez de esperar que o `fetch` seja rejeitado (`rejects.toThrow()`), passa a esperar a resposta 500 sintética do MSW (`response.status === 500`). A verificação do registro (`takeUnhandledRequests()` devolve `GET <url>`) continua igual | Defeito do `onUnhandledRequest` do `setup.ts` (Fase A): como função que não lançava, o MSW deixava a chamada sem handler **seguir para a rede real**, o que contrariava o SC-004 e causou uma falha intermitente. A correção faz o callback lançar um `Error`; com isso o `fetch` resolve com a 500 sintética, e o teste antigo (que esperava rejeição) deixou de descrever o comportamento correto. Ver `research.md`, item 4 |
| `src/Frontend/src/test/server.test.ts` › "não deixa a chamada sem handler sair para a rede" | Fase C (novo) | — | Teste novo, com servidor HTTP local real, que prova `received === 0`. Escrito primeiro e falhou (Red) antes da correção | Protege a regra acima contra regressão |

Nenhum teste foi apagado, pulado ou enfraquecido; o `setup.ts` (infraestrutura, não teste) também foi
corrigido na Fase C pelo mesmo motivo.

---

## Fase 1: Setup (infraestrutura compartilhada)

**Propósito**: dependências, scripts e configuração do executor de testes.

- [X] T001 Instalar as `devDependencies` em `src/Frontend/package.json` (e atualizar `src/Frontend/package-lock.json`) com `npm install -D vitest@^4.1.0 jsdom@^27.4.0 msw@^2.14.0 @testing-library/react@^16.3.0 @testing-library/dom@^10.4.0 @testing-library/user-event@^14.6.0 @testing-library/jest-dom@~6.9.1`. Conferir no `package.json` que as faixas ficaram exatamente assim (o `~` do `jest-dom` é intencional: a 6.10 exige Node >= 22). Não alterar `vite`, `typescript` nem nenhuma dependência existente. Os avisos de `install-scripts` do npm (`esbuild`, `msw`) são esperados e podem ser ignorados.
- [X] T002 Em `src/Frontend/package.json`, acrescentar os scripts `"test": "vitest run"` e `"test:watch": "vitest"`, mantendo `dev`, `build`, `preview` e `typecheck` como estão.
- [X] T003 Em `src/Frontend/vite.config.ts`, trocar o import para `import { defineConfig } from "vitest/config"` e acrescentar ao objeto o bloco `test: { environment: "jsdom", setupFiles: ["./src/test/setup.ts"], include: ["src/**/*.test.{ts,tsx}"], env: { VITE_API_BASE_URL: "http://api.test" } }`, sem `globals`. Manter `plugins`, `server` e `preview` intactos. O `env` fixo existe para um `.env.local` da máquina não mudar a base da API nos testes.

---

## Fase 2: Fundação (bloqueia TODAS as histórias)

**Propósito**: servidor MSW, `setup.ts`, fábricas de handlers, fixtures e helpers de render
([contracts/test-helpers.md](contracts/test-helpers.md)).

**⚠️ CRÍTICO**: nenhuma tarefa de história começa antes de T011 passar.

- [X] T004 [P] Escrever primeiro (deve **falhar**, pois `server.ts` ainda não existe) `src/Frontend/src/test/server.test.ts` — exceção à regra 7: esta tarefa só é marcada quando a T006 a deixa **verde**; registrar no relatório a saída da execução Red (antes de T005) e a da Green (depois de T006) — cenário US1.2: `it("registra a chamada que não tem handler")` faz `fetch` para `apiUrl("/v1/rota-nao-prevista")` (a requisição é rejeitada, o teste a captura), espera que `takeUnhandledRequests()` devolva `["GET <url>"]` e, com isso, esvazia o registro para o `afterEach` não reprovar o próprio teste; `it("não registra chamadas que têm handler")` registra um handler com `server.use(...)` e espera registro vazio.
- [X] T005 Criar `src/Frontend/src/test/server.ts`: exportar `server = setupServer()` (de `msw/node`), `apiUrl(path) => API_BASE_URL + path` (importando `API_BASE_URL` de `../lib/api-client`) e `takeUnhandledRequests(): string[]`, que devolve e esvazia a lista interna de chamadas sem handler (cada item `"MÉTODO URL"`). Exportar também uma função interna para o `setup.ts` registrar chamadas nessa lista (ex.: `recordUnhandled(request)`).
- [X] T006 Criar `src/Frontend/src/test/setup.ts` conforme o contrato: (1) `import "@testing-library/jest-dom/vitest"`; (2) `beforeAll` → `server.listen({ onUnhandledRequest(request) { recordUnhandled(request); throw new Error(...) } })` (o `throw` é obrigatório: sem ele o MSW deixa a chamada seguir para a rede real; ver research.md, item 4), `afterAll` → `server.close()`; (3) `afterEach` → `server.resetHandlers()`, `cleanup()` do Testing Library, `sessionStorage.clear()`, `localStorage.clear()` e, **se houver chamada não prevista registrada, lançar** `new Error("Chamada de API não prevista no teste: " + lista.join(", "))` (esvaziando a lista antes de lançar); (4) implementar `HTMLDialogElement.prototype.showModal` (define o atributo `open`) e `close` (remove `open` e dispara `new Event("close")`); (5) definir `window.matchMedia` (com `configurable: true, writable: true`, para um teste poder trocá-lo e depois restaurar este) devolvendo `matches: false`, `media`, `addEventListener`, `removeEventListener`, `addListener`, `removeListener`, `dispatchEvent`; (6) redefinir `globalThis.DOMException` com o `DOMException` do Node, obtido de `AbortSignal.abort().reason.constructor`, com `configurable: true, writable: true`. Comentar em português o porquê de cada ajuste ([research.md](research.md), item 3). Depois de T005, o `server.test.ts` (T004) deve passar.
- [X] T007 [P] Criar `src/Frontend/src/test/envelope.ts` com o tipo `Outcome<T>` (`success` | `failure` | `failure-with-ok-status` | `raw` | `network-error`, exatamente como em [contracts/test-helpers.md](contracts/test-helpers.md)) e `toResponse(outcome)`, que devolve uma resposta do MSW (`HttpResponse`) com o envelope real `{ message, status, isSuccess, notifications, data }`: `success` → status 200 (ou o informado), `message` padrão `"OK"`, `isSuccess: true`, `notifications: null`; `failure` → `isSuccess: false`, `data: null`, status/`message`/`notifications` informados; `failure-with-ok-status` → HTTP 200 com `isSuccess: false`; `raw` → corpo de texto cru; `network-error` → `HttpResponse.error()`. Usar os tipos de `../types/api`.
- [X] T008 [P] Criar `src/Frontend/src/test/fixtures.ts`: `buildSession(overrides?)` (padrão `role: "Operator"`, `token: "token-de-teste"`, e-mail `@example.test`), `adminSession`, `operatorSession`, `buildOperator(overrides?)` (`id` por contador determinístico, e-mail único `@example.test`, `isActive: true`), `buildOperatorList(operators, overrides?)` (calcula `total`, `totalPages`, `page`, `size` coerentes) e `buildNotification(key, message)`. Sem aleatoriedade (SC-004). Tipos de `../types/api`.
- [X] T009 Criar `src/Frontend/src/test/handlers.ts` com as 8 fábricas do contrato: `loginHandler` (`POST /v1/account/login`), `createOperatorHandler` (`POST /v1/account/create`), `updateOperatorHandler` (`PUT /v1/account/update/:id`), `listOperatorsHandler` (`GET /v1/account/get-list`, aceita `Outcome` ou função `(query: { page, size, isActive }) => Outcome`, lendo `Page`, `Size` e `IsActive` da query), `activateOperatorHandler` (`PUT /v1/account/activate/:id`), `deactivateOperatorHandler` (`PUT /v1/account/deactivate/:id`), `getMeHandler` (`GET /v1/account/get-me`), `updateMeHandler` (`PUT /v1/account/update-me`). Cada uma usa `apiUrl`, `toResponse` e aceita `onRequest?: (received: { method, url, authorization, body }) => void` (corpo interpretado como JSON ou `undefined`). Depende de T005 e T007.
- [X] T010 Criar `src/Frontend/src/test/render.tsx`: `storeSession(session)` (grava o JSON na chave **literal** `acs.session` do `sessionStorage`); `renderWithProviders(ui, { route = "/", routeState, session })` — grava a sessão se informada, renderiza com `ThemeProvider`, `AuthProvider` e `MemoryRouter` reais (`initialEntries` com `pathname`, `search` e `state`), **monta a `ui` numa `<Route>` cujo `path` é o `pathname` de `route`** (a busca, como `?id=1`, não entra no `path`) e registra uma rota coringa (`*`) como destino de qualquer navegação, mostrando `Rota atual: <pathname><search>`. Consequência: o teste deve passar em `route` a rota real da tela (ex.: `"/login"` para a `LoginPage`); se a tela navegar para o mesmo `pathname` em que foi montada, a `ui` continua na tela e o destino não aparece. Devolve o resultado do Testing Library mais `user = userEvent.setup()`; `renderApp({ path = "/", session })` — grava a sessão, faz `window.history.pushState({}, "", path)`, renderiza o `App` real de `../App` e devolve `user`. Depende de T006 e T008.
- [X] T011 Checkpoint da fundação: rodar `npm run typecheck`, `npm test` (só o `server.test.ts` deve existir e passar) e `npm run build`. Os três devem terminar sem erro.

**Checkpoint**: fundação pronta — as histórias podem começar (em paralelo, se houver capacidade).

---

## Fase 3: História 1 — Rodar a suíte e ser barrado por ela (Prioridade: P1) 🎯 MVP

**Objetivo**: um comando roda tudo, e o CI barra pull requests do front que quebrem tipos, testes ou
build.

**Teste Independente**: `npm test` passa localmente (offline); um teste quebrado de propósito faz o
comando falhar; o workflow existe e valida com as três etapas.

- [X] T012 [US1] Criar `.github/workflows/frontend-ci.yml` exatamente como em [contracts/frontend-ci.md](contracts/frontend-ci.md), no estilo de `.github/workflows/backend-ci.yml`: `name: Frontend CI`; `on.push.branches: [main]` e `on.pull_request.branches: [main]`, ambos com `paths: ['src/Frontend/**', '.github/workflows/frontend-ci.yml']`; job `frontend-build-and-test` em `ubuntu-latest` com `defaults.run.working-directory: src/Frontend`; passos `Checkout repository` (`actions/checkout@v7`), `Set up Node.js` (`actions/setup-node@v7`, `node-version: '22'`, `cache: npm`, `cache-dependency-path: src/Frontend/package-lock.json`), `Install dependencies` (`npm ci`), `Type check` (`npm run typecheck`), `Run tests` (`npm test`), `Build` (`npm run build`). **Sem** `npm audit` (decisão da clarificação) e sem segredos.
- [X] T013 [P] [US1] Em `src/Frontend/README.md`, acrescentar a seção "Testes" (FR-016): como rodar (`npm test`, `npm run test:watch`), onde ficam os testes (ao lado do arquivo testado e `src/test/`), como escrever um teste novo (passos de [quickstart.md](quickstart.md), "Como escrever um teste novo"), que toda mudança de front segue TDD e que a API é sempre simulada com MSW. Incluir `npm test` na lista de scripts, atualizar o pré-requisito para "Node.js 20.19+" e citar o `frontend-ci.yml`.
- [X] T014 [P] [US1] Em `CLAUDE.md`, na seção "Comandos", acrescentar sob o bloco do frontend a linha `npm test` (com o comentário `# testes (Vitest, API simulada com MSW)`), mantendo `npm run typecheck` e `npm run build`.
- [X] T015 [US1] Validar US1.1 e US1.5 localmente: rodar `npm test` (deve passar). A ausência de rede real é garantida pela trava de chamada não prevista do `setup.ts`; a execução offline é feita pelo revisor, e o relatório deve dizer explicitamente que ela **não** foi feita pelo implementador. Em seguida, **temporariamente**, quebrar um `expect` do `server.test.ts`, confirmar que `npm test` termina com código de saída diferente de zero apontando o teste, e **desfazer** a quebra. Anotar o resultado no relatório final. (US1.3 e US1.4 só podem ser confirmadas num PR real: ficam na tarefa T056.)

**Checkpoint**: história 1 entregue; o restante da suíte entra história por história.

---

## Fase 4: História 2 — Login e ciclo de vida da sessão (Prioridade: P1)

**Objetivo**: login, sessão e encerramento (pelo servidor ou pelo operador) protegidos.

**Teste Independente**: com a API simulada, conferir sucesso, erro, 401 em chamada autenticada e
saída manual, só com as telas de login e início.

- [X] T016 [P] [US2] Criar `src/Frontend/src/lib/session-storage.test.ts` (unitário, casos limite da spec): grava e lê a sessão na chave literal `acs.session`; `readSession` devolve `null` sem item, com JSON inválido, sem `token` ou com `token` vazio; `id` e `email` ausentes viram `""`; qualquer `role` diferente de `"Admin"` vira `"Operator"`; `clearSession` remove o item; com `Storage.prototype.getItem`/`setItem`/`removeItem` lançando (via `vi.spyOn`), `readSession` devolve `null` e `writeSession`/`clearSession` não lançam.
- [X] T017 [P] [US2] Criar `src/Frontend/src/lib/form-error.test.ts` (unitário): `toFormError(new ApiError("msg", 400, [n]))` devolve `{ message: "msg", notifications: [n] }`; qualquer outro erro (`Error`, string, `undefined`) devolve `UNEXPECTED_ERROR` (`Ocorreu um erro inesperado. Tente novamente.` e `notifications: []`); `NO_ERROR` é `{ message: "", notifications: [] }`; `isAbortError` reexportado reconhece um `DOMException` `AbortError`.
- [X] T018 [P] [US2] Criar `src/Frontend/src/lib/api-client.test.ts` — parte da sessão (usar `server.use` com handlers do MSW; nesta camada é permitido declarar handlers crus de `msw` para rotas fictícias): `API_BASE_URL` vem de `VITE_API_BASE_URL` sem barra final — como o `env` fixo do T003 já fixa `http://api.test`, o item usa `vi.stubEnv("VITE_API_BASE_URL", "http://api.test///")` + `vi.resetModules()` + `await import("./api-client")` (módulo novo) e espera `API_BASE_URL === "http://api.test"`; o mesmo mecanismo confere o padrão `https://localhost:7219` com `vi.stubEnv("VITE_API_BASE_URL", undefined)`; restaurar tudo no `afterEach` (`vi.unstubAllEnvs()` e `vi.resetModules()`) para não contaminar os demais testes do arquivo; chamada com `authenticated: true` envia `Authorization: Bearer <token>` lido da sessão e sem token não envia o cabeçalho; chamada sem `authenticated` nunca envia o cabeçalho; `401` em chamada autenticada aciona o handler registrado com `setUnauthorizedHandler(vi.fn())` **e** lança `ApiError` com a `message` e as `notifications` do envelope; `401` em chamada **não** autenticada **não** aciona o handler; `setUnauthorizedHandler(null)` desliga; `ApiError` expõe `status`, `message`, `notifications` e `name === "ApiError"`. Casos de US2.5 e do caso limite "401 no login/não autenticada".
- [X] T019 [P] [US2] Criar `src/Frontend/src/context/AuthContext.test.tsx` com um componente sonda que usa `useAuth`: inicia com a sessão do `sessionStorage` (via `storeSession`) ou `null`; `signIn` grava no `sessionStorage`, atualiza a sessão e zera `sessionExpired`; `signOut` apaga a sessão **sem** ligar `sessionExpired`; um `401` em chamada autenticada (disparada pela sonda com `apiRequest(..., { authenticated: true })`) apaga a sessão e liga `sessionExpired`; `useAuth` fora do provider lança `useAuth deve ser usado dentro de <AuthProvider>.`; com `Storage` indisponível o `signIn` mantém a sessão em memória. Cenários US2.5, US2.6 e caso limite de armazenamento.
- [X] T020 [P] [US2] Criar `src/Frontend/src/pages/LoginPage.test.tsx` com `renderWithProviders` e `route: "/login"` (a rota real da tela; com a `ui` montada em `/login`, o destino `/` cai na rota coringa e aparece como `Rota atual: /`): (US2.1) preenche `E-mail` e `Senha`, clica em `Entrar`, o MSW devolve `AuthenticatedOperator` e o teste confere o corpo enviado (`{ email, password }`), a sessão gravada em `sessionStorage` e o destino `Rota atual: /`; (US2.2) com `routeState: { from: "/operadores" }` o destino é `/operadores`; (US2.3) a API devolve `401` com `message` e duas `notifications`: aparecem em `role="alert"` exatamente como vieram e o operador continua no login, sem sessão e **sem** o aviso de sessão encerrada; (US2.4) sucesso com `data` sem `token` mostra `Ocorreu um erro inesperado. Tente novamente.` e não cria sessão; (US2.7) com sessão pré-carregada redireciona para `/` (ou para o `from`); (US2.8) durante o envio o botão mostra `Autenticando...` e fica desabilitado (resposta atrasada com `delay` do MSW). O aviso `Sua sessao foi encerrada. Faca login novamente.` (que depende de `sessionExpired`) é verificado de ponta a ponta na T022 e na T019, não aqui.
- [X] T021 [P] [US2] Criar `src/Frontend/src/components/layout/Topbar.test.tsx` com `renderWithProviders`, `route: "/"` (o `Topbar` é montado em `/`; o destino `/login` cai na rota coringa) e sessão `adminSession`: mostra o rótulo do perfil (`Administrador`); o botão `Encerrar sessao` apaga a sessão do `sessionStorage` e navega para `/login` (`Rota atual: /login`); com `operatorSession` mostra `Operador`. Cenário US2.6.
- [X] T022 [US2] Criar `src/Frontend/src/App.test.tsx` com `renderApp` — **jornadas de sessão**: (US2.5) sessão de `Operator` em `/minha-conta`, `getMeHandler` devolvendo `401` → a tela volta para o login (`heading` `Sistema de Controle de Acesso`), `sessionStorage` sem `acs.session` e o aviso `Sua sessao foi encerrada. Faca login novamente.` visível; (US2.2) sem sessão em `/operadores`, o login com sucesso (como `Admin`, com `listOperatorsHandler`) leva de volta a `/operadores`; (US2.6) `Encerrar sessao` leva ao login **sem** o aviso de sessão encerrada. Este arquivo será estendido nas tarefas T033, T041.

**Checkpoint**: história 2 entregue e verificável sozinha.

---

## Fase 5: História 3 — Lista de operadores (Prioridade: P1)

**Objetivo**: filtro, paginação, ativar/desativar com confirmação e erros da lista protegidos.

**Teste Independente**: com `listOperatorsHandler` devolvendo páginas, conferir listagem, filtros,
navegação e o fluxo de confirmação, sem depender de outra tela.

- [X] T023 [P] [US3] Criar `src/Frontend/src/components/ui/Alert.test.tsx`: tom `error` usa `role="alert"`, `success` e `info` usam `role="status"`; mostra o `title`; renderiza `children`; lista cada `notifications[].message` em itens `listitem`, e não renderiza lista com `notifications` ausente ou vazia. Apoia US3.9/US3.10.
- [X] T024 [P] [US3] Criar `src/Frontend/src/components/ui/ConfirmDialog.test.tsx` (renderização direta, sem providers): fechado, o `dialog` não é acessível; com `open` mostra `title`, `description` e os botões `Cancelar` (ou `cancelLabel`) e o `confirmLabel`; clicar nos botões chama `onCancel`/`onConfirm`; o evento `cancel` do `<dialog>` (`fireEvent(dialog, new Event("cancel", { cancelable: true }))`) chama `onCancel` e **não** fecha sozinho; com `pending` o botão de confirmar mostra `Aguarde...`, os dois ficam desabilitados e o `cancel` **não** chama `onCancel`; com `error` mostra a `message` e as `notifications` no `Alert`; `tone="danger"` e `default` não mudam o comportamento; ao trocar `open` para `false` o diálogo fecha. Cenários US3.8 e caso limite "cancelar bloqueado em andamento".
- [X] T025 [P] [US3] Criar `src/Frontend/src/pages/OperatorsListPage.test.tsx` — **carga, filtro, paginação e erros** (`renderWithProviders` com `route: "/operadores"`, sessão `adminSession`, `listOperatorsHandler` como função que registra a query): (US3.1) estado `Carregando operadores...`, depois a tabela com e-mail, perfil (`Administrador`/`Operador`) e situação (`Ativo`/`Inativo`), primeira requisição com `Page=1`, `Size=10`, `IsActive=Active` e filtro `Ativos` selecionado; (US3.2) trocar `Mostrar` para `Inativos` e `Todos` pede `IsActive=Inactive`/`All` e volta à página 1; (US3.3) `Proxima`/`Anterior` pedem as páginas certas, `Anterior` desabilitado na primeira e `Proxima` na última, e o texto `N operador(es) · pagina X de Y`; (US3.4) `Itens por pagina` pede o novo `Size` e volta à página 1; (US3.5) lista vazia mostra `Nenhum operador encontrado para este filtro.`; (US3.10) a API devolve `400` com `message` e `notifications` e eles aparecem no lugar da tabela; `Atualizar` refaz a chamada; (US3.11) a linha tem o link `Editar` com `href` `/operadores/editar?id=<id codificado>` e o link `Criar operador` aponta para `/operadores/novo`.
- [X] T026 [US3] Em `src/Frontend/src/pages/OperatorsListPage.test.tsx` (mesmo arquivo da T025), acrescentar **ativar/desativar**: (US3.6) clicar `Desativar` na linha de um operador ativo abre o `dialog` `Desativar operador?` com o e-mail do operador e o texto `A conta deixara de conseguir acessar o sistema.`; confirmar (botão `Desativar` **dentro** do diálogo, via `within(dialog)`) chama `PUT /v1/account/deactivate/<id>` com `Authorization`, fecha a janela e recarrega a lista (segunda chamada ao `get-list`); (US3.7) o mesmo para `Ativar` em operador inativo (`Ativar operador?`, `PUT .../activate/<id>`); (US3.8) `Cancelar` e o evento `cancel` fecham a janela sem chamar a API e sem recarregar; (US3.9) a API recusa com `400`, `message: "Requisição inválida"` e uma `notification` (ex.: `"Nao e possivel desativar a propria conta"`): a janela **continua aberta** mostrando a `message` **e** a notificação, e a lista **não** é recarregada; reabrir a janela limpa o erro anterior.
- [X] T027 [US3] Em `src/Frontend/src/pages/OperatorsListPage.test.tsx` (mesmo arquivo), acrescentar os **casos limite**: troca rápida de filtro/página com a primeira resposta atrasada — só a resposta mais recente aparece; resposta de sucesso da lista sem `data` mantém a tela como estava; durante a chamada de ativar/desativar o botão do diálogo mostra `Aguarde...` e `cancel`/`Cancelar` ficam bloqueados até a resposta; enquanto a lista carrega, `Mostrar` e `Atualizar` ficam desabilitados desde a carga inicial; `Itens por pagina` e `Anterior`/`Proxima` só existem com dados já na tela (a tabela some em `loading && !data`), então o teste carrega a lista, aciona `Atualizar` com a segunda resposta atrasada (`delay`) e afirma que esses quatro controles ficam desabilitados até a resposta chegar.

**Checkpoint**: histórias 2 e 3 funcionam e podem ser verificadas independentemente.

---

## Fase 6: História 4 — Navegação protegida por sessão e perfil (Prioridade: P2)

**Objetivo**: rotas, redirecionamentos e menu do jeito que são hoje.

**Teste Independente**: montar a navegação com sessões de perfis diferentes (ou sem sessão) e
conferir o destino de cada endereço.

- [X] T028 [P] [US4] Criar `src/Frontend/src/components/RequireSession.test.tsx` (`renderWithProviders` com `route: "/protegida"`, uma rota de teste distinta de `/login`, para o destino cair na rota coringa): sem sessão redireciona para `/login` e entrega `state.from` com o caminho de origem (conferir por uma sonda de `useLocation`); com sessão renderiza os filhos. Cenário US4.1.
- [X] T029 [P] [US4] Criar `src/Frontend/src/components/RequireRole.test.tsx` (`renderWithProviders` com `route: "/restrita"`, e **nunca** `"/"`: o destino do redirecionamento é `/`, e a `ui` montada em `/` entraria em laço): com a sessão do perfil exigido renderiza os filhos; com outro perfil (`role="Admin"` vs sessão `Operator` e o inverso) ou sem sessão redireciona para `/` (`Rota atual: /`). Cenário US4.2/US4.3 em nível de unidade.
- [X] T030 [P] [US4] Criar `src/Frontend/src/components/layout/Sidebar.test.tsx`: dentro de `navigation` `Navegacao principal`, `Admin` vê os links `Inicio` e `Operadores` e **não** vê `Minha conta`; `Operator` vê `Inicio` e `Minha conta` e **não** vê `Operadores`; o link da rota atual tem `aria-current="page"`; os grupos sem itens para o perfil não aparecem. Cenário US4.5.
- [X] T031 [P] [US4] Criar `src/Frontend/src/pages/NotFoundPage.test.tsx`: mostra o título `Pagina nao encontrada`, a descrição e o link `Voltar ao painel` apontando para `/`.
- [X] T032 [P] [US4] Criar `src/Frontend/src/pages/HomePage.test.tsx`: mostra o título `Inicio`, `Console de controle de acesso.` e o aviso de que as informações serão adicionadas.
- [X] T033 [US4] Em `src/Frontend/src/App.test.tsx` (mesmo arquivo da T022), acrescentar a **navegação com o App real** via `renderApp`: (US4.1) sem sessão, `/`, `/minha-conta`, `/operadores`, `/operadores/novo` e `/operadores/editar?id=1` terminam no login (cada um em seu `it`/`it.each`); (US4.2) com `operatorSession`, `/operadores`, `/operadores/novo` e `/operadores/editar?id=1` terminam em `/` (`Inicio`); (US4.3) com `adminSession`, `/minha-conta` termina em `/`; com `operatorSession`, `/minha-conta` abre a tela `Minha conta` (com `getMeHandler`); (US4.4) com sessão, `/rota-que-nao-existe` mostra `Pagina nao encontrada` e o link `Voltar ao painel` leva a `/`; `/login` com sessão redireciona para `/`; (**AppShell**, sem renumerar tarefas) com sessão em `/`, o link `Ir para o conteudo` aponta para `#conteudo` e o landmark `main` existe com `id="conteudo"` (consulta `getByRole("main")`), e o menu lateral e o topo aparecem junto do conteúdo da rota.

**Checkpoint**: histórias 1 a 4 funcionam e são verificáveis.

---

## Fase 7: História 5 — Criar e editar operador (Prioridade: P2)

**Objetivo**: formulários de criação e edição, com pré-preenchimento e erros por campo.

**Teste Independente**: abrir cada formulário isolado, preencher, enviar contra a API simulada
(sucesso e erro) e conferir o corpo enviado e a tela.

- [X] T034 [P] [US5] Criar `src/Frontend/src/components/ui/TextField.test.tsx`: o campo é encontrado por `getByLabelText(label)`; sem `error` não há `role="alert"` nem `aria-invalid`; com `error` mostra o texto em `role="alert"`, `aria-invalid="true"` e `aria-describedby` apontando para a mensagem; repassa atributos (`type`, `value`, `onChange`).
- [X] T035 [P] [US5] Criar `src/Frontend/src/components/ui/PasswordField.test.tsx`: começa com `type="password"`; o botão `Mostrar senha` troca para `type="text"` e o botão vira `Ocultar senha` (`aria-pressed`); clicar de novo oculta; com `error` o mesmo contrato de acessibilidade do `TextField`.
- [X] T036 [P] [US5] Criar `src/Frontend/src/components/ui/SelectField.test.tsx`: o `combobox` é encontrado pelo rótulo; renderiza as `option` filhas; `onChange` recebe o valor escolhido; com `error` o contrato de acessibilidade do `TextField`.
- [X] T037 [P] [US5] Criar `src/Frontend/src/components/ui/Button.test.tsx`: `type="button"` por padrão; chama `onClick`; com `loading` fica `disabled`, mostra `loadingText` (ou os filhos), expõe `aria-busy="true"` e não chama `onClick`; `disabled` impede o clique; `variant`/`size` não alteram o comportamento; com `icon` o ícone fica oculto para leitores de tela.
- [X] T038 [P] [US5] Criar `src/Frontend/src/pages/CreateOperatorPage.test.tsx` (`renderWithProviders` com `route: "/operadores/novo"`, `adminSession`, `createOperatorHandler` com espião): (US5.1) preencher `E-mail`, `Senha`, escolher `Administrador` em `Perfil de acesso`, enviar `Criar operador`, o corpo é `{ email, password, role: "Admin" }` com `Authorization`, aparece `Operador criado com sucesso.` com identificador, e-mail e perfil, e `Cadastrar outro` devolve o formulário **em branco** (perfil `Operador`); o botão mostra `Criando...` durante o envio; (US5.2) `400` com `message` e `notifications` de chaves `email` e `password` (e uma chave sem campo): `message` e todas as notificações aparecem no alerta, as de `email`/`password` também junto do campo (comparação sem diferenciar maiúsculas/minúsculas, ex.: chave `Email`) e os dados digitados continuam preenchidos; (US5.9) `Limpar` esvazia os campos, volta o perfil para `Operador` e some com o alerta de erro.
- [X] T039 [P] [US5] Criar `src/Frontend/src/pages/EditOperatorPage.test.tsx` — **abertura e envio parcial** (`renderWithProviders` com `route: "/operadores/editar?id=<id>"` e `routeState: { email, role }`, `updateOperatorHandler` com espião): (US5.3) e-mail e perfil vêm preenchidos com os da linha e `Nova senha` vem vazia; (US5.4) alterar só o e-mail envia `{ email }`; só a senha envia `{ password }`; só o perfil envia `{ role }`; sem nenhuma alteração envia `{}`; a requisição vai para `PUT /v1/account/update/<id codificado>`; (US5.5) com `route` e identificador **sem** `routeState`, o e-mail vem em branco e o perfil vem `Operador`; ao enviar só uma senha, o corpo inclui `role: "Operator"` mesmo sem alteração (comportamento atual, achado A-02); (US5.6) sem `?id=` (ou `id` só com espaços) mostra a orientação `Escolha um operador na lista de operadores para editar.` e nenhum formulário. Atenção: o texto está dividido por um `<Link>` em `EditOperatorPage.tsx`, então `getByText("Escolha um operador na lista de operadores para editar.")` não encontra o parágrafo. Consultar o parágrafo pelo conteúdo de texto completo (um matcher de função em `getByText` que compare o `textContent` do elemento, ou `toHaveTextContent` no elemento) e o link `lista de operadores` por `getByRole("link", { name: "lista de operadores" })`, conferindo o `href` `/operadores`. Isso não é achado nem motivo para alterar a tela.
- [X] T040 [US5] Em `src/Frontend/src/pages/EditOperatorPage.test.tsx` (mesmo arquivo da T039), acrescentar: (US5.7) sucesso mostra `Conta atualizada com sucesso.` com e-mail e perfil retornados, a senha é limpa, e `Editar novamente` volta ao formulário com os valores atualizados como **nova referência** (um segundo envio sem mudanças envia `{}`); (US5.8) `400` com `message` e `notifications` (chaves `email`, `password`, `role`) mostra tudo como veio, com erro por campo junto do campo; (US5.9) `Limpar` volta e-mail e perfil ao valor de referência (os da lista, ou em branco/`Operador` no acesso direto), esvazia a senha e some com os avisos.
- [X] T041 [US5] Em `src/Frontend/src/App.test.tsx` (mesmo arquivo da T022/T033), acrescentar a **jornada lista → edição** com `renderApp({ path: "/operadores", session: adminSession })`: a lista mostra um operador, clicar `Editar` abre `/operadores/editar` com `E-mail` e `Perfil de acesso` já preenchidos com os dados da linha (cenários US3.11 e US5.3 ponta a ponta); recarregar o endereço direto (`renderApp({ path: "/operadores/editar?id=<id>" })`) abre em branco.

**Checkpoint**: histórias 1 a 5 funcionam.

---

## Fase 8: História 6 — Minha conta (Prioridade: P2)

**Objetivo**: a tela do próprio operador protegida.

**Teste Independente**: abrir a tela com sessão de `Operator` e a API simulada para `get-me` e
`update-me`.

- [X] T042 [P] [US6] Criar `src/Frontend/src/pages/MyAccountPage.test.tsx` — **carga e envio** (`renderWithProviders` com `route: "/minha-conta"`, `operatorSession`, `getMeHandler` e `updateMeHandler` com espiões): (US6.1) chama `GET /v1/account/get-me` com `Authorization`, preenche `E-mail` com o e-mail do servidor, mostra o perfil como texto (`Operador`) — não é campo editável — e `Nova senha` vazia; (US6.2) alterar só o e-mail envia `{ email }`, só a senha envia `{ password }`, os dois enviam ambos e sem alterações envia `{}`; um e-mail só com espaços é ignorado; (US6.3) sucesso mostra a `message` devolvida pela API em `role="status"`, limpa `Nova senha`, chama `get-me` de novo e, estando o e-mail alterado, a sessão no `sessionStorage` passa a ter o novo e-mail.
- [X] T043 [US6] Em `src/Frontend/src/pages/MyAccountPage.test.tsx` (mesmo arquivo da T042), acrescentar: (US6.4) `400` no `update-me` com `message` e `notifications` (chaves `email`/`password`) mostra tudo como veio e o erro por campo junto do campo; (US6.5) `get-me` falhando com `400`, `message` e `notifications` mostra o erro acima do formulário e o perfil aparece como `—`; (US6.6) `Limpar` volta o e-mail ao atual do servidor, esvazia a senha e some com o sucesso e o erro; o botão mostra `Salvando...` durante o envio.

**Checkpoint**: histórias 1 a 6 funcionam.

---

## Fase 9: História 7 — Falhas de comunicação (Prioridade: P2)

**Objetivo**: mensagens de falha de comunicação e de resposta inesperada em qualquer tela.

**Teste Independente**: simular falha de rede e respostas fora do envelope em uma tela
representativa e conferir a mensagem.

- [X] T044 [P] [US7] Em `src/Frontend/src/lib/api-client.test.ts` (mesmo arquivo da T018), acrescentar: (US7.1) `HttpResponse.error()` → `ApiError` com `status` 0 e a mensagem `Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente.`; (US7.2) resposta de erro com corpo vazio e com corpo que não é JSON → `ApiError` com `Ocorreu um erro inesperado. Tente novamente em instantes.` e o `status` HTTP; (US7.3) HTTP 200 com `isSuccess: false` → `ApiError` com a `message`, o `status` do envelope e as `notifications`; (US7.4) `403` e `404` em chamada autenticada **não** acionam o handler de não autorizado; sucesso com corpo vazio devolve o envelope padrão `{ message: "", status, isSuccess: true, notifications: null, data: null }`; (US7.5) chamada abortada por `AbortController` rejeita com `AbortError` (reconhecido por `isAbortError`) e **não** vira `ApiError` de rede; `isAbortError` é `false` para outros erros.
- [X] T045 [P] [US7] Em `src/Frontend/src/pages/LoginPage.test.tsx` (mesmo arquivo da T020), acrescentar: (US7.1) falha de rede → o alerta mostra a mensagem de falha de comunicação; (US7.2) resposta fora do envelope → `Ocorreu um erro inesperado. Tente novamente em instantes.`; `401` no login **não** derruba sessão nem mostra o aviso de sessão encerrada (caso limite); sair da tela com o envio em andamento (desmontar) não mostra erro (US7.5).
- [X] T046 [P] [US7] Em `src/Frontend/src/pages/OperatorsListPage.test.tsx` (mesmo arquivo da T025–T027), acrescentar: (US7.1) falha de rede na carga mostra a mensagem de falha de comunicação no lugar da tabela; (US7.4) `403` na carga mostra `message`/`notifications` da API e a sessão **continua** no `sessionStorage`; `401` na carga apaga a sessão do `sessionStorage` (a regra de sessão vale na tela mais usada; o redirecionamento ao login é coberto pelo `App.test.tsx`); (US7.5) trocar o filtro com a chamada anterior em andamento não exibe erro da chamada abortada.

**Checkpoint**: todas as histórias funcionam e são verificáveis.

---

## Fase 10: Polimento e validações transversais

**Propósito**: componentes restantes com comportamento (Princípio VII), rastreabilidade, registro de
achados e os portões de qualidade.

- [X] T047 [P] Criar `src/Frontend/src/lib/labels.test.ts`: `roleLabel("Admin")` → `Administrador`, `roleLabel("Operator")` → `Operador`, valor desconhecido (cast para `Role`) devolve o próprio valor.
- [X] T048 [P] Criar `src/Frontend/src/components/ThemeToggle.test.tsx` (`renderWithProviders`): com `matchMedia` claro, o botão tem o nome `Usar tema escuro`; ao clicar o `<html>` ganha `data-theme="dark"`, o nome vira `Usar tema claro` e `localStorage` guarda `acs.theme` = `dark`; clicar de novo volta ao claro.
- [X] T049 [P] Criar `src/Frontend/src/context/ThemeContext.test.tsx`: respeita o valor guardado em `localStorage` (`acs.theme`); sem valor guardado segue a preferência do sistema — o teste guarda o `window.matchMedia` definido no `setup.ts`, troca-o por uma versão com `matches: true` (→ `dark`) e **restaura o original no `afterEach`**, para não contaminar os outros testes; `localStorage` indisponível (via `vi.spyOn(Storage.prototype, ...)`) não quebra; `useTheme` fora do provider lança `useTheme deve ser usado dentro de <ThemeProvider>.`.
- [X] T050 Registrar em `specs/20261101-164600-frontend-testing/research.md`, na seção "Achados de caracterização", cada comportamento estranho (ou que não pôde ser testado sem alterar código de produção) encontrado durante as tarefas T016–T049, com o que acontece hoje. Manter A-01 a A-06 (confirmar ou ajustar) e acrescentar os novos. **Nenhum é corrigido.** Se não houver achado novo, dizer isso explicitamente.
- [X] T051 Conferir `specs/20261101-164600-frontend-testing/contracts/cobertura-cenarios.md` contra os testes reais: para cada um dos 49 cenários e dos casos limite, localizar o `it(...)` e atualizar a coluna "Trecho do teste" com o título exato (FR-017, SC-002). Se algum cenário não tiver teste, **criar o teste** (não remover a linha).
- [X] T052 Portões de qualidade, em `src/Frontend`: rodar `npm run typecheck`, `npm test` e `npm run build`. Os três devem passar sem erro nem aviso novo. Colar no relatório o resumo final da suíte (arquivos e testes aprovados, tempo).
- [X] T053 Repetibilidade e tempo (SC-001, SC-004): rodar `npm test` 10 vezes seguidas e confirmar o mesmo resultado em todas (nenhuma intermitência); a ausência de rede real é garantida pela trava de chamada não prevista do `setup.ts`, e a execução offline é feita pelo revisor (o relatório deve dizer explicitamente que ela **não** foi feita pelo implementador); registrar o tempo da suíte completa (deve ficar abaixo de 2 minutos).
- [X] T054 Prova de que os testes protegem o comportamento (SC-003): executar a tabela "V4" do [quickstart.md](quickstart.md) — alterar **temporariamente** cada linha indicada, confirmar que ao menos um teste falha e desfazer com `git checkout -- <arquivo>`. Ao final, `git status` não pode mostrar nenhum arquivo de produção modificado. Registrar o resultado de cada grupo no relatório.
- [X] T055 Prova de que nenhum código de produção mudou (FR-015, SC-007): na raiz, rodar `git diff --stat main -- src/Frontend/src ':!src/Frontend/src/*.test.ts' ':!src/Frontend/src/*.test.tsx' ':!src/Frontend/src/**/*.test.ts' ':!src/Frontend/src/**/*.test.tsx' ':!src/Frontend/src/test'` — a saída deve ser **vazia** (cobre só os arquivos já rastreados). O `git diff` não mostra arquivos novos não rastreados, então rodar também `git status --porcelain --untracked-files=all -- src/Frontend/src`. Regra: todo arquivo novo ou modificado listado deve ser `*.test.ts`, `*.test.tsx` ou estar em `src/Frontend/src/test/`; qualquer outro é violação do FR-015. Rodar ainda `git diff --stat main -- src/Backend` (vazia) e `git status --porcelain --untracked-files=all -- src/Backend` (vazia). Por fim, listar os demais arquivos alterados: `src/Frontend/package.json`, `src/Frontend/package-lock.json`, `src/Frontend/vite.config.ts` e `src/Frontend/README.md` (dentro de `src/Frontend`, mas fora de `src/Frontend/src`), mais `.github/workflows/frontend-ci.yml` e `CLAUDE.md`; nenhum outro é esperado.
- [X] T056 Validação do CI (US1.3 a US1.5, SC-005, SC-006): como o implementador **não** abre PR, deixar escrito no relatório final o roteiro "V6" do [quickstart.md](quickstart.md) para o revisor executar (PR com a mudança → `Frontend CI` verde; PR só de `src/Backend/` → sem execução do front; PR de teste com `it` quebrado → vermelho). Conferir o YAML com uma leitura atenta contra [contracts/frontend-ci.md](contracts/frontend-ci.md), item a item.
- [X] T057 Conferência de dependências (informativa, `npm audit` **não** entra no CI desta spec): rodar `npm audit` em `src/Frontend` e comparar com o estado anterior (2 vulnerabilidades *high*: `vite <=6.4.3` e `source-map-js`). Confirmar que as ferramentas de teste não acrescentaram alerta novo e registrar o resultado no relatório final; **não** corrigir nem suprimir nada (spec 04).

---

## Dependências e ordem de execução

### Dependências entre fases

- **Setup (Fase 1)**: sem dependências.
- **Fundação (Fase 2)**: depende da Fase 1 e **bloqueia** todas as histórias.
- **Histórias (Fases 3 a 9)**: dependem da Fundação. São independentes, com **uma exceção de arquivo
  compartilhado**: `App.test.tsx` (T022 → T033 → T041) e as extensões da US7 (T044, T045, T046)
  acrescentam casos a arquivos criados antes (T018, T020, T025–T027), por isso vêm depois deles.
- **Polimento (Fase 10)**: depende de todas as histórias desejadas.

### Dependências entre tarefas (resumo)

- T004 → T005 → T006 (Red → Green da infraestrutura) → T010; T007 e T008 em paralelo; T009 depende de T005 e T007; T011 fecha a fase.
- T025 → T026 → T027 (mesmo arquivo); T039 → T040; T042 → T043; T022 → T033 → T041.
- T044 depois de T018; T045 depois de T020; T046 depois de T027.
- T050 a T057 só depois de todos os testes escritos.

### Oportunidades de paralelismo

- Depois de T011, as tarefas marcadas `[P]` de histórias diferentes podem andar juntas (arquivos diferentes).
- Dentro da Fase 4: T016, T017, T018, T019, T020 e T021 são independentes entre si.
- Dentro da Fase 7: T034 a T039 são independentes entre si.

### Exemplo de paralelismo: História 2

```text
Tarefa: "Criar src/Frontend/src/lib/session-storage.test.ts"        (T016)
Tarefa: "Criar src/Frontend/src/lib/form-error.test.ts"            (T017)
Tarefa: "Criar src/Frontend/src/lib/api-client.test.ts"            (T018)
Tarefa: "Criar src/Frontend/src/context/AuthContext.test.tsx"      (T019)
Tarefa: "Criar src/Frontend/src/pages/LoginPage.test.tsx"          (T020)
Tarefa: "Criar src/Frontend/src/components/layout/Topbar.test.tsx" (T021)
```

---

## Estratégia de implementação

O revisor prefere entregas por fases. Sugestão de cortes, cada um terminando com os portões
(`npm run typecheck`, `npm test`, `npm run build`) verdes e sem commits do implementador:

1. **Fase A — base**: T001 a T015 (Setup, Fundação e História 1). Entrega: o comando de testes
   funciona, o CI existe e a documentação está pronta. É o MVP.
2. **Fase B — P1**: T016 a T027 (Histórias 2 e 3): login, sessão e lista de operadores.
3. **Fase C — P2**: T028 a T046 (Histórias 4 a 7): navegação, criar/editar, minha conta e falhas.
4. **Fase D — fechamento**: T047 a T057 (polimento, achados, rastreabilidade e provas).

### Entrega incremental

1. Setup + Fundação → base pronta.
2. História 1 → comando e CI (MVP).
3. Histórias 2 e 3 → o que mais muda nas próximas specs fica protegido.
4. Histórias 4 a 7 → cobertura completa dos 49 cenários.
5. Polimento → provas de SC-001 a SC-008.

## Observações

- [P] = arquivos diferentes, sem dependência.
- Cada história é verificável sozinha pelo seu "Teste Independente".
- Os testes são de caracterização: devem passar sobre o código atual. Falha = leitura errada do
  código ou achado, nunca motivo para mexer na tela.
- Não faça commits; o revisor faz.
