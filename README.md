# Sistema de Controle de Acesso

Monorepo com duas partes:

| Pasta            | Stack                                   | Descrição                                    |
| ---------------- | --------------------------------------- | -------------------------------------------- |
| `src/Backend/`   | ASP.NET Core (.NET 10), EF Core, JWT    | API REST + PostgreSQL                        |
| `src/Frontend/`  | Vite 6 + React 19 + TypeScript          | Console administrativo (SPA de apresentação) |

## Rodando com Docker (desenvolvimento)

Pré-requisito: Docker Desktop (ou Docker Engine + Compose v2).

```bash
cp .env.example .env        # preencha JWT_PRIVATE_KEY (veja o arquivo)
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
| `backend`           | http://127.0.0.1:5290 | **A**, na base compartilhada `shared_01`   |
| `backend-b`         | http://127.0.0.1:5291 | **B**, na mesma base compartilhada         |
| `backend-dedicated` | http://127.0.0.1:5292 | **D**, na base dedicada `dedicated_01`     |
| Postgres            | 127.0.0.1:5433        | só para inspeção com um cliente            |

**As portas das APIs e a do Postgres só aceitam conexões da própria máquina** (`127.0.0.1`), não de
outros computadores da rede: o ambiente local tem credenciais de desenvolvimento e APIs sem proxy na
frente. Só o console (nginx, `http://localhost:5173`) é publicado como antes. No Windows, `localhost`
pode resolver para IPv6 (`::1`) antes de IPv4 e não alcançar essas portas; use `127.0.0.1`.

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
  (`src/Frontend/nginx.conf`) encaminha `/v1/` para o container `backend` e entrega o console com cabeçalhos de
  segurança (CSP sem `unsafe-inline`, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`) e
  HSTS só quando o acesso chega com `X-Forwarded-Proto: https` (ver `src/Frontend/README.md`, "Cabeçalhos de
  segurança do console").
- **Postgres**: dados persistidos no volume `pg-data`.

### Segurança da autenticação e da sessão

- **Rotas fechadas por padrão**: toda rota exige token; só o login é anônimo. O **Swagger só existe em
  `Development`** (fora dele, `/swagger/*` responde `404`).
- **Token**: a API só aceita token emitido por este sistema (`Jwt:Issuer`) para esta aplicação
  (`Jwt:Audience`), assinado em HS256, com tolerância de relógio de 30 s e validade de 8 h. A chave
  (`JWT_PRIVATE_KEY`) precisa ter **pelo menos 32 bytes** (o texto como está, sem decodificar base64);
  com chave ausente ou curta, ou sem emissor/destinatário, a API **não inicia**. `openssl rand -base64 64`
  gera uma chave de 88 caracteres.
- **Sessão que morre na hora**: trocar a senha de um operador (ele mesmo ou um administrador), mudar o
  perfil dele para outro ou desativá-lo derruba os tokens dele na requisição seguinte (reativar **não** os
  revalida). Trocar só o e-mail não derruba.
- **Login**: e-mail inexistente, conta inativa e senha errada recebem a mesma `401` e o mesmo trabalho de
  hash. O limite é por origem (20 por minuto) **e** por conta (5 tentativas seguidas bloqueiam a conta por
  1, 2, 4, 8 e até 15 minutos, com `Retry-After`). O limite por conta fica **na memória de cada instância**
  (violação temporária da constituição, resolvida com Redis na spec 06).
- **Proxy confiável**: a API só acredita em `X-Forwarded-For` vindo de um proxy conhecido (no compose, o
  nginx, em `172.31.250.10`); de qualquer outra origem o cabeçalho é ignorado e o limite usa o endereço
  real. A rede do compose é fixa (`172.31.250.0/24`). **Se colidir** com outra rede da sua máquina, o
  `docker compose up` acusa *"Pool overlaps"*: troque a sub-rede, o `ipv4_address` do `frontend` e o
  `ForwardedHeaders__KnownProxies__0` no `docker-compose.yml` (os três juntos).
- **Migration nova**: `AddSecurityVersionToOperators` (coluna `operators.security_version`) é aplicada
  **só pelo Migrator** (`docker compose run --rm migrator migrate`). Até lá, a API não funciona numa base
  que ainda não tem a coluna.

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
`POSTGRES_PASSWORD`, `ACS_MIGRATOR_PASSWORD`, `ACS_APP_PASSWORD`, `JWT_PRIVATE_KEY` (**mínimo 32
bytes**), `JWT_ISSUER` e `JWT_AUDIENCE` (opcionais; padrões `access-control-system` e
`access-control-system-console`; em homologação e produção devem ser **diferentes por ambiente**),
`DEV_ADMIN_PASSWORD`,
`DEV_OPERATOR_PASSWORD_A`, `DEV_OPERATOR_PASSWORD_B`, `BACKEND_PORT` (5290), `BACKEND_B_PORT`
(5291), `BACKEND_DEDICATED_PORT` (5292), `FRONTEND_PORT` (5173), `DB_PORT` (5433) e
`ASPNETCORE_ENVIRONMENT` (Development). Evite `;`, `=` e espaços nas senhas: elas entram em
connection strings.

`LOGIN_IP_PERMIT_LIMIT` (padrão 20, faixa 1 a 1000) e `LOGIN_MAX_FAILURES` (padrão 5, faixa 3 a 100)
existem **só para a medição de tempo do login** (veja o `quickstart.md` da spec de segurança): mude-as
por variável do shell, não no `.env`, e **volte para 20 e 5** depois; fora da faixa a API não inicia.

## Rodando sem Docker

Cada parte tem o seu próprio README:

- [`src/Backend/`](src/Backend/) — requer .NET 10 SDK e um PostgreSQL acessível.
- [`src/Frontend/README.md`](src/Frontend/README.md) — requer Node.js 20+.
