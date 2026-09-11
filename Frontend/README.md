# Sistema de Controle de Acesso — Frontend

Console administrativo do Sistema de Controle de Acesso.

Este projeto é um **cliente de apresentação**: ele renderiza telas, envia
requisições para a API e exibe exatamente o que a API responde. Nenhuma regra
de negócio ou de segurança é reproduzida aqui — validações de senha, de e-mail,
verificação de token e decisões de autorização são responsabilidade exclusiva do
backend (`../Backend`).

## Stack

- Vite 6 + React 19 + TypeScript (modo `strict`)
- React Router 7
- CSS puro com tokens de tema (claro/escuro)
- Sem dependências de UI de terceiros

## Pré-requisitos

- Node.js 20+ (testado com 24)
- API do backend em execução (ver `../Backend`)
- Banco PostgreSQL acessível pela API

## Configuração

Variáveis de ambiente (prefixo `VITE_`), lidas de `.env` / `.env.local`:

| Variável             | Padrão                      | Descrição                       |
| -------------------- | --------------------------- | ------------------------------- |
| `VITE_API_BASE_URL`  | `https://localhost:7219`    | URL base da API (sem barra final) |

Para sobrescrever por máquina, copie `.env.example` para `.env.local`.

## Executando

```bash
npm install
npm run dev      # http://localhost:5173 (porta fixa)
```

Outros scripts:

```bash
npm run build      # tsc -b + vite build  ->  dist/
npm run preview    # serve o build de produção
npm run typecheck  # apenas verificação de tipos
```

## Integração com o backend

### CORS

O backend expõe uma política de CORS nomeada (`FrontendCorsPolicy`) cujas origens
são lidas de `Cors:AllowedOrigins` na configuração. Em desenvolvimento
(`appsettings.Development.json`) já vêm liberadas:

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173", "http://127.0.0.1:5173" ]
}
```

Para produção, defina as origens reais do console em `appsettings.json` ou em
variáveis de ambiente (`Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ...).
Se a lista estiver vazia, nenhuma origem cruzada é permitida.

### HTTPS

A API aplica `UseHttpsRedirection()`. Requisições _pre-flight_ de CORS **não
seguem redirecionamentos**, portanto o frontend aponta para a origem HTTPS
(`https://localhost:7219`) diretamente. Confie no certificado de desenvolvimento
uma única vez:

```bash
dotnet dev-certs https --trust
```

Se preferir usar HTTP (`http://localhost:5290`), defina
`HttpsRedirection:Enabled=false` no backend e ajuste `VITE_API_BASE_URL`.

## Endpoints consumidos

| Método | Rota                                       | Uso                                            |
| ------ | ------------------------------------------ | ----------------------------------------------- |
| POST   | `/v1/account/login`                        | Autenticação; retorna o token                  |
| POST   | `/v1/account/create`                       | Cadastro de operador (envia o token)           |
| PUT    | `/v1/account/update/{id}`                  | Altera e-mail, senha ou perfil de um operador  |
| GET    | `/v1/account/get-list?Page=&Size=&IsActive=` | Lista paginada de operadores                 |
| PUT    | `/v1/account/update-me`                    | Altera e-mail/senha da própria conta           |
| GET    | `/v1/account/get-me`                       | Dados atuais da própria conta (somente leitura) |
| PUT    | `/v1/account/deactivate/{id}`              | Desativa um operador (exclusão lógica)         |
| PUT    | `/v1/account/activate/{id}`                | Reativa um operador                            |

`create`, `update`, `get-list`, `deactivate` e `activate` só respondem a token de
perfil `Admin`; um token de operador recebe 403. O console apenas oculta essas
telas para não-admins — a decisão de acesso é sempre da API.

`get-list` aceita `Page` (>= 1), `Size` (1–100) e `IsActive` (`Active`,
`Inactive` ou `All`, padrão `All`); `data` traz
`{ operators: [{ id, email, role, isActive }], page, size, total, totalPages }`.

`deactivate`/`activate` não recebem corpo — o id vem da rota, e quem pediu a
desativação vem do token no backend. Ambos devolvem
`{ id, email, role, isActive }`. Regras que só existem no backend (o front
nunca verifica isso antes de mandar a requisição, só exibe o erro que volta):
não é possível desativar a própria conta, nem desativar o último administrador
ativo.

`update-me` e `get-me` aceitam **qualquer** perfil autenticado — o servidor
identifica a conta pelo token (claim de id), sem receber identificador nem
perfil no corpo. `get-me` é só leitura: o perfil retornado aparece como texto
fixo (não editável) dentro do próprio formulário de "Minha conta"; o e-mail
retornado pré-preenche o campo de e-mail, que aí sim pode ser editado. Qualquer
alteração é enviada por `update-me`.

