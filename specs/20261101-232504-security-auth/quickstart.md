# Quickstart: validar a segurança da autenticação e da sessão

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Contratos**: [contracts/](contracts/)

Guia de validação ao vivo, para rodar depois das 6 fases. Os comandos são para o **Git Bash** (use
`curl.exe` no PowerShell) e para a raiz do repositório, salvo quando indicado. Valores entre `<...>` vêm do
seu `.env`.

## 0. Pré-requisitos

- Docker ligado; `.env` criado a partir de `.env.example` com `JWT_PRIVATE_KEY` de **pelo menos 32 bytes**
  (`openssl rand -base64 64`) e as senhas de desenvolvimento.
- Os comandos `curl` são para o **Git Bash** (no PowerShell use `curl.exe`); onde há variável de ambiente
  temporária (passos 5 e 9) o guia dá as duas sintaxes, Git Bash e PowerShell.
- Variáveis novas são **opcionais** (`JWT_ISSUER`, `JWT_AUDIENCE`, `LOGIN_IP_PERMIT_LIMIT`,
  `LOGIN_MAX_FAILURES`); deixe os padrões (20 e 5), exceto no passo 9, que manda voltar a eles. As opções
  têm faixa (`LOGIN_IP_PERMIT_LIMIT` de 1 a 1000, `LOGIN_MAX_FAILURES` de 3 a 100): fora dela a Api não sobe.
- Se `docker compose up` acusar *"Pool overlaps"*, outra rede sua usa `172.31.250.0/24`: troque a sub-rede
  e o IP do `frontend` no `docker-compose.yml` e o `ForwardedHeaders__KnownProxies__0` das três Apis.

## 1. Quality gates (sem Docker de aplicação)

```bash
cd src/Backend
dotnet build Backend.slnx                                   # 0 avisos, 0 erros
dotnet test Backend.slnx                                    # inclui integração (Docker ligado)
cd ../Frontend
npm run typecheck && npm test && npm run build
```

Esperado: tudo verde. Os números exatos de testes saem do `tasks.md`.

## 2. Subir o ambiente

```bash
docker compose run --rm --build migrator migrate   # aplica AddSecurityVersionToOperators (único caminho);
                                                   # --build evita rodar uma imagem antiga, sem a migration
docker compose up --build -d
```

Esperado: `acs-backend`, `acs-backend-b`, `acs-backend-dedicated`, `acs-db` e `acs-frontend` em execução.
Sem `JWT_ISSUER`/`JWT_AUDIENCE` o compose usa os padrões; sem chave ou com chave curta a Api **não sobe**
(`docker compose logs backend` mostra `Secrets:JwtPrivateKey deve ter pelo menos 32 bytes.`).

## 3. Portas só na máquina (FR-026, SC-007)

```bash
docker compose ps --format "table {{.Name}}\t{{.Ports}}"
```

Esperado: Apis e banco como `127.0.0.1:5290->8080/tcp`, `127.0.0.1:5291->8080/tcp`,
`127.0.0.1:5292->8080/tcp`, `127.0.0.1:5433->5432/tcp`; só o console em `0.0.0.0:5173`. De **outro
computador** da rede (ou com o IP da sua rede local), `Test-NetConnection <seu-ip> -Port 5290` falha.

## 4. Login, claims e rotas fechadas (US1, US3)

```bash
API=http://127.0.0.1:5290
login() { curl -s -X POST $API/v1/account/login -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}"; }
TOKEN=$(login admin@admin.com.br '<DEV_ADMIN_PASSWORD>' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
echo $TOKEN | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null; echo
```

Esperado no payload: `iss`, `aud`, `exp`, `Id`, `tenant` e `security_version` (0 ou o valor atual).

**Login em cada tenant de teste e pelo nginx (FR-027)**: o `admin@admin.com.br` existe nos três tenants, com
`<DEV_ADMIN_PASSWORD>`. Cada chamada deve devolver `200` e um token:

