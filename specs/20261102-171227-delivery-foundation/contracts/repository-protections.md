# Contrato: proteções do repositório (ruleset, scripts e conferência)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-10 a R-13 | **Spec**: FR-001 a FR-005a, FR-036 a FR-038

O dono roda os scripts com o `gh` autenticado, **no Git Bash** no Windows. **O implementador não
executa nada contra o GitHub real**: os scripts chamam `"$GH_BIN"` (padrão `gh`) e **só os testes**
trocam `GH_BIN` por um `gh` falso que registra as chamadas e devolve respostas enlatadas (o
`--dry-run` real é do dono).

## O ruleset (`scripts/github/protections/main-ruleset.json`)

Corpo enviado a `POST /repos/{dono}/{repo}/rulesets` (criar) ou `PUT .../rulesets/{id}` (atualizar).
Campos conferidos no esquema oficial (`github/rest-api-description`).

```json
{
  "name": "protect-main",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["refs/heads/main"], "exclude": [] } },
  "rules": [
    { "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false } },
    { "type": "required_status_checks",
      "parameters": {
        "required_status_checks": [ { "context": "ci-gate", "integration_id": 15368 } ],
        "strict_required_status_checks_policy": false } },
    { "type": "non_fast_forward" },
    { "type": "deletion" }
  ]
}
```

| Escolha | Motivo |
|---|---|
| `bypass_actors: []` | ninguém fura, nem admin; o dono confere `current_user_can_bypass = never` (conferência à mão) |
| `enforcement: active` | `evaluate` só existe no Enterprise |
| `required_approving_review_count: 0` e os 4 booleanos `false` | PR obrigatório **sem revisão humana** (dono único); os 5 parâmetros são obrigatórios no esquema, por isso explícitos |
| `strict_required_status_checks_policy: false` | **não** exige a branch atualizada com a `main` (decisão do clarify; a rede de segurança é o pós-merge) |
| `integration_id: 15368` (app **GitHub Actions**) | só um check **do Actions** satisfaz o `ci-gate`; um status externo (API de commit status, outro app) com o mesmo nome **não vale**. Valor conferido por `GET /apps/github-actions` (`id 15368`) e pelo `app.id` dos check runs reais do repositório; o esquema o descreve como "the optional integration ID that this status check must originate from". Um valor errado travaria todos os PRs, por isso o `apply-protections.sh` o confere antes de aplicar (passo 2) |
| `refs/heads/main` explícito | a regra é da `main`, não "da branch padrão" |
| `required_status_checks` com **só** `ci-gate` | o gate é o único check exigido (FR-003) |

## `scripts/github/apply-protections.sh`

```text
apply-protections.sh              # aplica (cria ou atualiza o ruleset) e liga a varredura de segredos
apply-protections.sh --dry-run    # LÊ o estado, imprime o corpo e as chamadas; NUNCA escreve (sem POST/PUT/PATCH)
apply-protections.sh --disable    # emergência: enforcement = disabled (a plataforma registra quem mudou)
apply-protections.sh --enable     # religa: enforcement = active
```

Fluxo da aplicação (cada passo falha alto com mensagem em português, sem fingir sucesso):

1. exige `gh` autenticado e `jq`; se faltar, a mensagem diz que **no Windows o script deve rodar no
   Git Bash** (o `bash` do PowerShell é o do WSL, que não enxerga o `gh` do Windows);
2. **ordem de ativação e conferência do `integration_id`**: só prossegue se existir um check
   `ci-gate` concluído na `main` (`GET .../commits/main/check-runs?check_name=ci-gate`,
   `total_count ≥ 1`) **e se o `app.id` desse check for igual ao `integration_id` de
   `main-ruleset.json`**; senão explica (exigir um check que ainda não existe, ou com outra origem,
   travaria todos os PRs) e sai com falha;
3. procura o ruleset de nome `protect-main`: se existe, `PUT` (idempotente); senão, `POST`;
4. **varredura de segredos**: lê `security_and_analysis` (`GET /repos/{dono}/{repo}`, só admin);
   se `secret_scanning` ou `secret_scanning_push_protection` não estiver `enabled`, envia `PATCH`
   com `{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}`
   e **relê para confirmar**; se o GitHub recusar, mostra o passo manual da interface e sai com
   falha;
5. roda a conferência automática (`check-rules.sh`) sobre o que acabou de aplicar e imprime os
   passos da conferência à mão.

## Conferência automática (`scripts/github/check-rules.sh`)

