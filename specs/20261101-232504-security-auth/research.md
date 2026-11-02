# Pesquisa (Fase 0): Segurança da autenticação e da sessão

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Não restou nenhuma pendência de clarificação. Cada decisão abaixo foi verificada no código de
2026-11-01 (arquivos citados). Formato: **Decisão**, **Justificativa**, **Alternativas**.

## R-01 — Autenticação por padrão

- **Decisão**: `AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`
  em `AddJwtAuthentication`; `[AllowAnonymous]` explícito só em `Login`. Os `[Authorize]` e
  `[Authorize(Roles = "Admin")]` atuais ficam como estão (a spec 08 os troca).
- **Justificativa**: hoje cada endpoint precisa lembrar de se proteger. A política de fallback só vale
  para endpoints que existem; rota inexistente continua `404` (caso de borda da spec). O
  `OnMessageReceived` do login (ignora token enviado) continua funcionando com `[AllowAnonymous]`.
- **Alternativas**: filtro global de autorização (mais invasivo e esconde a regra); manter só
  `[Authorize]` por controller (é o problema atual).

## R-02 — Swagger só em Development

- **Decisão**: `AddSwaggerDocumentation()` e `MapSwagger()`/`MapSwaggerUI()` só quando
  `Environment.IsDevelopment()` (em `Program.cs`). Fora dele as rotas `/swagger/...` não existem e
  respondem `404`. O ambiente dos testes é `Testing` (`ApiFactory`), então o `404` é testável sem
  mudar nada; o caso "Development continua servindo" usa `ApiFactory.WithEnvironment("Development")`.
- **Justificativa**: não registrar o gerador também evita ler o `AccessControlSystem.Api.xml` fora de
  Development. Risco: se o XML de documentação não estiver na pasta de saída dos testes, o teste de
  Development falha na partida; nesse caso a correção é copiar o XML para a saída do projeto de testes
  (fixture), não relaxar a condição.
- **Alternativas**: proteger o Swagger com autenticação (a spec pede `404`); `[ApiExplorerSettings]`
  (não remove as rotas).

## R-03 — Versão de segurança do operador

- **Decisão**: `Operator.SecurityVersion` (`int`, `private set`, começa em 0) e coluna
  `operators.security_version integer NOT NULL DEFAULT 0`. A claim `security_version` do token recebe
  o valor no login; o validador de sessão exige igualdade com o valor atual.
- **Justificativa**: é o desenho da constituição (Princípio IX/XII). Custa uma coluna e nenhuma ida
  extra. `DEFAULT 0` constante é só metadado no PostgreSQL 18: a migration é instantânea e compatível
  com a versão anterior da Api (que ignora a coluna).
- **Alternativas**: carimbo aleatório (`Guid`) em vez de contador (evita colisão, mas o contador é o
  que a constituição descreve e é mais fácil de ler); lista de tokens revogados (estado e leitura a
  cada requisição); diminuir a validade do token (não é imediato).
- **Transição da primeira implantação**: tokens emitidos antes da claim existir não a têm e passam a
  receber `401` (usuário faz login de novo). Não há produção; o efeito é só no ambiente local.

## R-04 — Quando a versão muda

- **Decisão** (métodos de domínio, nomes de intenção, testes de domínio primeiro):
  - `UpdatePassword(Password)`: **sempre** incrementa (senha informada conta como troca, mesmo igual à
    atual, decisão do usuário; o `Password` novo tem sal novo, então o hash sempre muda).
  - `UpdateRole(ERole)`: incrementa **só se o perfil é diferente** do atual.
  - `Deactivate()`: incrementa **só na transição** ativo → inativo (desativar de novo não faz nada).
  - `Activate()` e `UpdateEmail()`: **não** incrementam.
- **Justificativa**: cobre FR-004..FR-007. Os handlers `Update` e `UpdateMe` já só chamam
  `UpdatePassword` quando `Password != null`, então "senha em branco" não incrementa sem mudar handler.
  O `Update` chama `UpdateRole` sempre que `Role != null`; a comparação mora no domínio (Princípio IV).
- **Alternativas**: o handler decide quando incrementar (espalha regra de domínio); incrementar em todo
  `UpdateAsync` (derruba o token a cada edição inofensiva, contra o FR-007).

## R-05 — `ISessionValidator` (renomeia `IActiveSessionChecker`)

