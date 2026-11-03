# Proteções do repositório

Este guia diz **como a branch `main` está protegida, por quê, como refazer** a proteção do zero e **como conferir** que ela
está ativa. As proteções vivem na configuração do GitHub, não no código: sem este guia, ninguém saberia refazê-las depois de
uma mudança de plano, de visibilidade ou de conta. Tudo aqui é para o dono do projeto; o passo a passo de validação da spec
está em `specs/20261102-171227-delivery-foundation/quickstart.md`.

> **No Windows, rode todo comando `bash scripts/...` no Git Bash**, nunca no PowerShell nem no `bash` do WSL (ver a última seção).

## O que está protegido e por quê

A `main` é a fonte de tudo o que irá para os ambientes: se ela não estiver sempre pronta, qualquer atalho ("só dessa vez")
invalida as outras garantias. A proteção é um **ruleset** do GitHub chamado `protect-main`, cujo corpo está versionado em
`scripts/github/protections/main-ruleset.json` (a fonte única do nome do check, do `integration_id` e das regras).

| Proteção | Por quê |
|---|---|
| **A `main` só recebe mudança por pull request** | ninguém envia código direto, nem o dono |
| **O check `ci-gate` é obrigatório e vem do GitHub Actions** (`integration_id` 15368) | só o gate aprova o aceite; o `integration_id` impede que um status externo com o mesmo nome valha |
| **Sem bypass, nem para administrador** (`bypass_actors` vazio) | uma regra que o dono pode furar "só dessa vez" não protege |
| **Sem envio forçado e sem exclusão da `main`** | ninguém reescreve o histórico nem apaga a branch |
| **Sem revisão humana obrigatória** | o repositório tem um único dono; exigir outra pessoa travaria todos os aceites. A barreira é o `ci-gate` |
| **Sem exigir a branch atualizada com a `main`** | decisão do dono; a rede de segurança é rodar as verificações de novo sobre a `main` depois do aceite |
| **Varredura de segredos com bloqueio no envio** | um commit com senha, chave ou token é barrado antes de entrar num repositório público |

**Limite aceito**: a verificação de um pull request é a definida no próprio pull request, então uma mudança que afrouxe o
`ci-gate.yml` se avalia a si mesma como aprovada. A barreira é o dono **revisar o diff dos arquivos de CI** (`.github/`,
`scripts/ci/`) antes de aceitar, junto com a regra de Integridade das entregas da constituição.

## Como aplicar e refazer

Quem aplica é o dono, com o `gh` autenticado (`gh auth login`), **depois** de a ordem de ativação (abaixo) estar cumprida:

```bash
bash scripts/github/apply-protections.sh --dry-run   # lê o estado, mostra o que faria e NÃO escreve nada
bash scripts/github/apply-protections.sh             # aplica
```

O que o script faz, e em que ordem:

1. exige `gh` e `jq` (senão explica, citando o Git Bash);
2. **confere a ordem de ativação**: só prossegue se já existir um check `ci-gate` na `main` **e** se o aplicativo que o reportou
   tiver o mesmo `integration_id` do `main-ruleset.json`; senão **para sem escrever**;
3. cria o ruleset `protect-main` se ele não existir, ou **atualiza o mesmo** se já existir (nunca cria dois: é idempotente);
4. liga a varredura de segredos e a proteção de envio **só se estiverem desligadas**, e **relê para confirmar**; se o GitHub
   recusar, mostra o passo manual da interface (Settings > Advanced Security) e sai com falha;
5. roda a conferência automática sobre o que aplicou e imprime o que falta da conferência à mão.

**Refazer do zero** (outra conta, repositório novo, mudança de plano): é o mesmo procedimento. O script é idempotente, então
rodá-lo de novo é seguro.

## Conferência automática e conferência à mão

A conferência tem duas partes, porque o endpoint público de regras não mostra tudo.

**Automática** (`.github/workflows/protections-check.yml`): roda **toda segunda-feira** e sob demanda (Actions > Protections
Check > Run workflow). Lê as regras ativas da `main` **autenticada com o `GITHUB_TOKEN` da própria execução** (somente leitura,
nunca de forma anônima, e sem nenhuma credencial guardada) e **falha, avisando por e-mail**, se faltar: pull request
obrigatório; o check `ci-gate` **pelo nome e pelo `integration_id`**; a proteção contra envio forçado (`non_fast_forward`);
a proteção contra exclusão (`deletion`). Se alguém renomear o job `ci-gate`, ela acusa em até uma semana. Não conseguir ler as
regras (por exemplo, um `403`) também reprova: nunca é aprovar. Não roda por pull request.

**À mão** (`bash scripts/github/check-protections-manual.sh`, precisa ser administrador): confere o que o endpoint não mostra.
Imprime `OK` ou `FALHA` por item e sai `1` se qualquer um falhar:

