# Especificação da Feature: Fundação Multi-Tenant

**Branch da Feature**: `20261031-221333-multi-tenant-foundation`

**Criada em**: 2026-10-31

**Status**: Aprovada

**Entrada**: Descrição do usuário: "Preparar o sistema para atender de centenas a 150 mil clientes (tenants), sem mudar o comportamento que os operadores veem hoje. O sistema nasceu para um único cliente (uma base); agora é multi-tenant num modelo híbrido: por padrão vários tenants compartilham uma mesma base, isolados entre si; um tenant pode ganhar base dedicada sem mudança nas regras de negócio. Esta spec cria a fundação; o login com escolha de tenant é uma spec posterior."

## Clarificações

### Sessão 2026-10-31

- Q: Ao adotar a fundação multi-tenant, o que deve acontecer com o administrador de seed (`admin@admin.com.br`, senha fixa) que já existe em bases de ambientes que não são de desenvolvimento? → A: Hoje não existe nenhum ambiente fora de desenvolvimento, então não há base real a adaptar. O administrador de seed só existe em desenvolvimento. Nenhuma base criada fora de desenvolvimento (homologação, produção ou bases de tenant criadas pelo provisionamento) pode recebê-lo; nesses ambientes o primeiro operador vem do provisionamento (em homologação, do script de dados sintéticos).
- Q: Como o desenvolvedor faz login em cada um dos três tenants do ambiente de desenvolvimento, já que o login usa um único tenant padrão por configuração? → A: Uma Api por tenant de teste: o comando único sobe três instâncias da Api, cada uma com o seu tenant padrão e em porta distinta.

## Cenários de Usuário e Testes *(obrigatório)*

Os "usuários" desta feature são: o **operador** (quem usa o sistema hoje e não deve notar mudança), o **administrador da plataforma** (quem opera os ambientes, atualiza as bases e movimenta tenants) e o **desenvolvedor** (quem precisa testar o isolamento localmente).

### História de Usuário 1 - Cada requisição enxerga apenas os dados do seu tenant (Prioridade: P1)

Um operador autenticado só vê e só altera dados do tenant ao qual o seu token pertence. Isso vale tanto quando o tenant divide a base com outros quanto quando tem base própria. O operador não percebe nenhuma diferença no uso do sistema.

**Por que esta prioridade**: é a razão de existir da feature. Sem isolamento, um cliente poderia ler ou alterar dados de outro, o que num sistema de controle de acesso físico é uma falha de segurança grave. Todas as demais histórias dependem dela.

**Teste Independente**: criar dois tenants na mesma base e um terceiro numa base dedicada, cada um com operadores próprios. Autenticar em cada um e confirmar que listagens, consultas por identificador e alterações só alcançam os dados do próprio tenant. Entrega valor sozinha: o isolamento já pode ser demonstrado e auditado.

**Cenários de Aceitação**:

1. **Dado** dois tenants A e B na mesma base compartilhada, cada um com operadores, **Quando** um operador de A lista os operadores, **Então** vê somente os de A.
2. **Dado** os mesmos tenants A e B, **Quando** um operador de A tenta consultar, alterar, ativar ou desativar um operador de B usando o identificador dele, **Então** recebe a mesma resposta que receberia para um identificador inexistente (nunca a confirmação de que o registro existe em outro tenant).
3. **Dado** um tenant C numa base dedicada, **Quando** um operador de C faz as mesmas operações, **Então** o comportamento é idêntico ao dos tenants da base compartilhada.
4. **Dado** que o código de uma consulta esqueça de filtrar por tenant (simulado em teste), **Quando** a consulta é executada no contexto do tenant A, **Então** o próprio banco de dados devolve apenas as linhas de A.
5. **Dado** uma tentativa de gravar uma linha com o identificador de outro tenant na mesma base, **Quando** a gravação é executada no contexto do tenant A, **Então** o banco de dados a recusa.
6. **Dado** uma conexão em que nenhum tenant foi informado ao banco, **Quando** qualquer consulta a dados de tenant é executada, **Então** nenhuma linha é devolvida nem alterada (falha fechada).

---

### História de Usuário 2 - O token identifica o tenant e tokens inválidos são rejeitados (Prioridade: P1)

