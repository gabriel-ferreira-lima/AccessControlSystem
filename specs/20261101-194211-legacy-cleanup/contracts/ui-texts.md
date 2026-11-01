# Contrato: textos de interface do frontend

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Fase **D**. Caminhos relativos a `src/Frontend/src/`. Linhas de 2026-11-01 (antes da mudança).
Varredura feita em **todo** o código de produção (telas, componentes, contextos, `lib`): fora os 17
textos abaixo só restam comentários e a mensagem técnica de `main.tsx`, que a spec exclui.

## 1. Os 17 textos (produção)

| # | Texto atual | Texto corrigido | Arquivo:linha |
|---|---|---|---|
| 1 | `Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente.` | `Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente.` | `lib/api-client.ts:10` |
| 2 | `Sua sessao foi encerrada. Faca login novamente.` | `Sua sessão foi encerrada. Faça login novamente.` | `pages/LoginPage.tsx:80` |
| 3 | `O uso deste sistema e limitado a operadores autorizados.` | `O uso deste sistema é limitado a operadores autorizados.` | `pages/LoginPage.tsx:118` |
| 4 | `Inicio` | `Início` | `pages/HomePage.tsx:5` e `components/layout/Sidebar.tsx:24` |
| 5 | `As informacoes desta pagina serao adicionadas conforme os servicos forem disponibilizados.` | `As informações desta página serão adicionadas conforme os serviços forem disponibilizados.` | `pages/HomePage.tsx:10-11` |
| 6 | `Pagina nao encontrada` | `Página não encontrada` | `pages/NotFoundPage.tsx:8` |
| 7 | `O recurso solicitado nao existe ou foi movido.` | `O recurso solicitado não existe ou foi movido.` | `pages/NotFoundPage.tsx:10` |
| 8 | `Operacao` | `Operação` | `components/layout/Sidebar.tsx:23` |
| 9 | `Navegacao principal` | `Navegação principal` | `components/layout/Sidebar.tsx:74` (`aria-label`) |
| 10 | `Ir para o conteudo` | `Ir para o conteúdo` | `components/layout/AppShell.tsx:9` |
| 11 | `Encerrar sessao` | `Encerrar sessão` | `components/layout/Topbar.tsx:31` |
| 12 | `Acoes` | `Ações` | `pages/OperatorsListPage.tsx:158` (`sr-only`) |
| 13 | `Itens por pagina` | `Itens por página` | `pages/OperatorsListPage.tsx:214` |
| 14 | `{total} operador(es) · pagina {atual} de {total de páginas}` | `… · página {atual} de …` | `pages/OperatorsListPage.tsx:208` |
| 15 | `Proxima` | `Próxima` | `pages/OperatorsListPage.tsx:247` |
| 16 | `A conta deixara de conseguir acessar o sistema.` | `A conta deixará de conseguir acessar o sistema.` | `pages/OperatorsListPage.tsx:264` |
| 17 | `Salvar alteracoes` | `Salvar alterações` | `pages/EditOperatorPage.tsx:191` e `pages/MyAccountPage.tsx:153` |

São **10 arquivos de produção**: `lib/api-client.ts`, `pages/{LoginPage,HomePage,NotFoundPage,
OperatorsListPage,EditOperatorPage,MyAccountPage}.tsx` e `components/layout/{AppShell,Sidebar,
Topbar}.tsx`. Nenhuma outra linha desses arquivos muda.

## 2. Testes que afirmam cada texto

Todos os testes são de caracterização da spec 02 e hoje afirmam a forma **sem** acento, exceto os
textos 3 e 12, que nenhum teste afirma e passam a ser afirmados por **asserções acrescentadas**
(FR-021, seção 3). Por texto:

| # | Testes (arquivo:linha) |
|---|---|
| 1 | `lib/api-client.test.ts:204`; `pages/LoginPage.test.tsx:156`; `pages/OperatorsListPage.test.tsx:516` |
| 2 | `App.test.tsx:14` (constante `EXPIRED_NOTICE`); `pages/LoginPage.test.tsx:11` (constante) |
| 3 | **nenhum hoje**; asserção acrescentada em `pages/LoginPage.test.tsx` ("mostra o formulário em branco com os campos de e-mail e senha", linha ~137) |
| 4 | `App.test.tsx:84,92,137,144,151,158`; `pages/HomePage.test.tsx:10`; `components/layout/Sidebar.test.tsx:16,24,32,38,46` |
| 5 | `pages/HomePage.test.tsx:14` |
| 6 | `App.test.tsx:134`; `pages/NotFoundPage.test.tsx:10` |
| 7 | `pages/NotFoundPage.test.tsx:11` |
| 8 | `components/layout/Sidebar.test.tsx:51` |
| 9 | `App.test.tsx:159`; `components/layout/Sidebar.test.tsx:8` |
| 10 | `App.test.tsx:152` (o `"#conteudo"` das linhas 154 e 157 **não** muda) |
| 11 | `App.test.tsx:52,160`; `components/layout/Topbar.test.tsx:23` |
| 12 | **nenhum hoje**; asserção acrescentada em `pages/OperatorsListPage.test.tsx` ("carrega a primeira página com o filtro Ativos", linha ~64) |
| 13 | `pages/OperatorsListPage.test.tsx:162,165,178,435` |
| 14 | `pages/OperatorsListPage.test.tsx:90,113,121,133,138,143,149,160,170` (regex `/pagina X de Y/`) |
| 15 | `pages/OperatorsListPage.test.tsx:112,134,137,139,142,145,159,429,437,444` |
| 16 | `pages/OperatorsListPage.test.tsx:267` |
| 17 | `pages/EditOperatorPage.test.tsx` (16 linhas); `pages/MyAccountPage.test.tsx` (12 linhas) |

