# Plano de Implementação: Segurança da autenticação e da sessão

**Branch**: `20261101-232504-security-auth` | **Data**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `/specs/20261101-232504-security-auth/spec.md`

## Resumo

Fecha as brechas de autenticação e de sessão da Api (Princípio IX) em **7 fases pequenas** (A, B, C, D, E1, E2 e F), cada uma
com build e testes verdes ao final, para revisão e commit separados (o agente não faz commits).
Cada fase segue Red → Green: primeiro o teste que falha, depois a mudança.

Abordagem técnica (decisões e alternativas em [research.md](research.md)):

- **Fechado por padrão**: `FallbackPolicy = RequireAuthenticatedUser`; `[AllowAnonymous]` só no
  login; Swagger registrado e mapeado só em Development.
- **Sessão que morre na hora**: nova coluna `security_version` no operador (inteiro, default 0),
  incrementada pelos métodos de domínio `UpdatePassword` (sempre que a senha é informada),
  `UpdateRole` (só se o perfil muda) e `Deactivate` (na transição). O token carrega a claim; o
  validador de sessão confere `IsActive` **e** a versão na mesma consulta que já existe hoje.
- **JWT estrito**: emissor e destinatário em opções validadas na partida, `ValidateIssuer` e
  `ValidateAudience` ligados, tolerância de relógio de 30 s, algoritmo fixado em HS256, chave com
  pelo menos 32 bytes (fail fast).
- **Login em tempo uniforme**: costura `IPasswordVerifier` que sempre faz uma verificação de hash,
  contra um hash fictício do mesmo algoritmo e custo quando o operador não existe.
- **Limite por conta**: `ILoginThrottle` (Application) com implementação em memória
  (Infrastructure), registro por (tenant, e-mail normalizado) guardado só como hash, descarte após
  15 min, tamanho máximo e falha fechada ao saturar. A admissão é **atômica e anterior à
  verificação de hash** (`TryAcquireAttemptAsync` conta a tentativa como falha provisória; só `null`
  deixa a tentativa seguir), o que fecha a corrida de rajadas simultâneas (FR-018). O `429` do
  bloqueio leva `Retry-After`. As opções de limite têm faixas (`[Range]`) validadas na partida.
- **Origem confiável**: `KnownProxies`/`KnownIPNetworks` vindos de configuração, nunca esvaziados,
  `ForwardLimit = 1`; no compose, sub-rede e IP fixos para o nginx.
- **Compose**: portas das Apis e do banco publicadas só em `127.0.0.1`.
- **Console**: depois de trocar a própria senha, encerra a sessão e leva ao login com o aviso.
- **Testes de autorização** para todo endpoint, com um catálogo de rotas comparado ao
  `EndpointDataSource` real (a suíte falha se aparecer rota sem cobertura).

| Fase | Escopo | Red (como falha primeiro) | Green |
|---|---|---|---|
| **A** | Rotas fechadas por padrão + Swagger só em Development | rota de prova sem regra responde `200` em vez de `401`; `/swagger/v1/swagger.json` responde `200` fora de Development | política de fallback; `[AllowAnonymous]` no login; registro e mapeamento condicionais |
| **B** | JWT: emissor, destinatário, tolerância, algoritmo e chave | token com emissor/destinatário errados ou ausentes é aceito; Api sobe com chave curta e sem `Jwt:Issuer`/`Jwt:Audience` | opções validadas, parâmetros de validação, `TokenService` emite `iss`/`aud` |
| **C** | Versão de segurança (domínio, migration, token, validador) | testes de domínio não compilam; token antigo depois de trocar senha/perfil/desativar e reativar continua aceito | `SecurityVersion`, migration, claim, `ISessionValidator` |
| **D** | Login em tempo uniforme | teste do handler: o verificador **não** é chamado para e-mail inexistente e conta inativa | `IPasswordVerifier` sempre chamado; hash fictício com a mesma estrutura de um hash real |
| **E1** | Origem confiável + portas locais | `X-Forwarded-For` forjado de origem desconhecida contorna o `429` por IP; o limite por IP é fixo em 20; a Api sobe com `PermitLimit = 0` ou `1001`; `docker compose config` mostra portas em `0.0.0.0` | proxies conhecidos por configuração; `LoginRateLimitOptions` com faixas; sub-rede fixa; `127.0.0.1` nas portas |
| **E2** | Limite por conta | 6ª tentativa seguida na mesma conta não recebe `429`; 50 chamadas simultâneas à mesma conta deixam passar mais de 5; a Api sobe com `MaxFailures = 2` ou `MaxFailures = 101` | `ILoginThrottle` atômico em memória (`TryAcquireAttemptAsync`/`ResetAsync`), `Retry-After`, descarte, saturação e faixas das opções |
| **F** | Testes de autorização restantes, console e documentação | catálogo de rotas diverge das rotas reais; testes do console para a troca da própria senha falham | matriz completa; `AuthContext`/`LoginPage`/`MyAccountPage`; README, `.env.example`, comentários XML |

