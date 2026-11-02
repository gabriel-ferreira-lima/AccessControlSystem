# Especificação da Feature: Segurança da autenticação e da sessão

**Branch da Feature**: `20261101-232504-security-auth`

**Criada em**: 2026-11-01

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Endurecer a autenticação e a sessão da Api antes de o sistema ir para produção. Contexto: o sistema controla acesso físico a locais de clientes; uma falha na autenticação pode abrir portas. Esta é a primeira de duas specs de segurança; cabeçalhos do nginx/CSP/HSTS, tamanho máximo de campos, teste de DDL do usuário do banco e verificação de pacotes vulneráveis no CI ficam para a spec seguinte (04b) e estão FORA desta. Requisitos: (1) toda rota exige autenticação por padrão, só o login é anônimo, e o Swagger só existe em Development (404 fora dele); (2) o token deixa de valer na hora quando a senha do operador é trocada (por ele ou por um administrador) ou quando o perfil dele muda, e a desativação continua derrubando o token, agora com teste; (3) tokens só são aceitos se emitidos por este sistema para esta aplicação, com tolerância de relógio curta, e a Api não inicia com chave de assinatura ausente ou fraca; (4) o tempo de resposta do login não revela se o e-mail existe nem se a conta está inativa, e a resposta continua 401 \"Credenciais inválidas\" nos três casos de falha; (5) o login limita tentativas por endereço de origem e por conta (e-mail dentro do tenant), contém ataque distribuído contra a mesma conta e não pode ser contornado forjando X-Forwarded-For; (6) no ambiente local, as portas das Apis e do banco ficam acessíveis só na própria máquina; (7) todo endpoint tem testes de autorização: sem token (401), perfil sem permissão (403), token de outro tenant (401) e token antigo depois de troca de senha ou de perfil (401). Fora do escopo: permissões granulares (spec 08), auditoria (spec 07) e rate limit em Redis (spec 06)."

## Clarificações

### Sessão 2026-11-01

- Q: Qual regra de bloqueio por conta o login aplica depois de falhas seguidas na mesma conta? → A:
  5 falhas seguidas bloqueiam a conta por 1 minuto; a cada novo bloqueio a espera dobra (1, 2, 4, 8
  minutos) até o teto de 15 minutos; um login com sucesso zera a contagem.
- Q: Trocar apenas o e-mail do operador derruba o token que ele já tem? → A: Não; a identidade do
  operador é o identificador, e só troca de senha, troca de perfil e desativação invalidam a sessão.
- Q: O que o console web faz depois que o operador troca a própria senha? → A: Encerra a sessão e
  leva ao login com o aviso "Senha alterada. Faça login novamente."; a Api não devolve token novo
  (o contrato não muda).
- Q: Editar um operador sem mudar de fato a senha nem o perfil derruba o token dele? → A: Não, no
  caso do perfil e da senha em branco: um perfil igual ao atual, ou a senha em branco (não
  informada), não invalida. Ajuste posterior: uma senha informada (não nula) conta sempre como
  troca e invalida a sessão, mesmo que seja igual à atual.

## Cenários de Usuário e Testes *(obrigatório)*

O sistema controla portas e catracas de clientes: quem consegue se autenticar na Api consegue
cadastrar pessoas e liberar acessos. Esta spec fecha as brechas conhecidas da autenticação e da
sessão para que um atacante, ou um ex-operador com um token antigo, não consiga entrar nem
continuar dentro. Os públicos beneficiados são: o **cliente** (tenant), que não pode ter seus
locais abertos por terceiros; o **operador**, cuja conta não pode ser adivinhada nem travada à
toa; o **administrador**, que precisa que trocar uma senha ou um perfil tenha efeito na hora; e
quem **mantém** o sistema, que precisa de prova automatizada de que cada endpoint está protegido.

### História de Usuário 1 - Nenhuma rota abre sem login (Prioridade: P1)

Quem chama a Api sem estar autenticado só consegue chegar ao login. Qualquer outra rota, existente
hoje ou criada no futuro, responde `401` sem token. A documentação interativa da Api (Swagger)
existe apenas no ambiente de desenvolvimento; nos demais, suas rotas respondem `404`, como se não
existissem, para não entregar um mapa da Api a quem a ataca.

**Por que esta prioridade**: é a barreira mais básica. Hoje cada endpoint precisa lembrar de se
proteger; um endpoint novo esquecido nasceria aberto. Fechado por padrão elimina essa classe de
erro, e o Swagger público é um mapa pronto para o atacante.

**Teste Independente**: chamar todas as rotas da Api sem token (e as rotas do Swagger fora de
Development) e conferir `401` e `404`; chamar o login sem token e conferir que ele responde
normalmente.

**Cenários de Aceitação**:

