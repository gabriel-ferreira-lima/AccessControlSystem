# Especificação da Feature: Base de testes do frontend

**Branch da Feature**: `20261101-164600-frontend-testing`

**Criada em**: 2026-11-01

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Criar a base de testes automatizados do frontend (console administrativo) e cobrir com testes o comportamento que as telas já têm hoje, sem mudar esse comportamento. Contexto: o backend tem testes por use case, o frontend não tem nenhum. A partir desta spec, toda mudança no front deve ser feita com TDD, e para isso o comportamento atual precisa estar protegido por testes de caracterização."

## Clarificações

### Sessão 2026-11-01

- Q: O CI do front deve rodar também a verificação de pacotes vulneráveis (`npm audit`)? → A: Não,
  só tipos, testes e compilação; o `npm audit` fica para a spec de segurança (04).
- Q: É permitida alguma mudança mínima, sem efeito funcional, no código das telas para viabilizar
  os testes? → A: Não, nenhuma linha de código de produção muda; o que não for testável sem
  alterá-lo vira achado registrado, sem correção.

## Cenários de Usuário e Testes *(obrigatório)*

Nesta spec, o "usuário" é quem mantém o console administrativo (pessoa desenvolvedora ou revisora). O
valor entregue é a **confiança para mudar o front**: depois desta entrega, qualquer alteração que
quebre o comportamento atual das telas é apontada por um teste automatizado antes de chegar ao
operador final. Os testes são de **caracterização**: descrevem o que o console faz hoje, devem
passar desde o primeiro dia e **não mudam o comportamento** de nenhuma tela.

### História de Usuário 1 - Rodar a suíte e ser barrado por ela (Prioridade: P1)

Quem mantém o front roda um único comando e vê todos os testes do console executarem de forma
isolada, rápida e repetível. Ao abrir um pull request que altera o front, o CI roda a verificação
de tipos, os testes e a compilação; se qualquer um falhar, o pull request fica reprovado.

**Por que esta prioridade**: sem a base executável e sem o CI, nenhum dos demais testes protege
nada. É o alicerce de todas as outras histórias e viabiliza o TDD exigido pela constituição.

**Teste Independente**: pode ser verificado só com a base e um teste mínimo: rodar o comando de
testes localmente (passa), quebrar de propósito um comportamento coberto (falha) e abrir um pull
request de teste que altere o front (o CI executa as três verificações).

**Cenários de Aceitação**:

1. **Dado** um clone limpo do repositório com as dependências do front instaladas, **Quando** quem
   mantém roda o comando de testes do front, **Então** todos os testes executam sem precisar de
   API, banco, Docker ou internet, e o resultado final é "aprovado".
2. **Dado** que um teste dispara uma chamada à API que não foi prevista no cenário dele,
   **Quando** o teste roda, **Então** ele falha apontando a chamada não prevista (nenhuma chamada
   real sai da máquina).
3. **Dado** um pull request que altera qualquer arquivo em `src/Frontend/`, **Quando** o CI roda,
   **Então** ele executa verificação de tipos, testes e compilação do front, e o pull request só
   fica verde se as três passarem.
4. **Dado** um pull request que não toca o front (nem o workflow do front), **Quando** ele é
   aberto, **Então** o CI do front não roda.
5. **Dado** que um teste falha, **Quando** o CI termina, **Então** o resultado do pull request é
   "reprovado" e a saída mostra qual teste falhou e por quê.

---

### História de Usuário 2 - Login e ciclo de vida da sessão protegidos (Prioridade: P1)

O fluxo de entrada no console (login) e de saída (sessão encerrada pelo servidor ou pelo
operador) tem o comportamento de hoje coberto por testes.

**Por que esta prioridade**: é a porta de entrada de todo o console e a regra mais sensível do
front (qualquer resposta 401 derruba a sessão). Uma regressão aqui deixa operadores presos fora do
sistema ou, pior, dentro de uma sessão já invalidada pelo servidor.

**Teste Independente**: com apenas as telas de login e início, simular as respostas da API
(sucesso, erro com `message` e `notifications`, 401 numa chamada autenticada) e conferir o que a
tela mostra e para onde o operador é levado.

**Cenários de Aceitação**:

1. **Dado** um operador na tela de login, **Quando** ele informa e-mail e senha e a API responde
   sucesso com a sessão, **Então** a sessão é guardada para a aba e ele é levado à tela inicial.
