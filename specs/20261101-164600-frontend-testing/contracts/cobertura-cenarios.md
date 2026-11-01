# Contrato: cobertura dos cenários da spec

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Mapa de rastreabilidade (FR-017, SC-002), conferido contra os testes reais na Fase D (T051). Cada
cenário aponta para o arquivo e para o título **exato** do `it(...)`. Os arquivos ficam ao lado do
código testado (`*.test.ts` / `*.test.tsx`); `App.test.tsx` usa o `App` real.

## Situações

| Situação | Significado |
|---|---|
| coberto | Há teste automatizado que exercita o cenário pela interface, como o operador o veria |
| coberto parcialmente — ver A-0X | Parte do cenário não é alcançável ou observável pela interface; o que falta está descrito no achado indicado em [research.md](../research.md) |
| pendente de validação no PR | Depende de executar o CI no GitHub; não há teste automatizado local (roteiro V6 do [quickstart.md](../quickstart.md)) |

**Resumo dos 49 cenários**: 43 cobertos · 3 cobertos parcialmente (1.1, 3.8, 7.5) · 3 pendentes de
validação no PR (1.3, 1.4, 1.5).

Os títulos de `describe`/`it` levam acentuação correta; só os textos de interface afirmados nos
testes (ex.: `Pagina nao encontrada`) ficam sem acento, como estão hoje.

## História 1 — Rodar a suíte e ser barrado por ela

| Cenário | Arquivo | Título do teste / verificação | Situação |
|---|---|---|---|
| 1.1 Roda sem API, banco, Docker ou internet | `test/server.test.ts` e execução de `npm test` | "não deixa a chamada sem handler sair para a rede" | coberto parcialmente — a ausência de rede real é garantida e testada pela trava; a execução **offline** é feita pelo revisor e não foi feita pelo implementador |
| 1.2 Chamada não prevista reprova o teste | `test/server.test.ts` (+ `afterEach` do `test/setup.ts`) | "registra a chamada que não tem handler"; "não registra chamadas que têm handler" | coberto |
| 1.3 CI roda tipos, testes e build em PR do front | `.github/workflows/frontend-ci.yml` | passos 4 a 6 do contrato do CI | pendente de validação no PR |
| 1.4 CI não roda sem mudança no front | `.github/workflows/frontend-ci.yml` | filtro de `paths` | pendente de validação no PR |
| 1.5 Falha de teste reprova o PR | `.github/workflows/frontend-ci.yml` | PR de teste com um `it` quebrado de propósito | pendente de validação no PR |

## História 2 — Login e sessão

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 2.1 Sucesso guarda a sessão e vai ao início | `pages/LoginPage.test.tsx` | "guarda a sessão e leva ao início quando o login tem sucesso" | coberto |
| 2.2 Sucesso volta à rota de origem | `pages/LoginPage.test.tsx`; `App.test.tsx` | "leva à rota de origem depois do login"; "leva à rota de origem depois do login de quem tentou abrir uma rota protegida sem sessão" | coberto |
| 2.3 Erro mostra `message` e `notifications` | `pages/LoginPage.test.tsx` | "mostra a message e as notifications quando a api devolve 401" | coberto |
| 2.4 Sucesso sem credencial usável | `pages/LoginPage.test.tsx` | "mostra erro inesperado e não cria sessão quando a resposta vem sem token"; "mostra erro inesperado e não cria sessão quando o sucesso vem sem dados" | coberto |
| 2.5 `401` autenticado derruba a sessão | `App.test.tsx`; `lib/api-client.test.ts`; `context/AuthContext.test.tsx` | "volta ao login com aviso de sessão encerrada quando uma chamada autenticada recebe 401"; "aciona o handler de não autorizado e lança ApiError quando uma chamada autenticada recebe 401"; "apaga a sessão e marca como expirada quando uma chamada autenticada recebe 401" | coberto |
| 2.6 Encerrar sessão manualmente | `components/layout/Topbar.test.tsx`; `App.test.tsx`; `context/AuthContext.test.tsx` | "apaga a sessão e vai ao login ao encerrar a sessão"; "apaga a sessão e vai ao login, sem o aviso de sessão encerrada, ao encerrar a sessão"; "signOut apaga a sessão sem marcar a sessão como expirada" | coberto |
| 2.7 Sessão existente sai do login | `pages/LoginPage.test.tsx`; `App.test.tsx` | "redireciona para o início quando já há sessão"; "redireciona para a rota de origem quando já há sessão e há origem"; "redireciona /login para o início quando já há sessão" | coberto |
| 2.8 Carregamento durante o envio | `pages/LoginPage.test.tsx` | "mostra o estado de carregamento e bloqueia o botão enquanto envia" | coberto |