1. **Dado** uma requisição sem token, **Quando** ela chama qualquer rota que não seja o login,
   **Então** a resposta é `401` com a mensagem padrão de não autenticado.
2. **Dado** uma requisição sem token, **Quando** ela chama o login, **Então** o login é processado
   normalmente (aceita ou recusa as credenciais), sem exigir token.
3. **Dado** que um desenvolvedor cria um endpoint novo e não declara nenhuma regra de acesso nele,
   **Quando** o endpoint é chamado sem token, **Então** a resposta é `401`.
4. **Dado** a Api rodando fora do ambiente de desenvolvimento, **Quando** alguém pede a
   documentação interativa ou o arquivo de descrição da Api, **Então** a resposta é `404`.
5. **Dado** a Api rodando no ambiente de desenvolvimento, **Quando** alguém pede a documentação
   interativa, **Então** ela continua disponível como hoje.

---

### História de Usuário 2 - Trocar senha ou perfil derruba o token na hora (Prioridade: P1)

Quando a senha de um operador é trocada (pelo próprio operador ou por um administrador) ou quando
o perfil de acesso dele muda, todo token emitido antes disso deixa de valer **na requisição
seguinte**, sem esperar o vencimento do token. A desativação do operador já derruba o token hoje e
isso continua valendo, agora com teste que prova.

**Por que esta prioridade**: sem isso, quem teve a senha trocada por suspeita de vazamento, ou
perdeu privilégio, continua operando por até 8 horas com o token antigo. É o cenário clássico de
um ex-funcionário ou de uma conta comprometida.

**Teste Independente**: autenticar um operador, guardar o token, trocar a senha (e, em outro
caso, o perfil) e repetir uma chamada autenticada com o token guardado: deve dar `401`. Autenticar
de novo com a credencial nova dá um token que funciona.

**Cenários de Aceitação**:

1. **Dado** um operador com token válido, **Quando** ele troca a própria senha, **Então** o token
   antigo recebe `401` na chamada seguinte, e um login com a senha nova emite um token que
   funciona.
2. **Dado** um operador com token válido, **Quando** um administrador troca a senha dele, **Então**
   o token antigo recebe `401` na chamada seguinte.
3. **Dado** um operador com token válido, **Quando** um administrador muda o perfil dele, **Então**
   o token antigo recebe `401` na chamada seguinte, mesmo que o novo perfil tenha mais permissão.
4. **Dado** um operador com token válido, **Quando** um administrador o desativa, **Então** o token
   recebe `401` na chamada seguinte (comportamento atual, agora coberto por teste).
4a. **Dado** um operador desativado e depois reativado, **Quando** ele usa um token emitido antes
   da desativação, **Então** a resposta é `401`; um login novo depois da reativação emite token
   válido.
5. **Dado** um operador com token válido, **Quando** ele ou um administrador altera só o e-mail ou
   só dados que não são senha, perfil nem situação, **Então** o token continua válido.
5a. **Dado** um operador com token válido, **Quando** um administrador salva a edição dele
   reenviando o mesmo perfil que ele já tem e deixando a senha em branco (não informada),
   **Então** o token continua válido. Se a senha vier informada, mesmo igual à atual, é troca de
   senha e o token cai.
6. **Dado** que o operador A trocou a senha do operador B, **Quando** A usa o próprio token,
   **Então** o token de A continua válido (só os tokens de B morrem).
7. **Dado** que o operador trocou a própria senha no console web, **Quando** a Api responde
   sucesso, **Então** o console encerra a sessão e leva ao login com o aviso "Senha alterada. Faça
   login novamente.", em vez de deixar a pessoa numa tela que passa a falhar.

---

### História de Usuário 3 - Só vale token deste sistema, feito para esta aplicação (Prioridade: P1)

A Api só aceita um token se ele foi emitido por este sistema **e** destinado a esta aplicação, e
se está dentro da validade (com uma tolerância de relógio curta). Um token assinado com a mesma
chave mas emitido para outro fim, ou sem identificação de quem emitiu e para quem, é recusado. A
Api também se recusa a iniciar se a chave usada para assinar tokens estiver ausente ou for fraca
demais, em vez de subir insegura.

**Por que esta prioridade**: sem checar emissor e destinatário, um token feito para outro sistema
que compartilhe a chave (ou um ambiente que a reutilize) seria aceito aqui. Uma chave curta pode
ser descoberta por força bruta, e quem a descobre fabrica tokens de administrador de qualquer
tenant. Falhar na partida é melhor que operar exposto.

**Teste Independente**: gerar tokens com a chave correta mas emissor/destinatário errados ou
ausentes, e tokens vencidos além da tolerância, e conferir `401`; subir a Api com chave ausente,
curta, e sem as identificações de emissor/destinatário configuradas, e conferir que ela não
inicia.

