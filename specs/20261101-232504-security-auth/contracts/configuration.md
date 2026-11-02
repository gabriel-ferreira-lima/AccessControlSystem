# Contrato: configuração nova

**Spec**: [../spec.md](../spec.md) | **Decisões**: [../research.md](../research.md) (R-07, R-11, R-13, R-18)

Chaves em `appsettings.json` quando têm valor padrão seguro; **segredos e identidades obrigatórias ficam
vazias no arquivo versionado** e vêm de variável de ambiente, user-secrets ou do `.env`. Em variável de
ambiente, `:` vira `__` e posição de lista vira `__0`, `__1`.

## Obrigatórias (a Api **não inicia** sem elas)

| Chave | Exigência | Falha de partida (`OptionsValidationException`) |
|---|---|---|
| `Secrets:JwtPrivateKey` | texto cujo tamanho em **bytes UTF-8** seja ≥ 32 (a medida usada para assinar) | `Secrets:JwtPrivateKey é obrigatória.` / `Secrets:JwtPrivateKey deve ter pelo menos 32 bytes.` (nunca mostra o valor) |
| `Jwt:Issuer` | não vazio | `Jwt:Issuer é obrigatório.` |
| `Jwt:Audience` | não vazio | `Jwt:Audience é obrigatório.` |

## Opcionais (têm padrão)

| Chave | Padrão | Faixa válida | Uso |
|---|---|---|---|
| `ForwardedHeaders:KnownProxies` | `[]` | IPs válidos | IPs de proxies cujos `X-Forwarded-*` valem |
| `ForwardedHeaders:KnownNetworks` | `[]` | CIDRs válidos | CIDRs de proxies cujos `X-Forwarded-*` valem |
| `LoginProtection:Ip:PermitLimit` | `20` | 1 a 1000 | tentativas de login por origem por janela |
| `LoginProtection:Ip:WindowSeconds` | `60` | 1 a 3600 | janela do limite por origem |
| `LoginProtection:Account:MaxFailures` | `5` | 3 a 100 | tentativas contadas por nível até bloquear a conta |
| `LoginProtection:Account:BaseWaitSeconds` | `60` | 1 a 3600 | primeira espera |
| `LoginProtection:Account:MaxWaitSeconds` | `900` | 1 a 86400, ≥ `BaseWaitSeconds` | teto da espera |
| `LoginProtection:Account:IdleExpirySeconds` | `900` | 1 a 86400, ≥ `MaxWaitSeconds` | descarte do registro sem tentativa nem bloqueio |
| `LoginProtection:Account:MaxTrackedAccounts` | `100000` | 100 a 10000000 | tamanho máximo do registro por instância |

Valor fora da faixa (ou regra entre campos violada) derruba a partida com a chave na mensagem. Os tetos
existem para que um valor errado (ex.: `MaxFailures = 1000000`) não desligue a proteção na prática.

Regras de `ForwardedHeaders`:

- A lista **nunca** é limpa: sem configuração valem só os padrões do framework (loopback). Valor que não
  seja IP ou CIDR válido derruba a partida com mensagem indicando a chave.
- `ForwardLimit = 1`: só o último salto do `X-Forwarded-For` é considerado.

## docker compose (local)

| Variável (`.env`) | Efeito | Padrão |
|---|---|---|
| `JWT_ISSUER` | `Jwt__Issuer` das três Apis | `access-control-system` |
| `JWT_AUDIENCE` | `Jwt__Audience` das três Apis | `access-control-system-console` |
| `LOGIN_IP_PERMIT_LIMIT` | `LoginProtection__Ip__PermitLimit` | `20` (só mude na medição do quickstart; use no máximo `1000`, e volte para `20` depois) |
| `LOGIN_MAX_FAILURES` | `LoginProtection__Account__MaxFailures` | `5` (idem; no máximo `100`, e volte para `5` depois) |

Valores fixos no `docker-compose.yml` (não são variáveis do `.env`):

- Rede padrão com sub-rede `172.31.250.0/24`; serviço `frontend` (nginx) com IP `172.31.250.10`.
- Em cada Api: `ForwardedHeaders__KnownProxies__0: "172.31.250.10"`.
- Portas publicadas: `127.0.0.1:${BACKEND_PORT:-5290}:8080`, `127.0.0.1:${BACKEND_B_PORT:-5291}:8080`,
  `127.0.0.1:${BACKEND_DEDICATED_PORT:-5292}:8080`, `127.0.0.1:${DB_PORT:-5433}:5432`. A porta do console
  (`FRONTEND_PORT`) não muda.

`JWT_PRIVATE_KEY` continua obrigatória no `.env`; `.env.example` passa a explicar o mínimo de 32 bytes
(`openssl rand -base64 64` gera 88).

## Ambientes futuros (spec 05)

`Jwt:Issuer`/`Jwt:Audience` devem ser **diferentes por ambiente**, assim como a chave, para um token de
homologação nunca valer em produção. `ForwardedHeaders` deve apontar para o proxy/balanceador real.
