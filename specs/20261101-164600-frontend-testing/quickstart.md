# Guia de validação: Base de testes do frontend

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Passo a passo para provar que a entrega funciona. Todos os comandos rodam em `src/Frontend`, salvo
indicação. Não precisa de API, banco nem Docker.

## Pré-requisitos

- Node.js **20.19+** (CI usa 22; o protótipo rodou em 24).
- Dependências instaladas: `npm ci`.

## V1 — Rodar tudo (US1, SC-001, SC-004)

```bash
npm ci
npm test            # vitest run: roda uma vez e sai
npm run test:watch  # modo interativo, reroda ao salvar (para o TDD)
```

Esperado: todos os arquivos `*.test.ts(x)` aprovados, sem erro de "chamada não prevista", em menos de
2 minutos. Repita `npm test` 10 vezes seguidas: o resultado deve ser idêntico (SC-004). Para provar
que nada sai para a rede, desligue a internet e rode de novo.

## V2 — Portões de qualidade do front (Constituição, "Fluxo de Desenvolvimento")

```bash
npm run typecheck
npm test
npm run build
```

Esperado: os três terminam sem erro. O `typecheck` e o `build` incluem os arquivos de teste (estão em
`src/`), mas o pacote gerado em `dist/` não contém nenhum código de teste.

## V3 — Chamada não prevista reprova o teste (FR-002)

Escreva (temporariamente, sem commitar) um teste que renderize uma tela e dispare uma chamada para
uma rota sem handler. Esperado: o teste falha com
`Chamada de API não prevista no teste: GET <url>`. O teste permanente dessa garantia está em
`src/test/server.test.ts`.

## V4 — Os testes protegem o comportamento (SC-003)

Para cada grupo abaixo, altere **temporariamente** a linha indicada, rode `npm test` e confirme que
ao menos um teste falha; depois desfaça com `git checkout -- <arquivo>`. Nada disto é commitado.

| Grupo | Alteração temporária | Falha esperada em |
|---|---|---|
| Login | Em `LoginPage.tsx`, remover o `setError(toFormError(err))` | `LoginPage.test.tsx` |
| Sessão | Em `api-client.ts`, remover `unauthorizedHandler?.()` | `api-client.test.ts`, `App.test.tsx` |
| Navegação | Em `RequireRole.tsx`, trocar `!==` por `===` | `RequireRole.test.tsx`, `App.test.tsx` |
| Lista | Em `OperatorsListPage.tsx`, trocar o filtro inicial `"Active"` por `"All"` | `OperatorsListPage.test.tsx` |
| Confirmação | Em `OperatorsListPage.tsx`, remover o `setReloadKey` depois de confirmar | `OperatorsListPage.test.tsx` |
| Criar | Em `CreateOperatorPage.tsx`, enviar `{ email, password }` sem o `role` | `CreateOperatorPage.test.tsx` |
| Editar | Em `EditOperatorPage.tsx`, enviar sempre o e-mail | `EditOperatorPage.test.tsx` |
| Minha conta | Em `MyAccountPage.tsx`, enviar sempre o e-mail | `MyAccountPage.test.tsx` |
| Falha de comunicação | Em `api-client.ts`, mudar o texto de `NETWORK_ERROR` | `api-client.test.ts` |

## V5 — Nenhuma linha de produção mudou (FR-015, SC-007)

Na raiz do repositório:

```bash
git diff --stat main -- src/Frontend/src ':!src/Frontend/src/*.test.ts' ':!src/Frontend/src/*.test.tsx' ':!src/Frontend/src/**/*.test.ts' ':!src/Frontend/src/**/*.test.tsx' ':!src/Frontend/src/test'
```

O `git diff` não mostra arquivos novos não rastreados, então rode também:

```bash
git status --porcelain --untracked-files=all -- src/Frontend/src
```

Esperado: o `git diff` acima vazio e, na listagem do `git status`, só `*.test.ts`, `*.test.tsx` ou
arquivos em `src/Frontend/src/test/`. Mudanças legítimas ficam dentro de `src/Frontend`, mas fora de
`src/Frontend/src`: `package.json`, `package-lock.json`, `vite.config.ts` e `README.md` (mais
`.github/workflows/frontend-ci.yml` e `CLAUDE.md`).

## V6 — CI (US1.3 a US1.5, SC-005, SC-006)

1. Abra um PR com a mudança desta spec: o workflow `Frontend CI` deve rodar `npm ci`, typecheck,
   testes e build, e ficar verde.
2. Num PR de teste que altera só `src/Backend/`, o workflow do front **não** deve rodar.
3. Num PR de teste com um `it` quebrado de propósito em `src/Frontend/`, o workflow deve terminar
   vermelho e a saída deve apontar o teste (depois, descarte o PR).

Decisão do dono (2026-11-01): `frontend-build-and-test` **não** é marcada como obrigatória na `main`
até a spec 05; até lá, o revisor não mescla pull request com o CI vermelho. Motivo em
[contracts/frontend-ci.md](contracts/frontend-ci.md).

## V7 — Rastreabilidade (FR-017, SC-002)

Percorra [contracts/cobertura-cenarios.md](contracts/cobertura-cenarios.md): cada cenário da spec deve
ter seu arquivo de teste e um `it(...)` correspondente, localizável por busca.

## Como escrever um teste novo (FR-016)

1. Escreva o `it` em português, ao lado do arquivo testado, e rode-o: ele deve **falhar** (Red).
2. Declare o que a API devolve com as fábricas de `src/test/handlers.ts` via `server.use(...)`.
3. Renderize com `renderWithProviders` (uma tela) ou `renderApp` (rotas e jornadas); para entrar já
   autenticado, passe `session: adminSession` ou `operatorSession`.
4. Interaja com o `user` devolvido e consulte por papel, rótulo ou texto.
5. Implemente o mínimo para passar (Green) e refatore.

Detalhes das ferramentas: [contracts/test-helpers.md](contracts/test-helpers.md).