- **Decisão**: `Task<bool> IsValidAsync(Guid operatorId, int securityVersion, CancellationToken)` com
  uma única consulta `AnyAsync(x => x.Id == id && x.IsActive && x.SecurityVersion == version)`. Arquivos:
  `Application/SharedContext/UseCases/Services/ISessionValidator.cs` e
  `Infrastructure/Services/SessionValidator.cs`. Hoje não existe teste do `ActiveSessionChecker`; o
  `SessionValidatorTests` (integração) nasce com caso de isolamento entre tenants (Princípio VII).
- **Justificativa**: o nome antigo deixa de descrever a regra (Princípio III). Uma consulta mantém o
  custo do pipeline como está (Princípio XIII). A constituição cita o nome antigo nos Princípios II e
  IX; a emenda de redação é do dono do projeto (Complexity Tracking).
- **Alternativas**: manter o nome e acrescentar parâmetro (engana quem lê); duas consultas (uma ida a
  mais por requisição).

## R-06 — Validação do JWT

- **Decisão**: `ValidateIssuer = true`, `ValidateAudience = true`, `ValidIssuer`/`ValidAudience` das
  opções, `ValidateLifetime = true`, `RequireExpirationTime = true`, `RequireSignedTokens = true`,
  `ValidateIssuerSigningKey = true`, `ClockSkew = 30 s` (constante), `ValidAlgorithms = [HS256]`.
  `TokenService` passa a emitir `iss`, `aud` e `security_version`. Validade do token fica em 8 h.
- **Justificativa**: Princípio IX; 30 s vem das Premissas da spec. Fixar HS256 impede troca de
  algoritmo. As claims `Id`, `tenant` e `Role` continuam como estão (compatível com o front).
- **Alternativas**: `ClockSkew` configurável (mais uma chave para errar; nenhum caso de uso).

## R-07 — Chave de assinatura e opções de emissor/destinatário

- **Decisão**: nova seção `Jwt` (`Issuer`, `Audience`), obrigatórias, `ValidateOnStart`, **sem valor em
  `appsettings.json`** (como a chave, para a Api não subir "por acidente" com padrão). A chave
  (`Secrets:JwtPrivateKey`) ganha validador (`IValidateOptions<SecretsOptions>`) que exige
  **`Encoding.UTF8.GetByteCount(chave) >= 32`**, a mesma medida usada para assinar. A mensagem diz qual
  chave falhou e **nunca** o valor. Um helper único gera os bytes da chave (usado pelo validador, pelo
  `TokenService` e pelo `JwtBearer`).
- **Desvio declarado do roteiro**: o roteiro fala em "32 bytes depois de decodificar". A Api hoje assina
  com os bytes UTF-8 do texto (não decodifica base64) e mudar isso invalidaria todo token e exigiria
  outro formato de chave. Medir **o que de fato assina** é o que a RFC 7518 §3.2 exige para HS256
  (chave ≥ 256 bits). A recomendação operacional continua `openssl rand -base64 64` (88 caracteres).
- **Compose**: `Jwt__Issuer: ${JWT_ISSUER:-access-control-system}` e
  `Jwt__Audience: ${JWT_AUDIENCE:-access-control-system-console}`; `.env.example` documenta como
  opcionais. Ambientes futuros devem usar valores diferentes por ambiente (spec 05).
- **Fixture**: a chave de teste (`ApiFactory.JwtKey`, 64 caracteres) já passa; só se acrescentam
  `Jwt:Issuer` e `Jwt:Audience` à `ApiFactory`.
- **Alternativas**: exigir base64 (quebra o formato atual); heurística de entropia (falsa segurança).

## R-08 — Login em tempo uniforme (`IPasswordVerifier`)

- **Decisão**: `bool Verify(Operator? @operator, string password)` em
  `Authenticate/Contracts/IPasswordVerifier.cs`, implementado por `PasswordVerifier` (singleton, **campo
  de instância**) que guarda um `Password` fictício criado no construtor
  (`new Password(<texto forte aleatório>)`, que usa o mesmo `SecureIdentity.PasswordHasher`). Sempre
  executa **uma** verificação: contra `@operator.Password` quando existe (ativo ou não) e contra o
  fictício quando não existe; devolve `@operator is not null && resultado`. A decisão sobre inativo fica
  no handler, depois.