2. **Dado** que o operador tentou abrir uma rota protegida sem sessão e foi mandado ao login,
   **Quando** ele conclui o login com sucesso, **Então** ele é levado à rota que tentou abrir, e
   não à tela inicial.
3. **Dado** que a API recusa o login (por exemplo, `401`), **Quando** a resposta chega, **Então**
   a tela mostra a `message` e todas as `notifications` devolvidas, exatamente como vieram, e o
   operador permanece no login.
4. **Dado** que a API responde sucesso mas sem credencial de sessão utilizável, **Quando** a
   resposta chega, **Então** a tela mostra a mensagem genérica de erro inesperado e não cria
   sessão.
5. **Dado** um operador com sessão ativa, **Quando** qualquer chamada autenticada recebe `401`,
   **Então** a sessão local é apagada, ele é levado ao login e a tela de login exibe o aviso de
   sessão encerrada.
6. **Dado** um operador com sessão ativa, **Quando** ele aciona "encerrar sessão" no topo,
   **Então** a sessão é apagada e ele é levado ao login **sem** o aviso de sessão encerrada.
7. **Dado** um operador que já tem sessão, **Quando** ele abre a tela de login, **Então** ele é
   redirecionado para a tela inicial (ou para a rota de origem, se houver).
8. **Dado** um envio de login em andamento, **Quando** o botão de entrar é acionado, **Então** ele
   fica em estado de carregamento ("Autenticando...") até a resposta.

---

### História de Usuário 3 - Lista de operadores protegida (Prioridade: P1)

A tela de operadores (filtro, paginação, ativar/desativar com confirmação e exibição de erros)
tem o comportamento de hoje coberto por testes.

**Por que esta prioridade**: é a tela com mais comportamento e a única que altera o estado de
contas de terceiros (ativar/desativar). É onde mais se vai mexer nas próximas specs (roles,
permissões, usuários).

**Teste Independente**: com a API simulada devolvendo páginas de operadores, conferir a listagem,
os filtros, a navegação entre páginas e o fluxo de confirmação, sem depender de nenhuma outra
tela.

**Cenários de Aceitação**:

1. **Dado** um administrador com sessão, **Quando** a tela de operadores abre, **Então** ela
   carrega a primeira página com o filtro "Ativos" selecionado e mostra e-mail, perfil e situação
   de cada operador.
2. **Dado** a lista carregada, **Quando** o administrador troca o filtro para "Inativos" ou
   "Todos", **Então** a tela volta à página 1 e pede à API o filtro correspondente.
3. **Dado** uma lista com mais de uma página, **Quando** o administrador usa "Próxima" e
   "Anterior", **Então** a tela pede a página correspondente, e "Anterior" fica desabilitado na
   primeira página e "Próxima" na última.
4. **Dado** a lista carregada, **Quando** o administrador muda a quantidade de itens por página,
   **Então** a tela volta à página 1 e pede à API o novo tamanho.
5. **Dado** que a API devolve zero operadores, **Quando** a lista carrega, **Então** a tela mostra
   a mensagem de lista vazia para o filtro.
6. **Dado** um operador ativo na lista, **Quando** o administrador aciona "Desativar", **Então**
   abre uma janela de confirmação com o e-mail do operador; ao confirmar, a API é chamada, a
   janela fecha e a lista é recarregada.
7. **Dado** um operador inativo na lista, **Quando** o administrador aciona "Ativar" e confirma,
   **Então** o comportamento é o mesmo do cenário anterior, para ativação.
8. **Dado** a janela de confirmação aberta, **Quando** o administrador cancela (botão ou tecla
   Esc), **Então** a janela fecha, nenhuma chamada de ativar/desativar é feita e a lista não muda.
9. **Dado** que a API recusa a ativação/desativação, **Quando** a resposta chega, **Então** a
   janela permanece aberta mostrando a `message` e as `notifications` da API (inclusive regras que
   só aparecem em `notifications`), e a lista não é recarregada.
10. **Dado** que a API falha ao carregar a lista, **Quando** a resposta chega, **Então** a tela
    mostra a `message` e as `notifications` no lugar da tabela.
11. **Dado** a lista carregada, **Quando** o administrador aciona "Editar" numa linha, **Então**
    ele é levado à edição levando o identificador, o e-mail e o perfil daquela linha. **E** "Criar
    operador" leva à tela de criação.

---

### História de Usuário 4 - Navegação protegida por sessão e perfil (Prioridade: P2)

As rotas do console redirecionam conforme a sessão e o perfil, do jeito que fazem hoje.

