# Fechamento da entrega

Siga na ordem. Nada aqui muda regra da constituição: onde ela disser outra coisa, vale ela.

1. **Antes do push.** Tudo o que você aprovou está commitado e a árvore de trabalho está limpa. Rode você
   mesmo, sobre o estado final, os gates que a constituição exige antes de abrir o PR ("Fluxo de
   Desenvolvimento e Quality Gates"), e confira o código de saída de cada um. Se a branch conflitar com a
   `main`, pare e avise: o repositório não exige branch atualizada, e merge não é trabalho do executor.
   Todo achado tem destino: corrigido e reconferido, ou decisão do responsável, registrada com a data.
2. **Push.** Só a branch da entrega, sem forçar.
3. **PR.** Título e corpo no idioma e no padrão dos PRs anteriores do repositório. No corpo, só o que é
   verdade:
   - o que muda: a spec, as fases e as tarefas cobertas;
   - testes: os novos, e se algum teste existente mudou (só os que o `tasks.md` declara);
   - revisão: quantas rodadas (n de N) no plano e em cada fase, e os achados mais graves, com a correção;
   - validação: os gates que você rodou, com os números reais, e o que não rodou de novo.

   Escreva o corpo na criação do PR. Tudo ali é público.
4. **CI.** Espere terminar (por exemplo, `gh pr checks --watch` em segundo plano) e leia o resultado real do
   check exigido; no vermelho, leia o log. Falha de rede ou de ambiente: peça o "sim" e rode de novo só a
   execução que falhou. Defeito volta ao laço como achado e conta como rodada da revisão da fase a que
   pertence. Se o `tasks.md` tiver tarefa de fechamento que depende do CI verde, mande-a agora ao executor,
   confira, commite, faça o push e espere o CI de novo.
5. **Resumo final**, só agora, escrito para o responsável e curto o bastante para caber numa tela:

   **PR pronto para o merge do responsável**: link, título e em qual commit o CI ficou verde.

   1. O que foi entregue: spec, passos e tarefas.
   2. Decisões que o responsável tomou neste ciclo, uma por linha.
   3. Revisão do plano e de cada fase: quantas rodadas (n de N) e os achados mais graves, uma linha cada, com
      a correção.
   4. O que o revisor rodou e provou por conta própria, e o que não rodou de novo.
   5. O que ficou aberto: pendências e riscos.
   6. O que o responsável faz agora: lista numerada curta (ler o PR e mesclar; o que só ele pode executar; o
      que o merge dispara).

6. **Pare.** O merge é do responsável. Não mescle, não aprove e não peça revisão a terceiros.
7. **Depois do merge**, quando o responsável avisar: confira no GitHub e ofereça a limpeza (atualizar a `main`
   local, apagar a branch local e a remota). Só faça com o "sim".
