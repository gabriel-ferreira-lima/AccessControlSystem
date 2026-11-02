# Especificação da Feature: Segurança do perímetro (console web e entrada de dados da Api)

**Branch da Feature**: `20261102-125831-security-perimeter`

**Criada em**: 2026-11-02

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Fechar o perímetro do sistema: o console web e a entrada de dados da Api. Contexto: o sistema controla acesso físico a locais de clientes. Esta é a segunda spec de segurança; a autenticação e a sessão já foram tratadas na spec anterior (04). Requisitos: (1) o console web é servido com cabeçalhos de segurança que impedem carregamento em iframe e execução de scripts de terceiros, sem quebrar o tema claro/escuro (que hoje é aplicado por um script inline antes do render); (2) quando o console é acessado por HTTPS (atrás do load balancer, a partir da spec 05), o navegador é instruído a usar sempre HTTPS, e no ambiente local, que não tem TLS, nada quebra; (3) todo campo de texto recebido pela Api tem tamanho máximo, e entradas maiores retornam 400 \"Requisição inválida\", com as notificações; (4) o usuário de banco usado pela aplicação não consegue alterar a estrutura das tabelas nem contornar o isolamento por tenant (RLS), e isso é provado por teste; (5) o CI falha quando há pacote com vulnerabilidade conhecida (backend e frontend), exceto exceções justificadas e explícitas, e as vulnerabilidades que existem hoje são corrigidas nesta spec, para o CI nascer verde."

## Clarificações

### Sessão 2026-11-02

- Q: O console deve aceitar a indicação de acesso seguro somente do load balancer? → A: Não. O
  console emite a instrução de HTTPS obrigatório quando `X-Forwarded-Proto` = `https`, sem
  verificar a origem da indicação. Motivos (viram premissa): o navegador ignora a instrução
  recebida por HTTP (RFC 6797 §8.1); um cliente que forje a indicação só afeta o próprio
  navegador; o load balancer sobrescreve o `X-Forwarded-Proto`; e, em produção, o console só é
  alcançável pelo load balancer (spec 05).
- Q: Qual o critério de SC-003 (senha de 1 MB no login)? → A: Sem critério de tempo. A senha é
  recusada com `400` sem que o cálculo de hash seja chamado e sem consulta ao banco, provado por
  teste que confere que nenhum dos dois foi chamado.
- Q: Em quais bases cada prova do usuário de banco da aplicação vale? → A: DDL negado, atributos
  perigosos e não ser dono das tabelas valem nas três bases (catálogo, compartilhada e dedicada).
  A prova de RLS vale na compartilhada e na dedicada. No catálogo, prova-se que o usuário da
  aplicação só lê (INSERT, UPDATE e DELETE negados).
- Q: A verificação de vulnerabilidades deve falhar sozinha quando uma exceção não tem
  justificativa? → A: Não. A exceção é por aviso (pacote + aviso exato + escopo), com a
  justificativa escrita ao lado, e a presença e a qualidade da justificativa são conferidas na
  revisão do PR. O CI não valida a justificativa, e não existe regra de validade/vencimento da
  exceção.

## Cenários de Usuário e Testes *(obrigatório)*

O sistema controla portas e catracas de clientes. A spec anterior (04) fechou a autenticação e a
sessão; esta fecha o que está **em volta** delas: o que o navegador do operador aceita carregar e
executar, o que a Api aceita receber, o que o banco deixa a aplicação fazer se ela for
comprometida e o que entra no código por dependências de terceiros. Os públicos beneficiados são:
o **operador**, cujo navegador não pode ser usado contra ele (página embutida em site malicioso,
script injetado); o **cliente** (tenant), cujos dados e locais não podem ser expostos por uma
falha de terceiros ou por uma aplicação comprometida; e quem **mantém** o sistema, que precisa de
prova automatizada, a cada mudança, de que o perímetro continua fechado.

### História de Usuário 1 - Entrada de dados com tamanho limitado (Prioridade: P1)

Todo campo de texto que a Api recebe (e-mail, senha e qualquer outro texto de qualquer requisição)
tem um tamanho máximo. Uma entrada acima do limite é recusada logo na validação, com `400` e a
mensagem "Requisição inválida", acompanhada das notificações que dizem qual campo excedeu e qual é
o limite. A recusa acontece **antes** de qualquer trabalho caro (verificação de formato, cálculo de
hash de senha, consulta ao banco).

