# Sistema de Controle de Acesso

Monorepo com duas partes:

| Pasta            | Stack                                   | Descrição                                    |
| ---------------- | --------------------------------------- | -------------------------------------------- |
| `src/Backend/`   | ASP.NET Core (.NET 10), EF Core, JWT    | API REST + PostgreSQL                        |
| `src/Frontend/`  | Vite 6 + React 19 + TypeScript          | Console administrativo (SPA de apresentação) |

## Rodando com Docker (desenvolvimento)

Pré-requisito: Docker Desktop (ou Docker Engine + Compose v2).

```bash
cp .env.example .env        # preencha JWT_PRIVATE_KEY e PASSWORD_SALT_KEY (veja o arquivo)
docker compose down -v      # só na primeira vez: o volume antigo tem um banco no formato legado
docker compose up --build
```

> O compose é **só de desenvolvimento**. Não use estes valores, papéis nem senhas em homologação
> ou produção.

Um comando sobe: o PostgreSQL com o **catálogo**, uma **base compartilhada** e uma **base
dedicada**; o **migrator** (passo único que atualiza as bases e cria os dados sintéticos); **uma
API por tenant de teste**; e o frontend.

| Serviço             | URL                   | Tenant de teste (login padrão)             |
| ------------------- | --------------------- | ------------------------------------------ |
| Frontend            | http://localhost:5173 | nginx serve a SPA e faz proxy de `/v1` → `backend` (tenant A) |
| `backend`           | http://localhost:5290 | **A**, na base compartilhada `shared_01`   |
| `backend-b`         | http://localhost:5291 | **B**, na mesma base compartilhada         |
| `backend-dedicated` | http://localhost:5292 | **D**, na base dedicada `dedicated_01`     |
| Postgres            | localhost:5433        | só para inspeção com um cliente            |

Os três tenants têm `admin@admin.com.br` (senha `DEV_ADMIN_PASSWORD` do `.env`); A e B também têm
`operador@exemplo.com`, com senhas **diferentes** (`DEV_OPERATOR_PASSWORD_A` e `_B`). Cada API faz o
login no seu tenant padrão, mas qualquer uma atende o token de qualquer tenant (o tenant vem sempre
da claim `tenant` do token).

### Como funciona

- **Migrations: só pelo Migrator.** A API **não aplica migrations ao iniciar** e não recebe
  connection string com DDL. Quem atualiza o catálogo e todas as bases é o container `migrator`
  (`migrate`), e o `seed-dev` (só em `Development`) cria os tenants e operadores sintéticos.
  **Nunca** rode `dotnet ef database update` (nem bundle, nem script gerado) numa base nova: isso
  executaria a migration `SeedAdmin` e criaria o administrador de senha fixa. Gerar migrations
  (`dotnet ef migrations add`) continua normal.
- **Papéis do banco**: `acs_migrator` (dono, DDL, só o Migrator) e `acs_app` (usado pelas APIs:
  sem DDL, sem DELETE, sujeito ao RLS). Os papéis e os bancos nascem em
  `docker/postgres/init/01-init.sh`, só quando o volume está vazio.
- **Backend** (`src/Backend/AccessControlSystem.Api/Dockerfile`): build multi-stage
  (SDK → runtime ASP.NET), HTTP na porta 8080 dentro do container. O redirecionamento HTTPS é
  desligado via `HttpsRedirection__Enabled=false`.
- **Migrator** (`src/Backend/AccessControlSystem.Migrator/Dockerfile`): sem argumentos roda
  `migrate` e depois `seed-dev`; com argumento roda só o verbo informado.
- **Frontend** (`src/Frontend/Dockerfile`): build multi-stage (Node → nginx). O Vite é compilado
  com `VITE_API_BASE_URL=/`, então as requisições vão para a mesma origem; o nginx
  (`src/Frontend/nginx.conf`) encaminha `/v1/` para o container `backend`.
- **Postgres**: dados persistidos no volume `pg-data`.

### Comandos úteis

```bash
docker compose up --build -d                    # sobe em background
docker compose logs -f backend                  # acompanha os logs da API do tenant A
docker compose logs migrator                    # resumo da atualização das bases
docker compose run --rm migrator migrate        # atualiza o catálogo e as bases
docker compose run --rm migrator seed-dev       # recria os dados sintéticos (idempotente)
docker compose down                             # para e remove os containers
docker compose down -v                          # idem + apaga o volume do banco
docker compose build backend                    # rebuild só do backend
```

Em qualquer ambiente que não seja `Development`, o `seed-dev` é recusado (código de saída 3) sem
escrever nada. Os códigos de saída do Migrator são `0` (ok), `1` (alguma base falhou), `2`
(configuração inválida), `3` (`seed-dev` recusado) e `4` (a trava global esgotou o tempo).

### Configuração

Copie `.env.example` para `.env` (o `.env` não é versionado). Variáveis reconhecidas:
`POSTGRES_PASSWORD`, `ACS_MIGRATOR_PASSWORD`, `ACS_APP_PASSWORD`, `JWT_PRIVATE_KEY`,
`PASSWORD_SALT_KEY` (legado, sem uso no código mas ainda exigida), `DEV_ADMIN_PASSWORD`,
`DEV_OPERATOR_PASSWORD_A`, `DEV_OPERATOR_PASSWORD_B`, `BACKEND_PORT` (5290), `BACKEND_B_PORT`
(5291), `BACKEND_DEDICATED_PORT` (5292), `FRONTEND_PORT` (5173), `DB_PORT` (5433) e
`ASPNETCORE_ENVIRONMENT` (Development). Evite `;`, `=` e espaços nas senhas: elas entram em
connection strings.

## Rodando sem Docker

Cada parte tem o seu próprio README:

- [`src/Backend/`](src/Backend/) — requer .NET 10 SDK e um PostgreSQL acessível.
- [`src/Frontend/README.md`](src/Frontend/README.md) — requer Node.js 20+.
