# Especificação da Feature: Base de entrega sem nuvem (proteção da principal, verificações confiáveis, saúde da Api e dados de homologação)

**Branch da Feature**: `20261102-171227-delivery-foundation`

**Criada em**: 2026-11-02

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Preparar o caminho de entrega sem depender da nuvem: proteger a branch principal, tornar as verificações automáticas confiáveis, dar à Api verificações de saúde e preparar os dados fictícios que o futuro ambiente de homologação vai usar. Contexto: os ambientes de homologação e produção na AWS virão numa spec seguinte, quando o dono do projeto decidir assumir o custo. Tudo o que pode ser feito e testado de graça, localmente e no GitHub, entra agora, para que a branch principal já fique protegida para as próximas specs. Requisitos: (1) toda mudança chega na branch principal só por pull request com todas as verificações automáticas aplicáveis aprovadas, e ninguém, nem o dono, consegue enviar código direto, apagar a branch ou reescrever o histórico; (2) um pull request nunca fica esperando para sempre por uma verificação que não se aplica à mudança, e uma verificação aplicável que falhou, foi cancelada ou não rodou nunca conta como aprovada; (3) pull requests de fork rodam sem segredos e sem permissões; (4) a auditoria de pacotes vulneráveis roda também em agenda (semanal), mesmo sem mudança no código, e avisa o dono quando falha; (5) o passo de teste de carga curto fica previsto nas verificações e aparece como \"não ativado\" (nunca como aprovado) até existirem cenários de carga; (6) a Api expõe verificações de saúde anônimas, sem detalhes internos: uma que diz se o processo está de pé (sem depender de nada) e outra que diz se ela está pronta (catálogo acessível); (7) existe um script versionado de dados fictícios para homologação: vários tenants em base compartilhada e ao menos um em base dedicada, com operadores fictícios, idempotente, recusado em produção, com senhas vindas de fora do repositório; (8) existe uma verificação que confere todas as bases de um ambiente e reprova se alguma tiver o administrador de e-mail e senha fixos da migration de desenvolvimento; (9) existe um guia curto de como as proteções do repositório estão configuradas e como refazê-las."

## Clarificações

### Sessão 2026-11-02

- Q: A proteção da principal é aplicada por código ou só documentada? → A: Por script versionado
  mais o guia, usando conjuntos de regras da plataforma (rulesets), não a proteção de branch
  clássica. (Padrão confirmado pelo dono.)
- Q: Qual ambiente aceita o comando de dados fictícios? → A: Só homologação (`Staging`);
  desenvolvimento segue com o `seed-dev` e produção sempre recusa. (Padrão confirmado pelo dono.)
- Q: Onde roda a verificação do administrador fixo? → A: Comando do Migrator, executado depois do
  `migrate` e recusado em desenvolvimento. (Padrão confirmado pelo dono.)
- Q: Quem recebe o aviso da auditoria agendada? → A: O e-mail padrão do GitHub para falha de
  verificação agendada. (Padrão confirmado pelo dono.)
- Q: O pull request precisa estar atualizado com a principal para ser aceito? → A: Não. (Padrão
  confirmado pelo dono.)
- Q: Qual o tempo limite da verificação "pronta"? → A: 2 segundos. (Padrão confirmado pelo dono.)
- Q: O que conta como "só documentos" para dispensar backend e frontend? → A: `specs/`, qualquer
  arquivo `.md`, `.specify/` e `.claude/`; qualquer outro arquivo fora das áreas conhecidas roda
  todas as verificações.
- Q: As verificações devem rodar de novo sobre a principal depois do aceite? → A: Sim, e uma falha
  avisa o dono pelo e-mail padrão da plataforma.
- Q: A varredura de segredos com bloqueio no envio entra como proteção do repositório? → A: Sim,
  no script, no guia e na conferência à mão.
- Q: Quem executa a conferência das proteções? → A: O dono, à mão, e também uma execução
  automática semanal e sob demanda, sem credencial guardada, que lê as regras ativas da principal
  pelo endpoint de regras da plataforma **usando o `GITHUB_TOKEN` da própria execução (somente
  leitura; nunca de forma anônima)** e falha, avisando o dono por e-mail, se faltar pull request
  obrigatório, a verificação agregadora exigida **pelo nome certo e vinda do GitHub Actions**,
  envio forçado bloqueado ou exclusão bloqueada. O que esse endpoint não mostra (quem pode furar as
  regras; se a varredura de segredos está ligada) fica na conferência à mão, pelo guia. O plano
  confirmou na documentação que o endpoint exige só a permissão de leitura de metadados; que o
  token da execução a tem é provado pela primeira execução real.

## Cenários de Usuário e Testes *(obrigatório)*

Esta é a primeira metade da antiga spec de ambientes e entrega contínua. Ela foi dividida em
2026-11-02 porque a parte da AWS custa dinheiro todo mês e exige um domínio: **a nuvem ficou para
uma spec futura (05b)** e esta spec entrega tudo o que dá para fazer e provar de graça, sem conta
AWS. O ganho é imediato: a partir do aceite desta spec, a branch principal já fica protegida, e
toda spec seguinte entra por um caminho em que "verificação verde" quer dizer mesmo "verificação
que rodou e passou". Os públicos beneficiados são: o **dono do projeto**, que passa a ter a
garantia de que nada entra na principal sem prova automática; **quem escreve o código** (hoje,
uma IA seguindo o Spec Kit), que ganha um portão previsível e que não aceita atalho; e o
**futuro ambiente de homologação** (spec 05b), que já encontra pronto o que precisa de código:
saúde da Api, dados fictícios e a conferência de que nenhuma base tem o administrador de senha
fixa.

### História de Usuário 1 - Branch principal só aceita mudança verificada (Prioridade: P1)

A branch principal é a fonte de tudo o que irá para os ambientes. Ela passa a receber mudanças
**somente por pull request**, e só quando a verificação agregadora (História 2) aprova. Nem o dono
do projeto consegue enviar código direto para ela, apagá-la ou reescrever o seu histórico.
Desligar a proteção é possível apenas alterando a configuração do repositório, uma ação
deliberada e visível, nunca por um envio de código.

**Por que esta prioridade**: tudo o que vem depois (deploy, promoção, volta de versão) parte da
premissa de que a principal está sempre pronta para ir a produção. Sem esta proteção, qualquer
atalho ("só dessa vez") invalida as outras garantias, e hoje a principal **não** está protegida.

**Teste Independente**: primeiro, rodar a conferência das proteções (FR-037) e só seguir se ela
confirmar que estão ativas, porque sem proteção estes ensaios alterariam a principal de verdade.
Com a proteção confirmada e usando a conta do dono, tentar enviar um commit direto, apagar a
branch e reescrever o histórico (o envio forçado com o cuidado descrito no FR-038); abrir um pull
request com uma verificação vermelha e tentar aceitá-lo; e abrir um pull request verde e aceitá-lo.
Conferir que os três primeiros são recusados e o último é aceito.

**Cenários de Aceitação**:

1. **Dado** a principal protegida, **Quando** qualquer pessoa, inclusive o dono do projeto, tenta
   enviar um commit direto para ela, **Então** a tentativa é recusada.
2. **Dado** a principal protegida, **Quando** qualquer pessoa tenta apagá-la ou reescrever o seu
   histórico (envio forçado), **Então** a tentativa é recusada.
3. **Dado** um pull request cuja verificação agregadora está reprovada ou ainda não terminou,
   **Quando** alguém tenta aceitá-lo, **Então** o aceite é bloqueado.