**Por que esta prioridade**: o controle de acesso real é do backend; o front só decide o que
mostrar. Ainda assim, esse comportamento vai mudar na spec de roles e permissões, e precisa estar
fixado antes para a mudança ser visível e intencional.

**Teste Independente**: montar a navegação com sessões de perfis diferentes (ou sem sessão) e
conferir para onde cada endereço leva.

**Cenários de Aceitação**:

1. **Dado** que não há sessão, **Quando** qualquer rota protegida é aberta, **Então** o operador é
   levado ao login, e a rota pretendida é lembrada para depois do login.
2. **Dado** uma sessão de perfil Operador, **Quando** ele abre qualquer rota de operadores (lista,
   criar, editar), **Então** é redirecionado para a tela inicial.
3. **Dado** uma sessão de perfil Administrador, **Quando** ele abre "Minha conta", **Então** é
   redirecionado para a tela inicial (hoje a tela é exclusiva do perfil Operador; o Administrador
   edita a própria conta pela edição de operadores).
4. **Dado** uma sessão válida e um endereço inexistente, **Quando** ele é aberto, **Então** o
   console mostra a página "não encontrada" com link de volta ao início.
5. **Dado** uma sessão, **Quando** o menu lateral é exibido, **Então** cada perfil vê apenas os
   itens que hoje lhe cabem (Início para todos; "Minha conta" só para Operador; "Operadores" só
   para Administrador).

---

### História de Usuário 5 - Criar e editar operador protegidos (Prioridade: P2)

Os formulários de criação e edição de operador têm o comportamento de hoje coberto, incluindo o
pré-preenchimento da edição.

**Por que esta prioridade**: são os formulários que enviam dados sensíveis (senha, perfil) e
exibem erros da API campo a campo; precisam de proteção, mas dependem menos de outras telas que a
lista.

**Teste Independente**: abrir cada formulário isoladamente, preencher, enviar contra uma API
simulada (sucesso e erro) e conferir o que foi enviado e o que é exibido.

**Cenários de Aceitação**:

1. **Dado** o formulário de criação, **Quando** o administrador informa e-mail, senha e perfil e a
   API responde com sucesso, **Então** a tela mostra a confirmação com identificador, e-mail e
   perfil do operador criado, e "Cadastrar outro" devolve o formulário em branco.
2. **Dado** o formulário de criação, **Quando** a API recusa o envio, **Então** a tela mostra a
   `message` e as `notifications` como vieram, e erros cujo campo é e-mail, senha ou perfil também
   aparecem junto do campo correspondente; os dados digitados são preservados.
3. **Dado** que o administrador veio da lista, **Quando** a edição abre, **Então** e-mail e perfil
   já vêm preenchidos com os da linha escolhida e a senha vem vazia.
4. **Dado** a edição aberta pela lista, **Quando** o administrador altera só um campo e envia,
   **Então** apenas o campo alterado é enviado à API (a senha só vai se for digitada).
5. **Dado** que a edição foi aberta direto pelo endereço (com identificador, sem passar pela
   lista), **Quando** a tela abre, **Então** e-mail vem em branco e perfil vem como Operador, e o
   envio inclui o perfil mesmo sem alteração.
6. **Dado** que a edição foi aberta sem identificador, **Quando** a tela abre, **Então** ela
   mostra a orientação de escolher um operador na lista, com link para a lista, e nenhum
   formulário.
7. **Dado** uma edição enviada com sucesso, **Quando** a API responde, **Então** a tela mostra a
   confirmação com o e-mail e o perfil atualizados, e "Editar novamente" volta ao formulário com
   esses valores como nova referência.
8. **Dado** uma edição recusada pela API, **Quando** a resposta chega, **Então** a tela mostra a
   `message` e as `notifications` como vieram, com erros de campo junto do campo correspondente.
9. **Dado** qualquer um dos dois formulários, **Quando** o administrador aciona "Limpar", **Então**
   os campos voltam ao valor de referência (em branco na criação; valores da lista ou em branco
   na edição) e os avisos de erro/sucesso somem.

---

### História de Usuário 6 - Minha conta protegida (Prioridade: P2)

A tela do próprio operador (alterar e-mail e senha) tem o comportamento de hoje coberto.

**Por que esta prioridade**: afeta a credencial do próprio operador, mas é usada só pelo perfil
Operador e tem escopo pequeno.

**Teste Independente**: abrir a tela com sessão de Operador e a API simulada para "get-me" e
"update-me".

