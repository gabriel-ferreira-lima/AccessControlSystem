# Contrato: o gate de CI (`ci-gate.yml`)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-01 a R-08 | **Spec**: FR-006 a FR-015, FR-013a

O gate é **o único check exigido** pelo ruleset. Sempre roda, decide o que é aplicável, chama os
dois workflows existentes como reusable workflows e confere o resultado real de cada um.

## Gatilhos

| Evento | Detalhe |
|---|---|
| `pull_request` | `branches: [main]`, **sem** `paths` |
| `push` | `branches: [main]`, **sem** `paths` (verificação pós-merge, FR-013a) |

**Sem `workflow_dispatch`**: pela documentação, a execução manual **não satisfaz** um check exigido.
Nenhum `pull_request_target`. Nenhum `secrets.*`.

**Quando a plataforma nem inicia o gate** (FR-006, casos de borda; o aceite fica **bloqueado**, nunca
aprovado): (1) PR com **conflito de merge**: os workflows de `pull_request` não rodam até o conflito
ser resolvido; (2) PR de **fork de primeiro contribuinte**: a execução espera o dono aprovar ("Approve
and run workflows") e, depois, roda sem segredos e só com leitura. Não há o que configurar no
workflow: o guia descreve o que o dono vê e como sair.

Topo do arquivo: `permissions: contents: read`; `concurrency` com
`group: ci-gate-${{ github.event.pull_request.number || github.ref }}` e
`cancel-in-progress: ${{ github.event_name == 'pull_request' }}`.

## Jobs

| Job (id → `name:` exibido) | `needs` | Roda quando | Tempo limite | O que faz |
|---|---|---|---|---|
| `changes` → `changes` | — | sempre | 3 min | checkout (`fetch-depth` 2 no PR, 0 no push); `git diff --name-only` → `scripts/ci/detect-areas.sh`; saídas `backend`, `frontend`, `docs_only` |
| `backend` → `backend` | `changes` | `backend == 'true'` | 15 min (no job chamado) | `uses: ./.github/workflows/backend-ci.yml` |
| `frontend` → `frontend` | `changes` | `frontend == 'true'` | 10 min (no job chamado) | `uses: ./.github/workflows/frontend-ci.yml` |
| `k6-short` → **`k6 curto (não ativado)`** | `changes` | sempre | 2 min | escreve "NÃO ATIVADO" no `$GITHUB_STEP_SUMMARY` e termina com sucesso (saída `k6_active=false`); o `name:` faz a **lista de checks do PR** mostrar o estado ao lado do ✓ |
| **`ci-gate`** | `changes`, `backend`, `frontend`, `k6-short` | **`if: always()`** (nunca é pulado) | 3 min | `scripts/ci/evaluate-gate.sh` com `NEEDS_JSON: ${{ toJSON(needs) }}` e as três saídas de área; escreve a tabela no resumo; sai `0` ou `1` |

O nome do job final **`ci-gate`** é o `context` exigido pelo ruleset, **preso ao `integration_id` do
GitHub Actions (15368)**. Os jobs chamados por reusable workflow aparecem como
`backend / build-and-test` e `frontend / frontend-build-and-test`; **esses nomes não são exigidos**.
O `needs` usa **ids** (`k6-short`), nunca o `name:` exibido: trocar o texto "k6 curto (não ativado)"
(na spec 06, para "k6 curto") não mexe no `evaluate-gate.sh`.

## Os workflows chamados

`backend-ci.yml` e `frontend-ci.yml` **mantêm os passos e os nomes de job de hoje** (o
`PackageAuditConfigTests` lê `run: dotnet restore Backend.slnx` antes do build e `run: npm audit`
depois de `run: npm ci`). Mudam só:

- gatilhos: saem `push` e `pull_request` com `paths`; entram `workflow_call` e `workflow_dispatch`
  (este só para depurar o arquivo isolado);
- `permissions: contents: read` no topo;
- `timeout-minutes` no job (15 e 10).

## Regras de avaliação (`evaluate-gate.sh`)

Entradas: `NEEDS_JSON` (`toJSON(needs)`), `AREA_BACKEND`, `AREA_FRONTEND`, `AREA_DOCS_ONLY`.

| Verificação | Aplicável quando | Aceito | Qualquer outro resultado |
|---|---|---|---|
| `changes` | sempre | `success` | reprova |
| `backend` | `AREA_BACKEND=true` | `success` | reprova (`failure`, `cancelled`, `skipped`) |
| `backend` | `AREA_BACKEND=false` | `skipped` (dispensada) | reprova |
| `frontend` | `AREA_FRONTEND=true` | `success` | reprova |
| `frontend` | `AREA_FRONTEND=false` | `skipped` (dispensada) | reprova |
| `k6-short` (linha **k6 curto (não ativado)**) | sempre | `success`, mostrado como **não ativado** | reprova |

Reprova também: chave ausente em `NEEDS_JSON`; área vazia ou diferente de `true`/`false`; as três
saídas todas `false` ao mesmo tempo (estado incoerente). **Nunca** aprova por falta de resultado.

## Resumo (`$GITHUB_STEP_SUMMARY`)

```text
## Resultado do gate

| Verificação | Estado | Motivo |
|---|---|---|
| changes   | aprovada   | |
| backend   | aprovada   | |
| frontend  | dispensada | nenhum arquivo de frontend mudou |
| k6 curto (não ativado) | não ativado | cenários de carga: spec 06 |

Resultado do gate: APROVADO
```

Estados: **aprovada**, **reprovada**, **dispensada**, **não ativada** (data-model, seção 2).

## O que prova cada coisa

| Garantia | Teste |
|---|---|
| tabela de listas de arquivos → áreas | `CiScriptsTests` (detect-areas) |
| tabela `needs` × áreas → veredito e linha do resumo (inclui a linha "k6 curto (não ativado)") | `CiScriptsTests` (evaluate-gate) |
| o `name:` do job `k6-short` é exatamente "k6 curto (não ativado)" enquanto ele for o reservado (a spec 06 troca a asserção junto com o nome) | `CiWorkflowsConfigTests` |
| os testes de script acham o `bash` por `ACS_BASH`/Git Bash (Windows) ou PATH (Linux), recusam o WSL e falham, sem pular, se faltar `bash` ou `jq` | `BashLocatorTests` |
| gatilhos sem `paths`; job `ci-gate` com `if: always()` e `name: ci-gate`; `needs` lista os 4 jobs; sem `workflow_dispatch`; sem `pull_request_target`; sem `secrets.`; todo `uses:` é `actions/*` ou `./`; todo job com `timeout-minutes`; todo workflow com `permissions:` | `CiWorkflowsConfigTests` |
| `backend-ci.yml`/`frontend-ci.yml` têm `workflow_call` e **não** têm `paths`; os passos de auditoria seguem na ordem exigida | `CiWorkflowsConfigTests` + `PackageAuditConfigTests` (inalterado) |
| o nome do job `ci-gate` é igual ao `context` de `main-ruleset.json` | `CiWorkflowsConfigTests` |
| comportamento real no GitHub (documentos; só front; só back; teste quebrado; cancelado; fora das áreas; cancelamento em reusable) | PRs descartáveis do **revisor, partindo da `main` depois do merge e antes do ruleset** (quickstart V-06; antes do merge só valem o V-06a e o V-06b) |