| Item | Esperado |
|---|---|
| `bypass_actors` do ruleset | vazio |
| `current_user_can_bypass` | `never` |
| `enforcement` | `active` |
| varredura de segredos (`secret_scanning`) | `enabled` |
| proteção de envio (`secret_scanning_push_protection`) | `enabled` |

Rode a conferência à mão **depois de aplicar** e sempre que mexer nas configurações do repositório.

## Ordem de ativação

A proteção só pode exigir o `ci-gate` **depois de ele existir na `main`**: exigido antes, ele trava todos os pull requests,
inclusive o que o cria. A ordem é:

1. o pull request da feature (que traz o `ci-gate`) entra **sem** proteção, por pull request comum;
2. confirme na aba Actions que o `ci-gate` rodou **verde na `main`**;
3. o revisor valida o gate com pull requests descartáveis partindo da `main` (ainda sem ruleset: uma correção entra por um
   pull request pequeno, sem a proteção atrapalhar);
4. só então: `apply-protections.sh --dry-run`, `apply-protections.sh`, `check-protections-manual.sh`;
5. dispare `protections-check` e `package-audit` à mão: a **primeira execução real** do `protections-check` prova que o
   `GITHUB_TOKEN` consegue ler as regras.

**Renomear o job `ci-gate` derruba a exigência** em silêncio (o ruleset a faz pelo nome). O teste `CiWorkflowsConfigTests` compara
o nome do job com o `main-ruleset.json`, e a conferência semanal acusa; se renomear de propósito, troque o nome nos dois lugares
e rode `apply-protections.sh` de novo.

## Emergência: desligar e religar

Com a plataforma de CI fora do ar, ninguém consegue aceitar um pull request: é o comportamento seguro. Se mesmo assim for
preciso aceitar algo, o caminho é **deliberado e visível**, pela configuração, nunca por envio de código:

```bash
bash scripts/github/apply-protections.sh --disable   # enforcement = disabled
# ... faça o aceite de emergência ...
bash scripts/github/apply-protections.sh --enable    # enforcement = active
```

`--disable` e `--enable` mudam **só** o `enforcement` e não exigem o gate. A plataforma **registra quem mudou e quando** (a
alteração do ruleset fica no histórico e no log de auditoria do GitHub). Enquanto estiver desligado a `main` **não está protegida**: religue assim que
possível e rode `check-protections-manual.sh`. A conferência automática falha enquanto o ruleset estiver desligado (regras de
ruleset desligado não são devolvidas), então ela também lembra de religar.

## Ensaios das proteções

Para provar que a proteção funciona de verdade (SC-001): envio direto, envio forçado e exclusão da `main` têm de ser **recusados**.
**Cuidado**: sem a proteção ativa, o ensaio alteraria ou apagaria a `main` de verdade. Por isso:

1. **Só depois** de a conferência automática (disparada à mão) **e** a conferência à mão confirmarem que está tudo ativo.
2. **Antes de qualquer ensaio, anote o SHA da `main`**: `git fetch origin && git rev-parse origin/main`, e confira o mesmo SHA no
   GitHub. Ele recria a branch se um ensaio de exclusão a apagasse.
3. **Envio direto** com a conta do dono, com um commit trivial e desfazível: `git push origin HEAD:main` → deve ser **recusado**.
4. **Envio forçado**, voltando a `main` um commit (reescreve o histórico, por isso é forçado):
   `git push --force origin origin/main~1:main` → deve ser **recusado**. Se fosse aceito, a `main` volta ao SHA anotado
   com `git push origin <SHA>:main`.
5. **Exclusão**: `git push origin :main` → deve ser **recusada**. Se algo passasse, recrie a branch pelo SHA anotado:
   `gh api repos/{dono}/{repo}/git/refs -f ref=refs/heads/main -f sha=<SHA>`.
6. Registre o resultado e a data na tabela abaixo.

