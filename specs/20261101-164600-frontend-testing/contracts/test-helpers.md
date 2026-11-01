# Contrato: ferramentas de apoio aos testes (`src/test/`)

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Este é o contrato entre a infraestrutura de teste e quem escreve testes. A base é entregue por esta
spec; as specs seguintes (03 em diante) apenas **usam** estas ferramentas ao fazer TDD. Os nomes
estão em inglês (código); as descrições dos testes são em português.

Nenhum arquivo de `src/test/` entra no bundle de produção (nada em `src/` o importa, exceto outros
arquivos `*.test.*`).

## `setup.ts` (carregado antes de cada arquivo de teste)

| Responsabilidade | Comportamento garantido |
|---|---|
| Matchers de DOM | `@testing-library/jest-dom/vitest` registrado (`toBeInTheDocument`, `toHaveTextContent`…) |
| Servidor MSW | `server.listen()` antes de tudo; `server.resetHandlers()` depois de cada teste; `server.close()` no fim |
| Chamada não prevista | Toda requisição sem handler é registrada e **recusada pela simulação** (o callback lança, e o MSW responde uma 500 sintética; sem isso a chamada seguiria para a rede real); o `afterEach` **reprova o teste** com `Chamada de API não prevista no teste: MÉTODO URL` |
| Limpeza | `cleanup()` do Testing Library e esvaziamento de `sessionStorage` e `localStorage` depois de cada teste |
| Lacunas do jsdom | `<dialog>.showModal/close`, `window.matchMedia` e o `DOMException` global alinhado ao do Node (ver [research.md](../research.md), item 3) |

## `server.ts`

| Exportação | Assinatura | Uso |
|---|---|---|
| `server` | `SetupServer` do MSW | `server.use(...handlers)` dentro do teste para declarar o que a API devolve |
| `apiUrl` | `(path: string) => string` | Monta `API_BASE_URL + path`, igual ao `api-client` |
| `takeUnhandledRequests` | `() => string[]` | Devolve e esvazia o registro de chamadas não previstas (usado só pelo teste da própria infraestrutura) |

## `envelope.ts`

Resultado que uma fábrica de handler pode produzir, e o tipo que as descreve:

```text
Outcome<T> =
  | { kind: "success"; data: T; message?: string; status?: number }     // padrão: 200, message "OK"
  | { kind: "failure"; status: number; message: string; notifications?: ApiNotification[] }
  | { kind: "failure-with-ok-status"; message: string; notifications?: ApiNotification[]; status?: number }  // HTTP 200, isSuccess false; `status` é o do envelope (padrão 400)
  | { kind: "raw"; status: number; body: string }                       // fora do envelope (vazio ou não JSON)
  | { kind: "network-error" }
```

`envelope.ts` também expõe `toResponse(outcome)`, que transforma um `Outcome` na resposta do MSW com o
envelope real `{ message, status, isSuccess, notifications, data }`.

## `handlers.ts` — uma fábrica por endpoint

Todas devolvem um handler do MSW e aceitam, opcionalmente, um espião `onRequest(received)` em que
`received = { method, url, authorization, body }` (corpo já interpretado como JSON, ou `undefined`).

| Fábrica | Endpoint | Parâmetro de resultado |
|---|---|---|
| `loginHandler` | `POST /v1/account/login` | `Outcome<AuthenticatedOperator>` |
| `createOperatorHandler` | `POST /v1/account/create` | `Outcome<CreatedOperator>` |
| `updateOperatorHandler` | `PUT /v1/account/update/:id` | `Outcome<UpdatedOperator>` |
| `listOperatorsHandler` | `GET /v1/account/get-list` | `Outcome<OperatorList>` **ou** função `(query: { page, size, isActive }) => Outcome<OperatorList>` para responder por página/filtro |
| `activateOperatorHandler` | `PUT /v1/account/activate/:id` | `Outcome<OperatorListItem>` |
| `deactivateOperatorHandler` | `PUT /v1/account/deactivate/:id` | `Outcome<OperatorListItem>` |
| `getMeHandler` | `GET /v1/account/get-me` | `Outcome<Me>` |
| `updateMeHandler` | `PUT /v1/account/update-me` | `Outcome<UpdatedMe>` |

Os testes **não** mockam `src/api/`: a tela chama o `fetch` real e o MSW responde.

## `fixtures.ts`

| Exportação | Descrição |
|---|---|
| `buildSession(overrides?)` | `Session` sintética. Padrão: `Operator`, e-mail `@example.test`, token fixo |
| `adminSession`, `operatorSession` | Atalhos para os dois perfis |
| `buildOperator(overrides?)` | `OperatorListItem` com `id` determinístico e e-mail único `@example.test` |
| `buildOperatorList(operators, overrides?)` | `OperatorList` coerente (`total`, `totalPages`, `page`, `size`) |
| `buildNotification(key, message)` | `ApiNotification` |

## `render.tsx`

| Exportação | Descrição |
|---|---|
| `renderWithProviders(ui, options?)` | Renderiza com `ThemeProvider`, `AuthProvider` e `MemoryRouter` **reais**. `options`: `route` (padrão `/`), `routeState` (o `location.state`), `session` (gravada em `sessionStorage` antes de renderizar). Monta a `ui` numa rota cujo `path` é o `pathname` de `route` (a busca não entra no `path`) e registra uma rota "coringa" (`*`) que mostra `Rota atual: <caminho><busca>`, para os testes afirmarem o destino por texto. Por isso `route` deve ser a rota real da tela (ex.: `"/login"`), e nunca igual ao destino do redirecionamento testado (ex.: guardas que mandam para `/` não podem ser montadas em `/`). Devolve o resultado do Testing Library e `user` (`userEvent.setup()`) |
| `renderApp(options?)` | Renderiza o **`App` real** (que traz os próprios providers e `BrowserRouter`) depois de `history.pushState` em `path` (padrão `/`) e de gravar `session`. Devolve `user`. Usado nos testes de navegação e de jornada |
| `storeSession(session)` | Grava o JSON na chave literal `acs.session` do `sessionStorage` |

## Regras para quem escreve testes

1. Consultar por `getByRole`, `getByLabelText` ou `getByText`; nunca por classe CSS, `id` gerado ou
   estrutura do DOM.
2. Interagir com `user` (`userEvent`); `fireEvent` só para eventos que o `user-event` não gera
   (ex.: o `cancel` do `<dialog>`, equivalente ao Esc).
3. Responder à API sempre com as fábricas deste contrato; não criar `fetch` falso.
4. Descrever o comportamento em português **com acentuação correta**:
   `it("mostra as notifications quando a api devolve 400")`, `it("não chama a api ao cancelar")`. Só os
   textos de interface afirmados (que hoje não têm acento) ficam como estão.
5. Um comportamento novo ou alterado nasce com o teste **falhando** antes do código (Princípio VII,
   Red → Green → Refactor).