Não existe endpoint para consultar um operador específico por id (só a lista
paginada e o "me" do próprio token). Por isso, ao editar um operador
(`/operadores/editar`), o e-mail e o perfil atuais vêm da própria linha clicada
na lista — passados como *router state* do `<Link>`, não por nova chamada à
API — só para pré-preencher o formulário; se a página for aberta sem esse
estado (link direto, atualização da página), os campos ficam em branco/padrão
e o envio só inclui o que for digitado.

Na lista de operadores, o dropdown "Mostrar" (Ativos/Inativos/Todos, padrão
**Ativos**) filtra via `IsActive`. Cada linha tem um botão **Desativar** (ativos) ou **Ativar**
(inativos) que abre uma janela de confirmação (`ConfirmDialog`, centralizada na
tela — baseada no elemento nativo `<dialog>`) antes de chamar a API. O front
não tenta adivinhar quando o botão deve ficar desabilitado (ex.: última conta
admin, a própria conta) — sempre deixa a ação disponível e mostra o erro que a
API devolver, `message` **e** `notifications`: algumas regras do backend (ex.
"não é possível desativar a própria conta") só vêm dentro de `notifications`,
com `message` genérico ("Requisição inválida") — por isso todo lugar que
exibe erro de API neste projeto usa o helper `toFormError`
(`src/lib/form-error.ts`), que sempre carrega os dois. Se adicionar uma tela
nova, use esse helper em vez de ler só `error.message`.

O backend também rejeita **qualquer** chamada autenticada com 401 assim que a
conta é desativada (verificação a cada requisição, não só no login). O
`apiRequest` detecta esse 401 em chamadas com `authenticated: true` e aciona um
handler registrado pelo `AuthProvider`, que encerra a sessão local — a troca
para `/login` acontece pelo caminho normal (`RequireSession` redireciona assim
que a sessão fica nula), com um aviso "Sua sessão foi encerrada" na tela de
login.

Envelope de resposta esperado:

```json
{
  "message": "string",
  "status": 200,
  "isSuccess": true,
  "notifications": [{ "key": "Campo", "message": "texto" }],
  "data": {}
}
```

`message` e `notifications` são exibidos como recebidos, sem tradução ou
reinterpretação no cliente.

## Estrutura

```
src/
  api/            Chamadas HTTP (uma função por endpoint)
  components/
    layout/       AppShell, Sidebar, Topbar
    ui/           Button, TextField, SelectField, Alert, ConfirmDialog
    icons.tsx     Ícones SVG inline (sem emoji)
  context/        AuthContext (sessão), ThemeContext (tema)
  lib/            api-client (fetch + ApiError), session-storage, labels
  pages/          LoginPage, HomePage, MyAccountPage, OperatorsListPage,
                  CreateOperatorPage, EditOperatorPage, NotFoundPage
  styles/         tokens.css (temas), app.css
  types/          Contratos da API
```

## Sessão e rotas

- O token é guardado em `sessionStorage` (`acs.session`) e descartado ao fechar
  a aba.
- `RequireSession` redireciona para `/login` quando não há token local. Isso é
  **conveniência de navegação**; o controle de acesso efetivo é feito pela API a
  cada requisição.
- `RequireRole` oculta rotas fora do perfil (`/operadores/*` exigem `Admin`,
  `/minha-conta` exige `Operator`) e redireciona para `/`. Também cosmético; a
  API valida o perfil do token — `update-me`/`get-me` aceitam qualquer perfil,
  então um Admin que forçar a URL ainda seria atendido pelo backend.
- Rotas: `/login`, `/` (início), `/minha-conta` (só `Operator` — o
  Administrador já edita a própria conta em Operadores → Editar),
  `/operadores` (lista), `/operadores/novo`, `/operadores/editar?id=<guid>` — o
  identificador vem da linha da lista, não é exibido nem editável; sem `id` a
  tela pede para escolher um operador na lista. `/operadores/novo` e
  `/operadores/editar` não aparecem no menu lateral — só são alcançadas pelo
  botão "Criar operador" e pelo link "Editar" da tela `/operadores`.

## Tema

Claro e escuro via atributo `data-theme` no `<html>`. A preferência do sistema é
respeitada por padrão; a escolha manual é persistida em `localStorage`
(`acs.theme`). Um script inline em `index.html` aplica o tema antes da
renderização para evitar _flash_ de cor.