**Cenários de Aceitação**:

1. **Dado** um token assinado corretamente mas com emissor diferente do deste sistema, **Quando**
   ele é usado, **Então** a resposta é `401`.
2. **Dado** um token assinado corretamente mas com destinatário diferente desta aplicação,
   **Quando** ele é usado, **Então** a resposta é `401`.
3. **Dado** um token assinado corretamente mas sem emissor ou sem destinatário, **Quando** ele é
   usado, **Então** a resposta é `401`.
4. **Dado** um token vencido há mais que a tolerância de relógio, **Quando** ele é usado, **Então**
   a resposta é `401`; vencido há menos que a tolerância, o resultado segue a regra de tolerância
   definida na seção de Requisitos.
5. **Dado** a configuração sem chave de assinatura, ou com chave menor que o mínimo exigido,
   **Quando** a Api é iniciada, **Então** ela não inicia e informa o motivo sem revelar o valor
   da chave.
6. **Dado** a configuração sem o emissor ou sem o destinatário esperados, **Quando** a Api é
   iniciada, **Então** ela não inicia e informa qual configuração falta.
7. **Dado** a configuração completa e válida, **Quando** a Api é iniciada e um operador faz login,
   **Então** o token emitido é aceito nas chamadas seguintes.

---

### História de Usuário 4 - O login não conta se o e-mail existe (Prioridade: P2)

Quem tenta entrar recebe sempre `401` "Credenciais inválidas" quando falha, e a Api gasta o mesmo
trabalho nos três casos de falha: e-mail inexistente, conta inativa e senha errada. Assim, medir o
tempo de resposta não permite descobrir quais e-mails estão cadastrados nem quais contas estão
inativas.

**Por que esta prioridade**: enumerar e-mails válidos é o primeiro passo de um ataque de senha
direcionado e de phishing contra operadores. É um vazamento sutil, mas barato de corrigir e
exigido pela constituição.

**Teste Independente**: a prova automatizada, no nível do caso de uso, é que a verificação de senha
é executada nos três casos de falha (e no sucesso), e que o hash fictício usado quando o e-mail não
existe tem o mesmo algoritmo e o mesmo custo de um hash real. A comparação do tempo de resposta dos
três casos é uma medição feita na validação ao vivo e registrada no `quickstart.md`; não é teste
automatizado do CI, porque tempo de resposta é instável em máquina compartilhada.

**Cenários de Aceitação**:

1. **Dado** um e-mail que não existe, **Quando** o login é tentado, **Então** a resposta é `401`
   "Credenciais inválidas" e o mesmo trabalho de verificação de senha de um e-mail existente foi
   executado.
2. **Dado** uma conta inativa com a senha correta, **Quando** o login é tentado, **Então** a
   resposta é `401` "Credenciais inválidas", o mesmo trabalho de verificação foi executado e o
   token não é emitido.
3. **Dado** uma conta ativa com senha errada, **Quando** o login é tentado, **Então** a resposta é
   `401` "Credenciais inválidas".
4. **Dado** os três casos de falha, **Quando** o corpo da resposta é comparado, **Então** são
   idênticos (status, mensagem e notificações).
5. **Dado** uma conta ativa com a senha correta, **Quando** o login é tentado, **Então** o login
   continua funcionando como hoje.

---

### História de Usuário 5 - Tentativas de login são limitadas por origem e por conta (Prioridade: P2)

O login limita as tentativas por **endereço de origem** e também por **conta** (o e-mail dentro do
tenant: o mesmo e-mail em tenants diferentes são contas diferentes). Quando muitas tentativas
falham seguidas na mesma conta, a conta fica bloqueada por um tempo crescente, mesmo que o ataque
venha de muitos endereços diferentes. A origem usada no limite é a real: a Api só acredita nos
cabeçalhos de encaminhamento (`X-Forwarded-For`) quando eles vêm do proxy conhecido, então um
atacante não escapa do limite forjando esse cabeçalho.

**Por que esta prioridade**: limitar só por origem não contém um ataque distribuído (botnet) contra
uma conta, nem um atacante que forja o cabeçalho para parecer um endereço novo a cada tentativa.
Sem isso, senhas fracas caem por tentativa e erro.

**Teste Independente**: fazer sequências de falhas na mesma conta a partir de uma e de várias
origens e conferir o bloqueio; enviar `X-Forwarded-For` forjado de fora do proxy conhecido e
conferir que a origem usada é a real; conferir que contas de tenants diferentes com o mesmo
e-mail não se afetam.

**Cenários de Aceitação**:

1. **Dado** uma origem que excede o limite de tentativas de login por minuto, **Quando** ela tenta
   de novo, **Então** a resposta é `429` "Muitas tentativas. Tente novamente em instantes.", com o
   tempo de espera.