Linha de base: a spec 03 fechou em **430 testes de backend e 238 de front** (a confirmar na primeira
tarefa do `tasks.md`, antes de qualquer mudança). Os números finais saem do `tasks.md`.

## Contexto Técnico

**Linguagem/Versão**: C# / .NET 10 (SDK 10.0.402); TypeScript ~5.8.3 (`strict`), React 19. Nenhuma
versão muda.

**Dependências Principais**: ASP.NET Core (JWT Bearer, `ForwardedHeaders`, `AuthorizationPolicy`,
`EndpointDataSource`), EF Core 10 + Npgsql, Flunt, SecureIdentity 1.0.4 (já em uso no `Password`).
**Nenhum pacote NuGet ou npm novo** (Princípio II): o relógio injetável é o `TimeProvider` da BCL, e o
relógio falso dos testes é uma classe de duas linhas no projeto de testes, sem
`Microsoft.Extensions.TimeProvider.Testing`.

**Armazenamento**: PostgreSQL. **Uma** coluna nova, `operators.security_version` (migration só de
expansão, compatível com a versão anterior da Api). O registro de falhas de login fica **em memória**
da instância (violação temporária, ver Complexity Tracking).

**Testes**: backend xUnit + NSubstitute + Testcontainers.PostgreSql; front Vitest + Testing Library +
MSW. Nenhuma biblioteca nova.

**Plataforma de Destino**: Api em container Linux (.NET 10) atrás do nginx do console; ambiente local
por `docker compose`. Homologação e produção não existem (spec 05).

**Tipo de Projeto**: web-service (Api REST) + console web (SPA).

**Metas de Desempenho** (Princípio XIII, as do projeto): endpoints comuns p95 ≤ 300 ms e p99 ≤ 1 s;
taxa de erro 5xx < 0,1%. Esta spec **não acrescenta idas ao banco** no pipeline de autenticação (a
consulta do validador de sessão passa a conferir a versão junto de `IsActive`). O login passa a custar
sempre uma verificação de hash (PBKDF2, 10 000 iterações), inclusive para e-mail inexistente; conta
bloqueada responde sem calcular hash.

**Restrições**: nenhuma migration destrutiva; `Migrator` é o único caminho que aplica migrations;
nenhum segredo versionado; nenhuma mudança incompatível de contrato (só se acrescenta `429` ao login
por conta e a claim `security_version` ao token); estado estático proibido em Application e
Infrastructure (`NoStaticTenantStateTests`).

**Escala/Escopo**: de algumas centenas a 150 mil tenants (Princípio XIII). O registro de falhas é
limitado a **100 000 contas por instância** (~20 MB no pior caso), com descarte após 15 minutos.

## Constitution Check

*GATE: passa antes da pesquisa e é refeito depois do design. Resultado: **passa com 7 exceções
registradas em Complexity Tracking** (nenhuma sem justificativa).*