```bash
for porta in 5290 5291 5292; do        # A, B e D (base dedicada)
  echo -n "porta $porta: "; curl -s -o /dev/null -w '%{http_code}\n' -X POST http://127.0.0.1:$porta/v1/account/login \
    -H 'Content-Type: application/json' -d '{"email":"admin@admin.com.br","password":"<DEV_ADMIN_PASSWORD>"}'
done
# pelo nginx do console (tenant A):
curl -s -o /dev/null -w 'nginx: %{http_code}\n' -X POST http://127.0.0.1:5173/v1/account/login \
  -H 'Content-Type: application/json' -d '{"email":"admin@admin.com.br","password":"<DEV_ADMIN_PASSWORD>"}'
```

```powershell
# PowerShell (use curl.exe, não o alias curl)
foreach ($porta in 5290, 5291, 5292) {
  "porta ${porta}: " + (curl.exe -s -o NUL -w "%{http_code}" -X POST "http://127.0.0.1:$porta/v1/account/login" `
    -H "Content-Type: application/json" -d '{\"email\":\"admin@admin.com.br\",\"password\":\"<DEV_ADMIN_PASSWORD>\"}')
}
"nginx: " + (curl.exe -s -o NUL -w "%{http_code}" -X POST "http://127.0.0.1:5173/v1/account/login" `
  -H "Content-Type: application/json" -d '{\"email\":\"admin@admin.com.br\",\"password\":\"<DEV_ADMIN_PASSWORD>\"}')
```

```bash
curl -s -o /dev/null -w '%{http_code}\n' $API/v1/account/get-me                         # 401
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" $API/v1/account/get-me   # 200
curl -s -o /dev/null -w '%{http_code}\n' $API/swagger/v1/swagger.json                    # 200 (Development)
```

## 5. Swagger fora de Development (FR-003)

Use uma **variável de ambiente do shell** (tem precedência sobre o `.env` e não toca o seu `.env`). Exporte e
rode o `docker compose` **no mesmo shell**:

```bash
# Git Bash
export ASPNETCORE_ENVIRONMENT=Production
docker compose up -d backend
```

```powershell
# PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Production"
docker compose up -d backend
```

```bash
curl -s -o /dev/null -w '%{http_code}\n' $API/swagger/v1/swagger.json     # 404
curl -s -o /dev/null -w '%{http_code}\n' $API/swagger/index.html           # 404
```

Volte a Development limpando a variável e recriando a Api:

```bash
# Git Bash
unset ASPNETCORE_ENVIRONMENT
docker compose up -d backend
```

```powershell
# PowerShell
Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
docker compose up -d backend
```

## 6. Token antigo depois de troca de senha, perfil e desativação (US2)

```bash
# 6.1 senha: o próprio operador troca; o token antigo morre
OP=$(login operador@exemplo.com '<DEV_OPERATOR_PASSWORD_A>' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $OP" $API/v1/account/get-me      # 200
curl -s -X PUT $API/v1/account/update-me -H "Authorization: Bearer $OP" -H 'Content-Type: application/json' \
  -d '{"password":"Nova!Senha2026"}'
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $OP" $API/v1/account/get-me      # 401
login operador@exemplo.com 'Nova!Senha2026' | grep -c '"token"'                                       # 1

# 6.2 desativar e reativar: o token anterior continua recusado
OPID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/v1/account/get-list?page=1&size=100" | grep -o '"id":"[^"]*","email":"operador@exemplo.com' | cut -d'"' -f4)
OP2=$(login operador@exemplo.com 'Nova!Senha2026' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
curl -s -X PUT $API/v1/account/deactivate/$OPID -H "Authorization: Bearer $TOKEN"
curl -s -X PUT $API/v1/account/activate/$OPID   -H "Authorization: Bearer $TOKEN"
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $OP2" $API/v1/account/get-me      # 401
```

Esperado: `200`, `401`, `1`, `401`. Para o **perfil**, use um operador **descartável** (crie um com `create`,
perfil `2`), troque o perfil dele por `update/{id}` com `{"role":1}` e confira que o token anterior dá `401`.
**Não faça isso com o `operador@exemplo.com`**: a regra de domínio não deixa rebaixar um administrador, então o
perfil dele não volta pela Api. Se `get-list` mudar de formato, pegue o `id` na tela de operadores do console.
Restaure a senha do operador para `<DEV_OPERATOR_PASSWORD_A>` (com `update/{id}`) ao terminar.

## 7. `X-Forwarded-For` forjado não contorna o limite (US5, SC-006)

```bash
# Direto na Api (a origem é o gateway do Docker, não o proxy conhecido): o cabeçalho é ignorado
for i in $(seq 25); do
  curl -s -o /dev/null -w '%{http_code} ' -X POST $API/v1/account/login -H 'Content-Type: application/json' \
    -H "X-Forwarded-For: 203.0.113.$i" -d "{\"email\":\"ip$i@exemplo.com\",\"password\":\"errada\"}"
done; echo
```

Use **um e-mail diferente a cada tentativa** (`ip$i@...`): com o mesmo e-mail, o limite **por conta** bloqueia
na 6ª falha (`401` ×5 e depois `429`) e esconde o limite por origem.

Esperado: `401` nas primeiras 20 e `429` nas seguintes (cada IP forjado deveria dar 25 `401`). Aguarde 1 min
(ou `docker compose restart backend`). Pelo console (proxy conhecido), o IP usado é o **real** que o nginx
viu: `http://127.0.0.1:5173/v1/...` com o mesmo cabeçalho forjado e e-mails diferentes também termina em `429`.

## 8. Bloqueio por conta (US5, SC-005)

```bash
for i in 1 2 3 4 5; do login admin@admin.com.br errada | grep -o '"status":[0-9]*'; done      # 401 x5
curl -s -i -X POST $API/v1/account/login -H 'Content-Type: application/json' \
  -d '{"email":"admin@admin.com.br","password":"<DEV_ADMIN_PASSWORD>"}' | grep -iE "^HTTP|retry-after|message"
```

Esperado na 6ª tentativa (senha **correta**): `429`, `Retry-After: 60` (aprox.) e
`Muitas tentativas. Tente novamente em instantes.`. O mesmo e-mail no **tenant B**
(`http://127.0.0.1:5291`) continua livre. Um e-mail que não existe (`naoexiste@exemplo.com`) bloqueia do mesmo
jeito depois de 5 falhas. Após 1 min a conta volta; se as falhas recomeçarem, a espera seguinte é de 2 min.

## 9. Tempo de resposta do login (SC-004, medição manual, fora do CI)

Prepare com **variáveis de ambiente do shell** (os valores são os **tetos** permitidos; valores maiores
derrubam a partida; elas têm precedência sobre o `.env` e não tocam o seu `.env`). Exporte e rode o
`docker compose` **no mesmo shell**:

```bash
# Git Bash
export LOGIN_IP_PERMIT_LIMIT=1000 LOGIN_MAX_FAILURES=100
docker compose up -d        # recria as Apis
```

```powershell
# PowerShell
$env:LOGIN_IP_PERMIT_LIMIT = "1000"; $env:LOGIN_MAX_FAILURES = "100"
docker compose up -d        # recria as Apis
```

Cada caso faz 30 tentativas na mesma conta, folgadamente abaixo de 100. **Entre uma rodada e outra rode
`docker compose restart backend`**: o registro de falhas é em memória e as tentativas acumulam por 15 min.
Crie um operador inativo (precisa de senha forte):

```bash
curl -s -X POST $API/v1/account/create -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"inativo@exemplo.com","password":"Str0ng!Pass1","role":2}'
IID=<id devolvido>; curl -s -X PUT $API/v1/account/deactivate/$IID -H "Authorization: Bearer $TOKEN"
```

Meça 30 logins de cada caso e some a **mediana** (média do 15º e do 16º valor ordenado):

```bash
median() { for i in $(seq 30); do curl -s -o /dev/null -w '%{time_total}\n' -X POST $API/v1/account/login \
  -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$2\"}"; done \
  | sort -n | sed -n '15,16p' | awk '{s+=$1} END {printf "%.4f\n", s/2}'; }
median naoexiste@exemplo.com 'Qualquer!Coisa1'      # e-mail inexistente
median inativo@exemplo.com   'Str0ng!Pass1'          # conta inativa, senha correta
median admin@admin.com.br    'Errada!Senha1'         # senha errada
```

Esperado: a maior mediana é no máximo 10% maior que a menor. Rode com a máquina ociosa; se ficar na
borda, repita. **Registre os três valores aqui** (data, máquina, medianas) para a entrega:

| Data | Máquina | Inexistente | Inativa | Senha errada | Diferença máx. |
|---|---|---|---|---|---|
| 2026-11-02 | i7-12700H, 16 GB, Windows 11 + Docker Desktop; rodada 1 | 0,0111 s | 0,0107 s | 0,0099 s | 12,1% |
| 2026-11-02 | idem; rodada 2 | 0,0095 s | 0,0112 s | 0,0105 s | 17,9% |
| 2026-11-02 | idem; rodada 3 | 0,0105 s | 0,0099 s | 0,0112 s | 13,1% |
| 2026-11-02 | **90 amostras por caso (3 rodadas juntas)** | **0,0105 s** | **0,0106 s** | **0,0105 s** | **1,0%** |

Leitura: cada operação leva ~10 ms, então 1 ms de variação da máquina já é 10%. Medir um caso inteiro e só
depois o seguinte deixa essa variação cair num caso só: as três rodadas acima passaram de 10% e o caso mais
lento mudou a cada rodada (ruído, não viés).

**Método válido (revisão de 2026-11-02): casos intercalados.** Alterne os três casos a cada tentativa
(inexistente, inativa, senha errada, de novo inexistente...), 95 vezes, e tire a mediana de cada caso
(`time_starttransfer`). Use um e-mail inexistente diferente a cada tentativa e recrie a Api entre rodadas
(o registro de falhas é em memória). Assim a variação da máquina atinge os três casos por igual:

| Data | Máquina | Inexistente | Inativa | Senha errada | Diferença máx. |
|---|---|---|---|---|---|
| 2026-11-02 | idem; intercalado, 95 por caso, rodada A | 0,00989 s | 0,01013 s | 0,00996 s | 2,4% |
| 2026-11-02 | idem; intercalado, 95 por caso, rodada B | 0,00988 s | 0,00991 s | 0,01011 s | 2,3% |

Resultado do SC-004: **aprovado** (as duas rodadas intercaladas ficaram abaixo de 10%).

**Depois da medição, volte aos padrões (obrigatório)**: limpe as duas variáveis, recrie as Apis e **confira**
(no mesmo shell em que exportou):

```bash
# Git Bash
unset LOGIN_IP_PERMIT_LIMIT LOGIN_MAX_FAILURES
docker compose up -d
docker compose config | grep -E "PermitLimit|MaxFailures"      # 20 e 5
```

```powershell
# PowerShell
Remove-Item Env:LOGIN_IP_PERMIT_LIMIT, Env:LOGIN_MAX_FAILURES -ErrorAction SilentlyContinue
docker compose up -d
docker compose config | Select-String "PermitLimit|MaxFailures"    # 20 e 5
```

Se o shell foi fechado, as variáveis já sumiram; ainda assim rode o `docker compose up -d` e a conferência.

Para ter certeza, repita o passo 8 (a 6ª tentativa tem de dar `429`) e o passo 7.

## 10. Console: troca da própria senha (US2, cenário 7)

Em `http://127.0.0.1:5173`, entre como operador, vá em **Minha conta**, informe uma nova senha e salve.
Esperado: o console volta ao login com **"Senha alterada. Faça login novamente."**; a senha antiga não entra e
a nova entra. Trocar só o e-mail **não** encerra a sessão.

## 11. Fechamento

- Todos os passos 3 a 10 com o resultado esperado, o passo 9 preenchido **e o `.env` de volta aos padrões
  20 e 5**.
- `git status` mostra só os arquivos desta spec; **nenhum** commit foi feito pelo agente.
- Desfazer o ambiente de teste, se quiser: `docker compose down` (sem `-v`, para manter os dados).
