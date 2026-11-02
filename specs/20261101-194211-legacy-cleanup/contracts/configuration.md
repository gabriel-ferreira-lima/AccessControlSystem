# Contrato: remoção da configuração `PasswordSaltKey`

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Fase **A**. Caminhos relativos à raiz do repositório. Estado de 2026-11-01.

## 1. Fato verificado

Nenhum código C# lê a chave: `SecretsOptions` (`src/Backend/AccessControlSystem.Api/Options/SecretsOptions.cs`)
só tem `JwtPrivateKey`; a validação de opções (`OptionsValidationTests`) e o `ApiFactory` só tratam
`Secrets:JwtPrivateKey`; nenhum workflow (`backend-ci.yml`, `frontend-ci.yml`) a cita. Ela existe só
em arquivos de configuração e de documentação.

## 2. O que muda (4 arquivos, 6 linhas)

| Arquivo | Linha | Hoje | Depois |
|---|---|---|---|
| `src/Backend/AccessControlSystem.Api/appsettings.json` | 10-11 | `"JwtPrivateKey": "",` e `"PasswordSaltKey": ""` | só `"JwtPrivateKey": ""` (sem vírgula sobrando) |
| `docker-compose.yml` | 23 | `Secrets__PasswordSaltKey: ${PASSWORD_SALT_KEY:?defina PASSWORD_SALT_KEY no .env}` | linha removida (o bloco `x-backend-environment` segue com `Secrets__JwtPrivateKey`) |
| `.env.example` | 20-21 | comentário `# PASSWORD_SALT_KEY: sem uso no código (legado)...` e `PASSWORD_SALT_KEY=` | as duas linhas removidas |
| `README.md` (raiz) | 15 | `# preencha JWT_PRIVATE_KEY e PASSWORD_SALT_KEY (veja o arquivo)` | `# preencha JWT_PRIVATE_KEY (veja o arquivo)` |
| `README.md` (raiz) | 82 | lista de variáveis com `` `PASSWORD_SALT_KEY` (legado, sem uso no código mas ainda exigida), `` | item removido da lista |

## 3. O que já está limpo (conferido, nada a fazer)

`appsettings.Development.json` (só `Logging` e `Cors`), user-secrets de exemplo (não há), `Dockerfile`s,
workflows e `docker/`. O `.env` local do dono é gitignorado e **não é tocado**.

## 4. O que não muda

| Item | Estado |
|---|---|
| `Secrets:JwtPrivateKey` (obrigatória, validada na inicialização) | idêntica |
| Hash e verificação de senha (`SecureIdentity.PasswordHasher`, value object `Password`) | idênticos; a chave nunca participou, então nenhum hash existente é afetado |
| Registros históricos (`specs/20261031-221333-multi-tenant-foundation/{contracts/configuration.md,research.md,tasks.md}`) | **não reescritos** (descrevem o que foi decidido na época) |
| A constituição ("Legado conhecido") | não é editada pela entrega; o dono emenda depois do merge |

## 5. Validação (Red → Green)

| Verificação | Antes | Depois |
|---|---|---|
| `docker compose --env-file <arquivo sem PASSWORD_SALT_KEY> config --quiet` | **exit 1** (`required variable PASSWORD_SALT_KEY is missing a value`) — confirmado em 2026-11-01 | exit 0 |
| O mesmo comando com um arquivo que **ainda tem** `PASSWORD_SALT_KEY` | exit 0 | exit 0 (a variável sobrando é ignorada) |
| `docker compose --env-file <sem a variável> up --build` + login com operador sintético | impossível (o compose recusa) | sobe e o login devolve `200` com token |
| `git grep -nE "PasswordSalt\|PASSWORD_SALT" -- . ':!specs' ':!.specify'` | 6 linhas em 4 arquivos | 0 resultados |

O arquivo temporário da validação nasce de `.env.example` (valores fictícios), nunca do `.env` real.
Roteiro completo em [quickstart.md](../quickstart.md), V1 e V7.
