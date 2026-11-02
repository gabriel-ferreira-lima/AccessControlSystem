# Quickstart: validação ponta a ponta da base de entrega sem nuvem

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Contratos**: [contracts/](contracts/)

Este é o roteiro para **provar** que cada fase funciona. O guia permanente das proteções do
repositório é outro documento (`docs/protecoes-do-repositorio.md`, criado na fase D); aqui há só o
roteiro de validação e os passos manuais do dono. Os comandos do backend rodam em `src/Backend`.

**Pré-requisitos**: .NET 10 SDK; Docker ligado (testes de integração); `bash` e `jq`; `gh`
autenticado **só para o dono** na fase E. **Nada aqui roda contra o GitHub real até a fase E.**

**Atenção no Windows: qual `bash`.** Com o WSL instalado, o `bash` do PATH (e do PowerShell) é o
**atalho do WSL** (`C:\Users\...\AppData\Local\Microsoft\WindowsApps\bash.exe`), que roda o Ubuntu do
WSL e **não** enxerga o `jq` nem o `gh` do Windows. Por isso:

- **Os testes** (`dotnet test`) não usam o `bash` do PATH no Windows: eles procuram a variável de
  ambiente **`ACS_BASH`** (caminho do `bash.exe`) e, se ela não existir, `C:\Program Files\Git\bin\bash.exe`
  (Git para Windows). Se não acharem, **ou** se faltar o `jq` dentro desse `bash`, **falham com
  mensagem clara** (nunca pulam). Para apontar outro Git Bash: `setx ACS_BASH "D:\Git\bin\bash.exe"`
  (e abra um terminal novo). O `bash` do WSL é recusado de propósito.