2. **Dado** uma conta com a quantidade máxima de falhas seguidas, **Quando** alguém tenta entrar
   nela de qualquer origem, **Então** a resposta é a mesma `429` (mesma mensagem e tempo de
   espera), mesmo que a senha enviada esteja correta.
3. **Dado** falhas de login contra a mesma conta vindas de muitas origens diferentes, **Quando** o
   total de falhas da conta chega ao máximo, **Então** a conta é bloqueada para todas as origens.
4. **Dado** que a conta está bloqueada, **Quando** o tempo de espera passa, **Então** o login volta
   a ser aceito, e se as falhas recomeçarem a espera seguinte é maior.
5. **Dado** uma conta com algumas falhas abaixo do máximo, **Quando** o login é feito com sucesso,
   **Então** a contagem de falhas da conta é zerada.
6. **Dado** o mesmo e-mail em dois tenants diferentes, **Quando** a conta de um é bloqueada,
   **Então** a do outro continua livre.
7. **Dado** falhas contra um e-mail que **não existe**, **Quando** o máximo é atingido, **Então** o
   bloqueio e a resposta são idênticos aos de um e-mail que existe (não revela quais e-mails
   existem).
8. **Dado** uma requisição de um endereço que não é o proxy conhecido, **Quando** ela traz
   `X-Forwarded-For` com outro endereço, **Então** a Api ignora o cabeçalho e aplica o limite ao
   endereço real da conexão.
9. **Dado** uma requisição vinda do proxy conhecido com `X-Forwarded-For` informando o endereço do
   cliente final, **Quando** o limite é aplicado, **Então** ele usa o endereço do cliente final.

---

### História de Usuário 6 - Portas de teste do ambiente local só na própria máquina (Prioridade: P3)

No ambiente local (docker compose), as portas das Apis (5290, 5291, 5292) e do banco (5433) só
aceitam conexões da própria máquina do desenvolvedor, não de outros computadores da rede. O
console web continua acessível como hoje, e o proxy continua falando com a Api do tenant A.

**Por que esta prioridade**: o ambiente local tem credenciais e dados sintéticos de desenvolvimento
(inclusive um administrador de senha conhecida) e Apis sem proxy na frente. Hoje qualquer
computador na mesma rede de um notebook de desenvolvedor, como o Wi-Fi de um café, as alcança. As
portas 5291 e 5292 existem justamente para testar os outros tenants, então não podem ser
removidas, só restringidas. Não há ambiente de produção hoje; a exposição em produção é da spec
05.

**Teste Independente**: subir o ambiente local e conferir que cada porta publicada (das Apis e do
banco) está ligada apenas ao endereço local, e que o console, o login e os testes dos três
tenants seguem funcionando a partir da própria máquina.

**Cenários de Aceitação**:

1. **Dado** o ambiente local no ar, **Quando** se lista as portas publicadas, **Então** as das
   Apis e a do banco estão associadas apenas ao endereço local da máquina.
2. **Dado** o ambiente local no ar, **Quando** o desenvolvedor chama as Apis dos três tenants de
   teste a partir da própria máquina, **Então** todas respondem como hoje.
3. **Dado** o ambiente local no ar, **Quando** o console web é aberto a partir da própria máquina,
   **Então** o login e o uso funcionam como hoje, passando pelo proxy.
4. **Dado** o ambiente local no ar, **Quando** outro computador da rede tenta chegar às portas
   das Apis ou do banco, **Então** a conexão não é aceita.

---

### História de Usuário 7 - Todo endpoint tem prova de autorização (Prioridade: P2)

Quem mantém o sistema tem uma suíte automatizada que, para **cada** endpoint da Api, prova os
quatro comportamentos de autorização. Endpoint novo sem esses testes é pego antes de entrar.

**Por que esta prioridade**: a constituição exige esses testes em todo endpoint (Princípio VII), e
parte deles não existe para a troca de senha e de perfil. Sem prova, as correções das histórias
acima podem regredir sem ninguém notar.

**Teste Independente**: executar a suíte e conferir que há um caso para cada combinação de
endpoint e cenário aplicável, e que ela falha se um endpoint deixa de exigir autenticação.

**Cenários de Aceitação**:

1. **Dado** cada endpoint da Api (exceto o login), **Quando** é chamado sem token, **Então** o
   teste confirma `401`.
2. **Dado** cada endpoint que exige perfil de administrador, **Quando** é chamado por um operador
   de perfil comum, **Então** o teste confirma `403`.
3. **Dado** cada endpoint (exceto o login), **Quando** é chamado com um token válido emitido para
   outro tenant, **Então** o teste confirma `401`.
4. **Dado** cada endpoint (exceto o login), **Quando** é chamado com um token emitido antes de uma
   troca de senha ou de perfil do operador, **Então** o teste confirma `401`.