## História 3 — Lista de operadores

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 3.1 Carga inicial com filtro Ativos | `pages/OperatorsListPage.test.tsx` | "carrega a primeira página com o filtro Ativos"; "mostra a situação Inativo para operadores inativos" | coberto |
| 3.2 Trocar filtro volta à página 1 | `pages/OperatorsListPage.test.tsx` | "pede Inativos e Todos e volta à página 1 ao trocar o filtro" | coberto |
| 3.3 Próxima/Anterior e limites | `pages/OperatorsListPage.test.tsx` | "navega entre páginas e desabilita Anterior na primeira e Próxima na última" | coberto |
| 3.4 Itens por página | `pages/OperatorsListPage.test.tsx` | "pede o novo tamanho e volta à página 1 ao mudar os itens por página"; "oferece os tamanhos de página 10, 25, 50 e 100" | coberto |
| 3.5 Lista vazia | `pages/OperatorsListPage.test.tsx` | "mostra a mensagem de lista vazia" | coberto |
| 3.6 Desativar com confirmação | `pages/OperatorsListPage.test.tsx` | "abre a confirmação com o e-mail, chama a api, fecha e recarrega ao desativar" | coberto |
| 3.7 Ativar com confirmação | `pages/OperatorsListPage.test.tsx` | "abre a confirmação com o e-mail, chama a api, fecha e recarrega ao ativar" | coberto |
| 3.8 Cancelar (botão e Esc) | `pages/OperatorsListPage.test.tsx`; `components/ui/ConfirmDialog.test.tsx` | "não chama a api nem recarrega ao cancelar pelo botão"; "não chama a api nem recarrega ao cancelar com Esc"; "entrega a decisão ao componente pai ao apertar Esc, sem fechar sozinho" | coberto parcialmente — ver A-10 (o botão está coberto; a tecla Esc é simulada pelo evento `cancel`, e o comportamento nativo do `<dialog>` não é exercitado no jsdom) |
| 3.9 Erro da API dentro da janela | `pages/OperatorsListPage.test.tsx`; `components/ui/ConfirmDialog.test.tsx` | "mantém a janela aberta com message e notifications e não recarrega quando a api recusa"; "limpa o erro anterior ao reabrir a janela"; "mostra a message e as notifications do erro dentro da janela" | coberto |
| 3.10 Erro ao carregar a lista | `pages/OperatorsListPage.test.tsx` | "mostra message e notifications no lugar da tabela quando a carga falha" | coberto |
| 3.11 Editar leva e-mail e perfil; Criar leva à criação | `pages/OperatorsListPage.test.tsx`; `App.test.tsx` | "leva o identificador, o e-mail e o perfil da linha ao link Editar"; "tem o link Criar operador apontando para a tela de criação"; "abre a edição já preenchida com os dados da linha clicada"; "abre a criação de operador pelo link Criar operador da lista" | coberto |

## História 4 — Navegação

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 4.1 Sem sessão vai ao login e lembra a origem | `App.test.tsx`; `components/RequireSession.test.tsx` | "redireciona %s para o login quando não há sessão" (5 rotas); "redireciona para o login quando não há sessão"; "entrega a rota de origem em state.from ao redirecionar para o login" | coberto |
| 4.2 Operador fora das rotas de operadores | `App.test.tsx`; `components/RequireRole.test.tsx` | "redireciona o Operador de %s para o início" (3 rotas); "redireciona o Operador para o início quando a rota exige Admin" | coberto |
| 4.3 Administrador fora de Minha conta | `App.test.tsx`; `components/RequireRole.test.tsx` | "redireciona o Administrador de /minha-conta para o início"; "redireciona o Administrador para o início quando a rota exige Operator" | coberto |
| 4.4 Endereço inexistente | `App.test.tsx`; `pages/NotFoundPage.test.tsx` | "mostra a página não encontrada para um endereço inexistente e volta ao início pelo link"; "mostra o título, a descrição e o link de volta ao início" | coberto |
| 4.5 Menu por perfil | `components/layout/Sidebar.test.tsx` | "mostra para o Administrador os itens Inicio e Operadores, sem Minha conta"; "mostra para o Operador os itens Inicio e Minha conta, sem Operadores"; "não mostra os grupos que ficam sem itens para o perfil" | coberto |