**Cenários de Aceitação**:

1. **Dado** um operador com sessão, **Quando** "Minha conta" abre, **Então** a tela carrega os
   dados do servidor, pré-preenche o e-mail atual, mostra o perfil como somente leitura e deixa a
   senha vazia.
2. **Dado** os dados carregados, **Quando** o operador altera só o e-mail e envia, **Então** só o
   e-mail vai à API; se alterar só a senha, só a senha vai; sem alterações, nada além do vazio vai.
3. **Dado** um envio bem-sucedido, **Quando** a API responde, **Então** a tela mostra a mensagem de
   sucesso devolvida pela API, limpa a senha, recarrega os dados do servidor e a sessão local passa
   a refletir o novo e-mail.
4. **Dado** que a API recusa o envio, **Quando** a resposta chega, **Então** a tela mostra a
   `message` e as `notifications` como vieram, com erros de campo junto do campo.
5. **Dado** que o carregamento inicial falha, **Quando** a resposta chega, **Então** a tela mostra a
   `message` e as `notifications` da API acima do formulário.
6. **Dado** o formulário alterado, **Quando** o operador aciona "Limpar", **Então** o e-mail volta
   ao atual, a senha é esvaziada e os avisos somem.

---

### História de Usuário 7 - Falhas de comunicação tratadas (Prioridade: P2)

Quando a API não responde ou responde algo fora do formato esperado, o console mostra uma
mensagem compreensível, em qualquer tela.

**Por que esta prioridade**: é transversal, mas o comportamento é simples e já está concentrado
num único ponto do código.

**Teste Independente**: simular falha de rede e respostas fora do envelope esperado em uma tela
representativa (login e lista) e conferir a mensagem.

**Cenários de Aceitação**:

1. **Dado** que a API está inacessível, **Quando** uma tela faz uma chamada, **Então** ela mostra a
   mensagem de falha de comunicação com o servidor.
2. **Dado** que a API responde erro sem o envelope esperado (corpo vazio ou que não é JSON),
   **Quando** a resposta chega, **Então** a tela mostra a mensagem genérica de erro inesperado.
3. **Dado** que a API responde HTTP de sucesso mas com o envelope marcando falha, **Quando** a
   resposta chega, **Então** a tela trata como erro e mostra a `message` e as `notifications`
   do envelope.
4. **Dado** um erro `403` (ou outro que não seja `401`) numa chamada autenticada, **Quando** a
   resposta chega, **Então** a sessão **não** é encerrada e a tela mostra o erro da API.
5. **Dado** uma tela que sai da vista com chamada em andamento, **Quando** a chamada é abortada,
   **Então** nenhum erro é exibido.

---

### Casos Limite

- Sessão guardada com conteúdo inválido, sem credencial ou com perfil desconhecido: o console
  trata como sem sessão (credencial ausente) ou como perfil Operador (perfil desconhecido).
- Armazenamento da aba indisponível: o console continua funcionando em memória, sem quebrar.
- `401` no próprio login (credencial errada) **não** derruba sessão nem mostra o aviso de sessão
  encerrada: é erro comum de login.
- `401` em chamada não autenticada nunca aciona o encerramento de sessão.
- Troca rápida de filtro/página com chamadas em andamento: apenas a resposta mais recente
  prevalece; respostas de chamadas abandonadas são ignoradas.
- Janela de confirmação com chamada em andamento: cancelar/Esc ficam bloqueados até a resposta.
- Resposta de sucesso da lista sem dados: a tela permanece como estava.
- Se algum teste de caracterização revelar um comportamento que pareça bug, ele é registrado como
  achado e **não é corrigido** nesta spec.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: O front DEVE ter um comando único e documentado que executa todos os testes
  localmente, sem depender de API, banco de dados, Docker ou internet.
- **FR-002**: Os testes DEVEM simular a API devolvendo o envelope real (`message`, `status`,
  `isSuccess`, `notifications`, `data`) e DEVEM falhar quando o código fizer uma chamada não
  prevista no cenário.
- **FR-003**: Os testes DEVEM interagir como o operador (digitar, clicar, escolher) e consultar a
  tela por papel, rótulo ou texto visível, nunca por detalhes de estrutura ou de estilo.
- **FR-004**: Os testes DEVEM cobrir o login: sucesso leva à tela inicial (ou à rota de origem);
  erro da API mostra `message` e `notifications`; resposta sem credencial utilizável mostra erro
  inesperado; estado de carregamento durante o envio; sessão existente redireciona para fora do
  login.
