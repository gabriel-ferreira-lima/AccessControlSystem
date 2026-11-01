# Pesquisa: Base de testes do frontend

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-11-01

Todas as decisões abaixo foram **validadas num protótipo descartável** (cópia do `src/Frontend`
em pasta temporária, fora do repositório): instalação das bibliotecas, `tsc -b`, `vite build` e
testes reais sobre `api-client.ts` e `App.tsx`. Nada disso foi para o repositório.

Não restou nenhum `NEEDS CLARIFICATION`.

## 1. Versões das ferramentas e versão do Node

- **Decisão**: usar o conjunto abaixo, todo compatível com o Vite 6.4.3 e o TypeScript 5.8.3 que
  o projeto já tem (nenhum deles é atualizado nesta spec).

  | Pacote | Faixa no `package.json` | Resolvido no protótipo | Observação |
  |---|---|---|---|
  | `vitest` | `^4.1.0` | 4.1.12 | peer `vite ^6 \|\| ^7 \|\| ^8`; Node `^20 \|\| ^22 \|\| >=24` |
  | `jsdom` | `^27.4.0` | 27.4.0 | Node `^20.19 \|\| ^22.12 \|\| >=24` |
  | `msw` | `^2.14.0` | 2.15.0 | peer `typescript >= 4.8`; Node `>=18` |
  | `@testing-library/react` | `^16.3.0` | 16.3.3 | peer `@testing-library/dom ^10` |
  | `@testing-library/dom` | `^10.4.0` | 10.4.2 | peer obrigatório do `react` e do `jest-dom`; listado explicitamente |
  | `@testing-library/user-event` | `^14.6.0` | 14.6.8 | |
  | `@testing-library/jest-dom` | `~6.9.1` | 6.9.1 | a 6.10 passou a exigir Node >= 22, por isso o `~` |

- **Node**: o mínimo documentado passa de "20+" para **20.19+** (exigência do jsdom 27). O CI usa
  **Node 22**, o mesmo do `Dockerfile` do front (`node:22-alpine`). O protótipo rodou em Node 24.22;
  a primeira execução do CI (Node 22) é a validação final.
- **Por que não as versões mais novas**: `vitest 5` e `jest-dom 7` exigem Node >= 22; `msw 3` exige
  Node >= 22.12 **e** TypeScript >= 5.9 (o projeto está em 5.8.3); `jsdom 30` exige Node >= 22.22.
  Subir o TypeScript ou o Vite é mudança de toolchain de produção, fora desta spec (e o Vite é
  assunto da spec 04).
- **Alternativas**: Jest (rejeitado: o diretório de ferramentas da constituição é Vitest e ele
  reaproveita a configuração do Vite); `happy-dom` (rejeitado: a diretriz fixa jsdom).

## 2. Configuração dentro do `vite.config.ts`

- **Decisão**: importar `defineConfig` de `vitest/config` e acrescentar o bloco `test`:
  `environment: "jsdom"`, `setupFiles: ["./src/test/setup.ts"]`,
  `include: ["src/**/*.test.{ts,tsx}"]`, `env: { VITE_API_BASE_URL: "http://api.test" }`.
  Sem `globals`: os testes importam `describe`/`it`/`expect`/`vi` de `vitest`.
- **Por quê**: validado que `tsc -b` (que compila `vite.config.ts` pelo `tsconfig.node.json` e os
  testes pelo `tsconfig.app.json`, pois `include: ["src"]`) e `vite build` continuam passando, e
  que o bundle de produção não leva os testes.
- **`env` fixo**: validado que `test.env` **sobrescreve** um `.env.local` da máquina de quem
  desenvolve (testado com `VITE_API_BASE_URL=/`, o valor que o Docker usa). Sem isso, a base da API
  mudaria conforme a máquina. Os handlers do MSW montam a URL a partir de `API_BASE_URL`
  (exportada por `src/lib/api-client.ts`), então nunca divergem do código de produção.

## 3. Lacunas do jsdom que o `setup.ts` precisa cobrir

Medidas no protótipo (jsdom 27.4.0):