4. **Dado** um pull request com a verificação agregadora aprovada, **Quando** o dono o aceita,
   **Então** o aceite é permitido, sem exigir aprovação de outra pessoa (o repositório tem um único
   dono).
5. **Dado** que o dono precisa mudar ou desligar a proteção, **Quando** ele altera a configuração
   do repositório, **Então** isso é possível e fica registrado pela plataforma, mas **não** é
   possível por envio de código.
6. **Dado** o repositório com a varredura de segredos com bloqueio no envio ativa, **Quando**
   alguém tenta enviar um commit que contém um segredo reconhecido (senha, chave ou token),
   **Então** o envio é barrado antes de entrar no repositório.

---

### História de Usuário 2 - Verificação agregadora que não trava nem mente (Prioridade: P1)

Todo pull request para a principal recebe **uma única verificação agregadora**, que **sempre**
roda e é a única exigida para o aceite. Ela descobre quais áreas do código a mudança tocou
(backend, frontend, documentos e especificações), exige as verificações de cada área tocada e
dispensa as demais, mostrando claramente o que foi aprovado, reprovado, dispensado ou "não
ativado". Ela reprova se qualquer verificação aplicável falhou, foi cancelada, expirou ou não
chegou a rodar. O passo de teste de carga curto fica previsto entre as verificações e aparece como
**"não ativado"** (nunca como aprovado) enquanto não houver cenários de carga.

**Por que esta prioridade**: as verificações atuais só rodam quando certos arquivos mudam; uma
verificação obrigatória que não roda fica "aguardando" para sempre e trava o pull request (foi o
aprendizado da spec 02). E o outro lado é pior: uma verificação "pulada" pela plataforma conta
como verde para o bloqueio de aceite, o que deixaria passar uma mudança que nunca foi testada.

**Teste Independente**: abrir pull requests descartáveis: (a) só documentos; (b) só frontend;
(c) só backend; (d) com um teste quebrado; (e) com a verificação cancelada no meio; (f) que toca
um arquivo fora de qualquer área conhecida. Conferir o resultado da verificação agregadora em
cada um e fechar os descartáveis sem aceitá-los.

**Cenários de Aceitação**:

1. **Dado** um pull request que altera apenas documentos (`specs/`, qualquer arquivo `.md`,
   `.specify/` e `.claude/`), **Quando** as verificações rodam, **Então** a verificação agregadora
   aprova **sem** rodar backend nem
   frontend, mostrando os dois como dispensados, e entrega o resultado em até 5 minutos.
2. **Dado** um pull request que altera apenas o frontend, **Quando** as verificações rodam,
   **Então** roda a verificação do frontend, a do backend é dispensada e o resultado final
   depende só da do frontend. O inverso vale para um pull request só de backend.
3. **Dado** um pull request que altera backend e frontend, **Quando** as verificações rodam,
   **Então** as duas são exigidas e ambas precisam passar.
4. **Dado** um pull request que altera um arquivo que não pertence a nenhuma área conhecida (por
   exemplo, configuração de CI, de containers ou de scripts), **Quando** as verificações rodam,
   **Então** todas as verificações são exigidas (fechado por padrão): só se dispensa o que é
   comprovadamente irrelevante para a mudança.
5. **Dado** qualquer verificação aplicável que falhou, foi cancelada, expirou ou não chegou a
   rodar, **Quando** a verificação agregadora termina, **Então** ela **reprova**; nunca aprova por
   ausência de resultado.
6. **Dado** uma verificação aplicável que o mecanismo de CI marcou como "pulada" sem que a área
   tenha ficado sem mudanças, **Quando** a verificação agregadora avalia, **Então** ela reprova:
   pulada sem motivo de dispensa não é aprovada.
7. **Dado** o resultado de um pull request, **Quando** o dono o abre, **Então** vê, para cada
   verificação, o estado (aprovada, reprovada, dispensada por falta de mudança na área ou não
   ativada), sem precisar abrir os registros detalhados.
8. **Dado** que ainda não existem cenários de teste de carga, **Quando** a verificação agregadora
   avalia, **Então** o passo de carga curto aparece como **"não ativado"**, não conta como
   aprovado e não bloqueia o aceite; quando existirem cenários (spec de escalabilidade), esse
   passo passa a ser aplicável e a bloquear como as demais.
9. **Dado** que novos commits são enviados a um pull request, **Quando** isso acontece, **Então**
   a verificação agregadora roda de novo, e só vale o resultado do estado mais recente do pull
   request.
10. **Dado** uma mudança recém-aceita na principal, **Quando** o aceite se completa, **Então** as
    verificações aplicáveis às áreas tocadas rodam de novo sobre a principal, e uma falha fica
    visível e avisa o dono pelo e-mail padrão da plataforma.

---

### História de Usuário 3 - Pull requests de fork rodam sem segredos e sem permissões (Prioridade: P2)

O repositório é público, então qualquer pessoa pode propor uma mudança por fork. As verificações de
um pull request de fork rodam **sem acesso a nenhum segredo** e **só com permissão de leitura**,
e nenhuma verificação executa o código de um pull request com mais poder do que ler o repositório.

**Por que esta prioridade**: num repositório público, uma verificação que roda código alheio com
segredos ou permissão de escrita é uma porta aberta para roubo de credenciais e para alteração do
repositório. Hoje não há segredos, mas as próximas specs (nuvem) vão criá-los, e a regra precisa
estar valendo antes disso.

**Teste Independente**: abrir um pull request a partir de um fork de teste (ou simulá-lo com a
mesma configuração) e conferir, no registro da execução, que nenhum segredo está disponível e que
o token da execução só lê o repositório.

**Cenários de Aceitação**:

1. **Dado** um pull request vindo de um fork, **Quando** as verificações rodam, **Então** elas
   rodam, mas sem acesso a nenhum segredo e com permissão apenas de leitura.
2. **Dado** qualquer pull request (de fork ou não), **Quando** as verificações rodam, **Então**
   cada uma declara as permissões mínimas de que precisa (por padrão, só leitura), e nenhuma
   executa o código do pull request com permissão de escrita ou com segredos.
3. **Dado** que segredos passem a existir no repositório numa spec futura, **Quando** um pull
   request de fork roda, **Então** continua sem recebê-los.

---

### História de Usuário 4 - Auditoria de pacotes vulneráveis também em agenda (Prioridade: P2)

A verificação de pacotes vulneráveis (a da spec 04b) hoje só roda quando o código muda. Passa a
rodar **também toda semana**, mesmo sem nenhuma mudança, e pode ser iniciada à mão. Se falhar, o
dono é avisado.

**Por que esta prioridade**: um aviso novo sobre um pacote que já está instalado não acenderia
nenhuma verificação se ninguém mexer no código. Num sistema que abre portas físicas, descobrir uma
vulnerabilidade só semanas depois, quando alguém por acaso abrir um pull request, é tarde.

**Teste Independente**: iniciar a auditoria à mão e conferir que ela roda para backend e frontend;
provocar uma falha de propósito (pacote vulnerável conhecido num pull request descartável ou
execução forçada) e conferir que a falha é visível e que o aviso ao dono sai.

**Cenários de Aceitação**:

1. **Dado** uma semana sem nenhuma mudança no código, **Quando** o intervalo semanal passa,
   **Então** a auditoria roda sozinha para o backend e para o frontend.
2. **Dado** que surgiu um aviso de vulnerabilidade novo para um pacote já instalado, **Quando** a
   auditoria agendada roda, **Então** ela falha de forma visível e o dono do projeto é avisado.
