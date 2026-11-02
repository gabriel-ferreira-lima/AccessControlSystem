# Contrato: verificações de saúde da Api

**Plano**: [../plan.md](../plan.md) | **Pesquisa**: R-14 | **Spec**: FR-019 a FR-023, FR-039

Duas rotas **anônimas**, fora de `v1/`, mapeadas por `MapHealthEndpoints()` (extensão da Api, com
comentário XML) sobre o `MapHealthChecks` do framework. Não são casos de uso, não têm controller e
não entram no `EndpointCatalog` nem no Swagger.

## Rotas

| Rota | Métodos | Autenticação | O que verifica |
|---|---|---|---|
| `/health/live` | `GET`, `HEAD` | anônima (`AllowAnonymous`) | só que o processo atende; **nenhuma** consulta a banco, catálogo ou recurso externo |
| `/health/ready` | `GET`, `HEAD` | anônima (`AllowAnonymous`) | o **catálogo**: alcança, credencial vale e o esquema existe; **bases de tenant não entram**; Redis ainda não (spec 06) |

Outros métodos (`POST`, `PUT`, `DELETE`...) respondem `405`.

## Respostas

| Caso | Código | `Content-Type` | Corpo | Cabeçalhos |
|---|---|---|---|---|
| `live`, processo atendendo | `200` | `text/plain` | `Healthy` | `Cache-Control` contendo `no-store` |
| `ready`, catálogo bom | `200` | `text/plain` | `Healthy` | `Cache-Control` contendo `no-store` |
| `ready`, catálogo fora / sem esquema / credencial inválida / acima de 2 s | `503` | `text/plain` | `Unhealthy` | `Cache-Control` contendo `no-store` |

**Nada além disso**: sem JSON, sem lista de verificações, sem tempos, sem versão, sem nome de
servidor ou de base, sem texto de exceção, sem dado de tenant. **Não usa** o envelope
`ErrorResponse` (a constituição o impõe só a 401, 403, 429 e 500). Teste confere o corpo **exato**.

## A verificação da pronta

- Sonda (`ICatalogProbe`): `SELECT 1 FROM tenant_databases LIMIT 1` pelo `CatalogDbContext`
  (`ConnectionStrings:Catalog`, papel `acs_app`, somente leitura). Qualquer exceção ou tempo
  limite = `Unhealthy`.
- **Tempo limite: 2 segundos** (`CancellationTokenSource` com o `TimeProvider` registrado; o
  `ApiFactory.WithFakeTime` o controla nos testes).
- **Cache de 5 s, com uma única consulta em voo** (proteção contra abuso): verificador singleton com
  `SemaphoreSlim(1,1)`; guarda **só** `ultimoResultado` e `validoAte`; TTL de 5 s para sucesso e
  falha; por instância de propósito; sem dado de tenant.
- Log em caso de falha: **nível Warning**, só o **tipo** da exceção (nunca a mensagem nem a
  connection string).

## Garantias (cada uma tem teste)

1. `live` responde `200` **sem token** e **continua `200` com o catálogo fora do ar**.
2. `ready` responde `200` com o catálogo bom e `503` com o catálogo fora (porta fechada), com o
   esquema ausente (catálogo isolado sem migrations) e acima de 2 s (sonda que nunca termina; o
   teste espera o tempo limite **real**, ≈2 s: o relógio falso só controla o TTL do cache).
3. `ready` responde `200` mesmo com **bases de tenant** fora do ar (só o catálogo conta).
4. **Cache**: N chamadas em menos de 5 s fazem **1** consulta; depois de 5 s (relógio falso), uma
   nova; chamadas concorrentes com cache vencido fazem **1** consulta.
5. A resposta é exatamente `Healthy` ou `Unhealthy`, com `Cache-Control` contendo `no-store`, sem nenhum
   outro dado.
6. Um `Authorization`, **inválido ou válido**, enviado às rotas é **ignorado** (como no login: o
   `OnMessageReceived` do JWT chama `NoResult()` para o metadado `HealthEndpointMarker`), **não muda**
   o resultado e **não dispara consulta de sessão nem de catálogo**.
7. Os **demais endpoints continuam exigindo autenticação** (`DefaultAuthorizationTests`, já
   existente) e os **únicos endpoints sem controller** são `/health/live` e `/health/ready` (e, só
   em Development, o Swagger).
8. `/health/*` não é encaminhado pelo nginx do console (que só encaminha `/v1/`): sem mudança no
   `nginx.conf`.

## Quem usa

O balanceador de carga e o deploy da 05b (alcançam o container direto, pela rede privada), os
testes de funcionamento e quem opera o ambiente local. Em containers, `HttpsRedirection:Enabled`
é `false` (o TLS termina antes): sem isso a sonda receberia um redirecionamento.