5. **Dado** que um endpoint novo é adicionado à Api sem acrescentar caso à suíte, **Quando** a
   suíte roda, **Então** ela falha apontando o endpoint sem cobertura.
6. **Dado** o login, **Quando** a suíte roda, **Então** há testes de que ele é anônimo, de que
   ignora token enviado e de que aplica os limites da História 5.

---

### Casos de Borda

- **Token emitido no mesmo instante da troca de senha**: o token antigo cai e o token feito no
  login seguinte vale; não existe janela em que o token antigo ainda passe depois da resposta de
  sucesso da troca.
- **Duas trocas seguidas** (senha e depois perfil): todo token anterior à última troca é
  recusado, não só os anteriores à primeira.
- **Troca de e-mail**: não derruba o token (a identidade do operador é o identificador, não o
  e-mail), mas a conta de login nova começa sem histórico de falhas.
- **Edição sem mudança efetiva**: salvar um operador reenviando o mesmo perfil e deixando a senha
  em branco (não informada) não invalida a sessão. Perfil diferente do atual invalida; senha
  informada (não nula) invalida sempre, mesmo igual à atual.
- **Reativação**: reativar um operador não revalida tokens emitidos antes da desativação (a
  desativação já mudou a marca de validade da sessão); só um login novo depois da reativação emite
  token válido.
- **Registro de falhas antigo**: o registro de uma conta é descartado quando passam 15 minutos
  desde a última falha ou desde o fim do último bloqueio; na falha seguinte a contagem e a espera
  recomeçam do zero (1 minuto). Isso mantém limitada a memória usada pelo limite por conta, mesmo
  com muitas tentativas contra e-mails que não existem.
- **Maiúsculas e minúsculas no e-mail**: `Ana@x.com` e `ana@x.com` são a mesma conta para o limite;
  o atacante não escapa do bloqueio variando a caixa.
- **Bloqueio da conta como ataque de negação**: um atacante pode bloquear a conta de um operador
  errando a senha de propósito. A espera crescente tem teto, e o operador legítimo volta a entrar
  quando ela passa; um administrador de outra conta não é afetado. Aceito como compromisso
  conhecido, mais seguro do que não limitar.
- **Falha da própria contagem**: se a contagem de tentativas não puder ser consultada, o login
  NÃO é liberado sem limite; a regra é falhar fechado.
- **Várias instâncias da Api**: nesta spec, as contagens ficam na memória de cada instância. Com
  mais de uma instância o limite efetivo é por instância, o que é uma violação temporária do
  Princípio XIII, registrada e resolvida pela spec 06.
- **Reinício da Api**: as contagens da memória se perdem; o limite recomeça do zero (consequência da
  mesma violação temporária).
- **Cabeçalho de encaminhamento com vários endereços**: só o último salto, vindo do proxy
  conhecido, é considerado; endereços adicionais forjados à esquerda são ignorados.
- **Endereço de origem ausente ou IPv6**: o limite por origem continua aplicado (agrupado de forma
  estável), nunca desligado.
- **Token enviado ao login**: continua ignorado e nunca barra o login (comportamento atual).
- **Rota inexistente**: responde `404`, não `401`; só rotas que existem são protegidas.
- **Operador autenticado tenta acessar o Swagger fora de Development**: `404` do mesmo jeito.
- **Tamanho da chave de assinatura**: o mínimo de 32 bytes é medido nos bytes UTF-8 da chave como ela é
  usada para assinar (a Api não decodifica base64), e não em número de caracteres: uma chave com
  caracteres acentuados ocupa mais bytes que caracteres, e o que conta é o tamanho em bytes.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Rotas fechadas por padrão**

- **FR-001**: Toda rota da Api DEVE exigir usuário autenticado por padrão, sem que cada endpoint
  precise declarar isso. A única rota anônima é o login.
- **FR-002**: Um endpoint declarado sem nenhuma regra de acesso DEVE responder `401` a quem não
  tem token.
- **FR-003**: A documentação interativa da Api e o arquivo que a descreve DEVEM existir somente no
  ambiente de desenvolvimento; nos demais, responder `404`.

**Invalidação imediata do token**

- **FR-004**: O token de um operador DEVE ser recusado (`401`) na primeira requisição depois de a
  senha dele ser trocada, seja pelo próprio operador, seja por um administrador. Senha informada
  (não nula) conta sempre como troca, mesmo que seja igual à atual; só a senha em branco (não
  informada) não conta.
- **FR-005**: O token de um operador DEVE ser recusado (`401`) na primeira requisição depois de o
  perfil de acesso dele ser alterado para um perfil diferente do atual.