- **FR-005**: Os testes DEVEM cobrir a sessão: `401` em chamada autenticada apaga a sessão, leva ao
  login e exibe o aviso de sessão encerrada; encerrar sessão manualmente leva ao login sem o aviso;
  `401` no login e em chamadas não autenticadas não derruba sessão; outros erros (ex.: `403`) não
  derrubam sessão.
- **FR-006**: Os testes DEVEM cobrir a navegação: sem sessão, rotas protegidas levam ao login
  lembrando a rota de origem; Operador é redirecionado das rotas de operadores; Administrador é
  redirecionado de "Minha conta"; endereço inexistente mostra "não encontrada"; o menu lateral
  mostra os itens por perfil como hoje.
- **FR-007**: Os testes DEVEM cobrir a lista de operadores: carga inicial com filtro "Ativos";
  filtros Ativos/Inativos/Todos; paginação (próxima, anterior, limites); itens por página; lista
  vazia; carregamento; erro de carga; "Editar" levando e-mail e perfil da linha; "Criar operador".
- **FR-008**: Os testes DEVEM cobrir ativar e desativar: janela de confirmação com o e-mail do
  operador, confirmação chama a API e recarrega a lista, cancelamento (botão e Esc) não chama a
  API, erro da API aparece na janela com `message` e `notifications`, estado de espera bloqueia
  cancelar.
- **FR-009**: Os testes DEVEM cobrir a criação de operador: envio com e-mail, senha e perfil;
  confirmação de sucesso com identificador, e-mail e perfil; "Cadastrar outro"; erro da API com
  `message`, `notifications` e erros junto dos campos e-mail/senha/perfil; "Limpar".
- **FR-010**: Os testes DEVEM cobrir a edição de operador: pré-preenchimento a partir da linha da
  lista; formulário em branco (perfil Operador) quando aberta direto com identificador; orientação
  quando aberta sem identificador; envio só dos campos alterados; envio do perfil quando não há
  referência da lista; confirmação de sucesso e "Editar novamente"; erro da API; "Limpar".
- **FR-011**: Os testes DEVEM cobrir "Minha conta": carga dos dados do servidor, pré-preenchimento,
  perfil somente leitura, envio só dos campos alterados, mensagem de sucesso, atualização do
  e-mail na sessão, recarga após o sucesso, erro de envio, erro de carga, "Limpar".
- **FR-012**: Os testes DEVEM cobrir as falhas de comunicação: servidor inacessível, resposta
  fora do envelope, envelope de falha com HTTP de sucesso, chamadas abortadas sem erro exibido.
- **FR-013**: As funções utilitárias do front (cliente de API, normalização de erro de formulário,
  armazenamento da sessão, rótulos de perfil) DEVEM ter testes unitários próprios, e os componentes
  compartilhados com comportamento (janela de confirmação, guardas de rota, menu, topo) DEVEM
  ter testes próprios.
- **FR-014**: O CI DEVE rodar verificação de tipos, testes e compilação do front em todo pull
  request que altere `src/Frontend/` ou o próprio workflow, e DEVE reprovar o pull request se
  qualquer etapa falhar.
- **FR-015**: Os testes DEVEM ser de caracterização: passam sobre o código atual sem alterá-lo. Esta
  entrega NÃO DEVE alterar nenhuma linha do código de produção das telas nem mudar o comportamento
  de nenhuma delas. Qualquer comportamento que pareça bug, ou que não possa ser testado sem alterar
  esse código, DEVE ser registrado como achado, sem correção.
- **FR-016**: A documentação do front DEVE explicar como rodar os testes, como escrever um teste
  novo (como simular a API e como entrar numa tela já autenticado) e que toda mudança do front
  segue TDD.
- **FR-017**: Cada cenário de aceitação desta spec DEVE poder ser localizado em ao menos um teste
  (rastreabilidade por descrição em português).

### Entidades-Chave

- **Sessão**: identidade do operador logado no console (credencial, identificador, e-mail, perfil),
  guardada só enquanto a aba existe. Pode ser encerrada pelo operador ou pelo servidor (`401`).
- **Perfil (role)**: hoje `Admin` ou `Operator`. Define o que o console mostra e a quais rotas o
  operador chega.
- **Operador**: conta exibida, criada, editada, ativada e desativada pelo console (e-mail, perfil,
  situação ativo/inativo).
