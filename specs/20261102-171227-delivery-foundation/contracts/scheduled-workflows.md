# Contrato: workflows agendados (auditoria de pacotes e conferência das proteções)

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-06, R-09, R-12 | **Spec**: FR-016 a FR-018,
FR-037, FR-037a

Os dois rodam **só** por `schedule` e `workflow_dispatch` (nunca por pull request), com
`permissions: contents: read`, **sem segredo** e **sem ação de terceiros**. Todo job tem
`timeout-minutes`.

## `package-audit.yml`

| Item | Valor |
|---|---|
| Agenda | `cron: '23 9 * * 1'` (segunda, 09:23 UTC; fora da hora cheia) |
| Sob demanda | `workflow_dispatch` |
| Job `backend-audit` (10 min) | em `src/Backend`: `setup-dotnet` e `dotnet restore Backend.slnx` |
| Job `frontend-audit` (10 min) | em `src/Frontend`: `setup-node`, `npm ci`, `npm audit` |
| Critério | **o mesmo da 04b por construção**: NuGet Audit em `Directory.Build.props` (qualquer severidade; exceção só por aviso, no menor escopo); `npm audit` **sem** `--audit-level`; `CI=true` torna `NU1900` erro |
| Não faz | build, testes, publish (falha de teste não pode virar alarme de vulnerabilidade) |
| Falha de rede | `NU1900` (erro com `CI=true`) e `npm audit` sem registro reprovam: nunca "sem vulnerabilidades" |

## `protections-check.yml`

| Item | Valor |
|---|---|
| Agenda | `cron: '41 9 * * 1'` (segunda, 09:41 UTC) |
| Sob demanda | `workflow_dispatch` |
| Job `check` (5 min) | `actions/checkout`; `gh api repos/${{ github.repository }}/rules/branches/main` com `GH_TOKEN: ${{ github.token }}` e `X-GitHub-Api-Version: 2022-11-28`, a saída em `scripts/github/check-rules.sh` |
| Critério | os 4 pontos de `check-rules.sh` ([repository-protections.md](repository-protections.md)) |
| Autenticação | **sempre autenticada** com `GH_TOKEN: ${{ github.token }}`, **nunca anônima**: o limite anônimo é de 60 requisições por hora **por IP** e os runners compartilham IPs; o do `GITHUB_TOKEN` é de 1.000/h por repositório. O `check-rules.sh` não tem caminho anônimo |
| Permissão | só `contents: read` no workflow. A documentação lista o endpoint **somente** sob "Metadata: read" (token de instalação); a documentação **não afirma** textualmente que o `GITHUB_TOKEN` sempre tem Metadata read, então a **primeira execução real é a prova** (R-12, quickstart V-07). Se vier `403`, a conferência **falha** (nunca aprova) |
| Não faz | alterar nada; ler `bypass_actors` ou a varredura de segredos (isso é a conferência à mão) |

## Aviso ao dono (R-06)

E-mail padrão do GitHub para falha de workflow agendado. Vai para quem **criou** o workflow ou, se
outra pessoa alterar a linha `cron`, para ela (as duas páginas da documentação divergem no padrão
inicial: criador × último a alterar o `cron`). Como o dono aceita os PRs que criam e alteram esses
arquivos, ele recebe nos dois casos. Depende de a conta ter notificações de Actions por e-mail;
**o guia manda conferir**.

## Desativação por inatividade

Num repositório público, a plataforma desativa agendamentos quando não há atividade no repositório
por 60 dias. Reativa-se com um commit que mude a linha `cron` (por quem tem permissão de escrita)
ou manualmente em Actions → o workflow → Enable. O guia explica como notar (o workflow aparece como
desativado na aba Actions) e reativar. Vale para os **dois** workflows.

## O que prova cada coisa

| Garantia | Teste |
|---|---|
| `package-audit.yml`: `schedule` com `cron`, `workflow_dispatch`, `run: dotnet restore Backend.slnx`, `run: npm ci` antes de `run: npm audit` (sem `--audit-level`), sem `pull_request`, sem `secrets.`, `permissions`, `timeout-minutes` | `CiWorkflowsConfigTests` |
| `protections-check.yml`: `schedule` + `workflow_dispatch`, sem `pull_request`, sem `secrets.`, `permissions: contents: read`, usa `check-rules.sh`, `timeout-minutes` | `CiWorkflowsConfigTests` |
| lógica da conferência | `CiScriptsTests` (respostas simuladas) |
| primeira execução real de cada um e chegada do e-mail | **dono** (quickstart V-07, V-08) |