## História 5 — Criar e editar operador

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 5.1 Criação com sucesso e "Cadastrar outro" | `pages/CreateOperatorPage.test.tsx` | "envia e-mail, senha e perfil e mostra identificador, e-mail e perfil do operador criado"; "volta em branco, com o perfil Operador, ao cadastrar outro" | coberto |
| 5.2 Criação recusada | `pages/CreateOperatorPage.test.tsx` | "mostra message, notifications e erro junto do campo e preserva os dados quando a api recusa"; "mostra o erro do perfil junto do seletor" | coberto |
| 5.3 Edição pré-preenchida pela lista | `pages/EditOperatorPage.test.tsx`; `App.test.tsx` | "preenche e-mail e perfil com o estado vindo da lista e deixa a senha vazia"; "abre a edição já preenchida com os dados da linha clicada" | coberto |
| 5.4 Envia só o campo alterado | `pages/EditOperatorPage.test.tsx` | "envia apenas o e-mail quando só o e-mail mudou"; "envia apenas a senha quando só a senha foi digitada"; "envia apenas o perfil quando só o perfil mudou"; "envia um corpo vazio quando nada mudou e deixa a api decidir" | coberto |
| 5.5 Acesso direto com identificador | `pages/EditOperatorPage.test.tsx`; `App.test.tsx` | "abre em branco com perfil Operador quando acessada direto, sem o estado da lista"; "envia o perfil mesmo sem alteração quando acessada direto"; "abre a edição em branco quando o endereço é aberto direto, sem passar pela lista" | coberto |
| 5.6 Sem identificador | `pages/EditOperatorPage.test.tsx` | "mostra a orientação de escolher um operador na lista quando aberta %s" (sem `id` e com `id` só de espaços) | coberto |
| 5.7 Edição com sucesso e "Editar novamente" | `pages/EditOperatorPage.test.tsx` | "mostra e-mail e perfil atualizados e usa-os como nova referência ao editar novamente" | coberto |
| 5.8 Edição recusada | `pages/EditOperatorPage.test.tsx` | "mostra message e notifications, com erro por campo junto do campo, quando a api recusa" | coberto |
| 5.9 Limpar nos dois formulários | `pages/CreateOperatorPage.test.tsx`; `pages/EditOperatorPage.test.tsx` | "esvazia os campos, volta o perfil para Operador e some com o erro ao acionar Limpar"; "volta e-mail e perfil ao valor da lista, esvazia a senha e some com o erro ao acionar Limpar"; "volta ao formulário em branco com perfil Operador ao acionar Limpar quando acessada direto" | coberto |

## História 6 — Minha conta

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 6.1 Carrega e pré-preenche | `pages/MyAccountPage.test.tsx` | "carrega o get-me, preenche o e-mail e mostra o perfil como texto somente leitura" | coberto |
| 6.2 Envia só o que mudou | `pages/MyAccountPage.test.tsx` | "envia só o e-mail quando só o e-mail mudou"; "envia só a senha quando só a senha foi digitada"; "envia e-mail e senha quando os dois mudaram"; "envia um corpo vazio quando nada mudou e deixa a api decidir"; "ignora um e-mail que só tem espaços" | coberto |
| 6.3 Sucesso | `pages/MyAccountPage.test.tsx` | "mostra a mensagem da api, limpa a senha, recarrega os dados e atualiza o e-mail da sessão" | coberto |
| 6.4 Envio recusado | `pages/MyAccountPage.test.tsx` | "mostra message e notifications, com erro por campo junto do campo, quando o envio é recusado" | coberto |
| 6.5 Falha ao carregar | `pages/MyAccountPage.test.tsx` | "mostra o erro da carga acima do formulário, com o perfil ainda em branco" | coberto |
| 6.6 Limpar | `pages/MyAccountPage.test.tsx` | "volta o e-mail ao atual, esvazia a senha e some com o erro ao acionar Limpar"; "some com a mensagem de sucesso ao acionar Limpar" | coberto |

## História 7 — Falhas de comunicação