| Lacuna | Efeito sem o ajuste | Ajuste (só em `src/test/setup.ts`) |
|---|---|---|
| `HTMLDialogElement.showModal` e `close` não existem | `ConfirmDialog` quebra ao abrir | Implementar `showModal` (marca o atributo `open`) e `close` (remove `open` e dispara `close`) |
| `window.matchMedia` não existe | `ThemeProvider` usa `matchMedia?.()` na leitura inicial, mas chama `window.matchMedia(...)` sem `?.` no efeito e quebra | Implementação mínima que devolve `matches: false` e aceita `addEventListener`/`removeEventListener` |
| `DOMException` do jsdom ≠ `DOMException` do Node | O `fetch` do Node rejeita o abort com o `DOMException` do Node; `isAbortError` (`instanceof DOMException` global) devolve `false` e a chamada abortada vira "erro de rede" | Substituir o `DOMException` global pelo do Node (obtido de `AbortSignal.abort().reason.constructor`). Reproduz o navegador, onde há um só `DOMException` |
| `Esc` no `<dialog>` não dispara `cancel` | `userEvent.keyboard("{Escape}")` não fecha a janela | O teste dispara `fireEvent(dialog, new Event("cancel", { cancelable: true }))`, que é exatamente o evento que o navegador envia |

- **Importante**: nada disso altera código de produção (FR-015, SC-007). São diferenças entre o
  ambiente de teste e o navegador, e ficam documentadas no próprio `setup.ts`.

## 4. "Chamada não prevista" precisa reprovar o teste