**Por que esta prioridade**: hoje nada impede enviar uma senha ou um e-mail de megabytes. Isso
permite negar o serviço para todos os tenants (cada tentativa de login faz um cálculo de hash cujo
custo cresce com o tamanho da senha) e encher o banco com lixo. É a falha mais barata de explorar
e a que atinge todos os clientes de uma vez.

**Teste Independente**: enviar, a cada endpoint da Api que recebe texto, um corpo com um campo
exatamente no limite (aceito) e outro com um caractere a mais (recusado com `400`), e conferir que
o banco não foi consultado nos casos recusados.

**Cenários de Aceitação**:

1. **Dado** uma requisição de login com senha acima do limite, **Quando** ela chega à Api,
   **Então** a resposta é `400` com a mensagem "Requisição inválida" e uma notificação do campo de
   senha, sem que nenhum hash seja calculado e sem consulta ao banco.
2. **Dado** uma requisição de criação ou atualização de operador com e-mail acima do limite,
   **Quando** ela chega à Api, **Então** a resposta é `400` "Requisição inválida" com a
   notificação do campo de e-mail, e nada é gravado.
3. **Dado** um campo de texto com exatamente o tamanho máximo e formato válido, **Quando** a
   requisição é enviada, **Então** ela é processada normalmente (o limite é inclusivo).
4. **Dado** uma requisição que excede o limite em mais de um campo ao mesmo tempo, **Quando** ela
   é validada, **Então** a resposta traz uma notificação para cada campo excedido.
5. **Dado** que um desenvolvedor acrescenta um campo de texto a uma requisição e não define o
   limite dele, **Quando** a suíte de testes roda, **Então** um teste automatizado falha apontando
   o campo sem limite.

---

### História de Usuário 2 - Console web com cabeçalhos de segurança (Prioridade: P1)

O console web é entregue ao navegador com cabeçalhos de segurança que: (a) proíbem que qualquer
outro site o carregue dentro de um quadro (iframe), o que impede ataques de clique disfarçado;
(b) só permitem executar scripts do próprio console, de modo que um script de terceiro injetado
na página não roda; (c) impedem o navegador de adivinhar o tipo dos arquivos; (d) não vazam o
endereço do console para outros sites ao seguir um link; e (e) desligam recursos do navegador que
o console não usa. O tema claro/escuro continua funcionando sem flash de cor e sem erros no
console do navegador, mesmo com o script que o aplica antes da renderização.

**Por que esta prioridade**: o console é a porta de entrada dos operadores; uma falha de script
injetado ou de página embutida em outro site permite agir em nome do operador logado. Hoje o
servidor do console não envia nenhum desses cabeçalhos.

**Teste Independente**: subir o console, requisitar a página inicial, um arquivo estático e uma
rota inexistente e conferir os cabeçalhos em todas as respostas; abrir o console no navegador nos
temas claro e escuro e conferir que o tema é aplicado e que o console do navegador não mostra
nenhum bloqueio de segurança.

**Cenários de Aceitação**:

1. **Dado** o console no ar, **Quando** qualquer resposta dele é requisitada (página, arquivo
   estático, rota inexistente, resposta de erro), **Então** ela traz os cabeçalhos de segurança
   definidos nesta spec.
2. **Dado** uma página de outro site que tenta carregar o console num quadro, **Quando** o
   navegador a abre, **Então** o console não é exibido.
3. **Dado** um script de origem externa (ou escrito direto na página) que não faz parte do
   console, **Quando** a página tenta executá-lo, **Então** o navegador o bloqueia.
4. **Dado** um operador com preferência de tema escuro (guardada ou do sistema), **Quando** abre
   o console, **Então** o tema escuro é aplicado antes de a tela aparecer, sem flash de tema claro
   e sem erro de política de segurança no console do navegador.
5. **Dado** que o script do tema é alterado por um desenvolvedor, **Quando** a suíte de testes
   roda, **Então** um teste automatizado falha se a liberação dele na política de segurança não
   corresponder mais ao conteúdo novo, em vez de o tema quebrar só em produção.
6. **Dado** o console funcionando normalmente (login, navegação, chamadas à Api), **Quando** o
   operador o usa, **Então** nenhuma funcionalidade existente é bloqueada pelos cabeçalhos.

---

### História de Usuário 3 - Navegador obrigado a usar HTTPS quando o acesso é HTTPS (Prioridade: P2)