| Cenário | Arquivo | Título do teste | Situação |
|---|---|---|---|
| 7.1 Servidor inacessível | `lib/api-client.test.ts`; `pages/LoginPage.test.tsx`; `pages/OperatorsListPage.test.tsx` | "lança ApiError com status 0 e a mensagem de falha de comunicação quando o servidor está inacessível"; "mostra a mensagem de falha de comunicação quando o servidor está inacessível"; "mostra a mensagem de falha de comunicação no lugar da tabela quando o servidor está inacessível" | coberto |
| 7.2 Resposta fora do envelope | `lib/api-client.test.ts`; `pages/LoginPage.test.tsx`; `pages/OperatorsListPage.test.tsx` | "lança ApiError com a mensagem genérica de erro inesperado quando a resposta de erro vem com %s" (corpo vazio e que não é JSON); "mostra a mensagem genérica de erro inesperado quando a resposta de erro vem fora do envelope" | coberto |
| 7.3 HTTP de sucesso com envelope de falha | `lib/api-client.test.ts`; `pages/LoginPage.test.tsx` | "trata isSuccess false como erro e preserva message, status do envelope e notifications, mesmo com HTTP 200"; "mostra a message e as notifications do envelope quando o HTTP é de sucesso mas o envelope marca falha" | coberto |
| 7.4 `403` não derruba a sessão | `lib/api-client.test.ts`; `pages/OperatorsListPage.test.tsx` | "não aciona o handler de não autorizado quando uma chamada autenticada recebe %i" (403, 404, 409, 500); "não encerra a sessão e mostra message e notifications da api quando a carga recebe 403" | coberto |
| 7.5 Chamada abortada sem erro | `lib/api-client.test.ts`; `pages/LoginPage.test.tsx`; `pages/OperatorsListPage.test.tsx` | "rejeita com AbortError, sem virar ApiError, quando a chamada é abortada"; "aborta a chamada e não mostra erro quando a tela sai de vista com o envio em andamento"; "aborta a chamada da lista quando a tela sai de vista com a carga em andamento" | coberto parcialmente — ver A-13 (a tela já saiu de vista, então "nenhum erro exibido" não é observável; os testes de página afirmam que o sinal foi abortado e que nada foi gravado) |

## Casos limite da spec

| Caso | Arquivo | Título do teste | Situação |
|---|---|---|---|
| Sessão guardada inválida / sem credencial / perfil desconhecido | `lib/session-storage.test.ts` | "devolve null quando o conteúdo guardado não é um JSON válido"; "devolve null quando falta a credencial ou ela está vazia"; "trata qualquer perfil diferente de Admin como Operator" | coberto |
| Armazenamento indisponível | `lib/session-storage.test.ts`; `context/AuthContext.test.tsx`; `context/ThemeContext.test.tsx` | "não quebra quando o armazenamento da aba está indisponível"; "mantém a sessão em memória quando o armazenamento da aba está indisponível"; "continua funcionando em memória quando o armazenamento está indisponível" | coberto |
| `401` no login e em chamada não autenticada | `lib/api-client.test.ts`; `pages/LoginPage.test.tsx` | "não aciona o handler quando a chamada não autenticada (ex.: login) recebe 401"; "não derruba sessão nem mostra o aviso de sessão encerrada quando o login recebe 401" | coberto |
| Troca rápida de filtro/página (só a última resposta vale) | `pages/OperatorsListPage.test.tsx` | "desabilita os controles enquanto a lista carrega, o que impede trocar filtro ou página no meio de uma carga" | coberto parcialmente — ver A-08 (os controles ficam desabilitados durante a carga, então a situação não é alcançável pela interface; a proteção contra resposta antiga existe no código, mas nenhum teste a exercita) |
| Cancelar/Esc bloqueados enquanto a chamada está em andamento | `components/ui/ConfirmDialog.test.tsx`; `pages/OperatorsListPage.test.tsx` | "bloqueia confirmar, cancelar e Esc enquanto a chamada está em andamento"; "bloqueia confirmar, cancelar e Esc enquanto a chamada de desativar está em andamento" | coberto parcialmente — ver A-10 (Esc simulado pelo evento `cancel`) |
| Lista com sucesso mas sem dados | `pages/OperatorsListPage.test.tsx` | "mantém a tela como estava quando uma recarga volta com sucesso e sem dados"; "mostra a mensagem de lista vazia quando a primeira carga volta com sucesso e sem dados" | coberto (ver A-09) |

## Componentes e utilitários com comportamento (Princípio VII, FR-013)

| Arquivo | Testes |
|---|---|
| `components/ui/Alert.test.tsx`, `Button.test.tsx`, `ConfirmDialog.test.tsx`, `PasswordField.test.tsx`, `SelectField.test.tsx`, `TextField.test.tsx` | tom, rótulo, erro acessível, carregamento, janela de confirmação |
| `components/RequireSession.test.tsx`, `RequireRole.test.tsx` | guardas de rota |
| `components/layout/Sidebar.test.tsx`, `Topbar.test.tsx` | menu por perfil e topo; o `AppShell` (link `Ir para o conteudo` e landmark `main`) é coberto em `App.test.tsx`: "mostra o menu lateral e o topo junto do conteúdo da rota, com link para pular ao conteúdo" |
| `components/ThemeToggle.test.tsx`, `context/ThemeContext.test.tsx` | tema claro/escuro, persistência e preferência do sistema |
| `context/AuthContext.test.tsx` | sessão, expiração e armazenamento indisponível |
| `lib/api-client.test.ts`, `form-error.test.ts`, `labels.test.ts`, `session-storage.test.ts` | utilitários |
| `pages/HomePage.test.tsx`, `pages/NotFoundPage.test.tsx` | páginas simples |