3. **Dado** que não há aviso novo, **Quando** a auditoria agendada roda, **Então** ela passa e o
   resultado fica registrado e consultável.
4. **Dado** que a base de avisos está inacessível (falha de rede), **Quando** a auditoria roda,
   **Então** ela falha ou é repetida, e **nunca** passa em silêncio como "sem vulnerabilidades".
5. **Dado** o dono querendo conferir agora, **Quando** ele inicia a auditoria à mão, **Então** ela
   roda com os mesmos critérios da agendada.
6. **Dado** a exceção justificada que já existe (spec 04b), **Quando** a auditoria agendada roda,
   **Então** aplica os mesmos critérios dos pull requests: qualquer severidade reprova, e só passa
   a exceção que está justificada, por aviso e no menor escopo.

---

### História de Usuário 5 - Api com verificações de saúde (Prioridade: P2)

A Api passa a ter duas verificações de saúde, **anônimas** e com resposta mínima: **viva** (o
processo está de pé, sem depender de nenhum recurso externo) e **pronta** (o catálogo está
acessível). Quem opera o ambiente, o futuro balanceador de carga e os testes de funcionamento
usam essas verificações para saber se a Api serve ou não.

**Por que esta prioridade**: a constituição exige as duas (seção "Operação"), a Api hoje não tem
nenhuma, e o ambiente na nuvem (spec 05b) não consegue decidir se uma versão nova está saudável
sem elas. Como são endpoints anônimos numa Api exposta a atacantes, precisam ser mínimas e não
revelar nada.

**Teste Independente**: subir a Api com o catálogo no ar e chamar as duas sem credencial; derrubar
o catálogo e chamar de novo; conferir os resultados e que a resposta não traz nenhum detalhe
interno.

**Cenários de Aceitação**:

1. **Dado** a Api com o processo de pé, **Quando** a verificação **viva** é chamada sem
   credencial, **Então** responde sucesso, sem consultar banco, catálogo ou qualquer outro recurso.
2. **Dado** a Api com o catálogo acessível, **Quando** a verificação **pronta** é chamada sem
   credencial, **Então** responde sucesso.
3. **Dado** o catálogo fora do ar ou sem responder, **Quando** a verificação **pronta** é chamada,
   **Então** responde indisponível dentro de um tempo limite curto, e a **viva** continua
   respondendo sucesso. O estado da **pronta** reflete a queda ou a volta do catálogo em até 5
   segundos (o resultado é guardado por instância durante 5 segundos).
4. **Dado** qualquer chamada às duas verificações, **Quando** a resposta é examinada, **Então**
   não contém detalhe interno: nem nome de servidor ou de base, nem versão, nem texto de erro, nem
   dado de tenant, só o estado mínimo.
5. **Dado** uma ou várias bases de tenant fora do ar com o catálogo acessível, **Quando** a
   **pronta** é chamada, **Então** responde sucesso: as bases de tenant não entram nela, porque
   são muitas.
6. **Dado** que as duas verificações são anônimas, **Quando** qualquer outro endpoint da Api é
   chamado sem credencial, **Então** continua exigindo autenticação como antes.

---

### História de Usuário 6 - Dados fictícios de homologação por script (Prioridade: P2)

Existe um comando versionado que cria, no ambiente de homologação, os dados fictícios que o
ambiente vai usar: **vários tenants**, com pelo menos dois dividindo uma base compartilhada e pelo
menos um em base dedicada, cada um com operadores fictícios. Ele pode ser executado várias vezes
sem estragar nada, **recusa rodar em produção** e lê as senhas de fora do repositório.

**Por que esta prioridade**: a constituição exige que fora de produção só existam dados
sintéticos, gerados por script, e que nunca exista cópia de dado real fora de produção (LGPD).
O comando de desenvolvimento atual só roda em Development; a homologação precisa do seu próprio,
antes de o ambiente existir.

**Teste Independente**: em bases locais preparadas como homologação, rodar o comando e conferir os
tenants e operadores; rodar de novo e conferir que nada mudou; tentar rodá-lo como produção e como
desenvolvimento e conferir a recusa; rodá-lo sem senha e conferir a falha sem escrita.

**Cenários de Aceitação**:

1. **Dado** um ambiente de homologação com as bases criadas, mesmo que ainda sem estrutura
   atualizada nem registro no catálogo, **Quando** o comando roda, **Então** ele registra as bases e
   os tenants no catálogo, atualiza a estrutura das bases e existem ao menos três tenants fictícios
   (dois em uma base compartilhada e um em base dedicada), cada um com operadores fictícios.
2. **Dado** que o comando já rodou, **Quando** ele roda de novo, **Então** não falha, não duplica
   nada e deixa intactos os tenants e operadores já existentes.
3. **Dado** o ambiente de produção, **Quando** alguém tenta rodar o comando, **Então** ele se
   recusa, nada é escrito e a mensagem explica o motivo; o mesmo vale para desenvolvimento, que
   segue com o comando próprio.
4. **Dado** que uma senha de operador não foi informada ou não atende às regras de senha do
   sistema, **Quando** o comando roda, **Então** ele falha, nada é criado e nenhum valor de senha
   aparece na mensagem nem nos registros.
5. **Dado** os operadores fictícios criados, **Quando** se examinam seus e-mails, **Então** todos
   usam domínios reservados a testes, e **nenhum** usa o e-mail fixo do administrador de
   desenvolvimento.
6. **Dado** o comando de dados de desenvolvimento existente, **Quando** esta feature termina,
   **Então** ele continua funcionando exatamente como antes, só em desenvolvimento.
7. **Dado** os tenants fictícios, **Quando** se consulta a documentação do comando, **Então** os
   identificadores deles são estáveis e documentados, para que a configuração do ambiente
   (por exemplo, o tenant padrão do login) possa referenciá-los.
8. **Dado** que a estrutura de alguma das bases não pôde ser atualizada, **Quando** o comando roda,
   **Então** ele falha com mensagem clara e código de saída de falha, **nenhum operador é criado**,
   e o registro das bases e dos tenants no catálogo pode permanecer; uma nova execução, depois de
   corrigida a causa, conclui o trabalho sem duplicar nada.

---

### História de Usuário 7 - Verificação de que nenhuma base tem o administrador de senha fixa (Prioridade: P2)

Existe uma verificação que percorre **todas as bases de um ambiente** (o catálogo e cada base de
tenant) e **reprova** se encontrar o administrador de e-mail e senha fixos criado pela migration de
desenvolvimento. Basta **qualquer dado fixo** da migration bater (o identificador fixo **ou** o
e-mail fixo, comparado sem diferenciar maiúsculas de minúsculas), não só os dois juntos. Ela só lê,
nunca altera nada, e uma base que não pôde ser verificada conta como reprovação.

**Por que esta prioridade**: aquele administrador tem senha pública (está no repositório, que é
aberto). Se ele existir numa base de homologação ou produção, qualquer pessoa entra no sistema. Hoje
só uma regra impede que base nova o receba; esta verificação é uma segunda barreira, independente,
que a criação de qualquer ambiente vai poder exigir.

**Teste Independente**: com uma base limpa, rodar a verificação e conferir a aprovação; criar de
propósito uma base com o administrador fixo (como a migration o cria) e conferir a reprovação e a
indicação de qual base; repetir com o e-mail alterado e o identificador ainda o fixo, e com o
identificador trocado e o e-mail fixo escrito com outras maiúsculas e minúsculas; deixar uma base
inacessível e conferir a reprovação.

**Cenários de Aceitação**:

1. **Dado** um ambiente em que nenhuma base contém o administrador fixo, **Quando** a verificação
   roda, **Então** aprova e informa quantas bases verificou.