- **Formato confirmado** (sonda em 2026-11-01, SecureIdentity 1.0.4):
  `10000.<sal 16 bytes em base64>.<chave 32 bytes em base64>`, ou seja, 3 segmentos separados por `.`,
  PBKDF2 com 10 000 iterações. O teste do `PasswordVerifier` compara o hash fictício com o de um
  `Password` real: mesmo número de segmentos, mesma primeira parte (iterações) e mesmos comprimentos de
  sal e de chave. Assim, se a biblioteca mudar os parâmetros, o fictício acompanha e o teste detecta
  divergência.
- **Por que instância e não `static readonly`**: `NoStaticTenantStateTests` proíbe qualquer campo
  estático em Application e Infrastructure; um singleton com campo de instância não guarda dado de
  tenant e passa.
- **Efeito colateral aceito**: e-mail inexistente passa a custar uma verificação de hash (antes não
  custava). Quem protege contra abuso é o limite por IP e por conta; conta **bloqueada** responde `429`
  sem calcular hash.
- **Alternativas**: dormir (`Task.Delay`) para igualar o tempo (impreciso e gasta thread); hash
  constante no código (fica velho se o custo mudar).

## R-09 — E-mail normalizado no login

- **Decisão**: depois da `Specification`, o handler faz `new Email(request.Email)` (captura
  `DomainException → 400`, Princípio V) e usa `Address` (minúsculo, sem espaços) **tanto** na busca do
  operador **quanto** na chave do limite.
- **Justificativa**: o FR-017 define a conta com e-mail sem diferença de caixa. Hoje a busca compara o
  texto cru, então `Ana@x.com` nunca entra mesmo existindo `ana@x.com`. Normalizar nos dois lugares
  evita contar numa conta e consultar noutra. **Mudança de comportamento pequena e compatível**: logins
  com maiúsculas, que antes davam `401`, passam a funcionar. Será declarada no `tasks.md`.
- **Alternativas**: normalizar só na chave do limite (conta e consulta divergem).

## R-10 — `ILoginThrottle`: contrato e regra

- **Decisão**: em `Authenticate/Contracts/ILoginThrottle.cs`, **duas operações atômicas**, async para a
  troca por Redis:
  - `Task<TimeSpan?> TryAcquireAttemptAsync(Guid tenantId, string email, CancellationToken)` — sob o mesmo
    `lock`: se a conta está bloqueada, devolve o tempo restante e **não conta nada**; senão conta a
    tentativa como **falha provisória** e, se ela atingir o máximo, bloqueia pela espera do nível. Devolve
    `null` quando a tentativa pode seguir (inclusive a que acabou de atingir o máximo);
  - `Task ResetAsync(Guid tenantId, string email, CancellationToken)` — login com sucesso zera contagem e
    nível (e qualquer bloqueio que a própria tentativa tenha acabado de criar).
  - `RegisterFailureAsync` e `GetRetryAfterAsync` **deixam de existir**: a falha já foi contada na entrada.
- **Por que atômico (FR-018)**: o fluxo "consultar → verificar hash → registrar falha" deixa N requisições
  simultâneas passarem pela consulta antes de qualquer falha ser contada; uma rajada distribuída de 20
  tentativas por IP (o limite por IP é por origem) furaria o limite por conta. Contar **antes** da
  verificação, no mesmo `lock` da decisão, torna a admissão a única fonte de verdade: só 5 tentativas por
  nível chegam ao hash, por mais requisições que cheguem juntas. Mesma ideia do `INCR` atômico do Redis na
  spec 06.
- **Fluxo do handler** (regiões 01..06): **01** valida (`400`) → **02** normaliza o e-mail
  (`DomainException → 400`) → **03** `TryAcquireAttemptAsync`: se devolver tempo, `429` com `RetryAfter`,
  sem tocar no banco nem calcular hash → **04** busca o operador por e-mail → **05** `Verify` (sempre) →
  **06** decide: e-mail inexistente, conta inativa ou senha errada → `401` (nada a registrar, a falha já
  foi contada); sucesso → `ResetAsync` e emite o token. O `tenantId` vem de `ITenantContext` (já resolvido
  pelo filtro do login).
- **Regra** (FR-021, interpretação declarada): 5 tentativas por nível; a 5ª atinge o máximo e bloqueia por
  `1 min × 2^(nível−1)` (1, 2, 4, 8 min), teto de 15 min, **sem impedir a própria 5ª de seguir**; ao
  bloquear, o contador volta a 0 e o **nível** sobe, então o bloqueio seguinte exige mais 5 tentativas e dura
  o dobro. O nível só zera por login com sucesso ou por descarte (R-11). Durante o bloqueio não há
  verificação nem contagem. Efeito para quem acerta a senha: tentativas que terminam em sucesso não
  acumulam, porque `ResetAsync` zera tudo.
