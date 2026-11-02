# Especificação da Feature: Limpeza de legado

**Branch da Feature**: `20261101-194211-legacy-cleanup`

**Criada em**: 2026-11-01

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Alinhar o código existente às convenções da constituição, sem mudar nenhum comportamento da API além dos textos corrigidos. Contexto: o código será escrito por IA a partir de agora e ela copia os padrões que encontra; nomes enganosos e inconsistências precisam sair antes que se multipliquem. Mudanças: (1) todo texto exibido ao usuário no frontend passa a ter acentuação correta; (2) a mensagem "Requsição inválida" do update-me é corrigida para "Requisição inválida", com o teste de compatibilidade de contrato da Api atualizado de forma declarada (a clarificação de 2026-11-01 estendeu isso aos outros dois textos de API com erro de acentuação); (3) a configuração de segredo "PasswordSaltKey", exigida mas sem uso, deixa de existir (o sistema sobe sem ela e ela some da documentação e dos exemplos); (4) os métodos de repositório e de domínio passam a seguir as convenções de nome da constituição, sem nada mudar para quem usa a API. Critério de aceite: todos os testes existentes (backend e frontend) continuam passando, com apenas as alterações de nome e de texto esperado."

## Clarificações

### Sessão 2026-11-01

- Q: Além de "Requsição inválida", corrigir também os outros dois textos de API sem acento
  ("Sessão invalida - faça o login novamente" e "Usuario atualizado com sucesso")? → A: Sim, só a
  acentuação ("Sessão inválida - faça o login novamente" e "Usuário atualizado com sucesso"); o
  teste de contrato muda 2 expectativas, declaradas no `tasks.md`.
- Q: Os achados A-11, A-12 e A-14 da spec 02 entram nesta spec? → A: Não, nenhum; mudam
  comportamento e ficam para specs futuras.
- Q: Renomear `SaveAsync` (criar operador) para `CreateAsync`? → A: Não, `SaveAsync` fica como
  está.