2. **Dado** uma base de tenant que contém o administrador fixo, **Quando** a verificação roda,
   **Então** reprova com falha clara e indica qual base (pelo nome lógico, sem segredos nem dados
   pessoais).
3. **Dado** uma base em que o e-mail do administrador foi alterado, mas o identificador continua o
   fixo da migration, **Quando** a verificação roda, **Então** reprova: o identificador fixo
   sozinho basta.
4. **Dado** uma base em que o identificador foi trocado, mas o e-mail continua o fixo da migration
   (inclusive escrito com outras maiúsculas e minúsculas), **Quando** a verificação roda,
   **Então** reprova: o e-mail fixo sozinho basta, sem diferenciar maiúsculas de minúsculas.
5. **Dado** que o catálogo contém uma referência ao administrador fixo, **Quando** a verificação
   roda, **Então** reprova da mesma forma.
6. **Dado** uma base registrada no ambiente que está inacessível ou não pôde ser verificada,
   **Quando** a verificação roda, **Então** reprova: não conseguir verificar nunca é aprovar.
7. **Dado** uma base vazia ou ainda sem as tabelas de operadores, **Quando** a verificação roda,
   **Então** ela é tratada como "não contém" e aprovada, de forma distinta de uma base inacessível.
8. **Dado** o ambiente de desenvolvimento, onde o administrador de desenvolvimento existe de
   propósito, **Quando** alguém tenta rodar a verificação, **Então** ela se recusa e explica que
   não se aplica a esse ambiente.
9. **Dado** qualquer execução, **Quando** ela termina, **Então** nada foi alterado em nenhuma
   base.

---

### História de Usuário 8 - Conferência e guia das proteções do repositório (Prioridade: P2)

Existe uma **conferência das proteções** do repositório, em duas partes: uma **automática**, que
roda toda semana e sob demanda e avisa o dono se faltar alguma regra da principal, e uma **à mão**,
para o que a leitura automática não mostra. E existe um guia curto, em português, que diz **como
cada proteção está configurada**, **por quê**, e **como refazê-las do zero** e conferir que estão
ativas. O dono do projeto não domina infraestrutura, e as proteções vivem na configuração da
plataforma, não no código: sem conferência, uma proteção desligada ou uma verificação renomeada
passariam despercebidas; sem guia, ninguém saberia refazê-las depois de uma mudança de plano, de
visibilidade ou de conta.

**Por que esta prioridade**: o repositório hoje é público para ter essas proteções de graça, e o
dono pretende talvez voltar a privado. Se isso acontecer, a configuração precisa ser refeita, e
precisa existir um jeito reproduzível e conferível de fazê-lo. A conferência também é
pré-condição para ensaiar as proteções com segurança (História 1).

**Teste Independente**: pedir ao dono que, só com o guia, descreva cada proteção ativa, execute o
procedimento de refazê-las e rode a conferência, confirmando que está ativa; depois, com respostas
simuladas da plataforma, conferir que a parte automática falha quando falta uma regra, quando o
nome da verificação agregadora está trocado e quando o envio forçado está liberado.

**Cenários de Aceitação**:

1. **Dado** o guia, **Quando** o dono o lê, **Então** encontra a lista de cada proteção ativa
   (principal só por pull request, verificação agregadora obrigatória, sem exceção para ninguém,
   sem envio forçado nem exclusão, varredura de segredos com bloqueio no envio) e o motivo de
   cada uma.
2. **Dado** o guia, **Quando** o dono segue o procedimento de refazer as proteções, **Então** ele
   chega ao mesmo estado e tem uma forma de conferir que as proteções estão ativas.
3. **Dado** o guia, **Quando** o dono precisa agir numa emergência (por exemplo, a plataforma de CI
   fora do ar impede um aceite), **Então** encontra como desligar a proteção de forma deliberada e
   como religá-la.
4. **Dado** o guia, **Quando** o dono o consulta, **Então** encontra também: a ordem correta de
   ativação (a proteção só pode exigir a verificação agregadora depois de ela existir na
   principal); o que acontece se o repositório voltar a ser privado (exige plano pago, e a
   conferência automática precisa ser reavaliada); como ver o resultado da verificação
   agregadora, da auditoria agendada e da conferência automática; como iniciar a auditoria e a
   conferência automática à mão; e o cuidado para ensaiar as proteções (só depois de a conferência
   confirmar que estão ativas, com o SHA da principal anotado antes e, no envio forçado, uma
   mudança que possa ser desfeita).
5. **Dado** que a conferência automática roda (toda semana ou sob demanda) com todas as regras da
   principal no lugar, **Quando** ela termina, **Então** passa e o resultado fica registrado.
6. **Dado** que falta uma regra da principal (pull request obrigatório, verificação agregadora
   exigida, envio forçado bloqueado ou exclusão bloqueada), ou que a verificação agregadora é
   exigida por um nome que não existe mais **ou por uma origem que não é o GitHub Actions**,
   **Quando** a conferência automática roda, **Então** ela falha indicando o que faltou, e o dono
   é avisado pelo e-mail padrão da plataforma.
7. **Dado** que a conferência automática roda, **Quando** ela lê as regras, **Então** usa só o
   token da própria execução (somente leitura), sem nenhuma credencial guardada no repositório e
   sem leitura anônima, e só roda em agenda ou sob demanda, nunca por pull request.
8. **Dado** o guia, **Quando** o dono faz a conferência à mão, **Então** confirma o que a
   automática não vê: que ninguém pode furar as regras da principal e que a varredura de segredos
   está ligada.

---

### Casos de Borda

- **Ordem de ativação (o ovo e a galinha)**: a proteção só pode exigir a verificação agregadora
  depois que ela existe na principal; se for exigida antes, trava todos os pull requests, inclusive
  o que a cria. A feature entra primeiro (por pull request), e a proteção é aplicada depois.
- **Renomear a verificação agregadora**: a proteção exige a verificação por nome. Se o nome mudar,
  a exigência deixa de valer sem aviso e o aceite fica sem barreira. A conferência automática das
  proteções (História 8) detecta na execução semanal (ou sob demanda), falha e avisa o dono; o
  guia avisa para rodá-la logo depois de renomear.
- **Verificação "pulada" pela plataforma**: conta como verde para o bloqueio de aceite. Por isso a
  verificação agregadora é quem decide e confere o resultado real de cada verificação aplicável
  (História 2, cenário 6).
- **Verificação cancelada**: por nova mudança no pull request ou à mão. Conta como não aprovada;
  vale o resultado do estado mais recente do pull request.
- **Pull request com conflito de merge**: enquanto houver conflito com a principal, a plataforma
  **não inicia** as verificações de pull request; a verificação agregadora nem existe e o aceite
  fica bloqueado (nunca conta como aprovada). Depois de resolver o conflito, as verificações rodam
  normalmente. É a exceção prevista no FR-006; o guia explica o que o dono vê e como sair.
- **Pull request de fork de primeiro contribuinte**: a plataforma espera aprovação manual do dono
  para iniciar as verificações; até lá a verificação agregadora não existe e o aceite fica
  bloqueado. Depois de aprovada a execução, ela roda sem segredos e sem permissão de escrita. É a
  segunda exceção prevista no FR-006; o guia explica onde o dono aprova.
- **Pull request que altera a própria verificação agregadora**: a verificação de um pull request é
  a definida no próprio pull request, então uma mudança que a afrouxe se avaliaria a si mesma como
  aprovada. A barreira aqui é a revisão do dono antes de aceitar, e a constituição (Integridade
  das entregas) proíbe afrouxar verificações para uma entrega passar. O guia e as Premissas deixam
  esse limite explícito.