- **FR-006**: O token de um operador DEVE continuar sendo recusado (`401`) depois de ele ser
  desativado, e isso DEVE ter teste automatizado. Desativar o operador DEVE mudar a marca de
  validade da sessão dele, de modo que reativá-lo NÃO revalida tokens emitidos antes da
  desativação; só um login novo depois da reativação emite token válido.
- **FR-007**: Alterar apenas o e-mail do operador, reenviar o mesmo perfil que ele já tem ou deixar
  a senha em branco (não informada) NÃO DEVE derrubar o token dele; a troca de senha ou de perfil
  de um operador NÃO DEVE afetar tokens de outros operadores.
- **FR-008**: A recusa DEVE valer para tokens emitidos antes da troca e continuar valendo ainda
  que o token esteja dentro da validade; um novo login depois da troca DEVE emitir token válido.
- **FR-009**: Quando o operador troca a própria senha pelo console web, o console DEVE encerrar a
  sessão e levar à tela de login com o aviso "Senha alterada. Faça login novamente.".

**Validação do token e da chave**

- **FR-010**: A Api DEVE aceitar um token somente se ele foi emitido por este sistema (emissor
  esperado) e destinado a esta aplicação (destinatário esperado); token sem emissor, sem
  destinatário ou com valor diferente DEVE receber `401`.
- **FR-011**: A Api DEVE aplicar uma tolerância de relógio curta na validade do token (ver
  Premissas), e token vencido além dela DEVE receber `401`.
- **FR-012**: A Api NÃO DEVE iniciar se a chave de assinatura estiver ausente, vazia ou menor que
  256 bits (32 bytes, medidos nos bytes UTF-8 da chave, exatamente como ela é usada para assinar; a
  Api não decodifica base64), nem se o emissor ou o destinatário
  esperados não estiverem configurados. A mensagem de falha DEVE dizer qual configuração está
  errada, sem exibir o valor da chave.
- **FR-013**: Todo token emitido DEVE carregar o emissor e o destinatário esperados pela Api.

**Login em tempo uniforme**

- **FR-014**: O login DEVE executar o mesmo trabalho de verificação de senha quando o e-mail não
  existe, quando a conta está inativa e quando a senha está errada, de modo que o tempo de
  resposta não distinga os três casos. Quando o e-mail não existe, a verificação DEVE ser feita
  contra um hash fictício com o mesmo algoritmo e o mesmo custo de um hash real.
- **FR-015**: O login DEVE responder `401` "Credenciais inválidas", com o mesmo corpo, nos três
  casos de falha. Conta inativa NUNCA recebe token, mesmo com a senha correta.

**Limite de tentativas**

- **FR-016**: O login DEVE limitar as tentativas por endereço de origem, respondendo `429`
  "Muitas tentativas. Tente novamente em instantes." com o tempo de espera.
- **FR-017**: O login DEVE limitar as **falhas** por conta, sendo a conta o par tenant + e-mail
  (o e-mail comparado sem diferença de maiúsculas e minúsculas). O mesmo e-mail em tenants
  diferentes DEVE ter contagens independentes.
- **FR-018**: O limite por conta DEVE valer para todas as origens somadas, de modo a conter um
  ataque distribuído contra a mesma conta.
- **FR-019**: Bloqueio por conta DEVE usar a mesma resposta `429` (mesma mensagem, mesmo formato de
  tempo de espera) do limite por origem e DEVE valer também para a senha correta enquanto durar.
- **FR-020**: A contagem DEVE incluir falhas contra e-mails que não existem e se comportar de
  forma idêntica, para que o bloqueio não revele quais e-mails estão cadastrados.
- **FR-021**: A conta DEVE ser bloqueada ao atingir 5 falhas seguidas. A espera DEVE ser de 1
  minuto no primeiro bloqueio e dobrar a cada novo bloqueio (1, 2, 4, 8 minutos), com teto de 15
  minutos; a contagem DEVE ser zerada por um login bem-sucedido.
- **FR-022**: Se a contagem de tentativas não puder ser consultada, o login NÃO DEVE ser liberado
  sem limite.
- **FR-032**: O registro de falhas de uma conta DEVE ser descartado quando passam 15 minutos (o
  teto da espera) desde a última falha ou desde o fim do último bloqueio, e a espera DEVE recomeçar
  em 1 minuto na falha seguinte. Assim a memória usada pelo limite por conta fica limitada mesmo
  com muitas tentativas contra e-mails que não existem. O mecanismo de descarte e o tamanho máximo
  do registro são definidos no plano.

**Origem confiável**

- **FR-023**: A Api DEVE considerar os cabeçalhos de encaminhamento (`X-Forwarded-For` e
  `X-Forwarded-Proto`) somente quando a conexão vier de um proxy conhecido, definido por
  configuração. De qualquer outra origem, DEVE ignorá-los e usar o endereço real da conexão.