- **Os comandos `bash scripts/...` deste roteiro e do guia** rodam no **Git Bash** (abra o "Git
  Bash"; no PowerShell, chame `& "C:\Program Files\Git\bin\bash.exe" scripts/...`), nunca no
  PowerShell puro. Se o `apply-protections.sh` reclamar que falta `gh` ou `jq`, quase sempre é o
  `bash` do WSL.
- No Linux e no Actions (`ubuntu-latest`), o `bash` do PATH serve.

## V-01 Linha de base (antes de qualquer mudança)

```bash
dotnet restore Backend.slnx && dotnet build Backend.slnx
dotnet test Backend.slnx            # anotar o total no tasks.md
# em src/Frontend:
npm test                            # anotar o total
```

Esperado: tudo verde; **os totais anotados** valem para a verificação de que nenhum teste foi
apagado, pulado ou enfraquecido (SC-013).

## V-02 Fase A: saúde da Api (FR-019 a FR-023)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~Health"
```

Esperado: `live` `200` sem token e **continua `200` com o catálogo fora**; `ready` `200` com o
catálogo bom e `503` com o catálogo fora, sem esquema e acima de 2 s; resposta exatamente `Healthy`
ou `Unhealthy`; cache de 5 s (N chamadas = 1 consulta); os únicos endpoints sem controller são os
de saúde. Conferência manual opcional com o `docker compose up --build`:

```bash
curl -i http://127.0.0.1:5290/health/live      # 200 Healthy
curl -i http://127.0.0.1:5290/health/ready     # 200 Healthy
docker compose stop db
curl -i http://127.0.0.1:5290/health/ready     # 503 Unhealthy (em até ~5 s, o TTL do cache)
curl -i http://127.0.0.1:5290/health/live      # continua 200
docker compose start db
```

## V-03 Fase B: verbos do Migrator (FR-024 a FR-035)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~SeedStaging|FullyQualifiedName~VerifyNoSeedAdmin|FullyQualifiedName~SeedAdminFingerprint"
dotnet test Backend.slnx --filter "FullyQualifiedName~SeedDev|FullyQualifiedName~MigratorProgram|FullyQualifiedName~SeedAdminPolicy"
```

Esperado: `seed-staging` recusa Development, Production e ambiente ausente (código `5`, nada
escrito); em Staging cria 3 tenants (2 + 1) com operadores `.test`, nenhum com o e-mail fixo,
idempotente; senha ausente ou inválida sai `2` sem criar nada e sem imprimir a senha;
`verify-no-seed-admin` aprova base limpa e reprova (`7`) base contaminada de verdade, **e-mail
alterado com id fixo**, **id trocado com e-mail fixo (outras maiúsculas)** e catálogo contaminado,
reprova (`8`) base inacessível e divergência, aprova base sem tabelas, recusa Development (`6`), e
roda em transação somente leitura. **A segunda linha prova que o `seed-dev` e o `migrate`
continuam iguais** (nenhum teste existente foi editado).

Não rode o `seed-staging` contra o banco do `docker compose` (ele misturaria tenants de
homologação com os de desenvolvimento); os testes usam bancos isolados do Testcontainers.

## V-04 Fase C: workflows e scripts de CI (FR-006 a FR-018, FR-013a)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~BashLocatorTests|FullyQualifiedName~CiWorkflowsConfigTests|FullyQualifiedName~CiScriptsTests|FullyQualifiedName~PackageAuditConfigTests"
```

Esperado: `BashLocatorTests` prova a regra de localização do `bash` (Windows: `ACS_BASH` ou Git Bash;
recusa o atalho do WSL; Linux: PATH; sem `bash` ou sem `jq` **falha**, nunca pula).

Conferência manual dos scripts (sem GitHub), **no Git Bash**:

```bash
printf 'specs/x/spec.md\nREADME.md\n' | bash scripts/ci/detect-areas.sh        # docs_only=true
printf 'src/Frontend/src/a.ts\n'       | bash scripts/ci/detect-areas.sh        # frontend=true
printf '.github/workflows/x.yml\n'     | bash scripts/ci/detect-areas.sh        # backend=true frontend=true
printf ''                              | bash scripts/ci/detect-areas.sh        # tudo aplicável (lista vazia)

NEEDS_JSON='{"changes":{"result":"success"},"backend":{"result":"cancelled"},"frontend":{"result":"skipped"},"k6-short":{"result":"success"}}' \
AREA_BACKEND=true AREA_FRONTEND=false AREA_DOCS_ONLY=false bash scripts/ci/evaluate-gate.sh
# esperado: backend REPROVADA (cancelled), frontend dispensada, k6 não ativado; saída 1
```

Esperado nos testes: gatilhos sem `paths`; `ci-gate` com `if: always()`; todo workflow com
`permissions: contents: read`; nenhum `pull_request_target`, `secrets.` ou ação de terceiros; todo
job com `timeout-minutes`; nome do job `ci-gate` igual ao `context` de `main-ruleset.json`; o job
`k6-short` com `name: "k6 curto (não ativado)"`; o `PackageAuditConfigTests` **verde sem edição**.

## V-05 Fase D: script de proteções e guia (FR-001 a FR-005a, FR-036 a FR-038)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~RepositoryProtectionsConfigTests|FullyQualifiedName~CiScriptsTests"
```

Os testes de `scripts/github/` rodam com um **`gh` falso** (`GH_BIN`): o implementador **não** roda
`apply-protections.sh` com o `gh` real. O `--dry-run` real (que **lê** o GitHub e imprime o que
enviaria, sem nunca escrever) é do dono, no passo 2 do V-07.

Esperado: corpo do ruleset válido (`bypass_actors` vazio, as 4 regras, os 5 parâmetros do
`pull_request`, `strict` falso, `context` = `ci-gate` **com `integration_id` 15368**);
`check-rules.sh` aprova a resposta completa e reprova as 10 variações simuladas (contrato, inclusive
**check com o nome certo sem `integration_id`** e **com `integration_id` de outro app**); os scripts
em LF com `set -euo pipefail`; o guia tem todas as seções exigidas (inclusive "rode no Git Bash").
Nenhum teste toca o GitHub real.

## V-06 Validação do revisor, no GitHub real (depois do push, antes do merge)

O implementador **não** roda isto. O revisor abre PRs **descartáveis já contra a `main`** (mudar a
base depois não dispara) e os fecha com `gh pr close --delete-branch`:

| PR descartável | Esperado no `ci-gate` |
|---|---|
| só `specs/` ou `.md` | aprovado em até 5 min, sem rodar backend nem frontend; ambos "dispensada" |
| só `src/Frontend/` | roda o frontend; backend "dispensada" |
| só `src/Backend/` | roda o backend; frontend "dispensada" |
| backend com um teste quebrado | **reprovado** |
| execução cancelada no meio | **reprovado** (anotar o `result` que o reusable devolveu) |
| arquivo fora das áreas (`docker/x`, `.github/x`) | exige backend **e** frontend |
| PR de fork (ou simulado) | roda sem segredos e com token só leitura |

Conferir também: a **lista de checks do PR** mostra o passo como **"k6 curto (não ativado)"** (o
`name:` do job, para o ✓ nunca parecer uma aprovação) e o resumo traz a mesma linha como **não
ativado** (nunca "aprovada"); o merge deste PR dispara o `ci-gate` no `push` para a `main`.

## Fase E — passos manuais do dono (só depois do merge e do `ci-gate` verde na `main`)

O guia `docs/protecoes-do-repositorio.md` repete estes passos com o texto final. **Nunca** ensaie
antes de V-07 confirmar as proteções.

### V-07 Aplicar e conferir

Todos os comandos `bash scripts/...` abaixo rodam no **Git Bash** (não no PowerShell nem no `bash`
do WSL).

1. Confirme que o `ci-gate` existe e rodou verde na `main` (aba Actions).
2. `bash scripts/github/apply-protections.sh --dry-run` e leia o que seria enviado. O script confere
   que o app que reportou o `ci-gate` na `main` é o do `integration_id` (15368) antes de aplicar.
3. `bash scripts/github/apply-protections.sh` (cria o ruleset, liga a varredura de segredos).
4. `bash scripts/github/check-protections-manual.sh`: `bypass_actors` vazio, `current_user_can_bypass`
   `never`, ruleset `active`, varredura e proteção de envio `enabled`.
5. Em Actions, dispare `protections-check` e `package-audit` (`Run workflow`): ambos verdes. **Esta
   é a prova de que o endpoint de regras funciona com o `GITHUB_TOKEN` só leitura** (a conferência é
   sempre autenticada, nunca anônima; a documentação lista o endpoint em Metadata read, mas não diz
   textualmente que o token sempre a tem) e de que o formato da resposta é o esperado, **inclusive o
   `integration_id` do item `ci-gate`** (o contrato foi montado a partir do esquema oficial). Se der
   `403` ou o formato diferir, avise: o script e os testes se ajustam.
6. Confira em Settings → Notifications → Actions que o e-mail de falha está ligado, e que o
   merge do PR desta spec gerou (ou não) o e-mail do pós-merge (R-06).

### V-08 Falha provocada (opcional, sem tocar a `main`)

Em um PR descartável, quebre um teste de propósito: o `ci-gate` reprova e o aceite fica bloqueado.
Para a conferência automática, rode-a com o ruleset desligado (`apply-protections.sh --disable`,
**por poucos minutos**, depois `--enable`) e confirme que ela falha e que o e-mail chega.

### V-09 Ensaios das proteções (SC-001), cada um **só** depois de V-07

1. Anote o SHA da `main`: `git fetch origin && git rev-parse origin/main` (e confirme no GitHub).
2. **Envio direto** com a conta do dono (commit trivial e desfazível): `git push origin HEAD:main`
   → **recusado**.
3. **Envio forçado** (`git push --force-with-lease origin <commit>:main` com um commit cujo efeito
   é recolocar a `main` no SHA anotado) → **recusado**.
4. **Exclusão**: `git push origin :main` → **recusada**. (Se algo passasse, recrie pela API com o SHA
   anotado: `gh api repos/{dono}/{repo}/git/refs -f ref=refs/heads/main -f sha=<SHA>`.)
5. Registre no guia o resultado e a data.

## Quality gates da constituição (antes de abrir o PR)

```bash
dotnet restore Backend.slnx          # NuGet Audit
dotnet build Backend.slnx
dotnet test Backend.slnx             # com Docker ligado
# src/Frontend não muda, mas rode se tocar algo: npm run typecheck && npm test && npm run build && npm audit
```

Esperado: tudo verde, totais **iguais ou maiores** que os de V-01, nenhum teste existente editado
(se algum precisar mudar, a tabela "Testes existentes afetados" do `tasks.md` o declara).