- Q: Os 2 textos de interface que nenhum teste afirma hoje ("O uso deste sistema é limitado a
  operadores autorizados." e o cabeçalho "Ações" da lista) podem ser corrigidos sem teste? → A: Não
  (Princípio VII): o FR-021 passa a cobri-los, com uma asserção acrescentada a um teste existente da
  tela de login e a um da lista de operadores, escrita antes e vista falhando.

## Cenários de Usuário e Testes *(obrigatório)*

Esta é uma spec de **alinhamento**, não de funcionalidade. Quem se beneficia são três públicos:
quem mantém o código (pessoas e IA, que copiam os padrões que encontram), quem opera o console
(lê os textos da tela) e quem sobe o ambiente (configura os segredos). Depois da entrega, **nenhum
endpoint, tela ou regra de negócio se comporta de forma diferente**; as únicas diferenças
observáveis são os textos corrigidos e uma configuração que deixa de ser exigida. A proteção é o
conjunto de testes que já existe: ele continua passando, e cada alteração nele (só nomes e textos
esperados) é declarada.

### História de Usuário 1 - Nomes que dizem a verdade (Prioridade: P1)

Quem lê ou escreve código de repositório e de domínio encontra nomes que seguem a convenção da
constituição e que dizem exatamente o que o método devolve ou pergunta. Os seis itens fora da
convenção (inventário na seção de Requisitos) deixam de existir.

**Por que esta prioridade**: nomes enganosos se multiplicam quando o código é copiado. O pior caso
é `CheckIfEmailIsNotUsed`, que devolve `true` quando o e-mail **está** em uso (o contrário do que o
nome diz): quem copia o padrão sem ler o corpo inverte a condição e abre um bug de unicidade de
e-mail. Os demais nomes chamam de "usuário" o que é um operador e deixam de seguir o sufixo
`Async`, o que confunde as próximas specs de usuário e de credenciais.

**Teste Independente**: compilar e rodar toda a suíte do backend (rápidos e integração). Tudo
passa, a busca textual pelos nomes antigos não encontra nada fora dos registros históricos, e a
revisão do diff dos testes mostra apenas troca de nome de método.

**Cenários de Aceitação**:

1. **Dado** os use cases que buscam um operador pelo identificador (ativar, desativar, minha
   conta, atualizar e atualizar a própria conta), **Quando** quem mantém abre o contrato do
   repositório, **Então** o método diz que devolve um operador, é uma leitura por chave e termina
   em `Async`.
2. **Dado** o login, **Quando** quem mantém abre o contrato do repositório, **Então** a busca por
   e-mail segue a mesma convenção (operador, por e-mail, `Async`).
3. **Dado** a listagem de operadores, **Quando** quem mantém abre o contrato do repositório,
   **Então** o método de lista segue a convenção de listas e termina em `Async`.
4. **Dado** a criação de operador, **Quando** o repositório responde se o e-mail já existe,
   **Então** o método tem o formato de pergunta `{Algo}ExistsAsync` e devolve `true` quando existe.
5. **Dado** a atualização de operador e a atualização da própria conta, **Quando** o repositório
   responde se o e-mail já pertence a **outro** operador, **Então** o método tem o formato de
   pergunta `{Algo}ExistsAsync` e devolve `true` quando existe outro operador com o e-mail, a
   mesma resposta de hoje.
6. **Dado** um e-mail que pertence a outro operador, **Quando** o operador tenta usá-lo na
   atualização, **Então** a resposta continua sendo `409` com "Email já está em uso"; **e dado**
   um e-mail livre, **Então** o fluxo continua e a atualização é feita, como antes (a condição
   dos handlers não se inverte).
7. **Dado** a entidade de usuário do domínio, **Quando** ela é reativada, **Então** o método de
   reativação é `Activate()`, par de `Deactivate()` (como já é em `Operator`), e o efeito é o mesmo
   de hoje.
8. **Dado** que `User.Active()` não tem hoje nenhum chamador nem teste, **Quando** a suíte roda,
   **Então** existe ao menos um teste provando que `Activate()` reativa um usuário desativado.
9. **Dado** qualquer teste que citava um nome antigo (na chamada, no substituto ou no próprio
   nome do teste), **Quando** a suíte roda, **Então** ele cita o nome novo, com a mesma frase de
   comportamento, as mesmas asserções e os mesmos dados.

---

### História de Usuário 2 - Textos do console com acentuação correta (Prioridade: P1)

O operador (e quem usa leitor de tela) lê o console em português correto: mensagens de erro,
avisos, títulos, botões, cabeçalhos de tabela, itens de menu e nomes acessíveis. Nenhuma tela muda
de função, de rota ou de comportamento.

**Por que esta prioridade**: o front é a única parte que o operador vê, e o código novo copia o
que já existe: enquanto houver texto sem acento, cada tela nova nasce sem acento. A constituição
já exige acentuação em todo texto de interface.

**Teste Independente**: rodar os testes do front (os que afirmam os textos foram ajustados) e, em
paralelo, abrir cada tela e conferir os textos do inventário B. Nenhum fluxo, rota, ordem de
elementos ou estado habilitado/desabilitado mudou.

**Cenários de Aceitação**:

1. **Dado** que a API está inacessível, **Quando** uma tela faz uma chamada, **Então** a mensagem
   exibida é "Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente."
2. **Dado** que o servidor derrubou a sessão (`401` numa chamada autenticada), **Quando** a tela
   de login abre, **Então** o aviso é "Sua sessão foi encerrada. Faça login novamente."
3. **Dado** um operador com sessão, **Quando** a tela inicial abre, **Então** o título é "Início",
   o menu mostra o grupo "Operação" e o item "Início", a navegação tem o nome acessível
   "Navegação principal", o topo oferece "Encerrar sessão" e o link de pular a navegação diz
   "Ir para o conteúdo".
4. **Dado** um endereço inexistente, **Quando** a tela abre, **Então** ela mostra "Página não
   encontrada" e "O recurso solicitado não existe ou foi movido."
5. **Dado** a lista de operadores, **Quando** ela abre, **Então** o cabeçalho da coluna de ações
   (só para leitor de tela) é "Ações", o seletor é "Itens por página", o resumo da paginação diz
   "N operador(es) · página X de Y", o botão de paginação é "Próxima", e a confirmação de
   desativar diz "A conta deixará de conseguir acessar o sistema."
6. **Dado** os formulários de edição de operador e de "Minha conta", **Quando** eles abrem,
   **Então** o botão de envio é "Salvar alterações".
7. **Dado** a tela de login e a tela inicial, **Quando** elas abrem, **Então** os textos de apoio
   estão acentuados ("O uso deste sistema é limitado a operadores autorizados." e "As informações
   desta página serão adicionadas conforme os serviços forem disponibilizados.").
8. **Dado** qualquer mensagem ou notificação vinda da API, **Quando** ela é exibida, **Então**
   aparece exatamente como a API devolveu (a correção do front não reescreve texto da API).
9. **Dado** qualquer fluxo das telas (login, sessão, navegação, lista, criar, editar, minha
   conta), **Quando** executado, **Então** o comportamento é idêntico ao anterior, com a única
   diferença dos textos corrigidos.
10. **Dado** o texto de apoio do login e o cabeçalho "Ações" da tabela de operadores, que hoje
    nenhum teste afirma, **Quando** a suíte roda, **Então** um teste existente da tela de login e um
    da lista de operadores passam a afirmar o texto acentuado (FR-021).

---

### História de Usuário 3 - Mensagens de API com acentuação correta (Prioridade: P2)

Três textos de resposta da API passam a ter a grafia correta, e nada mais nelas muda: o `400` de
validação do update-me ("Requisição inválida", igual ao de todos os demais endpoints, e não mais
"Requsição inválida"), o `401` de sessão inválida do get-me e do update-me ("Sessão inválida -
faça o login novamente") e o `200` do update-me ("Usuário atualizado com sucesso").

**Por que esta prioridade**: são os únicos textos de API com erro de escrita, e a constituição
exige acento em toda mensagem da API. O teste de compatibilidade de contrato hoje protege dois
deles **com o erro** ("Requsição inválida" e "Usuario atualizado com sucesso"). Corrigir exige
mudar esse teste, e a mudança precisa ser explícita para respeitar a integridade das entregas.

**Teste Independente**: chamar a atualização da própria conta sem campos, com sucesso e sem
operador encontrado (sessão inválida), e chamar "minha conta" sem operador encontrado, conferindo
os três textos; rodar o teste de compatibilidade de contrato da Api e conferir que só as duas
expectativas esperadas mudaram.

**Cenários de Aceitação**:

1. **Dado** um operador autenticado, **Quando** ele envia a atualização da própria conta sem
   nenhum campo, **Então** a resposta é `400` com a mensagem "Requisição inválida" e as mesmas
   notificações de hoje.
2. **Dado** os demais endpoints do `AccountContext` que respondem `400` por validação, **Quando**
   recebem uma requisição inválida, **Então** a mensagem é a mesma "Requisição inválida", como já
   era.
3. **Dado** um token válido cujo operador não é encontrado na base, **Quando** ele chama minha
   conta ou a atualização da própria conta, **Então** a resposta é `401` com a mensagem "Sessão
   inválida - faça o login novamente".
4. **Dado** um operador autenticado, **Quando** a atualização da própria conta é bem-sucedida,
   **Então** a resposta é `200` com a mensagem "Usuário atualizado com sucesso" e os mesmos dados
   de hoje (identificador e e-mail).
5. **Dado** o teste de compatibilidade de contrato da Api, **Quando** a suíte roda, **Então** as
   únicas expectativas alteradas são as do update-me sem campos (agora "Requisição inválida") e do
   update-me com sucesso (agora "Usuário atualizado com sucesso"), o comentário que justificava o
   erro de digitação foi atualizado, e todos os outros cenários do teste permanecem como estavam.
6. **Dado** o `tasks.md` desta spec, **Quando** o revisor o lê, **Então** as duas alterações do
   teste de contrato estão listadas com o motivo (a spec mudou o comportamento esperado).
7. **Dado** o console, **Quando** ele recebe qualquer uma das três respostas, **Então** exibe a
   mensagem exatamente como veio (no `401` o console segue usando o aviso próprio de sessão
   encerrada).

---

### História de Usuário 4 - O sistema sobe sem a `PasswordSaltKey` (Prioridade: P2)

Quem configura o ambiente deixa de precisar inventar um valor para uma chave que não serve para
nada. A configuração some do sistema, dos exemplos e da documentação.

**Por que esta prioridade**: uma configuração obrigatória e sem uso induz quem lê a achar que ela
protege as senhas e induz código novo a depender dela. O custo de manter é pequeno, mas o risco de
confusão cresce a cada spec.

**Teste Independente**: partir de um `.env` sem `PASSWORD_SALT_KEY`, subir o ambiente de
desenvolvimento e fazer login com um operador sintético.

**Cenários de Aceitação**:

1. **Dado** um clone limpo e um `.env` sem `PASSWORD_SALT_KEY`, **Quando** o ambiente local sobe,
   **Então** ele sobe sem erro de variável ausente e o login funciona.
2. **Dado** a Api iniciada sem a configuração `Secrets:PasswordSaltKey`, **Quando** ela inicia,
   **Então** inicia normalmente (a única exigência de segredo continua sendo a chave do JWT).
3. **Dado** o arquivo de configuração versionado, o exemplo de variáveis, o compose e o README da
   raiz, **Quando** alguém os lê, **Então** nenhum cita `PasswordSaltKey` nem `PASSWORD_SALT_KEY`,
   e as instruções de configuração listam só o que é usado.
4. **Dado** quem ainda tem `PASSWORD_SALT_KEY` no `.env` local, **Quando** sobe o ambiente,
   **Então** nada quebra: o valor sobrando é ignorado.
5. **Dado** operadores já cadastrados, **Quando** fazem login depois da remoção, **Então** entram
   com as mesmas senhas de antes (a chave nunca participou do hash).

---

### Casos Limite

- **Booleano que não pode inverter**: o `EmailExistsAsync` das atualizações continua devolvendo
  `true` quando o e-mail pertence a **outro** operador (o próprio operador, ao reenviar o seu
  e-mail, não conta). Um substituto de teste configurado com o nome antigo não pode passar
  despercebido: o código deixa de compilar até todos os usos migrarem.
- **Nome citado só em texto**: nome antigo em nome de teste, comentário XML ou documentação
  vigente também é atualizado; não pode sobrar nome antigo "só no texto".
- **Texto fora de tela**: comentário, identificador (nome de classe, de rota do front, id de
  âncora) e mensagem técnica que nunca chega ao operador não entram na correção de acentos.
- **Texto da API**: o front exibe a `message` e as `notifications` como vieram, com ou sem
  acento. Corrigir o front não reescreve texto da API, e corrigir a API não reescreve o front.
- **Variável antiga no ambiente**: `PASSWORD_SALT_KEY` no `.env` local, ou `Secrets__PasswordSaltKey`
  injetada por fora, é ignorada sem erro nem aviso.
- **Migrations**: nenhuma migration é criada nem editada (a `SeedAdmin` continua como está, por
  spec própria). Esta entrega não mexe no banco.
- **Teste que "fica verde por acaso"**: nenhuma renomeação pode ser feita com busca e troca
  cega de palavras; cada ocorrência é conferida no contexto (por exemplo, `AnyAsync` do EF Core é
  outra coisa, e continua como está).
- Se a varredura de acentos achar um texto de interface fora do inventário B, ele entra na
  correção; se achar um comportamento que pareça bug, é registrado como achado e **não** é
  corrigido nesta spec.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Nomes de repositório e de domínio**

- **FR-001**: Todo método dos contratos de repositório dos use cases do `AccountContext`, e das
  suas implementações, DEVE seguir a convenção do Princípio III: leitura por chave
  `Get{Entidade}By{Campo}Async`, lista `Get{Entidade}ListAsync`, pergunta `{Algo}ExistsAsync` ou
  `Is{Algo}Async`, escrita `{Ação}Async`, sempre com o sufixo `Async`.
- **FR-002**: O nome de cada método DEVE refletir a entidade que ele realmente devolve (operador,
  não "usuário").
- **FR-003**: Todo método booleano DEVE responder exatamente o que o nome pergunta. Nenhum handler
  PODE mudar a decisão que toma por causa da renomeação (conflito de e-mail continua `409`).
- **FR-004**: Os seis itens do inventário A DEVEM ser renomeados para os nomes esperados, e a
  entidade de usuário do domínio DEVE usar o par `Activate()` / `Deactivate()`.
- **FR-005**: A renomeação DEVE ser completa: contratos, implementações, handlers, testes
  (substitutos e testes de integração) e qualquer documentação vigente que cite o nome antigo. Não
  DEVE sobrar nenhuma ocorrência dos nomes antigos no código nem na documentação vigente (os
  registros históricos citados nas Premissas ficam fora).
- **FR-006**: Todo teste cujo nome cita um método renomeado DEVE passar a citar o nome novo,
  preservando a frase de comportamento. Asserções, dados e cenários dos testes NÃO DEVEM mudar.
- **FR-007**: Todo método renomeado DEVE continuar exercitado por ao menos um teste. Como
  `User.Active()` não tem hoje chamador nem teste, DEVE ser escrito um teste de que `Activate()`
  reativa um usuário desativado, **antes** da renomeação, vendo-o falhar (TDD).

**Textos de interface do frontend**

- **FR-008**: Todo texto exibido ao usuário no frontend DEVE ter acentuação correta do português
  do Brasil. Isso inclui texto visível, nomes acessíveis (`aria-label`, `title`, texto só para
  leitor de tela), mensagens de erro geradas pelo próprio front e avisos.
- **FR-009**: Os 17 textos do inventário B DEVEM ser corrigidos, e uma varredura completa do código
  de produção do front (telas, componentes, contextos e funções utilitárias) DEVE confirmar que
  não resta outro texto de interface sem acento.
- **FR-010**: A correção NÃO DEVE alterar fluxo, rota, ordem de elementos, estado
  habilitado/desabilitado, nem o texto vindo da API (exibido como veio). Identificadores,
  caminhos de rota e ids de âncora NÃO mudam.
- **FR-011**: Os testes do front que afirmam textos corrigidos DEVEM ser ajustados **primeiro**
  para o texto acentuado, vistos falhando, e só então o código é corrigido. Só o texto esperado
  muda, nunca a lógica nem a forma da consulta; cada teste alterado DEVE estar listado no
  `tasks.md` com o motivo ("texto esperado mudou por esta spec").

**Mensagem da atualização da própria conta**

- **FR-012**: As três mensagens de API com erro de escrita DEVEM ser corrigidas, só na grafia e
  sem mudar mais nada da redação: o `400` de validação do update-me, de "Requsição inválida" para
  "Requisição inválida" (idêntica à dos demais endpoints); o `401` do get-me e do update-me, de
  "Sessão invalida - faça o login novamente" para "Sessão inválida - faça o login novamente"; e o
  `200` do update-me, de "Usuario atualizado com sucesso" para "Usuário atualizado com sucesso".
  Status, notificações, dados e formato do envelope NÃO mudam.
- **FR-013**: O teste de compatibilidade de contrato da Api DEVE mudar somente as duas
  expectativas que hoje afirmam o erro (update-me `400` e update-me `200`) e o comentário que o
  descrevia como erro "desde sempre"; nenhum outro cenário do teste muda. As alterações DEVEM
  estar listadas no `tasks.md` com o motivo (a spec mudou o comportamento esperado), conforme
  "Integridade das entregas" da constituição.
- **FR-014**: Nenhuma outra mensagem (além das três do FR-012), status, rota ou campo JSON de
  qualquer endpoint DEVE mudar.
  Os comentários XML dos endpoints, `src/Frontend/src/types/api.ts` e a tabela de endpoints do
  `src/Frontend/README.md` DEVEM continuar corretos sem precisar de alteração.

**Remoção da `PasswordSaltKey`**

- **FR-015**: O sistema (Api, Migrator e ambiente local do compose) DEVE iniciar normalmente sem a
  configuração `Secrets:PasswordSaltKey` e sem a variável `PASSWORD_SALT_KEY`. O compose NÃO DEVE
  exigi-la.
- **FR-016**: `PasswordSaltKey` / `PASSWORD_SALT_KEY` DEVE ser removida do arquivo de configuração
  versionado, do exemplo de variáveis, do compose e do README da raiz (instruções de preenchimento
  e lista de variáveis reconhecidas). Nenhum código nem teste pode referenciá-la.
- **FR-017**: Ter essa variável no `.env` local ou no ambiente NÃO DEVE causar erro nem mudar
  comportamento: o valor sobrando é ignorado.
- **FR-018**: A remoção NÃO DEVE afetar a geração nem a verificação de senhas: as senhas de
  operadores existentes continuam válidas.

**Integridade da entrega**

- **FR-019**: Esta entrega NÃO DEVE apagar, pular nem enfraquecer nenhum teste. As únicas
  alterações em testes existentes são nomes de método (FR-006), textos esperados (FR-011 e
  FR-013) e asserções acrescentadas (FR-021), todas listadas no `tasks.md` com o motivo; o único
  teste novo é o do FR-007.
- **FR-020**: Esta entrega NÃO DEVE criar nem editar migration, adicionar ou atualizar pacote, nem
  editar a constituição.
- **FR-021**: Todo texto corrigido por esta spec (os 3 de API do FR-012 e os 17 de interface do
  FR-009) DEVE ser afirmado por ao menos um teste. Onde hoje nenhum teste o afirma, a asserção do
  texto corrigido DEVE ser **acrescentada a um teste existente** (sem criar teste novo), escrita
  **antes** da correção e vista falhando (TDD). São 4 testes existentes ao todo:
  - o `401` de sessão inválida, nos testes de handler de minha conta e de atualizar a própria conta;
  - o texto de apoio do login ("O uso deste sistema é limitado a operadores autorizados."), no teste
    existente da tela de login que mostra o formulário em branco;
  - o cabeçalho "Ações" da tabela, no teste existente da lista de operadores que carrega a primeira
    página.

  Acrescentar uma verificação não enfraquece teste algum.

### Entidades-Chave

- **Método de repositório**: operação de consulta ou escrita de um use case (contrato na
  Application, implementação na Infrastructure). Seu nome é a documentação que a próxima pessoa, ou
  IA, copia.
- **Método de domínio**: operação com nome de intenção numa entidade (`Activate`, `Deactivate`,
  `UpdateEmail`...). As transições de ativação formam sempre o par `Activate()` / `Deactivate()`.
- **Texto de interface**: tudo o que o operador lê ou ouve no console (visível ou só para leitor de
  tela). Não inclui comentário, identificador nem rota.
- **Mensagem da API**: texto do envelope de resposta (`message`) que o console exibe como veio.
- **Configuração de segredo**: valor sensível lido do ambiente (hoje, a chave do JWT; a
  `PasswordSaltKey` é a que deixa de existir).

### Inventário inicial dos itens a alinhar

Levantamento feito no código de 2026-11-01 (o 17º texto do inventário B, o resumo da paginação,
foi apontado pelo dono na revisão do plano). É o ponto de partida: a varredura completa (FR-005 e
FR-009) pode achar mais ocorrências, que entram na entrega.

**A. Nomes do backend**

| Item atual | Onde | Problema | Nome esperado |
|------------|------|----------|---------------|
| `GetUserById` | ativar, desativar, minha conta, atualizar, atualizar a própria conta | devolve `Operator`, não "usuário"; sem `Async` | `GetOperatorByIdAsync` |
| `GetUserByEmailAsync` | login | devolve `Operator`, não "usuário" | `GetOperatorByEmailAsync` |
| `AnyAsync(email)` | criar | pergunta fora do formato `{Algo}ExistsAsync` | `EmailExistsAsync` |
| `CheckIfEmailIsNotUsed(email, id)` | atualizar, atualizar a própria conta | o nome diz "não usado", mas devolve `true` quando o e-mail **está** em uso por outro operador | `EmailExistsAsync(email, exceptId)` |
| `GetOperatorList` | listar | sem `Async` | `GetOperatorListAsync` |
| `User.Active()` | domínio, contexto de usuário | verbo fora do par `Activate()` / `Deactivate()` | `Activate()` |

**B. Textos de interface do front sem acento**

| Texto atual | Onde aparece | Texto corrigido |
|-------------|--------------|-----------------|
| "Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente." | erro de rede de qualquer tela | "Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente." |
| "Sua sessao foi encerrada. Faca login novamente." | aviso na tela de login | "Sua sessão foi encerrada. Faça login novamente." |
| "O uso deste sistema e limitado a operadores autorizados." | rodapé do login | "O uso deste sistema é limitado a operadores autorizados." |
| "Inicio" | título da tela inicial e item do menu | "Início" |
| "As informacoes desta pagina serao adicionadas conforme os servicos forem disponibilizados." | tela inicial | "As informações desta página serão adicionadas conforme os serviços forem disponibilizados." |
| "Pagina nao encontrada" | título da tela de endereço inexistente | "Página não encontrada" |
| "O recurso solicitado nao existe ou foi movido." | tela de endereço inexistente | "O recurso solicitado não existe ou foi movido." |
| "Operacao" | grupo do menu | "Operação" |
| "Navegacao principal" | nome acessível do menu | "Navegação principal" |
| "Ir para o conteudo" | link de pular a navegação | "Ir para o conteúdo" |
| "Encerrar sessao" | botão do topo | "Encerrar sessão" |
| "Acoes" | cabeçalho (só leitor de tela) da lista de operadores | "Ações" |
| "Itens por pagina" | seletor da lista de operadores | "Itens por página" |
| "{total} operador(es) · pagina {atual} de {total de páginas}" | resumo da paginação da lista de operadores | "{total} operador(es) · página {atual} de {total de páginas}" |
| "Proxima" | botão de paginação | "Próxima" |
| "A conta deixara de conseguir acessar o sistema." | confirmação de desativar operador | "A conta deixará de conseguir acessar o sistema." |
| "Salvar alteracoes" | botão de envio da edição de operador e de "Minha conta" | "Salvar alterações" |

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: 100% dos testes existentes do backend (inclusive os de integração) e do frontend
  passam, e a quantidade de testes é a mesma de antes, mais apenas o teste novo do FR-007 (o FR-021
  só acrescenta asserções a testes existentes).
- **SC-002**: A busca textual pelos nomes antigos do inventário A retorna 0 ocorrências no código
  e na documentação vigente (fora dos registros históricos das Premissas).
- **SC-003**: 0 métodos de contrato de repositório do `AccountContext` fora da convenção, e 100%
  dos métodos booleanos respondem ao que o nome pergunta.
- **SC-004**: 0 formas sem acento do inventário B (e de qualquer outra achada na varredura) nos
  textos de interface do front; 100% dos testes que afirmam esses textos passam a afirmar a forma
  correta; e 100% dos 17 textos de interface passam a ser afirmados por ao menos um teste (FR-021).
- **SC-005**: Todas as respostas dos endpoints são idênticas às de antes, exceto o texto das três
  mensagens do FR-012: o teste de compatibilidade de contrato passa com **exatamente duas**
  expectativas alteradas (update-me `400` e update-me `200`); o texto da sessão inválida não era
  afirmado e passa a ser, pelo FR-021.
- **SC-006**: O ambiente local sobe do zero com um `.env` sem `PASSWORD_SALT_KEY`, e a busca por
  `PasswordSaltKey` / `PASSWORD_SALT_KEY` na configuração e na documentação vigentes retorna 0
  ocorrências.
- **SC-007**: 0 alterações de teste não declaradas: o diff dos testes existentes contém apenas
  troca de nome de método, de texto esperado e asserção acrescentada (FR-021), e cada uma está
  listada no `tasks.md` com o motivo.
- **SC-008**: Os quality gates da constituição passam: build do backend, testes do backend (com
  integração), verificação de tipos, testes e compilação do front.

## Premissas

- **Depende** das specs 01 (multi-tenant-foundation) e 02 (frontend-testing), já entregues. A 02
  fixou os textos sem acento **como estavam**, deixando a correção para esta spec (achado A-04).
- **Escopo das mensagens de API** (clarificado em 2026-11-01): além de "Requsição inválida", os
  outros dois textos com erro de acentuação ("Sessão invalida - faça o login novamente" e
  "Usuario atualizado com sucesso") também são corrigidos, só na grafia. Fica fora a divergência
  de redação entre "Email já está em uso" (handlers) e "E-mail já está em uso" (repositório): não
  é erro de acento, e unificá-la mudaria a mensagem de um `409`.
- **Achados da spec 02 que mudam comportamento ficam de fora** (confirmado em 2026-11-01): A-11 (sucesso HTTP com corpo que
  não é JSON tratado como sucesso), A-12 (criar, editar e "Minha conta" com sucesso e `data` nula
  não mostram nada) e A-14 (`ThemeProvider` chama `matchMedia` sem `?.` no efeito). Só A-04
  (acentuação) é desta spec.
- **Fora da correção de acentos**: comentários (a constituição os permite sem acento),
  identificadores e caminhos de rota do front (`/minha-conta`, `/operadores`, o id de âncora do
  conteúdo) e a mensagem técnica de `main.tsx` ("Elemento #root nao encontrado..."), que só chega
  ao console do navegador de quem desenvolve, nunca ao operador.
- **Mudar o texto não é mudança incompatível de contrato** (Princípio VI): a lista de mudanças
  incompatíveis é campo, tipo, status e rota. Minha conta e a atualização da própria conta são
  usadas só pelo console, que exibe a mensagem como veio (no `401` ele usa o aviso próprio de
  sessão encerrada); aparelhos e integrações externas não as usam.
- **`SaveAsync` (criar) e `IsLastActiveAdminAsync` (desativar) ficam** (`SaveAsync` confirmado em
  2026-11-01): ambos já seguem a letra da convenção (`{Ação}Async` e `Is{Algo}Async`), então não
  estão no inventário A. A segunda será reescrita pela spec de roles e permissões (08).
- **Registros históricos não são reescritos**: os artefatos das specs já entregues (por exemplo,
  `contracts/configuration.md`, `research.md` e `tasks.md` da spec 01, que citam a
  `PasswordSaltKey` e os nomes antigos) descrevem o que foi decidido na época. As migrations
  também não mudam. A constituição também não é editada pela entrega ("Integridade das
  entregas").
- **Legado conhecido**: ao entrar na `main`, os itens da seção "Legado conhecido" que esta spec
  resolve (nomes de repositório do `AccountContext`, `User.Active()`, `Secrets:PasswordSaltKey` e,
  quanto ao front, os textos sem acento) passam a poder sair da lista. Isso é emenda da
  constituição, feita pelo dono via `/speckit-constitution` depois do merge, fora desta entrega.
- **Sem mudança de pacotes**: o resultado do `npm audit` e da verificação de pacotes vulneráveis é
  o mesmo de antes (as 2 vulnerabilidades *high* do front conhecidas ficam para a spec 04).
- **Dados**: nenhum dado pessoal é coletado, movido ou exibido de forma nova; auditoria, tenant e
  autorização não são tocados. O desempenho e a escala não mudam (renomeação e textos), então esta
  spec não cria nem altera caminho crítico e não exige cenário k6.