Todo token emitido passa a identificar o tenant do operador. Um token sem tenant, ou de um tenant que não existe ou está inativo, é rejeitado com `401` e a mesma mensagem genérica de hoje. O fluxo de login atual continua igual: enquanto o login com escolha de tenant não existir, o login usa o tenant padrão definido em configuração.

**Por que esta prioridade**: o tenant do token é a única fonte de verdade para escolher a base e o contexto de isolamento. Sem esta história, a História 1 não tem como saber de qual tenant é a requisição.

**Teste Independente**: fazer login com credenciais válidas do tenant padrão e confirmar que o token devolvido identifica esse tenant e funciona como antes. Depois, apresentar tokens sem tenant, com tenant inexistente e com tenant inativo e confirmar `401` com a mensagem genérica.

**Cenários de Aceitação**:

1. **Dado** um operador ativo do tenant padrão, **Quando** ele faz login com e-mail e senha corretos, **Então** o login responde exatamente como hoje (mesmos campos e mesmo status) e o token emitido identifica o tenant padrão.
2. **Dado** um token válido sem identificação de tenant, **Quando** ele é usado em qualquer endpoint autenticado, **Então** a resposta é `401` com a mensagem genérica de hoje (`"Não autenticado"`).
3. **Dado** um token que identifica um tenant que não existe no catálogo, **Quando** ele é usado, **Então** a resposta é `401` com a mesma mensagem genérica.
4. **Dado** um token de um tenant que foi marcado como inativo no catálogo, **Quando** ele é usado, **Então** a resposta é `401` com a mesma mensagem genérica, sem que a resposta revele o motivo.
5. **Dado** um tenant que estava ativo e foi inativado, **Quando** um operador dele, que já tinha um token ainda dentro da validade, faz a próxima requisição, **Então** a requisição é rejeitada com `401`.
6. **Dado** tokens emitidos antes desta mudança (sem identificação de tenant), **Quando** são apresentados, **Então** são rejeitados com `401` e o operador precisa fazer login de novo.

---

### História de Usuário 3 - Os dados existentes passam a pertencer a um tenant inicial (Prioridade: P1)

Os dados que o sistema já guarda hoje (um único cliente, uma única base, hoje existente apenas em desenvolvimento) passam a pertencer a um tenant inicial, registrado no catálogo, sem perda de nenhum registro. Os operadores atuais continuam entrando com o mesmo e-mail e a mesma senha.

**Por que esta prioridade**: sem ela não há como adotar a feature em um ambiente que já tem dados. É o que garante "sem mudar o comportamento que os operadores veem hoje".

**Teste Independente**: partir de uma base no formato atual com operadores cadastrados, aplicar a atualização e confirmar que todos os operadores existem, pertencem ao tenant inicial, e conseguem fazer login com as mesmas credenciais.

**Cenários de Aceitação**:

1. **Dado** uma base no formato atual com N operadores, **Quando** a atualização desta feature é aplicada, **Então** a base continua com os mesmos N operadores, todos pertencentes ao tenant inicial, sem nenhum campo alterado além do vínculo ao tenant.
2. **Dado** a atualização aplicada, **Quando** um operador existente faz login com as credenciais de antes, **Então** o login funciona e o token identifica o tenant inicial.
3. **Dado** a atualização aplicada, **Quando** o catálogo é consultado, **Então** o tenant inicial existe, está ativo e aponta para a base em que os dados estão.

---

### História de Usuário 4 - E-mail do operador único dentro do tenant (Prioridade: P2)

O e-mail do operador deixa de ser único no sistema inteiro e passa a ser único dentro de cada tenant. Tenants diferentes podem ter operadores com o mesmo e-mail, cada um com a sua própria senha.

**Por que esta prioridade**: é consequência direta do modelo multi-tenant (a mesma pessoa pode operar para mais de um cliente), mas só faz sentido depois que o isolamento e a identificação do tenant existem.

**Teste Independente**: cadastrar o mesmo e-mail em dois tenants diferentes (sucesso) e tentar cadastrá-lo duas vezes no mesmo tenant (conflito).

**Cenários de Aceitação**:

1. **Dado** um operador `maria@exemplo.com` no tenant A, **Quando** se cadastra `maria@exemplo.com` no tenant B, **Então** o cadastro é aceito e os dois operadores são independentes (senhas, situação e role próprias).
2. **Dado** um operador `maria@exemplo.com` no tenant A, **Quando** se tenta cadastrar `maria@exemplo.com` de novo no tenant A, **Então** a resposta é `409` com a mesma mensagem de e-mail em uso de hoje.
3. **Dado** o mesmo e-mail em dois tenants da mesma base, **Quando** um deles altera o próprio e-mail ou sua situação, **Então** o outro não é afetado.

---

### História de Usuário 5 - Atualização de todas as bases por um passo separado (Prioridade: P2)

A Api deixa de atualizar o banco ao iniciar. A atualização da estrutura passa a ser um passo separado, executado pelo administrador da plataforma, que atualiza todas as bases (compartilhadas e dedicadas) em lotes, pode ser retomado se falhar no meio e registra em que versão cada base está.

**Por que esta prioridade**: com muitas bases, a Api não pode ser quem as atualiza (cada instância tentaria atualizar todas, e a Api precisaria de permissões de alteração de estrutura). É pré-requisito para operar em escala, mas não muda o comportamento visto pelo operador.

**Teste Independente**: com várias bases em versões diferentes, executar o passo; interromper ou forçar a falha de uma das bases; executar de novo e conferir que só o que faltava foi feito e que o registro de versão por base está correto.

**Cenários de Aceitação**:

1. **Dado** várias bases (compartilhadas e dedicadas) em versões antigas, **Quando** o passo de atualização é executado, **Então** todas chegam à versão mais recente e o registro mostra, para cada base, a versão em que está.
2. **Dado** que a atualização falhou numa das bases, **Quando** o passo termina, **Então** as demais bases foram atualizadas, o resultado final aponta quais bases falharam e a execução sinaliza falha.
3. **Dado** uma execução interrompida no meio, **Quando** o passo é executado novamente, **Então** as bases já atualizadas não são refeitas e só as pendentes ou com falha são processadas.
4. **Dado** que a Api é iniciada, **Quando** ela sobe, **Então** não altera a estrutura de nenhuma base; se uma base estiver desatualizada, a defasagem fica visível no registro de versões por base (FR-023), sem a Api tentar corrigir.
5. **Dado** uma base nova (compartilhada ou dedicada) cadastrada no catálogo, **Quando** o passo é executado, **Então** ela também é atualizada, sem alteração no passo.

---

### História de Usuário 6 - Ambiente de desenvolvimento com os dois cenários de base (Prioridade: P2)

Um desenvolvedor sobe, com um único comando, um ambiente local com o catálogo, uma base compartilhada com dois tenants, uma base dedicada com um tenant e uma Api para cada um dos três tenants (cada uma com o seu tenant padrão), já com dados sintéticos, para testar o isolamento nos dois casos. O administrador criado com valores fixos (seed) só existe nesse ambiente de desenvolvimento.

**Por que esta prioridade**: sem um ambiente reproduzível, o isolamento só poderia ser verificado em produção. Ele sustenta os testes das histórias anteriores e a revisão de segurança.

**Teste Independente**: em uma máquina limpa, rodar o comando único e, sem nenhum passo manual extra, conseguir autenticar em cada um dos três tenants (cada um na sua Api) e confirmar o isolamento entre os dois que dividem a base e a separação em relação ao dedicado.

**Cenários de Aceitação**:

1. **Dado** uma máquina de desenvolvimento limpa, **Quando** o comando único é executado, **Então** sobem o catálogo, uma base compartilhada com dois tenants, uma base dedicada com um tenant e três instâncias da Api (uma por tenant, cada uma com o seu tenant padrão e em porta distinta), todos prontos para uso e já atualizados para a versão mais recente.
2. **Dado** o ambiente de desenvolvimento no ar, **Quando** se inspeciona cada tenant, **Então** cada um tem operadores sintéticos, e ao menos um e-mail se repete entre tenants diferentes (para exercitar a unicidade por tenant).
3. **Dado** um ambiente que não seja de desenvolvimento, **Quando** uma base é criada do zero e percorre todo o histórico de atualizações, **Então** ela termina sem nenhum operador, em particular sem o administrador de seed.
4. **Dado** o ambiente de desenvolvimento, **Quando** o comando é executado uma segunda vez, **Então** não duplica tenants, bases nem operadores.

---

