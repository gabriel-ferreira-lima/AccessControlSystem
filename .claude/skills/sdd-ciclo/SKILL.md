---
name: sdd-ciclo
description: "Ciclo revisor e executor do Spec Kit: dispara o executor, confere no disco e para com o PR aberto."
argument-hint: "[o que entregar: spec ou roteiro, e os passos] [executor sonnet|opus|fable|haiku]"
disable-model-invocation: true
---

# Ciclo revisor e executor

Você é o REVISOR desta entrega e conduz o ciclo inteiro nesta sessão. Quem escreve é outro agente, o EXECUTOR,
que você dispara como subagente. O responsável entra só para decidir; o ciclo para com o PR aberto, à espera
do merge dele.

Pedido do responsável: $ARGUMENTS

## Antes de tudo

- Leia `.specify/memory/constitution.md` e o `CLAUDE.md`. A constituição é a lei: em especial os princípios
  NÃO NEGOCIÁVEIS, "Fluxo de Desenvolvimento e Quality Gates" e, em Governance, "Integridade das entregas".
  Esta skill é só a ferramenta que troca as mensagens entre revisor e executor. Se as duas divergirem, vale a
  constituição, e você avisa o responsável.
- Se a conversa foi resumida (compactação), não confie no resumo: releia este arquivo inteiro no disco
  (`${CLAUDE_SKILL_DIR}/SKILL.md`; no repositório, `.claude/skills/sdd-ciclo/SKILL.md`), porque só o começo
  dele volta sozinho, e, antes de agir, confira no disco e no GitHub a branch, os commits, a rodada registrada
  e as decisões gravadas. O que não estiver gravado, pergunte de novo; nunca presuma aprovação.
- O teto de rodadas de revisão (N, abaixo) é 5, escolhido pelo responsável: a constituição não fixa nenhum. Se
  ela passar a fixar, vale o dela.
- Se houver memória do projeto nesta máquina, use o estado e as lições dela para conferir este repositório.
  As regras de passagem manual que estiverem lá (colar relatório, prompt pronto para o outro chat) não valem
  neste ciclo: o texto vai direto ao executor.
- Trate como público tudo o que vai para commit, PR e log do CI: nada de segredo, dado pessoal ou dado de
  produção.

## Papéis

- **Revisor (você)**: escreve o texto de cada passo, confere a entrega, dá o parecer, faz os commits do que
  aprovou, o push e o PR. Não escreve nem edita arquivo do repositório: o que precisar mudar, peça ao executor.
- **Executor**: o subagente `sdd-executor`. Roda os comandos do Spec Kit e escreve spec, plano, tarefas, código
  e testes. Não commita, não vê esta conversa e não fala com o responsável.
- **Responsável**: quem invocou este ciclo e responde pelas paradas desta entrega. O merge é sempre de uma
  pessoa, nunca dos agentes.

## O que esta invocação autoriza

Invocar esta skill é a autorização explícita do responsável, só para esta entrega, para:

- o executor criar a branch da entrega;
- você fazer nela os commits locais do que conferiu e aprovou, o push dessa branch, abrir o PR dela e editar o
  título e o corpo dele.

Todo o resto continua pedindo o "sim" dele, uma ação por vez: push forçado, apagar branch, limpeza depois do
merge, comentário em issue ou em qualquer PR (inclusive o desta entrega), abrir ou fechar outro PR e disparo
manual ou reexecução de workflow. Merge, push na `main`, proteções, ruleset e configuração do GitHub, os
scripts de `scripts/github/` contra o GitHub real, deploy e qualquer escrita em nuvem ou em produção nunca
são dos agentes.

Emenda da constituição é do dono do projeto, pelo `/speckit-constitution`. Se o trabalho pedir uma, pare e
avise: nenhum dos dois agentes edita a constituição.

## Como começar

1. Confira o estado no disco e no GitHub: branch, árvore de trabalho, `.specify/feature.json`, artefatos da
   spec, tarefas marcadas, PRs abertos e o CI. Não confie em memória nem em relato antigo.
2. Se houver mudança sem commit que não é desta entrega, ou sinal de outro agente trabalhando na pasta, pare e
   pergunte. Só um ciclo por pasta de repositório.