- **Mudança em arquivo fora de qualquer área conhecida**: roda todas as verificações (fechado por
  padrão), nunca nenhuma.
- **Agendamento desativado pela plataforma**: repositórios públicos sem atividade por muito tempo
  (hoje, 60 dias) podem ter agendamentos desativados. O guia explica como notar e reativar, para a
  auditoria semanal e a conferência automática das proteções não pararem em silêncio.
- **Aviso por e-mail que não chega**: notificações desligadas ou na caixa de spam. A falha continua
  visível no histórico das verificações, e o guia diz onde conferir.
- **Falha de rede na auditoria de pacotes**: a falha de consulta à base de avisos reprova (ou
  repete), nunca passa.
- **Verificação "pronta" sob abuso**: ela é anônima e consulta o catálogo; chamadas repetidas não
  podem esgotá-lo. A pronta faz no máximo 1 consulta ao catálogo a cada 5 segundos por instância
  (FR-022), com tempo limite curto; por isso o estado dela pode ficar até 5 segundos defasado em
  relação ao catálogo (FR-020).
- **Verificação "pronta" com catálogo lento**: responde indisponível no tempo limite, em vez de
  ficar pendurada.
- **Comando de dados fictícios em base sem estrutura**: o comando registra as bases e os tenants
  no catálogo e atualiza a estrutura das bases antes de criar operadores; se a estrutura de
  qualquer base não puder ser atualizada, ele falha com mensagem clara e **não cria nenhum
  operador**. O registro no catálogo pode permanecer (é idempotente), e uma nova execução, depois
  de corrigida a causa, termina o trabalho sem duplicar nada.
- **Comando de dados fictícios e o administrador fixo**: nenhum operador fictício pode usar o
  e-mail fixo do administrador de desenvolvimento, nem com outra senha (é um login conhecido e
  adivinhável, e faria a verificação da História 7 reprovar).
- **Verificação do administrador fixo e identidade alterada**: se alguém alterar o administrador
  depois de criado (por exemplo, o e-mail), uma busca por um só dado não o acharia. Por isso
  **qualquer dado fixo da migration basta para reprovar**: o identificador fixo **ou** o e-mail
  fixo (comparado sem diferenciar maiúsculas de minúsculas), e não só os dois juntos. Consequência
  aceita: um operador legítimo de qualquer tenant que use o e-mail fixo, mesmo com outra senha e
  outro identificador, também reprova, porque é um login conhecido e adivinhável.
- **Base registrada no catálogo mas ausente da configuração do ambiente** (ou o contrário): a
  verificação do administrador fixo trata como base não verificável e reprova.
- **Emergência sem CI**: com a plataforma de CI fora do ar, ninguém consegue aceitar um pull request;
  é o comportamento seguro, e o guia descreve o caminho deliberado (alterar a configuração do
  repositório) para quem decidir assumir o risco.
- **Ensaio das proteções sem proteção**: os ensaios de envio direto, envio forçado e exclusão da
  principal só podem ser feitos **depois** que a conferência das proteções (FR-037) confirmar que
  elas estão ativas, porque sem proteção o ensaio alteraria ou apagaria a principal de verdade. O
  ensaio de envio forçado anota antes o SHA atual da principal e usa uma mudança que possa ser
  desfeita; o guia descreve esse cuidado (FR-038).

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Proteção da branch principal**

- **FR-001**: A branch principal DEVE aceitar mudanças somente por pull request. O envio direto de
  commits DEVE ser recusado para todas as pessoas, inclusive o dono do projeto e administradores do
  repositório.
- **FR-002**: A exclusão da branch principal e a reescrita do seu histórico (envio forçado) DEVEM
  ser recusadas para todas as pessoas, sem exceção.
- **FR-003**: Um pull request só DEVE poder ser aceito com a verificação agregadora aprovada; essa
  é a única verificação exigida para o aceite, e a exigência NÃO DEVE ter exceção para ninguém. A
  exigência vale para a verificação **vinda do GitHub Actions**: um status externo com o mesmo nome
  NÃO a satisfaz.
- **FR-004**: A proteção NÃO DEVE exigir aprovação humana de outra pessoa: o dono do projeto pode
  aceitar o próprio pull request quando a verificação agregadora aprova.
- **FR-005**: Desligar ou alterar a proteção DEVE ser possível somente pela configuração do
  repositório (ação deliberada do dono, registrada pela plataforma), nunca por envio de código.
- **FR-005a**: A varredura automática de segredos com bloqueio no envio DEVE estar ativa no
  repositório, como proteção adicional: um envio que contenha um segredo reconhecido (senha,
  chave ou token) DEVE ser barrado antes de entrar no repositório. Ela faz parte do procedimento
  versionado, do guia e da conferência à mão (FR-037).

**Verificação agregadora**

- **FR-006**: DEVE existir uma verificação agregadora que **sempre** roda em todo pull request para a
  principal, independente de quais arquivos mudaram, e que sempre termina com um resultado
  (aprovada ou reprovada) dentro de um tempo limite definido; um pull request NUNCA DEVE ficar
  aguardando indefinidamente por ela, **salvo** nos dois casos em que a própria plataforma não
  inicia as verificações (conflito de merge e fork de primeiro contribuinte; ver os casos de
  borda).
- **FR-007**: A verificação agregadora DEVE identificar quais áreas a mudança toca (backend,
  frontend, documentos e especificações) e DEVE exigir as verificações das áreas tocadas. A
  verificação de uma área sem mudança DEVE ser dispensada, sem bloquear o aceite.
- **FR-008**: Só DEVE ser dispensada uma verificação cuja área comprovadamente não mudou. Uma
  mudança restrita a documentos DEVE passar sem rodar backend nem frontend; contam como
  documentos `specs/`, qualquer arquivo `.md`, `.specify/` (constituição e templates do Spec Kit)
  e `.claude/` (skills). Qualquer mudança em arquivo que não pertença a nenhuma área conhecida
  (por exemplo, configuração de CI, de containers ou de scripts) DEVE exigir todas as verificações.
- **FR-009**: A verificação agregadora DEVE reprovar se qualquer verificação aplicável falhou, foi
  cancelada, expirou ou não chegou a rodar. Uma verificação aplicável marcada como "pulada" sem o
  motivo de dispensa da área também DEVE reprovar. Ausência de resultado NUNCA DEVE contar como
  aprovação.
- **FR-010**: O resultado DEVE mostrar, para cada verificação, o seu estado (aprovada, reprovada,
  dispensada por falta de mudança na área ou não ativada), de forma visível sem abrir registros
  detalhados.
- **FR-011**: A cada novo envio de commits ao pull request, a verificação agregadora DEVE rodar de
  novo, e só o resultado do estado mais recente DEVE valer para o aceite.
- **FR-012**: O passo de teste de carga curto DEVE estar previsto entre as verificações e DEVE
  aparecer como **"não ativado"** enquanto não houver cenários de carga versionados: NUNCA como
  aprovado, nem como sucesso silencioso. Enquanto não ativado, ele NÃO DEVE bloquear o aceite;
  quando existirem cenários (spec de escalabilidade), a ativação o torna aplicável e bloqueante
  como as demais verificações.
- **FR-013**: As verificações de backend e de frontend que já existem DEVEM manter o mesmo conteúdo
  e o mesmo rigor. Nenhum teste, limite ou verificação existente PODE ser apagado, pulado ou
  afrouxado para esta feature passar (constituição, Integridade das entregas).