### História de Usuário 7 - Mover um tenant entre bases sem transformar dados (Prioridade: P3)

O administrador da plataforma consegue mover um tenant de uma base compartilhada para uma dedicada (e o contrário) copiando as linhas dele, sem converter nem recalcular nenhum dado. O comportamento do sistema para o tenant é o mesmo antes e depois.

**Por que esta prioridade**: é uma promessa de arquitetura (o tenant pode crescer para uma base própria), mas o procedimento de mover em produção pertence a outra frente. Aqui basta garantir que o modelo de dados permite e que isso foi demonstrado.

**Teste Independente**: copiar todas as linhas de um tenant de uma base compartilhada para uma base dedicada vazia, apontar o catálogo para a nova base e confirmar que o tenant funciona de forma idêntica (login, listagem, alteração).

**Cenários de Aceitação**:

1. **Dado** um tenant com dados numa base compartilhada, **Quando** suas linhas são copiadas como estão para uma base dedicada de mesma versão e o catálogo passa a apontar para ela, **Então** o tenant funciona de forma idêntica, com os mesmos identificadores de registro.
2. **Dado** o tenant movido, **Quando** operadores dos outros tenants da base de origem continuam usando o sistema, **Então** não percebem nenhuma alteração.
3. **Dado** o tenant movido, **Quando** o mesmo tenant é movido de volta para uma base compartilhada que já tem outros tenants, **Então** nenhum identificador de registro entra em conflito com os dos outros tenants.

---

### Casos de Borda

- **Tenant padrão do login ausente, inexistente ou inativo**: o login responde `401` genérico (indistinguível de credenciais inválidas para quem chama) e o problema de configuração é registrado no log da operação.
- **Token com identificador de tenant malformado** (não é um Guid): tratado como token sem tenant, `401`.
- **Tenant existe no catálogo, mas a base apontada está indisponível**: a requisição falha com erro de servidor (`500`, envelope padrão de erro) e nunca cai para outra base nem devolve dados de outro tenant.
- **Tenant inativado no meio de uma sessão**: a próxima requisição com o token dele é rejeitada com `401` (cenário 5 da História 2).
- **Dois tenants na mesma base com o mesmo identificador de registro**: não pode acontecer, porque os identificadores são gerados pela aplicação e são únicos globalmente; a cópia entre bases não gera conflito.
- **Duas execuções simultâneas do passo de atualização**: não podem atualizar a mesma base ao mesmo tempo nem corromper o registro de versões; a segunda aguarda ou termina sem fazer nada.
- **Base listada no catálogo mas inacessível durante a atualização**: é marcada como falha com o motivo, as demais seguem, e a próxima execução tenta de novo.
- **Atualização aplicada duas vezes sobre a mesma base**: é idempotente, sem erro e sem duplicar dados.
- **Mesmo CNPJ cadastrado duas vezes no catálogo**: recusado. Um CNPJ identifica um único tenant.
- **Base criada do zero fora de desenvolvimento, percorrendo todo o histórico de atualizações**: termina com 0 operadores, sem o administrador de seed (FR-031).

## Requisitos *(obrigatório)*

### Requisitos Funcionais

**Catálogo de tenants**

- **FR-001**: O sistema DEVE manter um catálogo central de tenants, separado das bases de dados dos tenants, onde cada tenant tem: identificador (Guid), CNPJ, nome, situação (ativo ou inativo) e a indicação de em qual base de dados ele está.
- **FR-002**: O CNPJ DEVE ser único no catálogo, e o identificador do tenant DEVE ser gerado pela aplicação (nunca pelo banco).
- **FR-003**: O catálogo DEVE conhecer todas as bases de dados do sistema (compartilhadas e dedicadas), distinguindo o tipo de cada uma, e DEVE registrar a versão da estrutura em que cada base está (ver FR-020).
- **FR-004**: O catálogo NÃO DEVE guardar senha, hash de senha nem credenciais de acesso às bases; ele identifica a base por uma referência, e o segredo de conexão vem da configuração do ambiente.

**Identificação do tenant e isolamento**

