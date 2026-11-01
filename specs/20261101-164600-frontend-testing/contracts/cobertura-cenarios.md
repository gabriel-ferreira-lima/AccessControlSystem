# Contrato: cobertura dos cenários da spec

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Mapa de rastreabilidade (FR-017, SC-002): cada cenário de aceitação aponta para o arquivo de teste
que o cobre. Os `it(...)` exatos nascem na implementação (`tasks.md`); a coluna "Trecho do teste" é a
intenção. Na convergência, cada linha deve ser localizável por busca da descrição.

Convenção de nomes dos arquivos: ao lado do arquivo testado, `*.test.ts` / `*.test.tsx`.
`App.test.tsx` usa o `App` real; `*Page.test.tsx` usa `renderWithProviders`.

Os títulos de `describe`/`it` levam **acentuação correta**; só os textos de interface afirmados nos
testes (ex.: `Pagina nao encontrada`) ficam sem acento, como estão hoje.

## História 1 — Rodar a suíte e ser barrado por ela

| Cenário | Arquivo | Trecho do teste / verificação |
|---|---|---|
| 1.1 Roda sem API, banco, Docker ou internet | execução de `npm test` (quickstart) | suíte inteira passa; a ausência de rede real é garantida pela trava de chamada não prevista |
| 1.2 Chamada não prevista reprova o teste | `src/test/server.test.ts` | "registra a chamada que não tem handler" e o `afterEach` reprova se não for consumida |
| 1.3 CI roda tipos, testes e build em PR do front | `frontend-ci.yml` (verificação no PR) | passos 4 a 6 do contrato do CI |
| 1.4 CI não roda sem mudança no front | `frontend-ci.yml` (verificação no PR) | filtro de `paths` |
| 1.5 Falha de teste reprova o PR | `frontend-ci.yml` (verificação no PR) | PR de teste com um `it` quebrado de propósito |

## História 2 — Login e sessão

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 2.1 Sucesso guarda a sessão e vai ao início | `pages/LoginPage.test.tsx` | "guarda a sessão e leva ao início quando o login tem sucesso" |
| 2.2 Sucesso volta à rota de origem | `pages/LoginPage.test.tsx`, `App.test.tsx` | "leva à rota de origem depois do login" |
| 2.3 Erro mostra `message` e `notifications` | `pages/LoginPage.test.tsx` | "mostra a message e as notifications quando a api devolve 401" |
| 2.4 Sucesso sem credencial usável | `pages/LoginPage.test.tsx` | "mostra erro inesperado e não cria sessão quando vem sem token" |
| 2.5 `401` autenticado derruba a sessão | `App.test.tsx`, `lib/api-client.test.ts`, `context/AuthContext.test.tsx` | "volta ao login com aviso de sessão encerrada quando uma chamada autenticada recebe 401" |
| 2.6 Encerrar sessão manualmente | `components/layout/Topbar.test.tsx`, `App.test.tsx` | "apaga a sessão e vai ao login sem o aviso de sessão encerrada" |
| 2.7 Sessão existente sai do login | `pages/LoginPage.test.tsx` | "redireciona para o início (ou a origem) quando já há sessão" |
| 2.8 Carregamento durante o envio | `pages/LoginPage.test.tsx` | "mostra o estado de carregamento e bloqueia o botão enquanto envia" |

## História 3 — Lista de operadores

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 3.1 Carga inicial com filtro Ativos | `pages/OperatorsListPage.test.tsx` | "carrega a primeira página com o filtro Ativos" (confere a query enviada) |
| 3.2 Trocar filtro volta à página 1 | `pages/OperatorsListPage.test.tsx` | "pede Inativos e Todos e volta à página 1 ao trocar o filtro" |
| 3.3 Próxima/Anterior e limites | `pages/OperatorsListPage.test.tsx` | "navega entre páginas e desabilita Anterior na primeira e Próxima na última" |
| 3.4 Itens por página | `pages/OperatorsListPage.test.tsx` | "pede o novo tamanho e volta à página 1" |
| 3.5 Lista vazia | `pages/OperatorsListPage.test.tsx` | "mostra a mensagem de lista vazia" |
| 3.6 Desativar com confirmação | `pages/OperatorsListPage.test.tsx` | "abre a confirmação com o e-mail, chama a api, fecha e recarrega" |
| 3.7 Ativar com confirmação | `pages/OperatorsListPage.test.tsx` | idem para ativação |
| 3.8 Cancelar (botão e Esc) | `pages/OperatorsListPage.test.tsx`, `components/ui/ConfirmDialog.test.tsx` | "não chama a api ao cancelar" |
| 3.9 Erro da API dentro da janela | `pages/OperatorsListPage.test.tsx` | "mantém a janela aberta com message e notifications e não recarrega" |
| 3.10 Erro ao carregar a lista | `pages/OperatorsListPage.test.tsx` | "mostra message e notifications no lugar da tabela" |
| 3.11 Editar leva e-mail e perfil; Criar leva à criação | `pages/OperatorsListPage.test.tsx`, `App.test.tsx` | "o link Editar abre a edição pré-preenchida com os dados da linha" |