- **Efeitos colaterais aceitos**: (a) se o banco falhar entre a admissão e a verificação (`500`), a
  tentativa fica contada como falha (conservador, nunca afrouxa); (b) `400` por formato de e-mail e a
  recusa do tenant padrão acontecem **antes** da admissão e não contam.
- **Teste de concorrência** (`InMemoryLoginThrottleTests`): 50 chamadas simultâneas à mesma conta
  (`Task.Run` + barreira de início + `Task.WhenAll`, relógio falso parado) → **exatamente 5** recebem `null`
  e as 45 demais recebem tempo de bloqueio positivo. Teste adicional na Api: rajada concorrente de logins com
  senha errada, a partir de IPs forjados por um proxy conhecido, admite no máximo 5 verificações de hash.
- **Relógio**: `TimeProvider` injetado (singleton `TimeProvider.System`), testes usam `FakeTimeProvider`
  próprio (classe pequena em `Tests/Support`). Nenhum pacote novo (Princípio II/VII).
- **Alternativas**: manter "consultar → verificar → registrar" (tem a corrida acima); `Interlocked` por
  conta sem `lock` global (mais complexo e sem ganho nesta escala; o Redis atômico é da spec 06); uma falha
  após o bloqueio já bloqueia de novo (mais estrito, mas pune erro de digitação do operador legítimo);
  bloqueio linear.

## R-11 — Registro de falhas: tamanho, descarte e saturação

- **Decisão** (`InMemoryLoginThrottle`, singleton, instância com `Dictionary` sob `lock`):
  - **Chave**: `SHA-256("{tenantId}:{email}")` em hexadecimal. O e-mail nunca fica em memória nem em log.
  - **Entrada**: `Failures` (tentativas provisórias desde o último bloqueio), `Level`, `BlockedUntil`,
    `LastEventAt` (instante da última tentativa contada **ou** do fim do último bloqueio, o que for maior).
  - **Descarte (FR-032)**: entrada com `agora − LastEventAt ≥ 15 min` é descartada ao ser tocada por
    `TryAcquireAttemptAsync` e por uma varredura no máximo 1 vez por minuto.
  - **Tamanho máximo**: `MaxTrackedAccounts = 100 000` por instância (~200 bytes por entrada, ~20 MB).
  - **Ao atingir o máximo**: (1) varre as expiradas; (2) se ainda cheio, remove um lote de **entradas não
    bloqueadas** (as de `LastEventAt` mais antigo; elas só têm contagem parcial); (3) **nunca** remove
    registro de conta bloqueada; (4) se não houver como abrir espaço, a conta nova **não pode ser
    rastreada** e `TryAcquireAttemptAsync` devolve bloqueio de 60 s, **sem contar nada**, para contas
    **não rastreadas** enquanto estiver saturado. **Falha fechada** (FR-022): jamais libera login sem
    limite.
  - **Concorrência**: **um único `lock`** cobre a consulta do bloqueio, a contagem e a decisão de bloquear
    dentro de `TryAcquireAttemptAsync` (é isso que fecha a corrida do R-10); as operações são O(1)
    amortizado (a varredura e a remoção em lote são raras e feitas sob o mesmo `lock`). Sem estático.
- **Justificativa**: atende o FR-032 e a orientação do usuário (nunca descartar bloqueadas, nunca liberar
  sem limite). Saturar com contas bloqueadas exige 100 000 contas × 5 falhas, limitado ainda pelo limite
  por IP; o pior efeito é negar login a contas novas, e não liberar acesso.
- **Alternativas**: `MemoryCache` com tamanho (não deixa proteger bloqueadas da remoção); descartar a
  mais antiga sempre (permite a um atacante expulsar bloqueios inundando com e-mails falsos).