Quando o console é acessado por HTTPS (a partir da spec 05, o acesso chega por um load balancer que
termina o TLS e informa ao console que o acesso original foi seguro), a resposta instrui o
navegador a usar sempre HTTPS com aquele endereço pelo período de, no mínimo, um ano. O console
emite a instrução quando o protocolo original informado (`X-Forwarded-Proto`) é HTTPS. Quando o
acesso é por HTTP simples, como no ambiente local, a instrução **não** é enviada, para não
prender o navegador do desenvolvedor num endereço que não tem TLS.

**Por que esta prioridade**: protege o operador de ataque de rebaixamento para HTTP em redes não
confiáveis, mas só faz sentido quando existir HTTPS (spec 05). Por isso vem depois dos cabeçalhos
e do limite de tamanho, que valem desde já.

**Teste Independente**: requisitar o console com e sem a indicação de acesso seguro vinda do
load balancer e conferir a presença e a ausência da instrução.

**Cenários de Aceitação**:

1. **Dado** uma requisição que o load balancer marca como originada de HTTPS, **Quando** o
   console responde, **Então** a resposta traz a instrução de HTTPS obrigatório com validade de
   pelo menos um ano.
2. **Dado** uma requisição por HTTP simples (ambiente local), **Quando** o console responde,
   **Então** a resposta não traz a instrução, e o console funciona normalmente em
   `http://localhost`.

---

### História de Usuário 4 - Prova de que a aplicação não altera a estrutura do banco nem fura o RLS (Prioridade: P2)

O usuário de banco que a aplicação usa em produção só pode ler e gravar dados; não pode criar,
alterar nem apagar tabelas, índices ou políticas, e não consegue enxergar ou alterar dados de
outro tenant, nem escolhendo outro tenant, nem desligando o isolamento, nem trocando de usuário.
Cada uma dessas garantias é verificada por teste automatizado contra um banco real, com o usuário
da aplicação.

**Por que esta prioridade**: se a Api for comprometida (por exemplo, por uma falha de injeção que
ainda não existe), o banco é a última barreira. Já existe uma separação de usuários (aplicação
sem DDL, migrations por usuário próprio) e testes parciais dela; falta a prova completa e
verificada a cada mudança, para que uma permissão a mais dada por engano seja pega pelo CI.

**Teste Independente**: rodar a suíte de integração conectando como o usuário da aplicação e
tentar cada operação proibida; todas devem falhar com permissão negada, e a leitura de dados de
outro tenant deve devolver zero linhas.

**Cenários de Aceitação**:

1. **Dado** uma conexão com o usuário da aplicação, **Quando** ele tenta criar, alterar ou
   apagar uma tabela, índice, coluna ou política de segurança, **Então** o banco nega por falta de
   permissão em todos os casos.
2. **Dado** uma conexão com o usuário da aplicação, **Quando** ele tenta desligar o isolamento
   por tenant de uma tabela, esvaziá-la por completo, apagar linhas fisicamente ou mudar os
   próprios atributos (ex.: ganhar o poder de ignorar o isolamento), **Então** o banco nega.
3. **Dado** uma conexão com o usuário da aplicação, **Quando** ele tenta assumir a identidade do
   usuário das migrations, **Então** o banco nega.
4. **Dado** dados de dois tenants na mesma base (compartilhada e dedicada), **Quando** o usuário
   da aplicação, com o tenant A informado, lê a tabela inteira sem nenhum filtro, **Então** só as
   linhas do tenant A aparecem; sem nenhum tenant informado, nenhuma linha aparece.
5. **Dado** o tenant A informado, **Quando** o usuário da aplicação tenta gravar ou alterar uma
   linha com o identificador do tenant B (base compartilhada e dedicada), **Então** o banco
   recusa.
6. **Dado** que alguém, por engano, concede ao usuário da aplicação uma permissão a mais (ex.:
   DDL, ou o atributo de ignorar o isolamento), **Quando** a suíte de integração roda, **Então**
   ao menos um teste falha.
7. **Dado** as três bases do ambiente (catálogo, base compartilhada e base dedicada), **Quando**
   a suíte roda, **Então**: DDL negado, atributos perigosos ausentes e o usuário da aplicação não
   ser dono das tabelas são verificados nas três; o isolamento por tenant (RLS) é verificado na
   compartilhada e na dedicada; e, no catálogo, INSERT, UPDATE e DELETE do usuário da aplicação
   são negados (só leitura).

---

### História de Usuário 5 - CI barra pacotes com vulnerabilidade conhecida (Prioridade: P2)