- **FR-005**: Toda requisição autenticada DEVE operar exclusivamente sobre o tenant identificado no token, sem que o tenant seja informado no corpo, na rota ou em parâmetros da requisição.
- **FR-006**: O sistema DEVE escolher a base de dados da requisição a partir do tenant e do catálogo, e o código de negócio NÃO DEVE precisar saber se a base é compartilhada ou dedicada.
- **FR-007**: Toda tabela de dados de tenant DEVE ter o vínculo com o tenant, obrigatório, preenchido pelo sistema a partir do tenant da requisição, nunca informado por quem chama.
- **FR-008**: O isolamento entre tenants que dividem a mesma base DEVE ser imposto em duas camadas independentes: pela aplicação (filtro em toda consulta) e pelo próprio banco de dados (regra de segurança por linha que vale mesmo sem o filtro da aplicação).
- **FR-009**: A regra do banco DEVE valer também para a conta que a aplicação usa para acessar o banco: essa conta não pode ter privilégios que permitam contornar a regra de segurança por linha.
- **FR-010**: O tenant corrente DEVE valer somente para a operação em curso, sem vazar para outras requisições que reutilizem a mesma conexão, e uma operação sem tenant informado NÃO DEVE enxergar nem alterar nenhuma linha de tenant.
- **FR-011**: O banco DEVE recusar a gravação de uma linha cujo tenant seja diferente do tenant corrente.
- **FR-012**: Um tenant NÃO DEVE conseguir confirmar a existência de um registro de outro tenant: o acesso a ele por identificador responde como registro inexistente.
- **FR-013**: Não DEVE existir estado compartilhado em memória (estático, singleton ou cache) que guarde dados ou a base escolhida de algum tenant.

**Token e autenticação**

- **FR-014**: Todo token emitido DEVE identificar o tenant do operador.
- **FR-015**: Um token sem identificação de tenant, com identificação malformada, de tenant inexistente ou de tenant inativo DEVE ser rejeitado com `401` e a mesma mensagem genérica de hoje, sem revelar o motivo da rejeição.
- **FR-016**: A situação do tenant DEVE ser verificada a cada requisição, de modo que inativar um tenant corta o acesso dele na requisição seguinte.
- **FR-017**: Enquanto o login com escolha de tenant não existir, o login DEVE usar o tenant padrão definido em configuração. Os campos, o status e as mensagens do login permanecem como hoje.
- **FR-018**: A verificação de que o token, o tenant e o operador batem DEVE continuar acontecendo a cada requisição, como hoje, agora no contexto do tenant do token.

**Operadores**

- **FR-019**: O e-mail do operador DEVE ser único dentro do tenant e NÃO DEVE ser único no sistema todo; tenants diferentes podem ter operadores com o mesmo e-mail. A mensagem e o status `409` de e-mail em uso permanecem como hoje.

**Atualização das bases**

- **FR-020**: A Api NÃO DEVE alterar a estrutura de nenhuma base ao iniciar, e o usuário de banco que ela usa NÃO DEVE ter permissão para isso.
- **FR-021**: A atualização da estrutura DEVE ser um passo separado da Api, que atualiza todas as bases do catálogo (compartilhadas e dedicadas), processando-as em lotes de tamanho limitado.
- **FR-022**: O passo de atualização DEVE ser retomável: ao ser executado de novo depois de uma falha ou interrupção, DEVE processar somente as bases que ainda não estão na versão mais recente.
- **FR-023**: O passo de atualização DEVE registrar, por base, a versão da estrutura em que ela está, a data da última tentativa e o resultado (sucesso ou falha com o motivo).
- **FR-024**: A falha de uma base NÃO DEVE impedir a tentativa de atualizar as demais; ao final, o passo DEVE informar quais bases falharam e sinalizar falha na sua execução.
- **FR-025**: O passo de atualização DEVE ser seguro contra execuções simultâneas e idempotente.
- **FR-026**: Nenhuma atualização de estrutura DEVE criar operador ou usuário com valores fixos em ambientes que não sejam de desenvolvimento.

**Dados existentes**

- **FR-027**: Os dados existentes de hoje DEVEM passar a pertencer a um tenant inicial, registrado no catálogo como ativo e apontando para a base em que os dados estão, sem perda, alteração ou duplicação de nenhum registro além do vínculo com o tenant.
- **FR-028**: O CNPJ e o nome do tenant inicial DEVEM vir de configuração do ambiente na hora da adoção, nunca de valores fixos no código.
- **FR-029**: O tenant padrão do login (FR-017) DEVE ser configurável, e o ambiente que adota esta feature DEVE poder apontá-lo para o tenant inicial.