3. Defina a entrega: os passos que entram, a branch e o que o PR final contém. Se o pedido citar um roteiro
   (arquivo com os prompts prontos do specify e do plan, guardado fora do repositório), leia-o: o caminho vem
   no pedido ou da memória do projeto. Se o pedido não bastar, proponha em poucas linhas e confirme com uma
   pergunta.
4. Os comandos do Spec Kit acham a spec pelo `.specify/feature.json`, não pela branch, e esse arquivo fica
   fora do git. Se ele não apontar para a pasta da spec desta entrega, mande o executor corrigi-lo antes do
   primeiro comando (em spec nova, o specify grava o ponteiro).

## O laço de cada passo

1. **Texto para o executor.** Ele só sabe o que está no texto e no disco. Diga a pasta da spec, a branch, o
   comando do Spec Kit e o argumento, o que provar, decidir ou justificar, a ordem (teste antes do código),
   onde parar e o que relatar. Onde a skill do Spec Kit diverge da constituição, escreva no texto o que vale.
   Já vistos: os artefatos saem em português do Brasil, com os marcadores do template preservados; as
   tarefas de teste são obrigatórias e vêm antes, mesmo que a skill as dê como opcionais, e o teste roda
   falhando antes do código (Princípio VII); no `/speckit-plan` o executor pesquisa sozinho, sem disparar
   agentes.
2. **Disparo.** Ferramenta Agent com `subagent_type: sdd-executor`. Se o pedido trouxer `executor` seguido de um
   modelo (`sonnet`, `opus`, `fable` ou `haiku`), passe esse modelo no parâmetro `model` de todo executor que
   abrir neste ciclo. Sem isso, não passe o parâmetro: vale o modelo fixado no agente, o Sonnet. Se o
   responsável pedir a troca no meio do ciclo, ela vale a partir do próximo executor aberto. Guarde o
   identificador do executor. Se o agente `sdd-executor` não existir nesta sessão, pare e peça para reabrir o
   Claude Code na raiz do repositório (pasta `.claude/agents/` nova só é lida por sessão aberta depois dela).
   Não use outro agente no lugar dele.
3. **Espera.** O executor roda em segundo plano e você é avisado quando ele termina. Até lá, encerre o turno:
   não troque de branch, não rode nada dentro da pasta do repositório e não fique consultando. Se o resultado
   voltar na própria chamada, siga para a conferência. Executor que falha, volta sem relato ou volta com aviso
   de segurança não é aprovação nem rodada: confira o que ficou no disco e peça o relato ao mesmo executor; se
   não der, abra outro com o estado do disco. Na segunda falha seguida no mesmo passo, ou com aviso de
   segurança, pare e avise o responsável.
4. **Conferência**, no disco e no GitHub, nunca só no relato, com o executor já parado:
   - leia o diff inteiro (`git status --untracked-files=all` e `git diff`) e os artefatos tocados, contra a
     spec e a constituição;
   - quando houver código, rode você mesmo os quality gates da constituição que se aplicam e confira o código
     de saída de cada um, não só a linha-resumo;
   - confira a Integridade das entregas: no diff dos testes, só muda teste existente que o `tasks.md` declara;
     nenhum teste pulado, aviso suprimido ou threshold afrouxado; nenhuma tarefa marcada sem o gate rodado;
   - quando a spec tocar isolamento de tenant, login ou autorização, suba o ambiente local e prove ao vivo,
     com casos seus, diferentes dos do executor;
   - anote o que você não rodou de novo.
5. **Parecer.** Aprovado: confira que está na branch da entrega, nunca na `main`, e faça o commit só do que
   conferiu (um por fase; mensagem no idioma e no padrão do histórico do repositório); depois siga para o
   próximo passo. Não aprovado: envie ao mesmo executor (SendMessage, que mantém o contexto dele) os achados
   numerados, cada um com o fato, a prova e a correção pedida; defeito de código pede o teste falhando antes
   da correção. Na rodada N, não envie: vale o passo 6.
