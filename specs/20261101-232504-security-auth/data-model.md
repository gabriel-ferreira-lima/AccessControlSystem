# Modelo de Dados: Segurança da autenticação e da sessão

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Decisões**: [research.md](research.md)

Só há **uma mudança persistida** (a coluna da versão de segurança). O restante é estado em memória
(registro de falhas), claims do token e opções de configuração.

## 1. Operador (`operators`) — coluna nova

| Propriedade (C#) | Coluna | Tipo | Regra |
|---|---|---|---|
| `SecurityVersion` | `security_version` | `integer NOT NULL DEFAULT 0` | `private set`; começa em 0; só cresce |

Mapeamento em `OperatorMap`: `builder.Property(x => x.SecurityVersion).HasColumnName("security_version").HasDefaultValue(0).IsRequired()`
(mesmo padrão de `IsActive`). Sem índice novo e sem mudança de RLS: a coluna acompanha a linha, que já tem
`tenant_id` e a política existente. As concessões do papel `acs_app` são por tabela, então ela herda
`SELECT, INSERT, UPDATE`.

### Transições (métodos de domínio, testes de domínio primeiro)

| Método | Efeito em `SecurityVersion` | Observação |
|---|---|---|
| `new Operator(email, password, role)` | `0` | operador novo |
| `UpdatePassword(Password)` | `+1` **sempre** | senha informada conta como troca, mesmo igual à atual |
| `UpdateRole(ERole)` | `+1` **se o perfil é diferente** do atual; senão nada | comparação no domínio |
| `Deactivate()` | `+1` **na transição** ativo → inativo; senão nada | reativar não revalida token antigo |
| `Activate()` | nenhum | |
| `UpdateEmail(Email)` | nenhum | a identidade é o `Id` |

### Invariante e verificação

Um token vale **se e somente se**, no momento da requisição: o operador existe no tenant da claim,
`IsActive = true` e `SecurityVersion` atual == claim `security_version`. Isso é uma única consulta
(`ISessionValidator.IsValidAsync`), executada depois de o tenant ser validado no catálogo.

### Migration

`AddSecurityVersionToOperators` (gerada por `dotnet ef migrations add`; aplicada só pelo Migrator):

```text
Up:   ALTER TABLE operators ADD COLUMN security_version integer NOT NULL DEFAULT 0;
Down: ALTER TABLE operators DROP COLUMN security_version;
```

Expansão pura (Princípio XIII): a Api anterior ignora a coluna; linhas existentes ficam em 0 (a coluna com
`DEFAULT` constante não reescreve a tabela). O `Down` existe só para o desenvolvimento.

## 2. Token de acesso (claims)

| Claim | Valor | Mudança |
|---|---|---|
| `iss` | `Jwt:Issuer` | **nova** |
| `aud` | `Jwt:Audience` | **nova** |
| `exp` | agora + 8 h | igual |
| `Id` | `Operator.Id` | igual |
| `tenant` | `TenantId` do contexto | igual |
| `security_version` | `Operator.SecurityVersion` no login | **nova** |
| `role` (`ClaimTypes.Role`) | `Admin` / `Operator` | igual (legado, spec 08) |
| `name` (`ClaimTypes.Name`) | e-mail do operador | igual, **só informativa**: pode ficar desatualizada e **não pode decidir nada** (a extensão `User.Email()` é removida) |

Token sem `security_version` válida (inteiro) é recusado (`401`) **antes** de qualquer consulta ao banco.

## 3. Registro de falhas de login (memória da instância)

Entidade conceitual do `InMemoryLoginThrottle`. Não é persistida e não tem tabela.

| Campo | Tipo | Significado |
|---|---|---|
| chave | `string` (64 hex) | `SHA-256("{tenantId}:{emailNormalizado}")`; o e-mail nunca fica em memória |
| `Failures` | `int` | tentativas provisórias contadas desde o último bloqueio (0 a `MaxFailures − 1` entre chamadas) |
| `Level` | `int` | número de bloqueios já aplicados nesta sequência |
| `BlockedUntil` | `DateTimeOffset?` | até quando a conta está bloqueada |
| `LastEventAt` | `DateTimeOffset` | maior entre o instante da última tentativa contada e o fim do último bloqueio |

### Regras

Duas operações, cada uma **atômica** (um único `lock` cobre leitura, contagem e decisão):

- `TryAcquireAttempt(agora)`:
  1. entrada **expirada** (`agora − LastEventAt ≥ IdleExpiry`) é descartada e a conta recomeça do zero;
  2. se `BlockedUntil > agora` → devolve `BlockedUntil − agora` **sem contar nada**;
  3. senão conta a tentativa como falha provisória: `Failures++`, `LastEventAt = agora`; se
     `Failures ≥ MaxFailures` → `Level++`, `BlockedUntil = agora + min(Base × 2^(Level−1), MaxWait)`,
     `Failures = 0`, `LastEventAt = BlockedUntil`; em qualquer caso devolve `null` (a tentativa segue, inclusive
     a que acabou de atingir o máximo);
  4. conta nova com o registro **saturado**: devolve 60 s sem contar nada (falha fechada).
- `Reset`: remove a entrada (login com sucesso zera contagem, nível e bloqueio).
- Não há operação de "registrar falha": a falha foi contada na admissão. Por isso, com os padrões, de 50
  chamadas simultâneas à mesma conta exatamente **5** recebem `null`.
- Escada com os padrões (5 falhas por nível): **1, 2, 4, 8 e 15 min** (o quinto bloqueio seria de 16 min e
  é limitado a 15).
- **Saturação**: ao chegar em `MaxTrackedAccounts`, varre expiradas; se ainda cheio, remove um lote de
  entradas **não bloqueadas** mais antigas; nunca remove bloqueada; se não abrir espaço, contas **não
  rastreadas** recebem bloqueio de 60 s enquanto durar a saturação (falha fechada).

### Parâmetros (`LoginProtection:Account`)

| Chave | Padrão | Faixa válida (`[Range]`) | Regra entre campos |
|---|---|---|---|
| `MaxFailures` | 5 | 3 a 100 | — |
| `BaseWaitSeconds` | 60 | 1 a 3 600 | — |
| `MaxWaitSeconds` | 900 | 1 a 86 400 | ≥ `BaseWaitSeconds` |
| `IdleExpirySeconds` | 900 | 1 a 86 400 | ≥ `MaxWaitSeconds` |
| `MaxTrackedAccounts` | 100 000 | 100 a 10 000 000 | — |

Tudo é validado na partida (`ValidateDataAnnotations` + `IValidatableObject` para as regras entre campos).
`IdleExpirySeconds ≥ MaxWaitSeconds` existe para o registro nunca ser descartado no meio de um bloqueio. Os
tetos impedem que um valor errado desligue a proteção na prática.

## 4. Opções de configuração novas

Detalhes e mensagens de falha de partida em [contracts/configuration.md](contracts/configuration.md).

| Seção | Classe | Onde vive |
|---|---|---|
| `Jwt` (`Issuer`, `Audience`) | `JwtOptions` | Api |
| `Secrets` (`JwtPrivateKey`, agora com tamanho mínimo) | `SecretsOptions` + validador | Api |
| `ForwardedHeaders` (`KnownProxies`, `KnownNetworks`) | `TrustedProxyOptions` | Api |
| `LoginProtection:Ip` (`PermitLimit` 1 a 1000, `WindowSeconds` 1 a 3600) | `LoginRateLimitOptions` | Api |
| `LoginProtection:Account` (tabela acima) | `LoginThrottleOptions` | Infrastructure |

## 5. Hash fictício do verificador de senha

Objeto em memória do `PasswordVerifier` (singleton, campo de instância): um `Password` criado no
construtor a partir de um texto forte aleatório. Estrutura esperada
(`SecureIdentity` 1.0.4): `10000.<sal b64 de 16 bytes>.<chave b64 de 32 bytes>`. O teste exige a mesma
estrutura de um `Password` real. Não é persistido e não é segredo (nunca confere com nenhuma senha de
operador).