**Administrador de desenvolvimento**

- **FR-030**: O administrador com e-mail e senha fixos (seed) DEVE existir somente quando o ambiente for de desenvolvimento. Em qualquer outro ambiente, bases novas NÃO DEVEM recebê-lo.
- **FR-031**: Nenhuma base criada fora do ambiente de desenvolvimento (homologação, produção ou bases de tenant criadas pelo provisionamento) DEVE receber o administrador de seed, inclusive quando percorre todo o histórico de atualizações do zero. Nesses ambientes, o primeiro operador de cada tenant vem do provisionamento e, em homologação, do script de dados sintéticos.

**Mobilidade do tenant**

- **FR-032**: O modelo de dados DEVE permitir mover um tenant entre uma base compartilhada e uma dedicada apenas copiando as linhas dele, sem transformar, recalcular ou renumerar nenhum dado, de modo que os identificadores de registro continuem únicos no destino.
- **FR-033**: Mover o tenant NÃO DEVE exigir nenhuma mudança no código ou nas regras de negócio.

**Ambiente de desenvolvimento**

- **FR-034**: DEVE existir um único comando que sobe, em desenvolvimento: o catálogo, uma base compartilhada com dois tenants, uma base dedicada com um tenant e uma Api para cada um dos três tenants (cada uma com o seu próprio tenant padrão, FR-017, e em portas distintas), já com a estrutura atualizada e dados sintéticos.
- **FR-035**: Os dados sintéticos de desenvolvimento DEVEM incluir operadores em cada tenant e ao menos um e-mail repetido entre tenants diferentes, e NÃO DEVEM conter dados de produção.
- **FR-036**: O comando de desenvolvimento DEVE poder ser executado mais de uma vez sem duplicar tenants, bases ou operadores.

**Compatibilidade**

- **FR-037**: O comportamento visível aos operadores (rotas, campos de requisição e resposta, status e mensagens) DEVE permanecer o mesmo de antes desta feature, com a única exceção da exigência de novo login para tokens emitidos antes dela.

### Entidades-Chave *(incluir se a feature envolver dados)*

- **Tenant**: o cliente da plataforma. Tem identificador (Guid), CNPJ (único no catálogo), nome, situação (ativo/inativo) e a base em que seus dados estão. Mora no catálogo.
- **Base de dados**: um repositório físico de dados de tenants. Pode ser **compartilhada** (vários tenants) ou **dedicada** (um tenant). Tem uma referência de conexão (sem segredo no catálogo), tipo e a versão da estrutura em que está. Mora no catálogo.
- **Registro de atualização por base**: histórico/estado da atualização de cada base: versão atual, data da última tentativa, resultado e motivo da falha. Alimenta a retomada do passo de atualização.
- **Operador**: quem usa o sistema (já existe). Passa a pertencer a exatamente um tenant; o e-mail é único dentro dele. Credenciais (hash de senha) ficam na base do tenant, nunca no catálogo.
- **Token de sessão**: credencial emitida no login. Passa a identificar o operador **e** o tenant dele.
- **Configuração de ambiente**: define o tenant padrão do login, os dados do tenant inicial e o ambiente em execução (desenvolvimento ou outro).

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: Em 100% dos testes de isolamento, nas duas topologias (dois tenants numa base compartilhada e um tenant em base dedicada), nenhuma operação de um tenant lê ou altera dado de outro, inclusive quando o filtro da aplicação é propositalmente omitido.
- **SC-002**: 100% das tentativas de uso de token sem tenant, com tenant malformado, inexistente ou inativo recebem `401` com a mensagem genérica de hoje, e nenhuma resposta revela qual dessas condições ocorreu.
- **SC-003**: Um operador do tenant padrão faz login com as mesmas credenciais de antes e usa todas as telas e ações atuais sem nenhuma diferença visível: 0 diferenças de contrato (rotas, campos, status, mensagens) nos testes de compatibilidade.
- **SC-004**: Na adoção em uma base de desenvolvimento com dados, a contagem de registros de cada tabela antes e depois é idêntica e 100% dos operadores existentes conseguem fazer login.
- **SC-005**: O mesmo e-mail pode ser cadastrado em N tenants diferentes (N ≥ 3 nos testes) e é recusado com `409` na segunda tentativa dentro do mesmo tenant.
- **SC-006**: Com 1.000 bases de teste em versões variadas, uma falha injetada em uma delas não impede a atualização das outras 999, e uma segunda execução processa somente as pendentes (0 bases já atualizadas são refeitas).
- **SC-007**: Em ambiente que não seja de desenvolvimento, 0 operadores com credenciais fixas são criados por qualquer passo de atualização ou de adoção.
- **SC-008**: Um desenvolvedor, numa máquina limpa, executa um único comando e em até 5 minutos consegue autenticar em cada um dos três tenants de teste, cada um na sua Api, sem nenhum passo manual extra.
- **SC-009**: Um tenant copiado de uma base compartilhada para uma dedicada funciona de forma idêntica após a mudança, sem nenhuma transformação de dados e sem alterar código, em 100% dos casos do teste de mobilidade.
- **SC-010**: Sob a carga de referência, os endpoints existentes mantêm p95 ≤ 300 ms e p99 ≤ 1 s, com taxa de erro 5xx < 0,1%, ou seja, a identificação do tenant e o isolamento no banco não degradam o desempenho que os operadores veem. *(Verificado na spec 06; fora desta entrega.)*