Entrada (stdin): a resposta de `GET /repos/{dono}/{repo}/rules/branches/main`. Lê o **nome do
check esperado** e o **`integration_id` esperado** de `main-ruleset.json` (nunca duplicados). Sai
`0` ou `1`; cada falta numa linha `falta: ...`. **A chamada que alimenta o script é sempre
autenticada** (o workflow passa `GH_TOKEN: ${{ github.token }}`; à mão, o `gh` do dono): o script
não tem caminho anônimo, porque o limite anônimo (60/h por IP) é compartilhado entre runners.

| # | Confere | Falta quando |
|---|---|---|
| 0 | a entrada é um **array JSON** | resposta de erro (inclusive `403` por permissão ou limite), `{}`, vazia → **reprova** (não conseguir ler nunca é aprovar) |
| 1 | regra `pull_request` | ausente |
| 2 | regra `required_status_checks` com um item de `context` igual ao nome esperado **e** `integration_id` igual ao esperado | ausente; sem o `context`; item com o nome certo **sem** `integration_id` ou com outro (um status externo com o mesmo nome passaria a valer) |
| 3 | regra `non_fast_forward` | ausente |
| 4 | regra `deletion` | ausente |

### Formato da resposta (exemplo **montado a partir do esquema oficial**, não capturado)

Hoje o endpoint devolve `[]` (não há ruleset). Cada item é a regra mais os três campos de origem
(`ruleset_source_type`, `ruleset_source`, `ruleset_id`), segundo o esquema `repository-rule-detailed`:

```json
[
  { "type": "pull_request", "ruleset_source_type": "Repository",
    "ruleset_source": "gabriel-ferreira-lima/AccessControlSystem", "ruleset_id": 1234,
    "parameters": { "required_approving_review_count": 0,
                    "dismiss_stale_reviews_on_push": false, "require_code_owner_review": false,
                    "require_last_push_approval": false, "required_review_thread_resolution": false } },
  { "type": "required_status_checks", "ruleset_source_type": "Repository",
    "ruleset_source": "gabriel-ferreira-lima/AccessControlSystem", "ruleset_id": 1234,
    "parameters": { "required_status_checks": [ { "context": "ci-gate", "integration_id": 15368 } ],
                    "strict_required_status_checks_policy": false } },
  { "type": "non_fast_forward", "ruleset_source_type": "Repository",
    "ruleset_source": "gabriel-ferreira-lima/AccessControlSystem", "ruleset_id": 1234 },
  { "type": "deletion", "ruleset_source_type": "Repository",
    "ruleset_source": "gabriel-ferreira-lima/AccessControlSystem", "ruleset_id": 1234 }
]
```

Regras em ruleset `evaluate` ou `disabled` **não são devolvidas** (documentação): desligar o ruleset
faz a conferência automática falhar. **Confirmar com a resposta real** depois que o ruleset existir
(tarefa do dono, quickstart V-07): se o formato diferir do esperado, o teste com respostas
simuladas e o script se ajustam.

## Conferência à mão (`scripts/github/check-protections-manual.sh`)

Para o que o endpoint de regras não mostra (precisa de admin):

| Confere | Como | Esperado |
|---|---|---|
| quem pode furar as regras | `GET /repos/{dono}/{repo}/rulesets` → `bypass_actors` e `current_user_can_bypass` do `protect-main` | `[]` e `never` |
| ruleset ativo | `enforcement` do `protect-main` | `active` |
| varredura de segredos | `GET /repos/{dono}/{repo}` → `security_and_analysis.secret_scanning.status` | `enabled` |
| proteção de envio | `security_and_analysis.secret_scanning_push_protection.status` | `enabled` |

Imprime `OK`/`FALHA` por linha e sai `1` se qualquer uma falhar.

## O que prova cada coisa

| Garantia | Teste |
|---|---|
| corpo do ruleset: nome, `target`, `enforcement`, `bypass_actors` vazio, `conditions`, as 4 regras, os 5 parâmetros obrigatórios do `pull_request`, `strict` falso, `context` = nome do job em `ci-gate.yml`, **`integration_id` presente, inteiro e igual a `15368`, em um único item `ci-gate`** | `RepositoryProtectionsConfigTests` |
| `check-rules.sh` com entradas simuladas: tudo certo; sem `pull_request`; sem o check; check com nome errado; **check com o nome certo sem `integration_id`**; **check com o nome certo e `integration_id` de outro app**; sem `non_fast_forward`; sem `deletion`; lista vazia; objeto de erro (`403`); entrada vazia | `CiScriptsTests` |
| scripts existem, em LF, com `set -euo pipefail`; `--dry-run` não envia nada | `RepositoryProtectionsConfigTests` |
| o guia cobre as proteções, as duas conferências, a ordem de ativação, a emergência, os ensaios, o repositório privado e os resultados | `RepositoryProtectionsConfigTests` (seções por título) |
| aplicação real, conferência real, ensaios | **dono** (quickstart V-07 a V-09) |