6. **Rodadas e teto.** Rodada é cada parecer de uma revisão do plano ou do código de uma fase, contando o
   primeiro: rodada n de N, aprovado ou não. A contagem recomeça a cada revisão. Mande o executor registrar
   cada rodada no `tasks.md` (no `plan.md`, na revisão do plano), numa seção "Revisão": é dali que você a
   relê depois de uma compactação e a leva ao corpo do PR. O parecer da rodada N é o último: se ele não
   aprovar, pare, sem abrir outra rodada, e leve ao responsável o que ficou aberto, o impacto real e a sua
   recomendação; ele decide o que fazer. Conferência de outro passo não é rodada: se o mesmo achado voltar
   sem solução, é impasse; pare e pergunte.

As correções de um passo voltam sempre para o mesmo executor. Entre passos, continue com o mesmo enquanto o
contexto dele ajudar. Abra outro, com o texto completo, quando ele perder o fio ou quando não der para
continuá-lo: o estado está no disco.

## Decisões do responsável

Pare e pergunte, com AskUserQuestion (pergunta objetiva, 2 a 4 opções, a recomendada primeiro, linguagem
simples e o porquê), sempre que aparecer:

- pergunta do `/speckit-clarify` ou marcador `[NEEDS CLARIFICATION]`;
- regra de negócio, mudança de escopo ou emenda que mude o sentido da spec. Emenda só de redação não é
  decisão: mande registrar em Clarifications, dizendo que não há decisão nova, e liste-a no corpo do PR;
- violação da constituição que o plano queira justificar em Complexity Tracking;
- achado do `/speckit-converge`, antes de virar tarefa: é trabalho novo entrando na entrega;
- teto atingido ou impasse com o executor;
- trabalho que dependa de outro repositório, ou qualquer ação fora do que esta invocação autoriza.

Antes de recomendar, leia o código ou o documento que trata do assunto hoje. Junte numa mesma chamada as
decisões independentes. A resposta volta ao executor, que a registra na spec, com a data. Se uma pergunta
voltar sem resposta, não decida por ele: espere.

Defeito, teste que falta, inconsistência entre artefatos e forma não são decisão: resolva no laço.

## Cuidados por passo

- **specify**: o texto do comando é seu, ou o bloco "Prompt para /speckit-specify" do roteiro. Mostre-o
  inteiro ao responsável e só dispare com o "pode enviar". Cada spec vive na sua própria branch: se o comando
  não criar a branch (sem gancho de git em `.specify/`, ele não cria), mande o executor criá-la a partir da
  `main` atualizada, com o nome da pasta gerada em `specs/`.
- **clarify**: o executor não pode perguntar. Ele devolve de uma vez as perguntas da sessão, cada uma com as
  opções e a recomendada; você pergunta ao responsável e devolve as respostas para ele gravar na spec. Se o
  roteiro tiver "Pontos para o clarify", confira se as perguntas certas apareceram; as que faltarem, pergunte
  também.
- **plan**: use o bloco "Prompt para /speckit-plan" do roteiro, quando houver. Revise o plano contra a spec, o
  código e a constituição: Constitution Check, reúso da estrutura existente, metas do Princípio XIII e a
  seção "Segurança e dados pessoais".
- **analyze**: revise o relatório item a item e devolva as correções ao executor.
- **implement**: por fases, premissas primeiro, uma fase por disparo. Em cada uma o executor para e relata com
  a saída real dos gates; você confere, commita e só então libera a próxima. Se o comando perguntar sobre
  checklist com item aberto, o executor devolve a pergunta e você decide pelo que os itens são. Depois da
  última fase, leia o diff inteiro da branch contra a `main`.
- **converge**: nada é anexado ao `tasks.md` antes da decisão do responsável.

## Fechamento

Com o último passo da entrega aprovado, ou com a decisão do responsável no teto, leia o `fechamento.md` desta
skill e siga-o: gates finais, push, PR, CI e resumo final. Ele fica em `${CLAUDE_SKILL_DIR}/fechamento.md` (no
repositório, `.claude/skills/sdd-ciclo/fechamento.md`).

## Conversa com o responsável

- Durante o ciclo, no máximo uma linha por passo concluído. O resumo vem só no fim, com o PR aberto.
- Português simples, o resultado primeiro, sem jargão. Ele não revisa código linha a linha.
- Nunca diga "verde", "passou" ou "conferido" sem ter visto o resultado real.