- **Configuração** (`LoginProtection:Account`, `ValidateDataAnnotations` + `ValidateOnStart`):
  `MaxFailures = 5` (`[Range(3, 100)]`), `BaseWaitSeconds = 60` (`[Range(1, 3600)]`),
  `MaxWaitSeconds = 900` (`[Range(1, 86400)]`), `IdleExpirySeconds = 900` (`[Range(1, 86400)]`),
  `MaxTrackedAccounts = 100000` (`[Range(100, 10000000)]`), mais a regra entre campos
  (`MaxWaitSeconds ≥ BaseWaitSeconds` e `IdleExpirySeconds ≥ MaxWaitSeconds`, via `IValidatableObject`).
  O limite por IP (`LoginProtection:Ip`) tem `PermitLimit` `[Range(1, 1000)]` e `WindowSeconds`
  `[Range(1, 3600)]`. **Os tetos impedem que um valor errado desligue a proteção na prática** (ex.:
  `MaxFailures = 1000000`); o piso 3 de `MaxFailures` evita travar contas por engano. Os padrões são os da
  spec; a configuração existe para os testes (relógio e limites pequenos, dentro das faixas) e para a
  medição do quickstart. Valor fora da faixa derruba a partida (`OptionsValidationTests` cobre
  `MaxFailures = 2`, `MaxFailures = 101`, `PermitLimit = 0`, `PermitLimit = 1001` e
  `IdleExpirySeconds < MaxWaitSeconds`).

## R-12 — `429` do bloqueio e `Retry-After`

- **Decisão**: o `Authenticate.Response` ganha `[JsonIgnore] TimeSpan? RetryAfter` (interface
  `IRetryAfterResponse`, declarada em `Application/Context/AccountContext/UseCases/Authenticate/Contracts/`)
  e o login usa `[RetryAfterHeader]`, um filtro de resultado da Api que, quando o valor da resposta é
  `IRetryAfterResponse` com tempo, escreve `Retry-After` em segundos (arredondado para cima). O filtro da
  Api referencia a interface nesse namespace. Mensagem: **"Muitas tentativas. Tente novamente em
  instantes."**, igual à do limite por IP. O controller continua `StatusCode(result.Status, result)`.
- **Onde fica a interface**: só o `Authenticate` a usa, então ela mora na pasta do use case (Princípio III:
  "só é promovido para `SharedContext` quando já for usado por dois ou mais contextos"). Quando um segundo
  use case precisar de `Retry-After` (ex.: o rate limit de aparelhos, Princípio IX), ela é promovida nesse
  momento. Sem entrada em Complexity Tracking porque não há desvio.
- **Diferença conhecida**: o corpo do bloqueio por conta é o envelope do use case (traz `data: null`); o do
  limite por IP vem do `ErrorResponse` (sem `data`). Status, mensagem e `Retry-After` são idênticos; o
  corpo difere só por esse campo nulo e não revela nada (os dois se aplicam a qualquer conta).
- **Alternativas**: o handler lançar exceção (exceção para fluxo esperado, contra o Princípio V); decidir o
  bloqueio num filtro lendo o corpo (duplica a regra e o tenant fora do use case).

## R-13 — Proxies conhecidos e cabeçalhos de encaminhamento

- **Decisão**: `TrustedProxyOptions` na seção `ForwardedHeaders` com `KnownProxies` (IPs) e
  `KnownNetworks` (CIDR). `AddForwardedHeaders` **acrescenta** esses valores às listas do
  `ForwardedHeadersOptions` e **não chama `Clear()`**: sem configuração, ficam só os padrões do
  framework (loopback). `ForwardLimit = 1`; cabeçalhos `XForwardedFor | XForwardedProto`. Valor
  inválido (IP/CIDR) falha na partida.
- **Por que nunca deixar as duas listas vazias**: no ASP.NET Core, listas vazias significam "confiar em
  qualquer origem" (é a vulnerabilidade atual). Um teste prova que, sem configuração, `X-Forwarded-For`
  de origem não loopback é ignorado.
- **Compose**: rede padrão com sub-rede fixa `172.31.250.0/24` e IP fixo `172.31.250.10` para o serviço
  `frontend` (nginx); as três Apis recebem `ForwardedHeaders__KnownProxies__0: "172.31.250.10"`. O nginx
  usa `$proxy_add_x_forwarded_for` (acrescenta o IP real à direita); com `ForwardLimit = 1` a Api lê só o
  último, então endereços forjados à esquerda são ignorados (caso de borda da spec). Se a sub-rede colidir
  com outra rede do desenvolvedor, o compose acusa na subida (anotado no quickstart).