- **FR-013a**: Depois de aceita uma mudança na principal, as verificações aplicáveis às áreas
  tocadas DEVEM rodar de novo sobre a principal, e uma falha DEVE ficar visível e avisar o dono
  pelo e-mail padrão da plataforma. Isso mantém a principal "sempre pronta" mesmo sem exigir que o
  pull request esteja atualizado com ela.

**Pull requests de fork e permissões**

- **FR-014**: As verificações de um pull request de fork DEVEM rodar sem acesso a nenhum segredo e
  com permissão apenas de leitura do repositório.
- **FR-015**: Toda verificação DEVE declarar explicitamente as permissões de que precisa, por padrão
  somente leitura, e NENHUMA verificação DEVE executar o código de um pull request com segredos ou
  com permissão de escrita.

**Auditoria de pacotes agendada**

- **FR-016**: A auditoria de pacotes vulneráveis (backend e frontend) DEVE rodar também em agenda
  semanal, independente de mudanças no código e de quais arquivos mudaram, e DEVE poder ser
  iniciada à mão.
- **FR-017**: A auditoria agendada e a iniciada à mão DEVEM usar os mesmos critérios das
  verificações de pull request (spec 04b): qualquer severidade reprova; só passa exceção
  justificada, por aviso e no menor escopo.
- **FR-018**: Uma falha da auditoria DEVE ser visível no histórico das verificações e DEVE avisar o
  dono do projeto. Falha ao consultar a base de avisos DEVE contar como falha (ou ser repetida),
  nunca como "sem vulnerabilidades".

**Verificações de saúde da Api**

- **FR-019**: A Api DEVE expor a verificação **viva** (`/health/live`), que responde sucesso se o
  processo está de pé e NÃO DEVE depender de banco, catálogo ou qualquer outro recurso externo.
- **FR-020**: A Api DEVE expor a verificação **pronta** (`/health/ready`), que responde sucesso
  somente quando o catálogo está acessível e, caso contrário, DEVE responder indisponível dentro
  de um tempo limite curto, sem ficar pendurada. O estado devolvido DEVE refletir a queda ou a
  volta do catálogo em até 5 segundos (o resultado é guardado por instância durante 5 segundos).
  As bases de tenant NÃO DEVEM entrar nela.
- **FR-021**: As duas verificações DEVEM ser anônimas (sem credencial) e a resposta DEVE ser mínima,
  sem nenhum detalhe interno: nome de servidor ou de base, versão, texto de erro ou dado de
  tenant.
- **FR-022**: As verificações NÃO DEVEM alterar a exigência de autenticação dos demais endpoints,
  e a pronta NÃO DEVE permitir que chamadas anônimas repetidas esgotem o catálogo: ela faz **no
  máximo 1 consulta ao catálogo a cada 5 segundos por instância**, sob qualquer volume de
  chamadas.
- **FR-023**: O comportamento das duas verificações DEVE ser provado por testes automatizados,
  inclusive o da pronta com o catálogo fora do ar, e as verificações DEVEM ter comentário de
  documentação como os demais endpoints.

**Dados fictícios de homologação**

- **FR-024**: DEVE existir um comando versionado que cria os dados fictícios de homologação: ao
  menos três tenants fictícios, sendo ao menos dois numa base compartilhada e ao menos um em
  base dedicada, cada um com operadores fictícios. Como o comando de dados de desenvolvimento, ele
  **registra as bases e os tenants no catálogo** (sem duplicar) e **atualiza a estrutura dessas
  bases** pelo mesmo mecanismo de atualização do Migrator, aplicando a política que impede o
  administrador fixo em base nova; a atualização que o Migrator já faz só alcança bases que já
  estão registradas, por isso o próprio comando as registra antes.
- **FR-025**: O comando DEVE ser idempotente: rodar de novo NÃO DEVE falhar, duplicar dados nem
  alterar tenants e operadores já existentes.
- **FR-026**: O comando DEVE rodar somente no ambiente de homologação. Em produção e em
  desenvolvimento DEVE se recusar, sem escrever nada, com mensagem clara e código de saída
  próprio. O comando de dados de desenvolvimento existente DEVE continuar como está, só em
  desenvolvimento.
- **FR-027**: As senhas dos operadores fictícios DEVEM vir de fora do repositório (configuração ou
  variável de ambiente) e NUNCA DEVEM ficar no código nem em registros. Senha ausente ou que não
  atenda às regras de senha do sistema DEVE fazer o comando falhar sem criar nada.
- **FR-028**: Os dados DEVEM ser claramente fictícios: nomes genéricos e e-mails em domínios
  reservados a testes. NENHUM operador fictício PODE usar o e-mail fixo do administrador de
  desenvolvimento.
- **FR-029**: Os tenants fictícios DEVEM ter identificadores estáveis e documentados, para que a
  configuração de um ambiente possa referenciá-los.
- **FR-030**: Se a estrutura de **qualquer** base não puder ser atualizada (base inacessível ou
  atualização que falha), o comando DEVE falhar com mensagem clara, código de saída de falha, e
  **sem criar nenhum operador** (nenhuma escrita parcial de dados fictícios). O registro das bases
  e dos tenants no catálogo PODE permanecer, porque é idempotente: rodar o comando de novo, depois
  de corrigida a causa, continua de onde parou sem duplicar nada.

**Verificação do administrador fixo**

- **FR-031**: DEVE existir uma verificação que percorre todas as bases do ambiente informado (o
  catálogo e cada base de tenant) e **reprova** se alguma contiver o administrador de e-mail e
  senha fixos criado pela migration de desenvolvimento. Basta **qualquer dado fixo** da migration
  bater para reprovar: o identificador fixo **ou** o e-mail fixo (comparado sem diferenciar
  maiúsculas de minúsculas), e não só os dois juntos.
- **FR-032**: A reprovação DEVE falhar de forma clara e indicar quais bases contêm o administrador
  (pelo nome lógico, sem segredos nem dados pessoais). A aprovação só DEVE ocorrer quando todas as
  bases foram de fato verificadas.
- **FR-033**: Uma base inacessível, não verificável, ou presente no ambiente mas ausente da
  configuração (ou o contrário), DEVE ser tratada como reprovação. Uma base vazia ou sem as
  tabelas de operadores DEVE ser tratada como "não contém", de forma distinta.
- **FR-034**: A verificação DEVE ser somente de leitura e DEVE se recusar a rodar em
  desenvolvimento, onde o administrador de desenvolvimento existe de propósito.
- **FR-035**: O comportamento DEVE ser provado por testes automatizados: base limpa aprova; base
  contaminada de propósito (catálogo e base de tenant) reprova; base em que o e-mail do
  administrador foi alterado e o identificador continua o fixo reprova; base em que o
  identificador foi trocado e o e-mail fixo continua (inclusive escrito com outras maiúsculas e
  minúsculas) reprova; base inacessível reprova.

**Guia das proteções do repositório**

- **FR-036**: DEVE existir um guia curto, versionado no repositório e em português do Brasil, que
  descreve cada proteção ativa (inclusive a varredura de segredos) e o motivo, e o procedimento
  para refazê-las do zero e conferir que estão ativas, nas duas partes da conferência (FR-037).