- **Descoberta**: `onUnhandledRequest: "error"` do MSW apenas **imprime o erro e rejeita a requisição
  com falha de rede**. Se o código engolir a falha (por exemplo, mostrando "Não foi possível se
  comunicar..."), o teste pode passar sem que ninguém perceba. Isso descumpriria o FR-002
  ("DEVEM falhar quando o código fizer uma chamada não prevista").
- **Decisão**: usar `onUnhandledRequest` como função que **registra** `MÉTODO URL` numa lista; o
  `afterEach` do `setup.ts` lança `Error("Chamada de API não prevista no teste: ...")` se a lista não
  estiver vazia. Validado no protótipo: o teste que chama uma rota sem handler falha com a URL
  apontada. Isso cumpre a intenção da diretriz (`"error"`) de forma garantida.
- **Alternativa rejeitada**: `onUnhandledRequest: "error"` puro (não reprova o teste quando o erro é
  tratado pela tela).

## 5. Como testar navegação e guardas sem tocar em `App.tsx`

- **Problema**: `App.tsx` cria o próprio `BrowserRouter` e os providers. Não dá para injetar um
  `MemoryRouter` sem alterar o código de produção (proibido pelo FR-015).
- **Decisão**: os testes de navegação renderizam o **`App` real** depois de
  `window.history.pushState({}, "", "/rota")` e com a sessão gravada em `sessionStorage`. Assim a
  tabela de rotas de verdade (inclusive os `RequireRole`) é a que está sendo caracterizada, sem cópia
  que possa divergir. Validado no protótipo: `Admin` em `/minha-conta` termina em `/`.
- Os testes de cada **página** usam `MemoryRouter` com os providers reais (`ThemeProvider`,
  `AuthProvider`) e, quando a tela depende de `location.state` (edição vinda da lista), passam o
  estado na entrada inicial. Para a ligação lista → edição existe um teste que percorre o `App` real.
- O teste de jornada "401 derruba a sessão" também usa o `App` real, porque depende do
  `RequireSession` redirecionar para `/login` depois que o `AuthProvider` zera a sessão.

## 6. Sessão pré-carregada

- **Decisão**: o helper grava o JSON em `sessionStorage` na chave literal `acs.session` **antes** de
  renderizar, em vez de chamar `writeSession`. Se alguém renomear a chave em
  `src/lib/session-storage.ts`, os testes de página quebram junto com o teste de unidade da própria
  chave (que lê `sessionStorage` direto). O `afterEach` limpa `sessionStorage` e `localStorage`.

## 7. Forma dos handlers do MSW

- **Decisão**: uma fábrica por endpoint (8 no total), tipada com os tipos de `src/types/api.ts`,
  que devolve o envelope real `{ message, status, isSuccess, notifications, data }`. Cada fábrica
  aceita um resultado do tipo: sucesso, falha da API (status, `message`, `notifications`), falha de
  rede, ou corpo cru (para "resposta fora do envelope"). Fábricas aceitam um espião opcional
  (`onRequest`) para os testes inspecionarem método, URL, cabeçalho `Authorization` e corpo.
- **Por quê**: a constituição manda simular na camada de rede com o envelope real e **não** mockar
  `src/api/`. Inspecionar o corpo enviado é o que permite provar "envia só os campos alterados"
  sem espiar funções internas.

## 8. Descrições e rastreabilidade (FR-017)

- **Decisão**: `describe` com o nome do componente/tela e `it` em português descrevendo o
  comportamento. A rastreabilidade cenário → teste fica em
  [contracts/cobertura-cenarios.md](contracts/cobertura-cenarios.md); cada linha cita o arquivo e o
  trecho do `it`. A tabela é conferida na etapa de convergência.

## 9. Consultas aos elementos

- Verificado no código que `TextField`, `PasswordField` e `SelectField` ligam `label` ↔ `input` por
  `htmlFor`/`id`, o `Alert` usa `role="alert"` (erro) e `role="status"`, e os erros de campo têm
  `role="alert"`. Logo, todas as consultas previstas usam `getByRole`/`getByLabelText`/`getByText`.
- Pontos de atenção que **podem** virar achado na implementação (se a consulta acessível não
  funcionar, registra-se aqui e **não** se altera a tela):
  - o rótulo do campo "Perfil de acesso" em "Minha conta" é um `<span>` sem associação a um controle
    (é texto estático, não um campo), então só dá para consultá-lo por texto;
  - botões "Ativar"/"Desativar" repetem o mesmo nome em cada linha; os testes localizam a linha pelo
    e-mail (`within(linha)`).

## 10. CI

- **Decisão**: `.github/workflows/frontend-ci.yml`, espelhando o `backend-ci.yml`: gatilhos `push`
  e `pull_request` na `main` com `paths` `src/Frontend/**` e o próprio workflow; `defaults.run
  .working-directory: src/Frontend`; `actions/checkout@v7`; `actions/setup-node@v7` com Node 22 e
  `cache: npm` (`cache-dependency-path: src/Frontend/package-lock.json`); `npm ci`;
  `npm run typecheck`; `npm test`; `npm run build`. Sem `npm audit` (decisão da clarificação).
- **Nome do job**: `frontend-build-and-test` (e não `build-and-test`, que o backend já usa). A
  proteção de branch identifica a verificação pelo nome do job, e nomes iguais em dois workflows
  ficam ambíguos.
- **Armadilha para o dono do repositório** (não é código desta spec): um workflow com filtro de
  `paths` **não roda** em pull requests que não tocam o front. Se a verificação for marcada como
  *obrigatória* na proteção da `main`, esses pull requests ficam eternamente "aguardando" e não
  podem ser mesclados. Isso já vale para o `backend-ci.yml`. A spec 05 (ambientes e CI/CD) deve
  resolver o problema com um job agregador que sempre roda (o roteiro da 05 já prevê isso). Até lá, o SC-006 é mantido (CI do front não
  roda sem mudança no front). **Decisão do dono em 2026-11-01**: a verificação do front não será
  marcada como obrigatória na `main` até a spec 05; até lá, o revisor não mescla pull request com o
  CI vermelho.
- **Fora desta spec**: `npm audit` (spec 04). No protótipo, `npm audit` antes e depois de instalar as
  ferramentas de teste dá o **mesmo resultado** (2 vulnerabilidades *high* em `vite`, preexistentes),
  ou seja, as ferramentas escolhidas não introduzem vulnerabilidade nova.

## 11. Scripts de instalação do npm

- O npm 11.19 avisa que `esbuild` e `msw` têm scripts de pós-instalação ainda não aprovados
  (`npm install-scripts`). É só aviso: o `esbuild` já existia e o `msw` usa o script apenas para
  copiar o *service worker* do navegador, que não é usado em Node. Os testes e o `npm ci` funcionam
  sem aprovar nada.

## 12. Tempo de execução

- Protótipo com 2 arquivos e 3 testes completos: ~3,7 s no total (~1,6 s de ambiente). A suíte
  completa (~25 arquivos) deve ficar bem abaixo do limite do SC-001 (2 minutos); a medição real é
  tarefa de validação.

## Achados de caracterização

Registro de comportamentos que parecem estranhos. **Nenhum é corrigido nesta spec** (FR-015,
SC-008). A tabela é preenchida durante a implementação; já há candidatos vindos da leitura do código:

| # | Comportamento observado hoje | Onde | Observação |
|---|---|---|---|
| A-01 | Perfil `Admin` é redirecionado de `/minha-conta` para `/` | `App.tsx` (`RequireRole role="Operator"`) | Intencional segundo o `README` do front; muda na spec 08 |
| A-02 | Edição aberta direto (sem `location.state`) sempre envia `role`, mesmo sem alteração | `EditOperatorPage.tsx` | Documentado no `README` |
| A-03 | Edição/Minha conta sem nenhuma alteração enviam corpo `{}` e deixam a API decidir | `EditOperatorPage.tsx`, `MyAccountPage.tsx` | Coerente com o Princípio VIII (front não valida) |
| A-04 | Textos de interface sem acento | todo o front | Legado conhecido; spec 03 |
| A-05 | Rótulo "Perfil de acesso" em "Minha conta" não está associado a um controle | `MyAccountPage.tsx` | Só consultável por texto |
| A-06 | Controle de acesso por nome de role fixo (`Admin`/`Operator`) | `RequireRole`, `Sidebar` | Legado conhecido; spec 08 |