- **Como testar sem rede**: `ApiFactory.WithRemoteIp(ip)` instala um `IStartupFilter` que define
  `Connection.RemoteIpAddress` antes de `UseForwardedHeaders`. O efeito é observado pelo limite por IP
  (21 logins com `X-Forwarded-For` diferentes: de origem desconhecida o 21º dá `429`; de proxy conhecido
  nenhum dá).
- **Alternativas**: confiar na sub-rede inteira (aceita qualquer container do compose); `KnownProxies` só
  por nome DNS (a API exige IP).

## R-14 — Portas do compose só em `127.0.0.1`

- **Decisão**: `"127.0.0.1:${BACKEND_PORT:-5290}:8080"` (e `BACKEND_B_PORT`, `BACKEND_DEDICATED_PORT`) e
  `"127.0.0.1:${DB_PORT:-5433}:5432"`. O console (`FRONTEND_PORT`) **não muda** (FR-026 cita só Apis e
  banco; "o console continua acessível como hoje"). README passa a citar `http://127.0.0.1:5290` (em
  Windows `localhost` pode resolver para IPv6 primeiro) e a explicar o porquê.
- **Teste automatizado**: `ComposePortsTests` lê o `docker-compose.yml` da raiz (sobe a árvore de pastas
  até achá-lo; falha se não achar) e exige que todo mapeamento de `backend*` e `db` comece por
  `127.0.0.1:`. Red já confirmado: hoje as portas estão sem endereço.
- **Alternativas**: deixar só validação manual (regride sem aviso); remover as portas 5291/5292 (são o
  único jeito de testar os outros tenants).

## R-15 — Cobertura de autorização de todo endpoint (FR-029/FR-030)

- **Decisão**: `Tests/Api/Support/EndpointCatalog.cs` lista cada endpoint (método, rota normalizada,
  acesso: `Anonymous`, `Authenticated` ou `AdminOnly`). `EndpointAuthorizationTests` passa a derivar dos
  dados do catálogo. Um teste compara o catálogo com o `EndpointDataSource` real da Api (rotas de
  controller, `{id:guid}` normalizado para `{id}`): **falha** se existir rota real fora do catálogo ou
  entrada de catálogo sem rota. Os cenários novos (token antigo por senha, por perfil, por desativação)
  rodam para todo endpoint autenticado, usando métodos de domínio reais para provocar a troca.
- **Rota de prova** (Fase A): `OpenProbeController` (sem nenhum atributo de acesso) existe só no projeto de
  testes e só é carregado por `ApiFactory.WithOpenProbeEndpoint()` (um `ApplicationPart` restrito a esse
  tipo), então não aparece no catálogo nem em outros testes.
- **Alternativas**: reflexão sobre os controllers (não vê rotas de middleware nem minimal API);
  confiar em revisão humana.

## R-16 — Console depois da troca da própria senha (FR-009)

- **Decisão**: `AuthContext` ganha `passwordChanged` e `signOutAfterPasswordChange()` (apaga a sessão,
  marca `passwordChanged`; `signIn` zera os dois avisos). `MyAccountPage`, depois do sucesso do
  `updateMe` **quando a senha foi enviada**, chama `signOutAfterPasswordChange()`; `RequireSession` leva
  ao login. `LoginPage` mostra `Alert tone="info"` com **"Senha alterada. Faça login novamente."**
  (precedência: erro > senha alterada > sessão encerrada).
- **Testes da spec 02 afetados** (declarados no `tasks.md` por mudarem de comportamento esperado):
  `MyAccountPage.test.tsx` — "mostra a mensagem da api, limpa a senha, recarrega os dados e atualiza o
  e-mail da sessão" (envia e-mail **e** senha) muda: o caso só de e-mail mantém a asserção atual; o caso com
  senha passa a esperar fim de sessão. "envia só a senha…" e "envia e-mail e senha…" só conferem o corpo e
  precisam continuar passando (verificar `act`). Novos: `AuthContext.test.tsx`, `LoginPage.test.tsx`,
  `App.test.tsx` (fluxo completo).
- **Administrador trocando a própria senha pela edição de operadores**: o próximo pedido recebe `401` e o
  tratador global de 401 encerra a sessão com o aviso genérico. Aceitável; não é escopo.
- **Alternativas**: a Api devolver token novo no `update-me` (muda o contrato; descartado na clarificação).

## R-17 — Migration