- **FR-037**: A configuração das proteções DEVE ser reproduzível a partir do repositório (um
  procedimento versionado, por script, usando conjuntos de regras da plataforma e não a proteção
  de branch clássica), e DEVE existir uma conferência que detecte proteção ausente, alterada ou
  apontando para uma verificação que não existe mais (por exemplo, por renomeação). A conferência
  tem duas partes: (a) **automática**, em agenda semanal e sob demanda, que lê as regras ativas da
  principal e falha, avisando o dono pelo e-mail padrão da plataforma, se faltar qualquer um destes
  pontos: pull request obrigatório; verificação agregadora exigida **pelo nome certo e vinda do
  GitHub Actions** (a conferência falha se faltar o nome **ou** a origem: um status externo de
  mesmo nome não basta); envio forçado bloqueado; exclusão bloqueada; (b) **à mão**, pelo dono,
  seguindo o guia, para o que a leitura
  automática não mostra: quem pode furar as regras (deve ser ninguém) e se a varredura de segredos
  está ligada.
- **FR-037a**: A conferência automática NÃO DEVE usar nenhuma credencial guardada no repositório:
  DEVE usar apenas o token que a plataforma entrega à própria execução (`GITHUB_TOKEN`), somente
  leitura, e **nunca** uma leitura anônima (o limite de chamadas sem autenticação é por endereço e
  muito menor). O plano confirmou na documentação da plataforma que a leitura das regras exige só a
  permissão de leitura de metadados; que o token da execução a tem é provado pela primeira execução
  real. A execução NÃO DEVE ser disparada por pull request: só em agenda e sob demanda.
- **FR-038**: O guia DEVE documentar: a ordem de ativação; como desligar e religar a proteção
  deliberadamente numa emergência; o efeito de o repositório voltar a ser privado (exige plano
  pago, e a conferência automática precisa ser reavaliada, porque a leitura com o token da
  execução não está confirmada para repositório privado);
  onde ver o resultado da verificação agregadora, da auditoria agendada e da conferência
  automática; como iniciar a auditoria e a conferência automática à mão; como fazer a parte da
  conferência que é à mão; como notar um agendamento desativado por inatividade; e o cuidado para
  ensaiar as proteções: só depois de a conferência (FR-037) confirmar que estão ativas, anotando
  antes o SHA atual da principal e usando, no envio forçado, uma mudança que possa ser desfeita.

**Não regressão**

- **FR-039**: Nenhuma funcionalidade existente da Api, do frontend ou do Migrator DEVE deixar de
  funcionar, e nenhum teste anterior PODE ser apagado, pulado ou enfraquecido. Os endpoints de
  saúde DEVEM ser os únicos novos endpoints anônimos, e a exceção ao fallback de autenticação DEVE
  ser justificada no plano (Princípio IX).

### Entidades-Chave *(inclua se a feature envolver dados)*

Esta feature não cria nem altera dados de negócio. Os conceitos que ela governa:

- **Proteção da principal**: o conjunto de regras (ruleset) da plataforma que decide quem pode
  alterar a branch principal e em que condições (só por pull request, com a verificação
  agregadora aprovada).
- **Verificação agregadora**: o resultado único, por pull request, que resume as verificações
  aplicáveis e é a única exigida para o aceite.
- **Verificação aplicável / dispensada / não ativada**: os estados de cada verificação no
  resultado; "dispensada" exige que a área não tenha mudado, e "não ativada" é o estado do passo de
  carga curto até haver cenários.
- **Auditoria agendada**: a execução periódica (semanal) da verificação de pacotes vulneráveis,
  independente de mudanças.
- **Verificação de saúde (viva e pronta)**: as duas respostas anônimas e mínimas da Api sobre o
  próprio estado.
- **Dados fictícios de homologação**: os tenants e operadores sintéticos criados por comando
  versionado, com identificadores estáveis.
- **Administrador fixo**: o operador de e-mail e senha fixos que a migration de desenvolvimento
  cria, que não pode existir em nenhuma base de homologação ou produção.
- **Guia das proteções**: o documento curto que descreve e permite refazer as proteções do
  repositório.
- **Conferência das proteções**: a leitura das regras ativas da principal, automática (semanal e
  sob demanda) e à mão, que confirma que as proteções estão como esperado.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% das tentativas de enviar código direto à principal, apagá-la ou reescrever o seu
  histórico são recusadas, inclusive as feitas pela conta do dono do projeto, comprovado por
  tentativas controladas registradas no guia e feitas somente depois de a conferência das
  proteções (FR-037) confirmar que estão ativas.
- **SC-002**: 0 pull requests ficam aguardando para sempre por uma verificação que não se aplica:
  um pull request só de documentos recebe o resultado da verificação agregadora em até 5 minutos,
  sem rodar backend nem frontend.
- **SC-003**: Em pull requests descartáveis de teste, o resultado da verificação agregadora está
  correto em 100% dos casos: só documentos aprova; só frontend exige só o frontend; só backend
  exige só o backend; teste quebrado reprova; execução cancelada reprova; arquivo fora das áreas
  conhecidas exige todas as verificações.
- **SC-004**: 0 pull requests são aceitáveis com uma verificação aplicável que falhou, foi
  cancelada, expirou, foi pulada sem motivo de dispensa ou não rodou.
- **SC-005**: Em 100% das execuções de verificações de pull requests de fork, nenhum segredo está
  disponível e a permissão é somente de leitura, comprovado no registro da execução.
- **SC-006**: A auditoria de pacotes roda ao menos uma vez por semana sem nenhuma mudança no
  código; num ensaio com falha provocada de propósito, a falha fica visível e o aviso ao dono
  sai.
- **SC-007**: A verificação **viva** responde sucesso em 100% das chamadas com o processo de pé,
  sem consultar nenhum recurso; a **pronta** responde sucesso com o catálogo acessível e
  indisponível com o catálogo fora, cada chamada em até 2 segundos, e o estado dela reflete a queda
  ou a volta do catálogo em até 5 segundos (resultado guardado por instância); e 0 respostas
  contêm detalhe interno.
- **SC-008**: A verificação **pronta** faz no máximo 1 consulta ao catálogo a cada 5 segundos por
  instância, sob qualquer volume de chamadas, provado por teste com uma sonda falsa que conta as
  chamadas.
- **SC-009**: Depois de rodar o comando de dados fictícios, existem ao menos 3 tenants (2 em base
  compartilhada e 1 em dedicada) com operadores; uma segunda execução deixa a contagem de tenants
  e de operadores idêntica (0 duplicatas); e a recusa em produção e em desenvolvimento ocorre em
  100% das tentativas, sem escrita.
- **SC-010**: Nenhuma senha de operador fictício aparece no código de produção, nos
  `appsettings*.json`, nos `docker-compose*.yml` nem em `.github/`, conferido por um teste de
  arquitetura (os testes podem ter senhas literais de teste, como o `SeedDevCommandTests`); e 0
  operadores fictícios usam o e-mail fixo do administrador de desenvolvimento.
- **SC-011**: A verificação do administrador fixo aprova 100% das bases limpas e reprova 100% das
  bases contaminadas de teste (catálogo e base de tenant), das bases em que só o e-mail foi
  alterado (identificador fixo mantido), das em que só o identificador foi trocado (e-mail fixo
  mantido, em qualquer combinação de maiúsculas e minúsculas) e das bases inacessíveis, indicando
  qual base falhou, sem alterar nenhuma base.
- **SC-012**: O dono do projeto, só com o guia, descreve cada proteção ativa, refaz as proteções do
  zero e a conferência confirma que estão ativas, sem nenhum passo não documentado.
- **SC-013**: Todos os testes automatizados anteriores continuam passando sem terem sido
  alterados, apagados ou enfraquecidos.
- **SC-014**: Em 100% das mudanças aceitas que tocam uma área com verificação, as verificações
  rodam de novo sobre a principal e o resultado fica visível; uma falha avisa o dono pelo e-mail
  padrão da plataforma.