A verificação automática de cada mudança (backend e frontend) falha quando o projeto depende de um
pacote com vulnerabilidade conhecida, direta ou indireta. Uma exceção só vale se estiver escrita
de forma explícita no repositório, por aviso (qual pacote, qual aviso de vulnerabilidade e qual
escopo), com a justificativa escrita ao lado, conferida na revisão do PR; uma exceção não cobre
outros avisos do mesmo pacote nem outros pacotes. As
vulnerabilidades que existem hoje são corrigidas nesta spec, para que o CI comece verde sem
esconder nada.

**Por que esta prioridade**: sem isso, uma dependência vulnerável entra e ninguém vê. Hoje o
frontend tem 2 vulnerabilidades de severidade alta (na ferramenta de build e numa biblioteca
indireta) e o backend tem 1 aviso indireto, de um pacote que só o projeto de testes usa. Vem
depois dos itens anteriores porque eles fecham brechas diretas na aplicação, mas é o que impede a
volta de brechas futuras.

**Teste Independente**: rodar a verificação em uma cópia limpa do repositório e conferir que ela
passa; acrescentar um pacote com vulnerabilidade conhecida numa mudança de teste (descartável) e
conferir que o CI do backend, ou do frontend, fica vermelho.

**Cenários de Aceitação**:

1. **Dado** o estado do repositório ao final desta spec, **Quando** o CI roda no backend e no
   frontend, **Então** as verificações de vulnerabilidade passam e os demais passos
   (compilação, testes) continuam verdes.
2. **Dado** uma mudança que acrescenta ao frontend um pacote com vulnerabilidade conhecida,
   **Quando** o CI roda, **Então** a verificação do frontend falha e o PR não pode ser integrado.
3. **Dado** uma mudança que acrescenta ao backend um pacote com vulnerabilidade conhecida (direta
   ou indireta), **Quando** o CI roda, **Então** a verificação do backend falha.
4. **Dado** a exceção já existente (pacote indireto do projeto de testes, nunca usado em
   produção), **Quando** o CI roda, **Então** ela é tratada como exceção explícita e justificada,
   e qualquer **outro** aviso, mesmo no mesmo pacote ou no mesmo projeto, ainda falha.
5. **Dado** as correções de pacotes desta spec, **Quando** o console é compilado e testado,
   **Então** a verificação de tipos, os testes e o build do frontend continuam verdes e o console
   se comporta como antes.

---

### Casos de Borda

- **Texto com caracteres especiais**: o tamanho máximo conta caracteres, não bytes; um e-mail
  com acentos, ou texto com emojis, não pode ser recusado nem aceito por contagem errada.
- **Espaços nas pontas**: o login já normaliza o e-mail (remove espaços nas pontas) antes de
  validá-lo; o limite é aplicado ao texto como recebido, para que espaços em excesso não driblem
  o limite.
- **Campo ausente ou nulo**: campos opcionais ausentes continuam ausentes; o limite só se aplica
  a texto presente.
- **Limite e formato juntos**: um texto acima do limite e também com formato inválido recebe a
  notificação de tamanho, e o formato nem precisa ser avaliado.
- **Requisição inteira enorme**: um corpo gigante (por exemplo, dezenas de megabytes) é um
  problema diferente do tamanho de um campo; o limite de corpo da requisição não faz parte desta
  spec (ver Premissas).
- **Cabeçalhos em respostas de erro**: erros (404, 500, página não encontrada) do console também
  levam os cabeçalhos; uma rota de arquivo estático não pode "perder" cabeçalhos por ter regra
  própria de cache.
- **Respostas da Api passando pelo console**: o console encaminha as chamadas da Api; essas
  respostas também recebem os cabeçalhos de segurança, sem duplicá-los nem quebrar o que a Api já
  envia.
- **Navegador sem suporte a um cabeçalho**: cabeçalhos redundantes (um moderno e um legado para
  a mesma proteção) devem coexistir sem conflito.
- **Instrução de HTTPS e ambiente local**: o navegador de quem já acessou o ambiente de
  homologação/produção por HTTPS não pode ficar com a instrução aplicada a `localhost`, porque
  ela vale só para o endereço que a enviou.
- **Falha de rede na consulta de vulnerabilidades**: se a base de avisos estiver inacessível, a
  verificação do CI deve falhar (ou ser repetida), e não passar em silêncio como "sem
  vulnerabilidades".
- **Atualização de pacote que muda o comportamento**: subir a versão da ferramenta de build do
  console não pode alterar o que o console faz nem exigir afrouxar um teste.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Tamanho máximo na entrada da Api**

- **FR-001**: Todo campo de texto de toda requisição recebida pela Api DEVE ter um tamanho
  máximo, em caracteres, definido e validado na especificação (validação de formato) do caso de
  uso correspondente.