**10 arquivos de teste, cerca de 84 linhas**: `App.test.tsx`, `lib/api-client.test.ts`,
`components/layout/{Sidebar,Topbar}.test.tsx` e `pages/{LoginPage,HomePage,NotFoundPage,
OperatorsListPage,EditOperatorPage,MyAccountPage}.test.tsx`.

### Títulos de `it` que citam o texto antigo (2)

`components/layout/Sidebar.test.tsx:12` ("...os itens Inicio e Operadores...") e `:20` ("...os itens
Inicio e Minha conta..."): o título passa a dizer "Início". Só o título muda; o corpo do teste é o
mesmo além do texto esperado.

## 3. Os dois textos que nenhum teste afirmava: asserção acrescentada (FR-021)

Os textos **3** (`LoginPage.tsx:118`) e **12** (`OperatorsListPage.tsx:158`) não eram afirmados por
nenhum teste, e **não podem ser corrigidos sem teste** (Princípio VII). Cada um ganha uma asserção em
um teste **existente** (nenhum teste novo; o total continua 430 / 238), escrita antes da correção e
vista falhando:

| Texto | Teste existente | Asserção acrescentada | Red (antes da correção) |
|---|---|---|---|
| 3 | `pages/LoginPage.test.tsx`, `"mostra o formulário em branco com os campos de e-mail e senha"` | `expect(screen.getByText("O uso deste sistema é limitado a operadores autorizados.")).toBeInTheDocument();` | falha: a tela mostra `...sistema e limitado...` |
| 12 | `pages/OperatorsListPage.test.tsx`, `"carrega a primeira página com o filtro Ativos"` | `expect(screen.getByRole("columnheader", { name: "Ações" })).toBeInTheDocument();` | falha: o cabeçalho se chama `Acoes` |

Motivo na tabela "Testes existentes afetados" do `tasks.md`: **asserção acrescentada pelo FR-021**.
Com isso, os 17 textos passam a ser afirmados por teste; a lacuna que era o achado F-03 virou tarefa
(ver [research.md](../research.md), item 7).

## 4. Como obter a lista exata de testes afetados (para o `tasks.md`)

1. Ajustar os 10 arquivos de teste para os textos corrigidos (e os 2 títulos) e acrescentar as 2
   asserções da seção 3.
2. `npm test` em `src/Frontend`: a saída lista os testes que **falham**. Essa lista é a tabela
   "Testes existentes afetados" do front, com o motivo "texto esperado mudou por esta spec" (ou
   "asserção acrescentada pelo FR-021", nos 2 testes da seção 3).
3. Corrigir os 10 arquivos de produção até `npm test` voltar a **238 aprovados**.
4. A diferença entre a lista de testes editados (`git diff`) e a lista de falhas do Red deve ser
   explicável: testes editados que **não** falharam no Red indicam edição desnecessária (revisar);
   testes que falharam e não foram editados, edição esquecida.

## 5. Não mudam

| Item | Por quê |
|---|---|
| `href="#conteudo"` e `id="conteudo"` (`AppShell.tsx:8,14`) | identificador, não texto; o teste afirma `"#conteudo"` |
| Rotas `/minha-conta`, `/operadores`, `/operadores/novo`, `/operadores/editar` | mudariam URL (comportamento) |
| `"Elemento #root nao encontrado no documento."` (`main.tsx:9`) | mensagem técnica que não chega ao operador |
| Comentários do código (`/** ... */`, `//`, CSS) | a constituição permite sem acento |
| `&middot;` e a estrutura JSX dos textos | só a grafia dos textos muda |
| Textos já corretos (`Console administrativo`, `Carregando operadores...`, `Nenhum operador encontrado...`, etc.) | sem erro |

## 6. Sincronia da rastreabilidade da spec 02

`specs/20261101-164600-frontend-testing/contracts/cobertura-cenarios.md` cita títulos de `it` e
textos literais. Na fase **E** são ajustados 3 trechos: a **linha 21** (nota "ficam sem acento, como
estão hoje", que deixa de ser verdade), a **linha 70** (os 2 títulos do `Sidebar.test.tsx`) e a
**linha 124** (`Ir para o conteudo` → `Ir para o conteúdo`).