| Princípio | Situação | Observação |
|---|---|---|
| I. Isolamento de tenant | ⚠ exceção temporária | O registro de falhas é singleton em memória com chave que inclui o tenant (e-mail só como hash). O login continua lendo só o tenant resolvido. |
| II. Clean Architecture | ✔ | Interfaces na Application, implementações na Infrastructure e na Api; sem pacote novo; `Domain` só ganha uma propriedade e comportamento. |
| III. Vertical slice | ✔ | `IPasswordVerifier` e `ILoginThrottle` ficam em `Authenticate/Contracts` (um só contexto os usa); `ISessionValidator` fica em `SharedContext/UseCases/Services`, junto de `ITokenService`. Registro de DI manual por `#region`. |
| IV. Domínio rico | ✔ | `SecurityVersion` com `private set`, alterada só por `UpdatePassword`, `UpdateRole` e `Deactivate` (nomes de intenção); testes de domínio primeiro. |
| V. Contrato de use case | ⚠ nota | O `Authenticate` passa a devolver `429`, status fora da lista da constituição; emenda de redação sugerida. Validação e `DomainException → 400` mantidas. |
| VI. Api fina | ✔ | Controller sem lógica; `Retry-After` por filtro de resultado; `[AllowAnonymous]` só no login; comentários XML atualizados. Autorização por nome de role continua (legado, spec 08). |
| VII. Test-First | ✔ | Red → Green em cada fase; teste de handler/spec/repositório do que muda; testes de autorização para todo endpoint (`401`, `403`, outro tenant, token antigo); caso de isolamento no teste do `SessionValidator`. |
| VIII. Frontend só apresentação | ✔ com nota | O console encerra a sessão quando **enviou** uma senha nova (decisão da clarificação, não adivinha resposta); `RequireSession` segue só de navegação. |
| IX. Segurança | ✔ | Cobre fallback policy, JWT (`iss`, `aud`, skew curto, chave ≥ 256 bits, versão de segurança), login com hash sempre executado, limite por IP e por conta com backoff, proxies conhecidos, Swagger só em Development. Fora desta spec (04b): tamanho máximo de campos (exceção registrada em Complexity Tracking, sem trava de tamanho aqui), nginx, DDL, pacotes vulneráveis (a tarefa final roda o scan e reporta). |
| X. LGPD | ✔ | Nenhum dado pessoal novo. O e-mail só aparece em memória como hash e nunca em log. |
| XI. Auditoria | ⚠ adiada | Login e trocas de senha/perfil não geram evento hoje; a trilha é a spec 07 (fora do escopo, por decisão do usuário). |
| XII. Permissões | ✔ parcial | Nenhuma permissão nova. A regra "mudar a role invalida os tokens" passa a valer para a role do operador; permissões de role dinâmicas são a spec 08. |
| XIII. Escalabilidade | ⚠ 3 exceções | Limite em memória (temporário), sem cenário k6 (k6 nasce na spec 06) e pipeline de autenticação com 2 idas (catálogo + base do tenant; **já era assim**, não piora). |