- **FR-002**: Uma requisição com qualquer campo de texto acima do seu máximo DEVE ser recusada com
  status `400`, mensagem "Requisição inválida" e as notificações no mesmo formato das demais
  recusas de validação, com uma notificação por campo excedido, informando o campo e o limite.
- **FR-003**: A verificação de tamanho DEVE acontecer antes de qualquer verificação de formato,
  cálculo de hash de senha ou acesso ao banco, e uma requisição recusada por tamanho NÃO DEVE
  causar nenhum desses trabalhos.
- **FR-004**: O limite de e-mail DEVE ser 255 caracteres (o mesmo da coluna do banco) e o de senha
  DEVE ser 128 caracteres, em todos os casos de uso que os recebem: login, criação de operador,
  atualização de operador e atualização do próprio cadastro. Os demais campos de texto
  (nome, telefone, RG, CPF, PIN, observação) têm os limites definidos nas Premissas.
- **FR-005**: O limite DEVE ser inclusivo (texto com exatamente o máximo é aceito, desde que o
  restante seja válido) e DEVE contar caracteres, não bytes.
- **FR-006**: Um texto que respeitava o formato antes desta spec e tem tamanho dentro do limite
  continua válido: o comportamento de quem já usa o sistema com dados normais NÃO DEVE mudar.
- **FR-007**: DEVE existir um teste automatizado que percorra todas as requisições da aplicação e
  falhe se encontrar um campo de texto sem limite de tamanho definido, para que campos futuros
  nasçam com o limite (a constituição exige isso para toda string de requisição).
- **FR-008**: O texto das notificações e da mensagem de tamanho DEVE estar em português do Brasil
  com acentuação correta.

**Cabeçalhos de segurança do console**

- **FR-009**: Toda resposta do console (página inicial, arquivos estáticos, rotas de navegação
  do próprio console, respostas de erro e respostas da Api que passam por ele) DEVE trazer uma
  política de segurança de conteúdo que: só permite scripts do próprio console; proíbe que o
  console seja carregado em quadro por qualquer outro site; restringe base, formulários, imagens,
  estilos e conexões ao próprio console (imagens incorporadas em texto também são aceitas, por já
  serem usadas); e proíbe objetos embutidos (plugins).
- **FR-010**: A política NÃO DEVE liberar a execução de scripts escritos diretamente na página de
  forma genérica. O único script escrito na página, o que aplica o tema, DEVE ser liberado de
  forma individual, identificado pelo seu conteúdo, e qualquer outro script escrito na página
  DEVE ser bloqueado.
- **FR-011**: Toda resposta do console DEVE trazer também: proibição de que o navegador adivinhe
  o tipo de um arquivo; política de origem de referência que não vaze o endereço para outros
  sites; proibição de carregamento em quadro também no formato legado (para navegadores que não
  entendem a política moderna); e uma política mínima de recursos do navegador (câmera, microfone,
  localização e similares desligados).
- **FR-012**: O console DEVE continuar funcionando integralmente com os cabeçalhos ativos: login,
  navegação entre telas, chamadas à Api e os temas claro e escuro. O tema DEVE continuar sendo
  aplicado antes da primeira renderização, sem flash de cor.
- **FR-013**: DEVE existir verificação automatizada que sobe o console empacotado como é entregue
  e confere, por requisição real, a presença e o valor dos cabeçalhos da FR-009 e da FR-011 em uma
  página, um arquivo estático, uma rota inexistente e uma resposta encaminhada da Api.
- **FR-014**: DEVE existir um teste automatizado que falha quando o conteúdo do script do tema
  muda sem que a sua liberação na política (FR-010) seja atualizada, e a documentação do projeto
  DEVE explicar como recalcular essa liberação.

**HTTPS obrigatório quando o acesso é seguro**

- **FR-015**: Quando a requisição ao console tiver sido originalmente feita por HTTPS
  (`X-Forwarded-Proto` = `https`), a resposta DEVE trazer a instrução de HTTPS obrigatório com
  validade mínima de 1 ano (31.536.000 segundos), em todas as respostas do console, inclusive as
  de erro e as encaminhadas da Api.
- **FR-016**: Quando a requisição for por HTTP simples, a instrução da FR-015 NÃO DEVE ser enviada
  e o console DEVE funcionar normalmente, sem redirecionamento para HTTPS e sem exigir TLS.