| Ensaio | Resultado | Data |
|---|---|---|
| Gate no GitHub real, antes do ruleset (7 casos: só documentos, só frontend, só backend, teste quebrado, cancelada, fora das áreas, fork) | aprovou/reprovou como esperado em 6; fork **não exercitado** (sem fork de teste; coberto pelo `CiWorkflowsConfigTests`). Cancelada: `needs.backend.result` = `cancelled` → REPROVADO | 2026-11-03 |
| Auditoria com pacote vulnerável (`Newtonsoft.Json` 12.0.3, por `workflow_dispatch` numa branch descartável) | `backend-audit` falhou com `NU1903`; o e-mail de falha chegou | 2026-11-03 |
| Aplicação (`apply-protections.sh`) e conferência à mão (`check-protections-manual.sh`) | ruleset criado; conferência automática com as 4 regras `ok`; à mão, 5 `OK` (varredura já estava ligada) | 2026-11-03 |
| Conferência automática real (`protections-check`) e `package-audit` disparados à mão na `main` | os dois verdes: o `GITHUB_TOKEN` lê as regras, inclusive o `integration_id` 15368 | 2026-11-03 |
| Envio direto (`git push origin HEAD:main`) | **recusado** (GH013: exige pull request e o `ci-gate`) | 2026-11-03 |
| Envio forçado (`git push --force origin origin/main~1:main`) | **recusado** (GH013: "Cannot force-push to this branch") | 2026-11-03 |
| Exclusão (`git push origin :main`) | **recusada** ("refusing to delete the current branch": a `main` é a branch padrão, o que barra antes da regra `deletion`; a regra aparece ativa na conferência automática) | 2026-11-03 |
| Conferência automática falhando com o ruleset desligado (opcional; `--disable` por cerca de 2 min) | `protections-check` **falhou** com as 4 regras como `falta:`; o e-mail de falha chegou; religado com `--enable` e a conferência à mão voltou a 5 `OK` | 2026-11-03 |

## Se o repositório voltar a ser privado

As proteções só são gratuitas porque o repositório é público (desde 2026-11-02). Se voltar a ser privado:

- o ruleset e a varredura de segredos **exigem plano pago**;
- a conferência automática **precisa ser reavaliada**: a leitura das regras com o `GITHUB_TOKEN` não está confirmada para
  repositório privado, e é preciso decidir como autenticá-la (sem guardar credencial no repositório);
- refaça tudo com `apply-protections.sh` e rode as duas conferências;
- o repositório não pode conter segredo em commit, em issue nem em registro de execução enquanto for público.

## Onde ver os resultados

- **O gate** (`ci-gate`): aba Actions > "CI Gate", e a lista de checks de cada pull request. O passo k6 aparece como
  **"k6 curto (não ativado)"** (o texto existe para o ✓ nunca parecer uma aprovação de carga). O **resumo** da execução traz a tabela
  com o estado de cada verificação: *aprovada*, *reprovada*, *dispensada* (a área não mudou) e *não ativado*, e a linha final
  "Resultado do gate". Depois de cada aceite o gate roda de novo sobre a `main` (um push): uma falha ali avisa por e-mail.
- **A auditoria de pacotes**: aba Actions > "Package Audit" (toda segunda-feira e sob demanda).
- **A conferência automática**: aba Actions > "Protections Check".
- **Pull request sem verificação**: há dois casos em que a plataforma **nem inicia** o `ci-gate`, e o aceite fica bloqueado
  (nunca aprovado):
  - **conflito de merge**: as verificações de pull request não iniciam até o conflito ser resolvido; o `ci-gate` nem aparece na
    lista. Resolva o conflito (atualize a branch) e as verificações rodam;
  - **fork de primeiro contribuinte**: a execução espera o dono aprovar ("Approve and run workflows"); depois de aprovada, roda
    **sem segredos e sem permissão de escrita**.

## Agendamentos desativados e avisos por e-mail

Num repositório público, o GitHub **desativa os agendamentos** (a auditoria e a conferência semanais) depois de **60 dias sem
atividade** no repositório. Para notar: na aba Actions o workflow aparece como desativado, ou a última execução agendada está
velha. Para reativar: Actions > o workflow > "Enable workflow", ou um commit que mude a linha `cron`. Rode as duas à mão depois.

O aviso de falha vai por **e-mail**, para quem criou o workflow (ou quem alterou a linha `cron`). Confira em
**Settings > Notifications > Actions** que o e-mail de falha de workflow está ligado, e olhe a caixa de spam. Se o e-mail não
chegar, a falha continua visível no histórico da aba Actions.

## Rodar no Windows (Git Bash, ACS_BASH, jq)

Os scripts são de `bash` e usam `gh` e `jq`. **No Windows, rode-os no Git Bash** (abra o "Git Bash"; no PowerShell,
`& "C:\Program Files\Git\bin\bash.exe" scripts/...`). O `bash` que o PowerShell acha no PATH costuma ser o **atalho do WSL**, que
não enxerga o `gh` nem o `jq` do Windows e faz os scripts reclamarem que faltam.

Os **testes** que rodam os scripts procuram o `bash` por `ACS_BASH` (se definida) ou, senão, o Git Bash em
`C:\Program Files\Git\bin\bash.exe`; **nunca** o do PATH. Sem `bash` ou sem `jq` visível dentro dele, o teste **falha** com
mensagem clara (nunca é pulado). Se o Git estiver em outro lugar, defina `ACS_BASH` com o caminho do `bash.exe` dele. O `jq`
instala-se com `winget install jqlang.jq` (abra um terminal novo depois).