- **FR-024**: A lista de proxies conhecidos NÃO DEVE ser esvaziada em tempo de execução nem ficar
  em branco "para funcionar": sem configuração válida, a Api não deve confiar em nenhum proxy
  além do loopback (padrão do framework) e, portanto, em nenhum cabeçalho de encaminhamento
  vindo de outra origem.
- **FR-025**: O limite por origem e o limite por conta DEVEM usar o endereço resolvido conforme
  FR-023.

**Ambiente local**

- **FR-026**: No ambiente local (docker compose), as portas publicadas das Apis (tenant A, tenant B
  e tenant dedicado) e do banco DEVEM aceitar conexões apenas da própria máquina.
- **FR-027**: O acesso do console web ao tenant A pelo proxy e os testes manuais dos outros dois
  tenants pelas portas próprias DEVEM continuar funcionando a partir da própria máquina.
- **FR-028**: A documentação do ambiente local (README e `.env.example`, se citarem as portas ou
  novas variáveis) DEVE ser atualizada no mesmo trabalho.

**Testes de autorização**

- **FR-029**: Para cada endpoint da Api, exceto o login, DEVE existir teste automatizado de: `401`
  sem token; `403` com perfil sem permissão (onde o endpoint exige perfil); `401` com token de
  outro tenant; `401` com token emitido antes de uma troca de senha; e `401` com token emitido
  antes de uma troca de perfil.
- **FR-030**: A suíte DEVE falhar quando um endpoint da Api não tiver cobertura nos cenários do
  FR-029.
- **FR-031**: Os testes de autorização e de isolamento que já existem DEVEM continuar passando sem
  serem enfraquecidos; qualquer ajuste de fixture (por exemplo, chave de teste mais longa) DEVE
  ser declarado no `tasks.md`.

### Entidades Principais

*Glossário (spec ↔ plano e código)*: **marca de validade da sessão** = `SecurityVersion` (coluna `security_version`
e claim do token); **limite por origem** = limite por IP; **falha** do bloqueio por conta = tentativa de
login admitida e contada como falha provisória antes da verificação da senha, zerada pelo login com
sucesso (ver Premissas); **perfil de acesso** = `Role`; **proxy conhecido** = `KnownProxies`/`KnownIPNetworks`.

- **Operador**: pessoa que opera o console. Tem e-mail, senha, perfil de acesso e situação
  (ativo/inativo). Passa a ter uma **marca de validade da sessão**, que muda toda vez que a senha,
  o perfil ou a situação mudam, e que decide se um token antigo ainda vale.
- **Token de acesso**: comprovante de login, com validade, emissor, destinatário, tenant do
  operador e a marca de validade da sessão do momento em que foi emitido.
- **Conta de login**: identificação de quem tenta entrar para fins de limite, formada pelo tenant e
  pelo e-mail normalizado, existindo ou não um operador com esse e-mail.
- **Registro de falhas da conta**: contagem de falhas seguidas, instante da última falha e até
  quando a conta está bloqueada, por conta de login. Dura em memória nesta spec e é descartado
  depois de 15 minutos sem falha nem bloqueio (FR-032).
- **Origem da requisição**: endereço de quem chama, resolvido só a partir do proxy conhecido ou da
  conexão real.
- **Proxy conhecido**: o ponto de entrada autorizado a informar o endereço do cliente final.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% das rotas da Api, exceto o login, respondem `401` sem token, comprovado por um
  teste que percorre todas as rotas existentes.
- **SC-002**: Em 100% dos casos testados de troca de senha, troca de perfil e desativação, o token
  emitido antes é recusado já na primeira chamada seguinte (zero requisições aceitas depois da
  troca).
- **SC-003**: Em 100% dos casos testados, token com emissor ou destinatário errados, ausentes ou
  vencido além da tolerância é recusado, e a Api não inicia com chave ausente, curta ou sem
  emissor/destinatário configurados.
- **SC-004**: A verificação de senha é executada nos três casos de falha do login (e-mail
  inexistente, conta inativa, senha errada) e no sucesso, o hash fictício usado quando o e-mail não
  existe tem o mesmo algoritmo e o mesmo custo de um hash real, e o corpo da resposta é idêntico
  nos três casos de falha (provas automatizadas). Na validação ao vivo, a mediana do tempo de
  resposta dos três casos de falha difere em no máximo 10% entre os casos, e o resultado é
  registrado no `quickstart.md` (medição manual, fora do CI).
- **SC-005**: Depois de 5 falhas seguidas na mesma conta, a 6ª tentativa, de qualquer origem,
  recebe `429`; o mesmo vale para uma conta cujo e-mail não existe. A espera é de 1 minuto no
  primeiro bloqueio e dobra a cada novo bloqueio, sem passar de 15 minutos.
