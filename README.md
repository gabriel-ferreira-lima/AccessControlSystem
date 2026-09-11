# Sistema de Controle de Acesso

Monorepo com duas partes:

| Pasta       | Stack                                   | Descrição                                    |
| ----------- | --------------------------------------- | -------------------------------------------- |
| `Backend/`  | ASP.NET Core (.NET 10), EF Core, JWT    | API REST + PostgreSQL                        |
| `Frontend/` | Vite 6 + React 19 + TypeScript          | Console administrativo (SPA de apresentação) |

## Rodando com Docker

Pré-requisito: Docker Desktop (ou Docker Engine + Compose v2).

```bash
docker compose up --build
```

Serviços expostos no host:

| Serviço  | URL                      | Observação                                       |
| -------- | ------------------------ | ----------------------------------------------- |
| Frontend | http://localhost:5173    | nginx serve a SPA e faz proxy de `/v1` → API    |
| API      | http://localhost:5290    | acesso direto (ex.: `POST /v1/account/login`)   |
| Postgres | localhost:5433           | só para inspeção com um cliente; a API acessa pela rede interna |

Abra **http://localhost:5173** — o frontend fala com a API pela mesma origem
(via proxy do nginx), então não há configuração de CORS nem de HTTPS a fazer.

### Como funciona

- **Backend** (`Backend/AccessControlSystem.Api/Dockerfile`): build multi-stage
  (SDK → runtime ASP.NET), roda em HTTP na porta 8080 dentro do container.
  As **migrations são aplicadas no startup** (com novas tentativas enquanto o
  Postgres inicializa). O redirecionamento HTTPS é desligado via
  `HttpsRedirection__Enabled=false`.
- **Frontend** (`Frontend/Dockerfile`): build multi-stage (Node → nginx). O Vite
  é compilado com `VITE_API_BASE_URL=/`, então o `api-client` usa base vazia e
  as requisições vão para a mesma origem; o nginx (`Frontend/nginx.conf`)
  encaminha `/v1/` para o container `backend`.
- **Postgres**: dados persistidos no volume `db-data`.

### Comandos úteis

```bash
docker compose up --build -d        # sobe em background
docker compose logs -f backend      # acompanha os logs da API
docker compose down                 # para e remove os containers
docker compose down -v              # idem + apaga o volume do banco
docker compose build backend        # rebuild só do backend
```

### Configuração

Funciona sem nenhuma configuração extra: cada `${VAR}` no `docker-compose.yml`
tem um valor padrão embutido (a parte `:-` em `${VAR:-padrão}`).

Para mudar algum valor (porta, usuário/senha do Postgres, ambiente), há duas
opções — nas duas o compose continua funcionando:

- editar direto o `docker-compose.yml`; ou
- criar um arquivo `.env` ao lado dele (o compose lê automaticamente) com só as
  chaves que você quer trocar, ex.:

  ```
  BACKEND_PORT=8080
  POSTGRES_PASSWORD=umaSenhaMinha
  ```

Variáveis reconhecidas: `FRONTEND_PORT` (5173), `BACKEND_PORT` (5290),
`DB_PORT` (5433), `POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_DB`,
`ASPNETCORE_ENVIRONMENT` (Production).

Os **segredos da API** (`JwtPrivateKey`, `PasswordSaltKey`) vêm do
`appsettings.json`. Para trocá-los sem rebuild, descomente as linhas `Secrets__*`
no serviço `backend` do `docker-compose.yml` e defina os valores lá (ou via
`.env`).

## Rodando sem Docker

Cada parte tem o seu próprio README:

- [`Backend/`](Backend/) — requer .NET 10 SDK e um PostgreSQL acessível.
- [`Frontend/README.md`](Frontend/README.md) — requer Node.js 20+.
