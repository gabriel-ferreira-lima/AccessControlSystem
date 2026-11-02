# Quickstart: validação da segurança do perímetro

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Roteiro para provar, de ponta a ponta, que a feature funciona. Os comandos assumem Git Bash (ou
PowerShell onde indicado) na raiz do repositório. Os contratos estão em [contracts/](contracts/).

## Pré-requisitos

- Docker ligado (testes de integração e imagens).
- `.env` na raiz preenchido (copie de `.env.example`): `POSTGRES_PASSWORD`, `ACS_MIGRATOR_PASSWORD`,
  `ACS_APP_PASSWORD`, `JWT_PRIVATE_KEY`, `DEV_ADMIN_PASSWORD` etc.
- .NET 10 SDK e Node 22+ (o CI usa Node 22).
- Rede para a consulta de vulnerabilidades (NuGet e npm).

## 0. Linha de base (antes de qualquer mudança)

```bash
cd src/Backend && dotnet test Backend.slnx            # anote o total e os que passam
cd ../Frontend && npm test                             # idem
```

Registre os números no `tasks.md`. No fim, nenhum teste anterior pode ter sido apagado, pulado ou
enfraquecido (SC-011).

## 1. Tamanhos máximos (Fase A)

Testes:

```bash
cd src/Backend
dotnet test Backend.slnx --filter "FullyQualifiedName~Specification|FullyQualifiedName~RequestTextLimits|FullyQualifiedName~InputLimits|FullyQualifiedName~ContractExtensions|FullyQualifiedName~AuthenticateHandler"
```

Esperado: tudo verde; o `RequestTextLimitsTests` lista os campos que cobriu.

Na prática, com o ambiente de pé (`docker compose up --build`, na raiz):

```bash
# senha de 1 MB no login → 400, sem hash e sem consulta ao banco (direto na Api do tenant A, porta 5290)
head -c 1048576 /dev/zero | tr '\0' 'a' > /tmp/big.txt
printf '{"email":"admin@x.com","password":"%s"}' "$(cat /tmp/big.txt)" > /tmp/login-big.json
curl -s -o /dev/null -w "%{http_code}\n" -H "Content-Type: application/json" \
  --data-binary @/tmp/login-big.json http://127.0.0.1:5290/v1/account/login
```

Esperado: `400`. Pelo console (`http://localhost:5173`) um corpo acima de 1 MiB nem chega à Api: o nginx tem o limite
padrão de corpo (`client_max_body_size 1m`) e responde `413`; com uma senha de 900 KB (ou 129 caracteres) pelo console
o resultado é o mesmo `400` da Api. O limite do corpo inteiro da requisição não faz parte desta spec. O corpo é `{"message":"Requisição inválida", … "notifications":[{"key":"Password", …}]}`.
Com senha de exatamente 128 caracteres o resultado deixa de ser `400` (vira `401`, credencial
errada).

## 2. Usuário de banco da aplicação (Fase B)

```bash
cd src/Backend
dotnet test Backend.slnx --filter "FullyQualifiedName~AppRolePrivilege|FullyQualifiedName~CatalogReadOnly|FullyQualifiedName~DedicatedRowLevelSecurity|FullyQualifiedName~AppRoleProbeCanary|FullyQualifiedName~SchemaContract|FullyQualifiedName~RowLevelSecurity"
```

Esperado: verde nas três bases. Os testes de **canário** mostram que, dado um privilégio a mais, a
sonda acusa a violação (a prova falha quando a permissão sobra, FR-023).

## 3. Cabeçalhos do console (Fase C)

Com o ambiente de pé:

```bash
curl -sI http://localhost:5173/                                   # página, HTTP simples
curl -sI -H "X-Forwarded-Proto: https" http://localhost:5173/     # com a indicação de HTTPS
curl -sI http://localhost:5173/assets/nao-existe.js               # 404 do nginx
```

Esperado nos três: `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options:
nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy`. `Strict-Transport-Security:
max-age=31536000` **só** no segundo.

Verificação automática (a mesma do CI), que sobe a imagem e um upstream simulado:

```bash
cd src/Frontend && bash scripts/verify-security-headers.sh
```

Esperado: `OK` em página, arquivo estático, 404 e rota encaminhada, com e sem `https`, e o hash da
CSP igual ao do script servido.

Teste do hash do script do tema:

```bash
cd src/Frontend && npm test -- security-headers
```

Para ver o teste falhar de propósito: altere qualquer caractere do `<script>` em `index.html`; o teste
mostra o `'sha256-…'` novo para colar em `security-headers.conf`. Reverta a alteração de teste.

### Conferência manual do tema (no navegador)

1. Abra `http://localhost:5173`, DevTools → Console.
2. Tema claro e depois escuro (botão do console, ou `localStorage.setItem("acs.theme","dark")` e
   recarregar; e a preferência do sistema operacional com `acs.theme` removido).
3. Esperado: tema aplicado **antes** da tela aparecer (sem flash) e **nenhum** erro "Refused to
   execute inline script"/CSP no console.
4. Teste de quadro: numa página HTML local qualquer com `<iframe src="http://localhost:5173">`, o
   console não deve aparecer (erro de `frame-ancestors`/`X-Frame-Options` no console).

## 4. Pacotes (Fase D)

```bash
cd src/Frontend && npm audit                      # esperado: found 0 vulnerabilities
cd ../Backend && dotnet restore Backend.slnx      # esperado: sem NU1901–NU1904 (a exceção do SSH.NET está no csproj de testes)
dotnet build Backend.slnx
```

Prova da exceção por aviso: remova (temporariamente) um dos dois `NuGetAuditSuppress` do projeto de
testes e rode `dotnet restore`; deve falhar com `NU1903`. Restaure a linha.

### Prova de CI vermelho (PR descartável) — validação do **revisor**, depois do push

Não é tarefa do implementador (exige push e PR no GitHub). O implementador prova localmente que, sem
a exceção, o `dotnet build` falha com `NU1903`, e que `npm audit` sai com código 1 com um pacote
vulnerável.

1. Branch `tmp/vuln-check-backend` a partir da branch da feature, acrescentando um
   `PackageReference` vulnerável conhecido (ex.: `Newtonsoft.Json 12.0.3`) a um projeto; PR em
   **rascunho** → o passo de auditoria do `Backend CI` deve ficar **vermelho**.
2. Branch `tmp/vuln-check-frontend` com `npm install --save-dev lodash@4.17.15` → o passo `npm
   audit` do `Frontend CI` deve ficar **vermelho**.
3. Feche os dois PRs sem merge e apague as branches. Anote os links no `tasks.md`.

## 5. Portões de qualidade finais (Constituição)

```bash
cd src/Backend && dotnet build Backend.slnx && dotnet test Backend.slnx
cd ../Frontend && npm run typecheck && npm test && npm run build && npm audit
cd ../.. && docker compose up --build
```

Esperado: tudo verde; `curl -I` do item 3 conferido; tema conferido no navegador. O PR da feature
deve ficar verde no `Backend CI` e no `Frontend CI` (incluindo os passos novos de auditoria e de
cabeçalhos).

## Como registrar uma exceção de vulnerabilidade

Veja [contracts/dependency-audit.md](contracts/dependency-audit.md): por aviso (URL do GHSA), no menor
escopo, com a justificativa em comentário ao lado; a revisão do PR confere.