- **SC-015**: A conferência automática das proteções falha em 100% dos casos de teste em que falta
  pull request obrigatório, verificação agregadora exigida, envio forçado bloqueado ou exclusão
  bloqueada, em que o nome exigido da verificação agregadora não existe mais, ou em que a origem
  exigida não é o GitHub Actions, e passa quando tudo está no lugar; isso é provado por teste
  automatizado sobre respostas simuladas da plataforma e por uma execução real aprovada, sem
  nenhuma credencial guardada.
- **SC-016**: A conferência à mão confirma que a varredura de segredos com bloqueio no envio está
  ativa e que ninguém pode furar as regras da principal.

## Premissas

- **Escopo**: esta spec cobre a proteção da principal, a verificação agregadora e o que ela
  engloba, as regras de fork, a auditoria de pacotes agendada, as verificações de saúde da Api, o
  comando de dados fictícios de homologação, a verificação do administrador fixo, a varredura de
  segredos, a conferência das proteções (automática e à mão) e o guia das proteções. **Fora do
  escopo**, e ficando para a spec 05b (nuvem): contas e ambientes na AWS,
  domínio e HTTPS, infraestrutura como código, entrega automática em homologação, promoção para
  produção com aprovação, volta de versão, backup e restauração, alarmes, custos e o guia de
  operação da nuvem. Fora do escopo por outras specs: cenários de carga e a carga de referência
  (spec 06), Redis e telemetria (spec 06), verificações de saúde detalhadas e logs estruturados
  (spec 10), login que descobre o tenant pelo e-mail (spec 09).
- **Origem da divisão**: a antiga spec 05 foi dividida em 2026-11-02 porque a parte da AWS custa
  dinheiro todo mês (estimativa de US$ 100 a 250 para os dois ambientes) e exige domínio. A spec
  gerada antes do corte está guardada fora do repositório como referência para a 05b; esta spec
  não a reaproveita no código.
- **Decisões confirmadas no `/speckit-clarify`** (sessão 2026-11-02, ver "Clarificações"):
  - **Como a proteção é aplicada**: por procedimento versionado (script) mais o guia, usando
    **conjuntos de regras da plataforma (rulesets)** e não a proteção de branch clássica, porque o
    repositório pode voltar a ser privado e a configuração precisaria ser refeita.
  - **Ambiente do comando de dados fictícios**: aceita somente homologação (`Staging`);
    desenvolvimento continua com o seu comando próprio; produção sempre recusa.
  - **Onde a verificação do administrador fixo roda**: como comando próprio do Migrator, executado
    depois da atualização das bases, para a spec 05b encaixá-lo no pipeline da nuvem. Roda em
    homologação e produção; recusa em desenvolvimento.
  - **Aviso das falhas**: o e-mail padrão da plataforma para falha de verificação agendada; não se
    cria um canal novo. O mesmo vale para a falha das verificações na principal (FR-013a) e da
    conferência automática (FR-037).
  - **Atualização obrigatória da branch**: NÃO se exige que a branch do pull request esteja
    atualizada com a principal antes do aceite (dono único, uma spec por vez). A rede de segurança
    é rodar as verificações de novo sobre a principal depois do aceite (FR-013a).
  - **Tempo limite**: o da "pronta" é de 2 segundos; o da agregadora é definido no plano, a partir
    da duração atual das verificações de backend e frontend.
  - **O que conta como documentos**: `specs/`, qualquer arquivo `.md`, `.specify/` e `.claude/`;
    mantém o que acontece hoje (esses pull requests não disparam as verificações).
  - **Varredura de segredos**: entra como proteção do repositório (FR-005a), gratuita em
    repositório público.
  - **Conferência das proteções**: à mão e também automática (semanal e sob demanda), sem
    credencial guardada, lendo as regras ativas da principal pelo endpoint de regras da plataforma
    **com o `GITHUB_TOKEN` da própria execução, somente leitura, nunca de forma anônima** (conferido
    em 2026-11-02: o endpoint respondeu sem credencial e a lista está vazia porque ainda não há
    regras; isso só mostra que o endpoint é legível, não é a forma de uso). O plano confirmou na
    documentação que o endpoint exige só a permissão de leitura de metadados e define a lógica de
    comparação, que confere o nome **e a origem** (GitHub Actions) da verificação agregadora. O que
    o endpoint não mostra (quem pode furar as regras; se a varredura de segredos está ligada) fica
    na conferência à mão.
- **Sem revisão humana obrigatória**: o repositório tem um único dono e o código é escrito por IA;
  exigir aprovação de outra pessoa travaria todos os aceites. A barreira é a verificação
  automática obrigatória, sem exceção para ninguém.
- **Limite da verificação agregadora**: como a verificação de um pull request é a definida no
  próprio pull request, nenhuma automação protege contra uma mudança que afrouxe a própria
  verificação. A barreira é o dono revisar o diff dos arquivos de CI antes de aceitar, junto com a
  regra de Integridade das entregas da constituição. É um limite aceito e explícito.
- **Repositório público e plano gratuito**: a proteção da principal e as demais regras só são
  gratuitas porque o repositório é público (desde 2026-11-02). Se voltar a ser privado, a proteção
  exige plano pago e a conferência automática das proteções precisa ser reavaliada (a leitura com o
  token da execução não está confirmada para repositório privado); o guia diz isso. O repositório não pode conter segredos nem em commit, nem
  em issue, nem em registro de execução.
- **Estado atual relevante** (conferido em 2026-11-02, depois da spec 04b e da constituição
  1.2.4): a principal **não** está protegida e não há nenhuma regra de proteção configurada; só
  existem as verificações de backend e de frontend, ambas com filtro de arquivos e nenhuma em
  agenda; a Api não tem nenhuma verificação de saúde; o comando de dados de desenvolvimento só roda
  em desenvolvimento e cria três tenants (dois numa base compartilhada, um numa dedicada) com um
  administrador de e-mail fixo cuja senha vem do ambiente; a migration de desenvolvimento cria um
  administrador de e-mail, identificador e senha fixos, e hoje uma política do Migrator só impede
  que uma base **nova** o receba (a verificação desta spec é uma barreira independente, que
  também pega base antiga ou alterada à mão); o login é preso a um tenant por instância da Api até
  a spec 09 (não afeta esta spec).
- **Natureza do comando de dados fictícios**: é uma ferramenta operacional, como o comando de
  desenvolvimento, e não um caso de uso da Api; por isso não gera evento de auditoria (a trilha de
  auditoria é da spec 07) e escreve os operadores pelo mesmo caminho do comando de
  desenvolvimento, sob o isolamento por tenant.
- **Saúde da Api**: as duas verificações são os endpoints anônimos previstos pela constituição
  (Princípio IX e seção "Operação"); a exceção aos testes de autorização 401/403 do Princípio VII
  é justificada no plano. Elas não são um caminho crítico no sentido do Princípio XIII, então esta
  spec não exige cenário de carga para elas.
- **Dados pessoais**: nenhum dado pessoal real entra nesta feature. Os dados fictícios usam
  e-mails em domínios reservados a testes e nomes genéricos (Princípio X).
- **Dependências**: depende das specs 01 (isolamento por tenant, Migrator e separação de usuários
  do banco), 02 (testes do frontend), 04 (autenticação) e 04b (verificação de pacotes e política de
  exceções), todas entregues. A spec 05b depende desta: herda a proteção, a verificação agregadora,
  as verificações de saúde, o comando de dados fictícios e a verificação do administrador fixo.
