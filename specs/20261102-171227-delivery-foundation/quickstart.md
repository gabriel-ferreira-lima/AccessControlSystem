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
npm run typecheck && npm test && npm run build   # anotar o total de testes
npm audit                           # esperado: 0 vulnerabilidades (a constituição exige, sempre)
```

Esperado: tudo verde; **os totais anotados** (e o resultado do `npm audit`) valem para a
verificação de que nenhum teste foi apagado, pulado ou enfraquecido (SC-013) e de que não há
pacote vulnerável. Anote também o `HEAD` (`git rev-parse --short HEAD`).

## V-02 Fase A: saúde da Api (FR-019 a FR-023)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~Health"
```

Esperado: `live` `200` sem token e **continua `200` com o catálogo fora**; `ready` `200` com o
catálogo bom e `503` com o catálogo fora, sem esquema e acima de 2 s; resposta exatamente `Healthy`
ou `Unhealthy`; **no máximo 1 consulta ao catálogo a cada 5 s por instância**, sob qualquer volume
de chamadas (SC-008, provado com uma sonda falsa que conta as chamadas, sem medir tempo) e o estado
refletindo a queda e a volta do catálogo em até 5 s; um token válido ou inválido em `/health/*`
não muda nada e não consulta nada, e **um endpoint protegido segue aceitando token válido e
recusando inválido**; os únicos endpoints sem controller são os de saúde. Conferência manual
opcional com o `docker compose up --build`:

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
dotnet test Backend.slnx --filter "FullyQualifiedName~SeedStaging|FullyQualifiedName~VerifyNoSeedAdmin|FullyQualifiedName~SeedAdminFingerprint|FullyQualifiedName~FictitiousPasswordsNotInRepo"
dotnet test Backend.slnx --filter "FullyQualifiedName~SeedDev|FullyQualifiedName~MigratorProgram|FullyQualifiedName~SeedAdminPolicy"
```

Esperado: `seed-staging` recusa Development, Production e ambiente ausente (código `5`, nada
escrito); em Staging, **partindo de bases vazias e sem registro**, registra as bases e os tenants
no catálogo, atualiza a estrutura (com a `SeedAdminPolicy`) e cria 3 tenants (2 + 1) com
operadores `.test`, nenhum com o e-mail fixo, idempotente; se a estrutura de alguma base não puder
ser atualizada, sai `1` **sem criar nenhum operador** (o registro no catálogo pode ficar) e uma
nova execução, com a causa corrigida, conclui; senha ausente ou inválida sai `2` sem criar nada
e sem imprimir a senha; **nenhuma senha fictícia literal** no código de produção, nos
`appsettings*.json`, nos `docker-compose*.yml` nem em `.github/` (SC-010,
`FictitiousPasswordsNotInRepoTests`, com controle negativo);
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

**Prova local de que a auditoria reprova** (sem GitHub; nada disto entra no repositório): em
pastas temporárias **fora** do repositório,

```bash
# .NET: .csproj mínimo (net10.0) com Newtonsoft.Json 12.0.3 + cópia de src/Backend/Directory.Build.props
CI=true dotnet restore              # esperado: FALHA com NU1903/NU1904 (vulnerabilidade), não NU1900 (rede)
# npm: package.json mínimo com lodash@4.17.15 + lock gerado por `npm install --package-lock-only`
npm audit                           # esperado: código de saída diferente de zero
npm audit --registry http://127.0.0.1:1   # esperado: diferente de zero (falha de consulta reprova)
```

Conferir depois que `git status --short` do repositório não mudou. Se o `dotnet restore` passar ou
o `npm audit` sair `0`, **pare**: a auditoria não estaria protegendo.

## V-05 Fase D: script de proteções e guia (FR-001 a FR-005a, FR-036 a FR-038)

```bash
dotnet test Backend.slnx --filter "FullyQualifiedName~RepositoryProtectionsConfigTests|FullyQualifiedName~CiScriptsTests"
```

Os testes de `scripts/github/` rodam com um **`gh` falso** (`GH_BIN`): o implementador **não** roda
`apply-protections.sh` com o `gh` real. O `--dry-run` real (que **lê** o GitHub e imprime o que
enviaria, sem nunca escrever) é do dono, no passo 2 do V-07.

Esperado: corpo do ruleset válido (`bypass_actors` vazio, as 4 regras, os 5 parâmetros do
`pull_request`, `strict` falso, `context` = `ci-gate` **com `integration_id` 15368**);
`check-rules.sh` aprova a resposta completa e reprova as variações simuladas (contrato, inclusive
**check com o nome certo sem `integration_id`** e **com `integration_id` de outro app**); os scripts
em LF com `set -euo pipefail`; o guia tem as dez seções exigidas (inclusive "rode no Git Bash").
Com o **`gh` falso**, o `apply-protections.sh`: no `--dry-run` **não escreve nada** (nenhum `POST`,
`PUT` ou `PATCH`) e mostra o corpo; **para sem escrever** se o check `ci-gate` não existe na `main`
(ordem de ativação) ou se ele vem de um aplicativo de `integration_id` diferente do exigido;
**cria** o ruleset com **um** `POST` do `main-ruleset.json` se ele não existe e **atualiza** com
**um** `PUT` no mesmo id se existe (nunca cria dois); liga a varredura de segredos só se estiver
desligada, com **um** `PATCH` seguido de **releitura** (e falha com o passo manual da interface se o
`PATCH` for recusado); `--disable` e `--enable` enviam **só** o `enforcement`. O
`check-protections-manual.sh` reprova `bypass_actors` não vazio, `current_user_can_bypass` diferente
de `never`, `enforcement` diferente de `active`, varredura ou proteção de envio desligadas e a falta
do bloco `security_and_analysis` (sem permissão de administrador). Nenhum teste toca o GitHub real.

## V-06a e V-06b Validação do revisor **antes do merge** (no GitHub real)

O implementador **não** roda isto. **Só estas duas** valem antes do merge:

| # | Validação | Como | Esperado |
|---|---|---|---|
| V-06a | **PR da feature verde** no `ci-gate` | abrir o pull request depois dos commits por checkpoint | o diff contra a `main` traz **todas** as áreas (backend, frontend, `.github`), então o gate **roda tudo** e deve **aprovar** |
| V-06b | **Nada foi aplicado** no GitHub pelo implementador | `gh api repos/{dono}/{repo}/rulesets` | devolve `[]` |

**Por que o V-06 com PRs descartáveis não cabe aqui**: antes do merge o `ci-gate.yml` só existe na
**branch da feature**. Um PR descartável teria de partir dela, e o diff contra a `main` incluiria
**todos** os arquivos da feature, dando sempre "roda tudo": nenhum caso por área seria exercitado.
Ele vem **depois do merge** (V-06, dentro da Fase E, abaixo).

## Fase E — validação do gate e passos manuais (só depois do merge e do `ci-gate` verde na `main`)

**Ordem**: V-06 (revisor) → V-07 → V-08 (opcional) → V-09. O ruleset só é aplicado **depois** de o
V-06 passar, porque uma correção do gate encontrada nele entra por um **PR pequeno ainda sem
proteção** (com o ruleset ativo, a correção também teria de passar pelo gate que se quer corrigir).

### V-06 O `ci-gate` no GitHub real (REVISOR, depois do merge e **antes** de aplicar o ruleset)

Tarefa T066. A `main` já tem o `ci-gate.yml` e **ainda não tem ruleset**. Para **cada** caso, uma
branch nova **a partir da `main`** com **uma** mudança mínima na área, aberta **já contra a `main`**
(mudar a base depois não dispara o gate): `git switch main && git pull && git switch -c tmp/gate-<caso>`,
mudança, `git push`, `gh pr create --base main --draft`. Fechar cada um com
`gh pr close --delete-branch`:

| PR descartável (partindo da `main`) | Esperado no `ci-gate` |
|---|---|
| só `specs/` ou `.md` | **aprovado** em até 5 min, sem rodar backend nem frontend; ambos "dispensada" |
| só `src/Frontend/` | roda o frontend; backend "dispensada" |
| só `src/Backend/` | roda o backend; frontend "dispensada" |
| backend com um teste quebrado de propósito | **reprovado** |
| execução cancelada no meio (`gh run cancel`) | **reprovado** (anotar o `result` que o reusable devolveu: confirmação pendente do research R-02) |
| arquivo fora das áreas (`docker/x`, `.github/x`) | exige backend **e** frontend |
| PR de fork (ou simulado) | roda sem segredos e com token só leitura (sem fork para testar: registrar "não exercitado" e apoiar-se no `CiWorkflowsConfigTests`) |
| **8 (opcional)**: branch descartável com um pacote vulnerável conhecido (ex.: `Newtonsoft.Json` 12.0.3 no `.csproj` da Api), **sem PR**, disparando `gh workflow run package-audit.yml --ref <branch>` | o `package-audit` **falha** (job `backend-audit`) e o **e-mail de falha chega** a quem disparou; apagar a branch (`git push origin --delete <branch>`) |

Conferir também: a **lista de checks do PR** mostra o passo como **"k6 curto (não ativado)"** (o
`name:` do job, para o ✓ nunca parecer uma aprovação) e o resumo traz a mesma linha como **não
ativado** (nunca "aprovada"); o merge da feature disparou o `ci-gate` no `push` para a `main`.

**Se algum caso falhar**: a correção entra por um **PR pequeno, ainda sem proteção**, e o caso é
repetido. **Só quando os 7 casos obrigatórios passarem** o dono segue para o V-07 (o 8º é opcional).

Os dois casos em que a plataforma **nem inicia** o `ci-gate` (PR com **conflito de merge** e PR de
**fork de primeiro contribuinte**, que espera o dono aprovar a execução) não são reproduzidos aqui:
ficam descritos no guia (FR-006, casos de borda).

O guia `docs/protecoes-do-repositorio.md` repete os passos do dono com o texto final. **Nunca**
ensaie antes de V-07 confirmar as proteções.

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
cd ../Frontend && npm run typecheck && npm test && npm run build && npm audit   # SEMPRE, mesmo com o front intocado
```

Esperado: tudo verde, totais **iguais ou maiores** que os de V-01, `npm audit` sem
vulnerabilidades (a constituição exige o `npm audit` antes do PR, sem condição; o resultado fica
anotado em cada checkpoint do `tasks.md`), nenhum teste existente editado (se algum precisar
mudar, a tabela "Testes existentes afetados" do `tasks.md` o declara).