- **SC-006**: Em 100% dos testes com `X-Forwarded-For` forjado a partir de uma origem que não é o
  proxy conhecido, o limite é aplicado ao endereço real e não ao informado no cabeçalho.
- **SC-007**: Nenhuma porta de Api ou de banco do ambiente local fica publicada fora do endereço
  local da máquina, e os três tenants de teste e o console continuam utilizáveis a partir dela.
- **SC-008**: 100% dos endpoints da Api têm cobertura dos cenários de autorização do FR-029, e a
  suíte falha ao se acrescentar um endpoint sem cobertura.
- **SC-009**: Fora do que esta spec muda de propósito, todos os testes que existem hoje (backend e
  frontend) continuam passando, e nenhum teste é apagado, pulado ou enfraquecido.
- **SC-010**: Um operador legítimo cuja senha foi trocada consegue voltar a trabalhar em menos de
  1 minuto: o console o leva ao login com o aviso e um novo login com a senha nova funciona de
  primeira.

## Premissas

- **Valores adotados**: limite por origem mantido como hoje (**20 tentativas por minuto**);
  tolerância de relógio do token de **30 segundos**; validade do token mantida em **8 horas**;
  chave de assinatura com pelo menos **32 bytes** (bytes UTF-8 do texto, como é usada para assinar; a
  Api não decodifica base64). As regras do bloqueio por
  conta (5 falhas, espera de 1 a 15 minutos) estão no FR-021.
- O bloqueio por conta conta tentativas de login como **falhas provisórias**, no momento em que a
  tentativa é admitida e antes de a senha ser verificada (para que uma rajada simultânea não passe
  pelo limite); um login bem-sucedido zera a contagem. Requisição recusada antes (`400` de formato, tenant
  padrão inválido) e requisição já barrada pelo bloqueio não contam.
- A troca de e-mail não derruba o token: a identidade do operador é o identificador, e só senha,
  perfil e desativação invalidam a sessão (clarificação de 2026-11-01).
- No console, depois de trocar a própria senha, a Api não devolve token novo: a pessoa entra de
  novo (alternativa descartada por mudar o contrato da Api; clarificação de 2026-11-01).
- O login hoje resolve o tenant por um tenant padrão configurado em cada Api; por isso a "conta"
  desta spec é o par tenant + e-mail. A escolha de tenant no login por diretório é da spec 09 e
  não muda a regra do limite.
- "Perfil sem permissão" significa, hoje, o perfil comum de operador chamando endpoint de
  administrador. As permissões granulares e o fim da autorização por nome de perfil são da spec
  08; os testes desta spec são reescritos naquela spec sem perder cobertura.
- O proxy conhecido do ambiente local é o servidor do console web (nginx) na rede interna do
  compose. Em ambientes futuros, o proxy conhecido vem de configuração e é definido pela spec 05.
- As contagens do limite por conta ficam na memória da instância da Api. Isso viola o Princípio XIII
  (estado compartilhável entre instâncias deve ficar no Redis) e é uma **violação temporária**:
  vai para Complexity Tracking do `plan.md` e é resolvida pela spec 06, que troca o armazenamento
  sem mudar a regra.
- O gate de carga do Princípio XIII (cenário k6) não pode ser entregue aqui, porque o k6 só passa a
  existir na spec 06; o login e o pipeline de autenticação mudam aqui, e a linha de base da spec 06
  cobre esses caminhos. Também vai para Complexity Tracking.
- A mudança de que a marca de validade da sessão passe a existir no operador exige uma alteração
  de banco compatível com a versão anterior da aplicação (acrescenta antes de usar); a forma
  exata é decidida no plano.
- Este trabalho resolve os itens do "Legado conhecido" da constituição relativos a
  `AddForwardedHeaders`, `AddJwtAuthentication`, `Authenticate/Handler.cs`, ao limite só por IP de
  `AddRateLimiting` (a parte "memória da instância" continua) e ao Swagger em qualquer ambiente
  no `Program.cs`; a constituição é atualizada por emenda própria, não por esta spec.

## Fora do Escopo

- Cabeçalhos de segurança, CSP e HSTS no nginx, tamanho máximo dos campos de texto, teste de que o
  usuário de banco da aplicação não tem DDL e verificação de pacotes vulneráveis no CI: spec 04b.
- Permissões granulares, roles dinâmicas e o fim de `[Authorize(Roles = "Admin")]`: spec 08.
- Trilha de auditoria de login e de troca de senha ou perfil: spec 07.
- Rate limit, cache e contagens em Redis, e limite por tenant: spec 06.
- Exposição, TLS e proxy de produção e homologação: spec 05.
- Login por diretório de e-mails entre vários tenants: spec 09.
- Endpoints e autenticação de aparelhos (ControlID): specs de aparelhos.