## História 4 — Navegação

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 4.1 Sem sessão vai ao login e lembra a origem | `App.test.tsx`, `components/RequireSession.test.tsx` | "redireciona para o login guardando a rota de origem" |
| 4.2 Operador fora das rotas de operadores | `App.test.tsx`, `components/RequireRole.test.tsx` | "redireciona o Operador de /operadores, /operadores/novo e /operadores/editar para o início" |
| 4.3 Administrador fora de Minha conta | `App.test.tsx` | "redireciona o Administrador de /minha-conta para o início" |
| 4.4 Endereço inexistente | `App.test.tsx`, `pages/NotFoundPage.test.tsx` | "mostra a página não encontrada com link de volta" |
| 4.5 Menu por perfil | `components/layout/Sidebar.test.tsx` | "mostra só os itens do perfil" |

## História 5 — Criar e editar operador

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 5.1 Criação com sucesso e "Cadastrar outro" | `pages/CreateOperatorPage.test.tsx` | "mostra identificador, e-mail e perfil e volta em branco ao cadastrar outro" |
| 5.2 Criação recusada | `pages/CreateOperatorPage.test.tsx` | "mostra message, notifications e erro junto do campo e preserva os dados" |
| 5.3 Edição pré-preenchida pela lista | `pages/EditOperatorPage.test.tsx` | "preenche e-mail e perfil com o estado da lista e deixa a senha vazia" |
| 5.4 Envia só o campo alterado | `pages/EditOperatorPage.test.tsx` | "envia apenas o que mudou" (confere o corpo) |
| 5.5 Acesso direto com identificador | `pages/EditOperatorPage.test.tsx` | "abre em branco com perfil Operador e envia o perfil" |
| 5.6 Sem identificador | `pages/EditOperatorPage.test.tsx` | "mostra a orientação com link para a lista e nenhum formulário" |
| 5.7 Edição com sucesso e "Editar novamente" | `pages/EditOperatorPage.test.tsx` | "mostra e-mail e perfil atualizados e usa-os como nova referência" |
| 5.8 Edição recusada | `pages/EditOperatorPage.test.tsx` | "mostra message e notifications, com erro por campo" |
| 5.9 Limpar nos dois formulários | `pages/CreateOperatorPage.test.tsx`, `pages/EditOperatorPage.test.tsx` | "volta ao valor de referência e some com os avisos" |

## História 6 — Minha conta

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 6.1 Carrega e pré-preenche | `pages/MyAccountPage.test.tsx` | "carrega o get-me, preenche o e-mail e mostra o perfil somente leitura" |
| 6.2 Envia só o que mudou | `pages/MyAccountPage.test.tsx` | "envia só o e-mail, só a senha, ou corpo vazio sem alterações" |
| 6.3 Sucesso | `pages/MyAccountPage.test.tsx` | "mostra a mensagem da api, limpa a senha, recarrega e atualiza a sessão" |
| 6.4 Envio recusado | `pages/MyAccountPage.test.tsx` | "mostra message e notifications, com erro por campo" |
| 6.5 Falha ao carregar | `pages/MyAccountPage.test.tsx` | "mostra o erro acima do formulário" |
| 6.6 Limpar | `pages/MyAccountPage.test.tsx` | "volta o e-mail ao atual, esvazia a senha e some com os avisos" |

## História 7 — Falhas de comunicação

| Cenário | Arquivo | Trecho do teste |
|---|---|---|
| 7.1 Servidor inacessível | `lib/api-client.test.ts`, `pages/LoginPage.test.tsx`, `pages/OperatorsListPage.test.tsx` | "mostra a mensagem de falha de comunicação" |
| 7.2 Resposta fora do envelope | `lib/api-client.test.ts`, `pages/LoginPage.test.tsx` | "mostra a mensagem genérica de erro inesperado" |
| 7.3 HTTP de sucesso com envelope de falha | `lib/api-client.test.ts` | "trata isSuccess false como erro e preserva message e notifications" |
| 7.4 `403` não derruba a sessão | `lib/api-client.test.ts`, `pages/OperatorsListPage.test.tsx` | "não encerra a sessão e mostra o erro da api" |
| 7.5 Chamada abortada sem erro | `lib/api-client.test.ts`, páginas | "não mostra erro quando a chamada é abortada" |

## Casos limite da spec

| Caso | Arquivo |
|---|---|
| Sessão guardada inválida / sem credencial / perfil desconhecido | `lib/session-storage.test.ts` |
| Armazenamento indisponível | `lib/session-storage.test.ts`, `context/AuthContext.test.tsx` |
| `401` no login e em chamada não autenticada | `lib/api-client.test.ts`, `pages/LoginPage.test.tsx` |
| Troca rápida de filtro/página (só a última resposta vale) | `pages/OperatorsListPage.test.tsx` |
| Cancelar/Esc bloqueados enquanto a chamada está em andamento | `components/ui/ConfirmDialog.test.tsx`, `pages/OperatorsListPage.test.tsx` |
| Lista com sucesso mas sem dados | `pages/OperatorsListPage.test.tsx` |

## Componentes compartilhados com comportamento (Princípio VII, FR-013)

`components/ui/{Alert,Button,ConfirmDialog,PasswordField,SelectField,TextField}.test.tsx`,
`components/ThemeToggle.test.tsx`, `context/ThemeContext.test.tsx`, `lib/labels.test.ts`,
`lib/form-error.test.ts`, `pages/HomePage.test.tsx`.

O `AppShell` (link `Ir para o conteudo` para `#conteudo` e landmark `main`) é coberto em
`App.test.tsx`, junto da navegação (tarefa T033).