## Premissas

- O sistema atual tem uma única base e um único cliente; esse cliente vira o **tenant inicial**. Hoje não existe nenhum ambiente fora de desenvolvimento (nem homologação nem produção): o "dado existente" é o da base de desenvolvimento, e não há outro tenant antes desta feature.
- O catálogo é uma base própria e separada. Nesta feature, os tenants e as bases entram no catálogo somente pelo ambiente de desenvolvimento e pela adoção do tenant inicial. **Não há tela nem endpoint para cadastrar tenants**: isso pertence ao sistema de provisionamento, fora do escopo.
- O diretório de e-mails (e-mail → tenants) não existe ainda. Por isso o login depende do tenant padrão em configuração, e a escolha de tenant no login é uma spec posterior.
- Mover um tenant entre bases é, aqui, uma **propriedade do modelo de dados**, demonstrada em teste. O procedimento operacional de mover em produção (janela, verificação, troca no catálogo) fica fora desta feature.
- Os tokens são assinados com uma única chave global; por isso a verificação do tenant no catálogo é obrigatória a cada requisição (FR-015, FR-016).
- Nesta feature não há cache: a consulta ao catálogo acontece a cada requisição. O cache e o Redis entram na spec de fundação de escala. O `plan.md` precisa tratar o impacto no custo por requisição e no desempenho (Princípio XIII) e justificá-lo em Complexity Tracking.
- Esta é a migração fundadora do isolamento: a Api anterior não é compatível com bases que já têm o vínculo com o tenant e a regra de segurança por linha. Por isso a adoção exige substituir a Api e atualizar a base ao mesmo tempo. Como não existe ambiente fora de desenvolvimento, nenhuma instância antiga convive com as bases novas. Continua sendo a única exceção à regra de migrações compatíveis com a versão anterior (Princípio XIII), a ser justificada no `plan.md` (Complexity Tracking).
- O histórico atual de atualizações contém a migration `SeedAdmin`, que cria o administrador de seed, e uma migration já aplicada não deve ser editada (constituição). Cabe ao `plan.md` decidir como cumprir FR-030 e FR-031 diante disso.
- Ambiente de desenvolvimento é identificado pelo ambiente de execução da Api e das ferramentas, e o comando único usa a mesma ferramenta local de contêineres que o projeto já usa.
- Dados de desenvolvimento são sintéticos; nunca há cópia de dados de produção fora de produção (Princípios X e "Dados fora de produção").
- O catálogo pertence ao sistema de provisionamento: esta feature só lê o CNPJ, como texto, e não o valida (formato e dígitos verificadores são responsabilidade de quem cadastra o tenant). A feature garante apenas que ele seja único (FR-002).
- Esta feature não introduz dado pessoal novo: o tenant guarda dados da empresa (CNPJ, nome), e o operador continua guardando o mesmo e-mail e hash de senha de hoje.
- **Fora do escopo**: login com escolha de tenant, diretório de e-mails, sistema de provisionamento, roles dinâmicas, Redis e cache (spec de fundação de escala).