Reavaliação depois do design (Fase 1): sem novas violações. O único risco novo é a saturação do
registro de falhas, tratada por **falha fechada** (ver [research.md](research.md), R-11).

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261101-232504-security-auth/
├── plan.md                         # Este arquivo
├── research.md                     # Fase 0: decisões, justificativas e alternativas
├── data-model.md                   # Fase 1: coluna nova, claims, registro de falhas, transições
├── quickstart.md                   # Fase 1: validação ao vivo (inclui a medição de tempo do SC-004)
├── contracts/
│   ├── login-and-session.md        # Respostas do login, claims e regras de validação do token
│   ├── configuration.md            # Chaves de configuração novas e falhas de partida
│   └── internal-interfaces.md      # ISessionValidator, IPasswordVerifier, ILoginThrottle
├── checklists/requirements.md
└── tasks.md                        # Fase 2 (/speckit-tasks, não criado aqui)
```

### Código-fonte (raiz do repositório)

Reaproveita a estrutura existente. Use case de referência mais parecido já implementado:
`AccountContext/UseCases/UpdateMe` (domínio rico + repositório + handler com `DomainException`) e,
para serviço de infraestrutura atrás de interface da Application, o par
`IActiveSessionChecker`/`ActiveSessionChecker`.

```text
src/Backend/
├── AccessControlSystem.Domain/Contexts/AccountContext/Entities/Operator.cs        # + SecurityVersion
├── AccessControlSystem.Application/
│   ├── SharedContext/UseCases/Services/ISessionValidator.cs                       # renomeia IActiveSessionChecker
│   └── Context/AccountContext/UseCases/Authenticate/
│       ├── Contracts/IPasswordVerifier.cs, ILoginThrottle.cs, IRetryAfterResponse.cs   # novos
│       ├── Handler.cs, Response.cs                                                # reescritos / + RetryAfter
├── AccessControlSystem.Infrastructure/
│   ├── Services/SessionValidator.cs                                               # renomeia ActiveSessionChecker
│   └── Context/AccountContext/
│       ├── Mapping/OperatorMap.cs                                                 # + security_version
│       └── UseCases/Authenticate/PasswordVerifier.cs, InMemoryLoginThrottle.cs, LoginThrottleOptions.cs
├── AccessControlSystem.Api/
│   ├── Program.cs                                                                 # Swagger só em Development
│   ├── Extensions/BuilderExtensions.cs, ClaimPrincipalExtensions.cs, UseCasesExtensions.cs
│   ├── Options/JwtOptions.cs, SecretsOptions.cs (+ validador), TrustedProxyOptions.cs, LoginRateLimitOptions.cs
│   ├── Services/AccountContext/TokenService.cs                                    # + iss, aud, security_version
│   ├── Filters/RetryAfterHeaderAttribute.cs                                       # novo
│   ├── Controllers/AccountContextController.cs                                    # [AllowAnonymous] + XML
│   └── Migrations/<timestamp>_AddSecurityVersionToOperators.*                     # gerada por `dotnet ef migrations add`
└── AccessControlSystem.Tests/
    ├── Api/ (DefaultAuthorizationTests, SwaggerTests, JwtValidationTests, SessionInvalidationTests,
    │         LoginUniformityApiTests, ForwardedHeadersTests, LoginRateLimitApiTests, LoginThrottleApiTests,
    │         EndpointCoverageTests, EndpointAuthorizationTests, OptionsValidationTests, TokenServiceTests,
    │         Support/ApiFactory + TestTokens + EndpointCatalog + OpenProbeController + CountingPasswordVerifier)
    ├── Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs
    ├── Architecture/ComposePortsTests.cs
    ├── Domain/Entities/OperatorTests.cs
    ├── Infrastructure/Services/SessionValidatorTests.cs
    ├── Infrastructure/AccountContext/ (OperatorMappingTests; Authenticate/PasswordVerifierTests, Authenticate/InMemoryLoginThrottleTests)
    ├── Infrastructure/Tenancy/TenantSeed.cs                                       # + helpers (fixture)
    └── Support/FakeTimeProvider.cs
