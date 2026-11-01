# Contrato: workflow `frontend-ci.yml`

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Arquivo a criar: `.github/workflows/frontend-ci.yml`, no mesmo estilo do `backend-ci.yml`.

| Item | Valor |
|---|---|
| `name` | `Frontend CI` |
| Gatilhos | `push` na `main` e `pull_request` para a `main` |
| Filtro de `paths` (nos dois gatilhos) | `src/Frontend/**` e `.github/workflows/frontend-ci.yml` |
| Job | id `frontend-build-and-test` (nome distinto do job do backend), `runs-on: ubuntu-latest` |
| Diretório de trabalho | `defaults.run.working-directory: src/Frontend` |
| Checkout | `actions/checkout@v7` (mesma versão do backend) |
| Node | `actions/setup-node@v7`, `node-version: '22'`, `cache: npm`, `cache-dependency-path: src/Frontend/package-lock.json` |

## Passos, em ordem

| # | Nome do passo | Comando | Falha reprova o PR |
|---|---|---|---|
| 1 | Checkout repository | `actions/checkout@v7` | sim |
| 2 | Set up Node.js | `actions/setup-node@v7` | sim |
| 3 | Install dependencies | `npm ci` | sim |
| 4 | Type check | `npm run typecheck` | sim |
| 5 | Run tests | `npm test` | sim |
| 6 | Build | `npm run build` | sim |

## Garantias (rastreiam FR-014, SC-005, SC-006)

- PR que altera qualquer arquivo em `src/Frontend/` ou o workflow: o workflow roda e só fica verde se
  os passos 3 a 6 passarem.
- PR que não toca nesses caminhos: o workflow não roda.
- Sem variáveis de ambiente de API e sem segredos: os testes não usam rede.
- Sem `npm audit`: fica para a spec 04 (decisão registrada em Clarificações).

## Passo administrativo (fora do código)

Decisão do dono em 2026-11-01: `frontend-build-and-test` **não** será marcada como verificação
obrigatória na proteção da `main` até a spec 05. Motivo ([research.md](../research.md), item 10): com
filtro de `paths`, PRs que não tocam o front ficam sem essa verificação e, se ela fosse obrigatória,
não conseguiriam ser mesclados. Até lá, o revisor não mescla pull request com o CI vermelho.