- **FR-017**: (Removido na sessão de esclarecimentos de 2026-11-02; ver Premissas, "Indicação de
  acesso seguro". O ID é mantido para preservar a numeração.)
- **FR-018**: O redirecionamento de HTTP para HTTPS NÃO faz parte desta spec: é responsabilidade
  do load balancer, na spec de ambientes (05).

**Prova do isolamento do usuário de banco da aplicação**

- **FR-019**: DEVE existir teste automatizado de integração, executado com o usuário de banco da
  aplicação (nunca com o usuário das migrations), nas três bases do ambiente (catálogo,
  compartilhada e dedicada), provando que ele NÃO consegue: criar, alterar ou
  apagar tabelas, índices, colunas ou políticas; criar outros objetos no esquema; esvaziar tabelas
  por completo; apagar linhas fisicamente de tabelas que a constituição proíbe apagar; desligar o
  isolamento por tenant de uma tabela; mudar os próprios atributos ou criar usuários; e assumir a
  identidade do usuário das migrations.
- **FR-020**: DEVE existir teste automatizado de integração, executado com o usuário de banco da
  aplicação e sem nenhum filtro da aplicação, na base compartilhada e na dedicada, provando que:
  com o tenant A informado, uma leitura sem filtro da tabela inteira devolve só linhas do tenant
  A; sem tenant informado, devolve zero linhas; e uma gravação ou alteração com o identificador de
  outro tenant é recusada. O catálogo não tem prova de RLS (não guarda dados de negócio de
  tenant); a prova dele está na FR-020a.
- **FR-020a**: DEVE existir teste automatizado de integração, executado com o usuário de banco da
  aplicação no catálogo, provando que ele só lê: INSERT, UPDATE e DELETE são negados em todas as
  tabelas do catálogo.
- **FR-021**: O usuário da aplicação DEVE ser verificado por consulta ao catálogo do próprio banco
  quanto a atributos perigosos (superusuário, criação de bancos, criação de usuários, ignorar o
  isolamento por tenant) e quanto a não ser dono das tabelas, em todas as bases do ambiente
  (catálogo, compartilhada e dedicada).
- **FR-022**: Os testes das FR-019 a FR-021 (e da FR-020a) DEVEM reaproveitar os que já existem (spec 01),
  sem duplicá-los, acrescentando só o que falta; o que já é provado hoje DEVE continuar sendo
  provado.
- **FR-023**: Se o usuário da aplicação ganhar por engano qualquer permissão proibida pela
  FR-019 ou pela FR-020a, ao menos um desses testes DEVE falhar (a prova vale como proteção contra regressão, não
  só como verificação única).

**Verificação de vulnerabilidades no CI**

- **FR-024**: O CI do backend DEVE falhar quando qualquer projeto da solução depender, direta ou
  indiretamente, de um pacote com vulnerabilidade conhecida, em qualquer severidade, exceto as
  exceções da FR-026.
- **FR-025**: O CI do frontend DEVE falhar quando qualquer dependência, direta ou indireta, tiver
  vulnerabilidade conhecida, em qualquer severidade, exceto as exceções da FR-026. Dependências
  usadas só para desenvolvimento e build contam, pois executam na máquina de quem constrói.
- **FR-026**: Uma exceção DEVE ser explícita e versionada: identifica o pacote e o aviso exato
  (não o pacote inteiro), o escopo a que se aplica (por exemplo, só o projeto de testes) e traz a
  justificativa escrita ao lado. A presença e a qualidade da justificativa são conferidas na
  revisão do PR (a verificação do CI não as valida). Um aviso novo no mesmo pacote DEVE falhar
  mesmo que um aviso anterior dele esteja na lista.
- **FR-027**: A exceção hoje existente (pacote indireto trazido pela biblioteca de contêineres de
  teste, em recurso nunca usado e que só existe no projeto de testes) DEVE ser migrada para o
  mecanismo da FR-026, mantendo a justificativa que já tem; ela NÃO DEVE continuar sendo uma
  supressão geral do aviso.
- **FR-028**: Se a consulta à base de vulnerabilidades falhar (sem rede, serviço fora do ar), a
  verificação DEVE falhar ou ser repetida, nunca passar como se não houvesse vulnerabilidades.
- **FR-029**: As vulnerabilidades presentes no repositório hoje (2 de severidade alta no frontend,
  na ferramenta de build do console e numa biblioteca indireta dela) DEVEM ser corrigidas por
  atualização de pacote, de modo que o CI desta spec nasça verde sem exceção nova. Se uma
  atualização exigir ajuste de configuração ou de código do console, o ajuste DEVE ser listado na
  lista de tarefas, e nenhum teste pode ser apagado, pulado ou enfraquecido para a atualização
  passar.
- **FR-030**: DEVE ser demonstrado, na prática, que a verificação funciona: o CI da feature fica
  verde e uma mudança descartável (rascunho de PR, nunca integrada) que acrescenta um pacote
  vulnerável deixa o CI vermelho, tanto no backend quanto no frontend.

**Documentação**

- **FR-031**: A documentação operacional do projeto (README / quickstart da feature) DEVE explicar
  como conferir os cabeçalhos do console, como recalcular a liberação do script do tema, como
  registrar uma exceção de vulnerabilidade e como rodar localmente as verificações de
  vulnerabilidade.
- **FR-032**: O item "Legado conhecido" da constituição relativo ao servidor do console sem
  cabeçalhos e ao CI sem verificação de pacotes deixa de ser legado ao final da spec; a retirada
  do item na constituição é feita por emenda do dono do projeto, fora desta spec.

### Entidades-Chave *(inclua se a feature envolver dados)*

Esta feature não cria nem altera dados de negócio. Os conceitos que ela governa:

- **Limite de tamanho de campo**: o máximo de caracteres aceito para cada campo de texto de cada
  requisição da Api; vive junto da validação do caso de uso.
- **Conjunto de cabeçalhos de segurança do console**: a política de conteúdo e os demais
  cabeçalhos enviados em toda resposta do console, mais a instrução condicional de HTTPS
  obrigatório.
- **Exceção de vulnerabilidade**: registro versionado de um aviso conhecido aceito de forma
  consciente (pacote, aviso, justificativa, escopo).
- **Usuário de banco da aplicação**: a identidade, sem poder de alterar estrutura e sujeita ao
  isolamento por tenant, com que a Api acessa o catálogo e as bases de tenant.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% dos campos de texto das requisições da Api têm limite de tamanho, comprovado
  por um teste automatizado que falha ao encontrar um campo sem limite.
- **SC-002**: Em 100% dos endpoints que recebem texto, um campo com 1 caractere acima do limite é
  recusado com `400` "Requisição inválida" e um campo exatamente no limite é aceito.
- **SC-003**: Uma senha de 1 MB enviada ao login é recusada com `400` sem que o cálculo de hash
  seja chamado e sem consulta ao banco, provado por teste que confere que nenhum dos dois foi
  chamado.
- **SC-004**: 100% das respostas do console verificadas (página, arquivo estático, rota
  inexistente e resposta encaminhada da Api) trazem todos os cabeçalhos de segurança definidos.
- **SC-005**: Uma página de outro site não consegue exibir o console em quadro, e um script
  injetado ou de terceiro não executa, em 100% das tentativas verificadas.
- **SC-006**: O tema claro e o escuro são aplicados em 100% das aberturas do console, sem flash de
  cor perceptível e com zero erros de política de segurança no console do navegador.
- **SC-007**: A instrução de HTTPS obrigatório aparece em 100% das respostas de acessos
  marcados como HTTPS (`X-Forwarded-Proto` = `https`) e em 0% das respostas de acessos por HTTP
  simples.
- **SC-008**: 100% das operações proibidas ao usuário de banco da aplicação (estrutura, atributos
  perigosos, troca de identidade) são negadas nos testes, nas três bases do ambiente (catálogo,
  compartilhada e dedicada); a leitura de dados de outro tenant devolve 0 linhas na base
  compartilhada e na dedicada; e INSERT, UPDATE e DELETE são negados no catálogo.
- **SC-009**: A verificação de vulnerabilidades reporta 0 vulnerabilidades não justificadas no
  backend e no frontend ao final da spec, e 0 exceções novas são criadas para isso.
- **SC-010**: Uma mudança descartável que acrescenta um pacote vulnerável conhecido deixa o CI
  vermelho em 100% das tentativas, no backend e no frontend.
- **SC-011**: Nenhuma funcionalidade existente do console ou da Api deixa de funcionar: todos os
  testes automatizados anteriores continuam passando sem terem sido alterados, apagados ou
  enfraquecidos.

## Premissas

- **Escopo**: esta spec cobre o console web (servidor estático e o encaminhamento da Api que ele
  faz), a entrada de dados de texto da Api, as permissões do usuário de banco da aplicação e o CI
  de pacotes. Fora do escopo: autenticação e sessão (spec 04, já entregue), limite por IP/tenant
  em Redis (spec 06), auditoria (spec 07), HTTPS, load balancer e redirecionamento HTTP→HTTPS
  (spec 05), limite do tamanho total do corpo da requisição, validação de formato além do
  tamanho e qualquer novo endpoint.
- **Limites de campo**: e-mail 255 e senha 128 (do pedido). Para os campos de texto do cadastro de
  pessoas (hoje o caso de uso de criação de usuário existe na validação, mas não está exposto na
  Api): nome 100 e observação 100 (já existentes), telefone 20, RG 20, CPF 14 (11 dígitos com
  máscara) e PIN 6 (o padrão já exige 4 a 6 dígitos). Esses limites seguem as colunas e os padrões
  atuais, são ajustáveis no `/speckit-clarify` ou no plano, e a spec do cadastro de pessoas (11)
  pode revê-los.
- **Senha no login**: o limite de senha também vale no login, para impedir a recusa tardia depois
  do cálculo de hash; o login continua devolvendo a mesma resposta genérica para credenciais
  erradas, mas uma senha acima do limite é um erro de validação (`400`), pois nenhuma senha válida
  do sistema passa de 128 caracteres.
- **Valores de cabeçalho**: referência de partida, a ser fechada no plano. Política de conteúdo
  restrita ao próprio console; referência `no-referrer`; quadro proibido (`frame-ancestors 'none'`
  e `X-Frame-Options: DENY`); `nosniff`; política de recursos do navegador mínima. HTTPS
  obrigatório com validade de 1 ano, sem `preload` (que é irreversível e exige decisão separada
  do dono do produto) e sem estender a subdomínios.
- **Indicação de acesso seguro**: o console emite a instrução de HTTPS obrigatório quando
  `X-Forwarded-Proto` = `https`, sem verificar a origem do cabeçalho, porque: (a) o navegador
  ignora a instrução recebida por HTTP (RFC 6797 §8.1); (b) um cliente que forje a indicação só
  afeta o próprio navegador; (c) o load balancer sobrescreve o `X-Forwarded-Proto`; e (d) em
  produção o console só é alcançável pelo load balancer (spec 05, que deve garantir que o console
  não fica exposto direto). No ambiente local não há `X-Forwarded-Proto: https`, então a
  instrução fica desligada.
- **Cabeçalhos e estilos**: o console hoje não usa estilos escritos na página nem scripts de
  terceiros; se o build gerar estilos escritos na página, o plano decide como liberá-los sem
  abrir a política de scripts.
- **Severidade**: a verificação de vulnerabilidades falha em qualquer severidade (baixa a
  crítica), porque hoje só existem avisos de severidade alta e o custo de manter a regra mais
  estrita é nulo. Se no futuro um aviso de baixa severidade sem correção bloquear o CI, ele entra
  como exceção justificada, nunca como afrouxamento da regra.
- **Versão do pacote do console**: aceita-se subir de versão principal (major) a ferramenta de
  build do console se for o necessário para eliminar as vulnerabilidades, desde que verificação de
  tipos, testes e build continuem verdes e o comportamento do console não mude.
- **Exceção existente**: a única exceção prevista é a do pacote indireto da biblioteca de
  contêineres de teste, que só existe no projeto de testes e não vai para a imagem de produção;
  se uma versão corrigida da biblioteca existir, a exceção é removida em vez de migrada.
- **Banco já separado**: a separação de usuários (aplicação sem DDL e sujeita a RLS; migrations
  por usuário próprio) já existe desde a spec 01 e é coberta em parte por testes; esta spec não
  muda as permissões, só prova e protege, e só as altera se a prova descobrir uma permissão a
  mais.
- **Bases testadas**: o ambiente local de teste tem catálogo, uma base compartilhada e uma base
  dedicada; as provas de DDL, de atributos perigosos e de não ser dono das tabelas rodam nas três,
  e a de RLS na compartilhada e na dedicada, usando os mesmos mecanismos de teste de integração
  que a suíte já usa. No catálogo a prova é de só leitura (hoje o usuário da aplicação só tem
  `SELECT` lá); se uma spec futura (ex.: o diretório de operadores, spec 09) passar a dar escrita
  em tabelas específicas do catálogo, ela ajusta essa prova.
- **Ambiente de entrega**: homologação e produção ainda não existem (spec 05); por isso a
  instrução de HTTPS é provada por simulação do cabeçalho `X-Forwarded-Proto`, e a conferência em
  um ambiente real fica para a spec 05.
- **Dependências**: depende da spec 04 (entregue) e da separação de usuários do banco (spec 01,
  entregue). A spec 05 depende desta para herdar os cabeçalhos e a verificação de pacotes no CI.