src/Frontend/src/{context/AuthContext.tsx, pages/LoginPage.tsx, pages/MyAccountPage.tsx} + testes ao lado
docker-compose.yml, .env.example, README.md, src/Frontend/README.md
```

**Decisão de estrutura**: aplicação web existente (Api .NET + SPA). Nenhum projeto novo. As três
interfaces novas seguem o Princípio II (declaradas na Application, implementadas fora dela).

## Segurança e dados pessoais

### Permissões novas do catálogo

Nenhuma. A autorização continua por nome de role (`Admin`), que é legado a ser removido pela spec 08.

### Matriz de autorização (endpoint × cenário)

Todo cenário abaixo vira teste automatizado (`EndpointAuthorizationTests`, alimentado pelo
`EndpointCatalog`, que a suíte compara com o `EndpointDataSource` real).

| Endpoint | Acesso | sem token | role sem permissão | token de outro tenant | token antigo (senha / perfil / desativação) |
|---|---|---|---|---|---|
| `POST v1/account/login` | anônimo | n/a (ignora token) | n/a | n/a | n/a |
| `POST v1/account/create` | Admin | 401 | 403 | 401 | 401 |
| `GET v1/account/get-me` | autenticado | 401 | n/a | 401 | 401 |
| `GET v1/account/get-list` | Admin | 401 | 403 | 401 | 401 |
| `PUT v1/account/update-me` | autenticado | 401 | n/a | 401 | 401 |
| `PUT v1/account/update/{id}` | Admin | 401 | 403 | 401 | 401 |
| `PUT v1/account/activate/{id}` | Admin | 401 | 403 | 401 | 401 |
| `PUT v1/account/deactivate/{id}` | Admin | 401 | 403 | 401 | 401 |
| `GET /swagger/*` | **anônimo, só em Development** (segunda exceção a `[AllowAnonymous]`, justificada: a documentação já era pública em desenvolvimento e nem existe fora dele) | 404 fora de Development (rota sem endpoint responde 404, não 401) | n/a | n/a | n/a |

### Casos de abuso considerados

| Abuso | Defesa |
|---|---|
| Adivinhar senha de uma conta de muitos IPs (botnet) | Bloqueio por conta (FR-017/018/021), independente da origem |
| Rajada simultânea contra a mesma conta (várias requisições passando antes de qualquer falha contada) | Admissão atômica **antes** do hash: só 5 tentativas por nível chegam à verificação (teste com 50 chamadas concorrentes) |
| Valor de configuração errado desliga o limite (ex.: `MaxFailures = 1000000`) | `[Range]` com piso e teto nas opções, validado na partida |
| Adivinhar muitas contas de um IP | Limite por IP (20/min), mantido |
| Forjar `X-Forwarded-For` para parecer IP novo | Cabeçalho só vale vindo do proxy conhecido; `ForwardLimit = 1` (FR-023..025) |
| Descobrir e-mails cadastrados pelo tempo de resposta | Verificação de hash sempre executada, hash fictício de mesmo custo (FR-014) |
| Descobrir e-mails cadastrados pelo bloqueio | Falhas de e-mail inexistente contam e bloqueiam igual (FR-020) |
| Usar token de ex-operador, de conta com senha trocada ou de perfil rebaixado | Versão de segurança conferida a cada requisição (FR-004..008) |
| Reativar conta e voltar a usar token antigo | `Deactivate` incrementa a versão (FR-006) |
| Token de outro sistema ou ambiente com a mesma chave | Emissor e destinatário validados (FR-010) |
| Chave de assinatura fraca | A Api não inicia (FR-012) |
| Esgotar a memória com milhões de e-mails inexistentes | Limite de 100 000 contas, descarte em 15 min, falha fechada (FR-032, FR-022) |
| Travar a conta de uma vítima de propósito | Aceito (espera com teto de 15 min); o operador volta quando passa |
| Mapear a Api pelo Swagger em produção | Swagger só em Development (FR-003) |
| Acessar Apis e banco do ambiente local pela rede | Portas só em `127.0.0.1` (FR-026) |
| Rota nova esquecida sem proteção | Política de fallback + teste que compara rotas reais com o catálogo (FR-001, FR-030) |

### Dados pessoais tocados

Nenhum dado pessoal novo é coletado (Princípio X). O e-mail do operador aparece no registro de falhas
**apenas como hash SHA-256** da chave `tenant:e-mail`; nunca em log, nem em memória em texto. O claim
`Name` (e-mail) do token fica **só informativo**: pode estar desatualizado depois de uma troca de
e-mail e não decide nada (a extensão `User.Email()`, sem uso, é removida).

### Eventos de auditoria gerados

Nenhum. Login, falha de login e trocas de senha/perfil não geram evento hoje (legado do Princípio XI);
a trilha é a **spec 07**, fora do escopo. O que esta spec registra em **log** é só o bloqueio de conta
(`LogWarning` com tenant e prefixo do hash da conta, sem e-mail, senha ou token).

### Consultas novas ou alteradas e índices

| Consulta | Onde | Índice que atende |
|---|---|---|
| `SELECT 1 FROM operators WHERE id = @id AND is_active AND security_version = @v` (substitui a de `IsActive`; **mesma única ida ao banco**) | `SessionValidator` | chave primária `id`; o filtro por `tenant_id` do EF/RLS é aplicado sobre o mesmo acesso |
| Busca do operador por e-mail no login (inalterada, agora com e-mail normalizado) | `Authenticate/Repository` | índice único `(tenant_id, email)` |

Não há consulta em laço, lista nem carga sem limite. A coluna nova não precisa de índice (nunca é filtro
sozinha).

## Complexity Tracking

> Violações da constituição que esta spec aceita, com a justificativa. Todas têm saída já definida.

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque | Resolvida em |
|---|---|---|---|
| **Princípios I e XIII**: registro de falhas de login em memória da instância (singleton, chave com tenant) | O limite por conta é requisito desta spec; Redis só entra na spec 06. A regra fica atrás de `ILoginThrottle`, então a spec 06 troca só a implementação. Com várias instâncias o limite efetivo é por instância. | Não limitar por conta deixa o ataque distribuído passar. Antecipar o Redis faria esta spec carregar infraestrutura da 06. | Spec 06 |
| **Princípio XIII**: sem cenário k6 para o login e o pipeline de autenticação | O k6 só existe a partir da spec 06; o login e o pipeline mudam aqui. A baseline da 06 cobre esses caminhos. | Criar o k6 aqui antecipa a spec 06 (carga de referência, ambiente de medição). | Spec 06 |
| **Princípio XIII**: o pipeline de autenticação faz 2 idas (catálogo para o tenant + base do tenant para a sessão) | **Já era assim**; esta spec não acrescenta ida (a versão vai na mesma consulta de `IsActive`). Cache é spec 06. | Cache em memória viola o próprio Princípio XIII. | Spec 06 |
| **Princípio IX/II (texto)**: `IActiveSessionChecker` é citado na constituição e passa a se chamar `ISessionValidator` (confere ativo **e** versão) | O nome antigo deixaria de descrever o que faz (Princípio III). | Manter o nome esconde a regra de versão. | **Emenda de redação do dono do projeto** (PATCH): trocar o nome nos Princípios II e IX |
| **Princípio V (texto)**: `Authenticate` passa a devolver `429` | Bloqueio por conta é decidido no use case (conta, falhas, tempo) e precisa da mesma resposta do limite por IP. | Decidir fora do use case obriga a ler o corpo e o tenant num filtro, duplicando regra. | **Emenda de redação do dono do projeto** (PATCH): incluir `429` na lista de status |
| **Princípio XI**: sem auditoria de login e de troca de senha/perfil | Fora do escopo por decisão do usuário. | Auditar aqui antecipa a spec 07 (tabela, transação, eventos). | Spec 07 |
| **Princípio IX (Entrada)**: `Authenticate.Request` sem tamanho máximo de senha/e-mail | A spec **04b** define os tamanhos máximos de **todas** as Specifications de uma vez, com TDD, de forma consistente. O PBKDF2 reduz a senha longa a um hash uma vez (o custo de iterações não cresce com o tamanho), a conta bloqueada responde sem calcular hash e o limite por IP (20/min) contém o abuso. O login passa a calcular o hash também para e-mail inexistente, mas continua limitado por IP e por conta. | Pôr uma trava de tamanho só no login agora criaria um número arbitrário que a 04b teria de reconciliar com as demais Specifications. **Sem trava de tamanho nesta spec** (decisão do dono do projeto). | **Spec 04b** |

### Itens do "Legado conhecido" que esta spec encerra (para o dono do projeto emendar)

`BuilderExtensions.AddForwardedHeaders` (proxies conhecidos), `BuilderExtensions.AddJwtAuthentication`
(emissor, destinatário, chave, versão de segurança), `Authenticate/Handler.cs` (hash sempre
calculado), `AddRateLimiting` (limite por conta; **a parte "memória da instância" continua**, spec 06)
e `Program.cs` (Swagger em qualquer ambiente). A constituição só é editada pelo dono do projeto.