- **Envelope de resposta**: formato único de resposta da API (`message`, `status`, `isSuccess`,
  `notifications`, `data`) que as telas exibem sem reinterpretar.
- **Notificação**: par chave/mensagem de uma regra recusada pela API, exibido junto da `message` e,
  quando a chave é e-mail, senha ou perfil, também junto do campo.
- **Cenário de caracterização**: descrição de um comportamento atual do console, que vira ao menos
  um teste automatizado.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: Quem mantém o front roda a suíte completa com um comando e obtém o resultado em menos
  de 2 minutos numa máquina de desenvolvimento comum.
- **SC-002**: 100% dos cenários de aceitação das histórias 1 a 7 têm ao menos um teste
  correspondente, localizável pela descrição.
- **SC-003**: Para cada grupo de comportamento coberto (login, sessão, navegação, lista,
  confirmação, criar, editar, minha conta, falhas de comunicação), alterar deliberadamente o
  comportamento no código faz ao menos um teste falhar.
- **SC-004**: Em 10 execuções consecutivas da suíte, o resultado é idêntico (nenhuma falha
  intermitente) e nenhuma delas faz chamada de rede real.
- **SC-005**: Em 100% dos pull requests que alteram o front, o CI executa tipos, testes e
  compilação; um pull request com qualquer uma dessas etapas falhando nunca aparece como aprovado.
- **SC-006**: Pull requests que não alteram o front não gastam tempo de CI do front.
- **SC-007**: Nenhuma linha de código de produção das telas muda nesta entrega e, portanto, o
  comportamento observado pelo operador é idêntico ao anterior.
- **SC-008**: Todo comportamento suspeito de bug encontrado durante a escrita dos testes fica
  registrado com a descrição do que acontece hoje, e nenhum é corrigido nesta spec.

## Premissas

- A suíte roda inteira no computador de quem mantém e no CI, sem ambiente externo. A API é
  sempre simulada com o envelope real, nunca substituída por mock dos módulos do front.
- Os textos exibidos hoje não têm acentuação (ex.: "Sua sessao foi encerrada. Faca login
  novamente."). Os testes verificam o texto **como está hoje**; a correção dos acentos é da spec
  de limpeza (03), que atualizará os testes junto, com a mudança explícita nas tarefas.
- Esta spec considera as regras de perfil atuais (`Admin`/`Operator`, inclusive "Minha conta"
  exclusiva do Operador) como o comportamento a ser fixado. A spec de roles e permissões (08)
  mudará esse comportamento e, ao fazê-lo, atualizará os testes correspondentes.
- Menu lateral, topo (encerrar sessão), tela inicial e "não encontrada" entram na cobertura por
  fazerem parte do ciclo de navegação e sessão; não foram listados um a um na descrição original.
- Não há meta de percentual de cobertura: o critério é a cobertura dos cenários desta spec
  (FR-017), não um número.
- O CI "bloquear o merge" dependeria de marcar a verificação como obrigatória na proteção da branch
  `main` do GitHub. Como o workflow só roda quando o front muda (filtro de `paths`, SC-006), uma
  verificação obrigatória deixaria "aguardando" para sempre os pull requests que não tocam o front,
  e eles não poderiam ser mesclados (o `backend-ci.yml` tem o mesmo efeito). **Decisão do dono em
  2026-11-01**: a verificação do front **não** será marcada como obrigatória na `main` até a spec
  05 (que deve resolver o problema com um job agregador que sempre roda; o roteiro da 05 já prevê
  isso); até lá, o revisor não mescla pull request com o CI vermelho. A spec entrega a verificação, não a obrigatoriedade.
- A verificação de pacotes vulneráveis (`npm audit`) no CI do front fica para a spec de segurança
  (04), junto com a do backend. Em 2026-11-01, `npm audit` em `src/Frontend` aponta 2
  vulnerabilidades high (vite <=6.4.3 e source-map-js), que a spec 04 deve resolver ao ligar a
  verificação.
- O código de produção das telas fica intocado: se algum comportamento não for testável como está
  (por exemplo, um campo sem rótulo acessível), isso vira achado registrado (SC-008), não um
  ajuste de código nesta spec.
- A escolha das ferramentas e da organização dos arquivos de teste é decisão do `plan.md`,
  seguindo o Princípio VII da constituição (frontend).
- Fora do escopo: corrigir textos sem acento, mudar qualquer comportamento das telas, testes de
  ponta a ponta com navegador real, testes de acessibilidade automatizados e de desempenho do front.
