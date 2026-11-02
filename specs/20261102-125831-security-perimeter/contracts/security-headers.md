# Contrato: cabeçalhos de segurança do console

**Spec**: FR-009 a FR-018 | **Plano**: Fase C | **Princípio**: IX ("Rede")

Definidos em `src/Frontend/security-headers.conf` e incluídos em todo `server`/`location` do
`nginx.conf` que responde ao navegador. Todos com `always` (saem também em 4xx/5xx).

## Cabeçalhos

| Cabeçalho | Valor | Quando |
|---|---|---|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' 'sha256-<hash do script do tema>'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'` | sempre |
| `X-Frame-Options` | `DENY` | sempre (legado, para navegadores sem `frame-ancestors`) |
| `X-Content-Type-Options` | `nosniff` | sempre |
| `Referrer-Policy` | `no-referrer` | sempre (igual ao `<meta name="referrer">` do `index.html`) |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=(), usb=()` | sempre |
| `Strict-Transport-Security` | `max-age=31536000` | **somente** quando a requisição chega com `X-Forwarded-Proto: https`; ausente caso contrário |

Sem `includeSubDomains`, sem `preload`, sem `upgrade-insecure-requests`.

O `<hash>` é o `sha256` (base64) do texto do único `<script>` escrito em `index.html`, com quebras
de linha em LF. Como recalcular: rodar `npm test` em `src/Frontend`; o teste
`security-headers.test.ts` falha dizendo o `'sha256-…'` correto; colar na CSP do
`security-headers.conf`.

## Por tipo de resposta (o que `verify-security-headers.sh` confere)

Cada linha é testada **sem** e **com** `X-Forwarded-Proto: https` na requisição.

| Resposta | Exemplo | Cabeçalhos do conjunto | HSTS |
|---|---|---|---|
| Página | `GET /` | os 5 fixos, **uma vez cada** | só com `https` |
| Arquivo estático (`location /assets/`) | `GET /assets/index-….js` | os 5 fixos (o `location` tem `add_header` próprio e repete o `include`), mais `Cache-Control` | só com `https` |
| Erro do nginx | `GET /assets/nao-existe.js` → `404` | os 5 fixos | só com `https` |
| Encaminhada à Api | `GET /v1/ping` (upstream simulado) | os 5 fixos, **sem duplicata** mesmo que o upstream já envie um deles (`proxy_hide_header`) | só com `https` |

Adicionalmente: o hash `sha256` presente na CSP é o do script escrito na página **servida**.

## Regras de manutenção do `nginx.conf`

1. Todo `location` que declare `add_header` próprio DEVE repetir o `include` do trecho (o nginx não
   herda `add_header` do `server` quando o `location` tem algum).
2. Todo `add_header` de segurança DEVE levar `always`.
3. Novo cabeçalho que a Api também envie DEVE entrar na lista de `proxy_hide_header` do `/v1/`.
4. Estilo escrito na página (`<style>`, atributo `style=""` no HTML) é bloqueado por `style-src
   'self'`; se o build um dia gerar, a verificação do CI falha e a decisão é revista.

## O que não é coberto

- Redirecionamento HTTP→HTTPS e o TLS em si: ALB, spec 05 (FR-018).
- Que o console só seja alcançável pelo load balancer em produção: spec 05.
- Erros de CSP em tempo de execução no navegador (tema claro/escuro): conferência manual, no
  [quickstart.md](../quickstart.md).