- **Decisão**: `dotnet ef migrations add AddSecurityVersionToOperators` gera `AddColumn<int>` com
  `defaultValue: 0`, `nullable: false`. Aplicada **só pelo Migrator** (`docker compose run --rm migrator
  migrate`); nunca `dotnet ef database update`. As concessões do papel `acs_app` já são em nível de tabela
  e `DEFAULT PRIVILEGES`, então a coluna nova herda `SELECT, INSERT, UPDATE`. O RLS não muda (a coluna
  pertence à linha). Compatível com expand/contract: a versão anterior ignora a coluna.
- **Teste**: o `SchemaContractTests` existente continua verde; um teste de integração do repositório
  confirma que `SecurityVersion` persiste e que `Update` do EF grava o valor novo.

## R-18 — Medição de tempo do login (SC-004) e configurabilidade dos limites

- **Decisão**: a comparação de tempo **não é teste do CI**; é o passo 9 do `quickstart.md` (30 logins de
  cada caso contra o compose, mediana de cada um, diferença ≤ 10%). Para a medição não esbarrar nos
  limites, o limite por IP vira configurável (`LoginProtection:Ip:PermitLimit`, `WindowSeconds`; padrão
  20 por 60 s) e o compose expõe `LOGIN_IP_PERMIT_LIMIT` e `LOGIN_MAX_FAILURES` (padrões 20 e 5, só para
  essa medição, comentados no `.env.example`). Como as opções têm teto (R-11: `PermitLimit` ≤ 1000 e
  `MaxFailures` ≤ 100), a medição usa **1000 e 100**; 30 tentativas por conta cabem folgadamente em 100. O
  quickstart manda **voltar aos padrões (20 e 5) e conferir** depois da medição, e reiniciar as Apis entre
  rodadas (o registro de falhas é em memória).
- **Alternativas**: espaçar 3,1 s entre logins e rotacionar contas (lento e frágil).

## R-19 — Ajustes de fixture (FR-031), a listar no `tasks.md`

| Fixture | Ajuste | Por quê |
|---|---|---|
| `ApiFactory` | `Jwt:Issuer`, `Jwt:Audience` nas configurações; `WithEnvironment`, `WithRemoteIp`, `WithOpenProbeEndpoint`, `WithFakeTime` | opções novas e simulação de origem/relógio |
| `TestTokens` | emissor, destinatário e `security_version` (do `Operator`, com sobrecarga para valor explícito); `Create(...)` aceita nulos para os casos negativos | JWT estrito e versão de segurança |
| `TenantSeed` / helpers | alterar senha, perfil e situação de um operador pelos **métodos de domínio** e gravar | token antigo depois de cada troca |
| `FakeTimeProvider` (novo) | relógio avançável | bloqueio, dobra, teto e descarte sem esperar |
| `EndpointCatalog` (novo) | lista oficial de rotas e acesso | FR-030 |

Nenhum teste existente é apagado, pulado ou enfraquecido. Os que mudam de expectativa por decisão da spec
(só os do console, R-16) são declarados.

## R-20 — Riscos aceitos

| Risco | Tratamento |
|---|---|
| Duas edições simultâneas do mesmo operador podem gravar a mesma versão (sem `xmin`) e deixar um token com a role antiga viver até o fim da validade | Janela mínima; a concorrência otimista é da spec 10 (legado conhecido). Registrado. |
| Sem tamanho máximo de senha, uma senha enorme encarece o hash (agora também para e-mail inexistente) | A spec 04b limita o tamanho dos campos (registrado no Complexity Tracking do `plan.md`, sem trava de tamanho nesta spec); o PBKDF2 reduz a senha longa a um hash uma vez; conta bloqueada responde sem hash; o limite por IP segue. |
| Limite por conta é por instância | Violação temporária (spec 06). |
| Colisão da sub-rede fixa do compose com a rede do desenvolvedor | Documentado no quickstart; troca-se a sub-rede num lugar só (e o IP do nginx). |
| Bloqueio de conta usado como negação de serviço contra um operador | Aceito na spec (teto de 15 min). |

## R-21 — Emendas de redação sugeridas ao dono do projeto

1. Princípios II e IX: trocar `IActiveSessionChecker` por `ISessionValidator`.
2. Princípio V: incluir `429` nos status usados por use cases.
3. "Legado conhecido": retirar `AddForwardedHeaders`, `AddJwtAuthentication`, `Authenticate/Handler.cs`
   e `Program.cs` (Swagger); em `AddRateLimiting`, manter só "memória da instância (spec 06)".
