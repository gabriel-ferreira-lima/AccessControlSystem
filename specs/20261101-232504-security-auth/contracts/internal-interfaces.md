# Contrato: interfaces internas

**Spec**: [../spec.md](../spec.md) | **Decisões**: [../research.md](../research.md) (R-05, R-08, R-10, R-12)

Todas declaradas na **Application** e implementadas na **Infrastructure** ou na **Api** (Princípio II).
Assinaturas orientam o `tasks.md`; os nomes dos métodos seguem o Princípio III.

## `ISessionValidator` (renomeia `IActiveSessionChecker`)

`Application/SharedContext/UseCases/Services/ISessionValidator.cs` — implementação
`Infrastructure/Services/SessionValidator.cs`, `Scoped`.

```csharp
public interface ISessionValidator {
    /// true se o operador existe no tenant corrente, está ativo e tem exatamente esta versão de segurança.
    Task<bool> IsValidAsync(Guid operatorId, int securityVersion, CancellationToken cancellationToken = default);
}
```

- **Uma** consulta ao banco. Roda sob o RLS do tenant (o tenant já foi resolvido pelo `JwtBearer`).
- Chamada por `OnTokenValidated` depois da validação do tenant no catálogo.

## `IPasswordVerifier`

`Application/Context/AccountContext/UseCases/Authenticate/Contracts/IPasswordVerifier.cs` — implementação
`Infrastructure/Context/AccountContext/UseCases/Authenticate/PasswordVerifier.cs`, `Singleton`.

```csharp
public interface IPasswordVerifier {
    /// Sempre executa UMA verificação de hash. Com operador nulo, verifica contra um hash fictício de mesmo
    /// algoritmo e custo e devolve false. Não decide sobre conta inativa (isso é do handler).
    bool Verify(Operator? @operator, string password);
}
```

- A implementação mantém o hash fictício em **campo de instância** (nunca `static`).
- Testes: o handler chama `Verify` nos três casos de falha e no sucesso (NSubstitute); o `PasswordVerifier`
  tem hash fictício com a mesma estrutura de um `Password` real.

## `ILoginThrottle`

`Application/.../Authenticate/Contracts/ILoginThrottle.cs` — implementação `InMemoryLoginThrottle`
(Infrastructure, `Singleton`, depende de `TimeProvider` e `IOptions<LoginThrottleOptions>`).

```csharp
public interface ILoginThrottle {
    /// Admissão ATÔMICA de uma tentativa de login para a conta (tenant + e-mail normalizado).
    /// Sob o mesmo lock: se a conta está bloqueada, devolve o tempo restante (e não conta nada); senão conta
    /// a tentativa como falha provisória e, se ela atingir o máximo, bloqueia pela espera do nível.
    /// Devolve null quando a tentativa pode seguir (inclusive a que acabou de atingir o máximo).
    Task<TimeSpan?> TryAcquireAttemptAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    /// Login com sucesso: zera contagem, nível e bloqueio da conta.
    Task ResetAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);
}
```

- O handler chama `TryAcquireAttemptAsync` **antes** da busca do operador e da verificação de hash, e
  `ResetAsync` só no sucesso. Não existe "registrar falha": a falha já foi contada na admissão (FR-018: sem
  janela para uma rajada simultânea passar pela consulta antes de qualquer falha ser contada).
- O `email` já chega normalizado (`Email.Address`). A implementação o transforma em hash antes de guardar.
- Falha fechada: se não puder rastrear a conta (registro saturado), devolve bloqueio de 60 s, nunca `null`
  por engano.
- A spec 06 implementa **o mesmo contrato** com `INCR` atômico no Redis, sem mudar a interface nem a regra.
- Teste de contrato (`InMemoryLoginThrottleTests`): 50 chamadas simultâneas à mesma conta → exatamente 5
  `null` e 45 tempos de bloqueio positivos.

## `IRetryAfterResponse`

`Application/Context/AccountContext/UseCases/Authenticate/Contracts/IRetryAfterResponse.cs` (só o
`Authenticate` a usa; promovida a `SharedContext` quando um segundo use case precisar, Princípio III). O
filtro `RetryAfterHeaderAttribute` da Api referencia a interface neste namespace.

```csharp
public interface IRetryAfterResponse {
    TimeSpan? RetryAfter { get; }   // [JsonIgnore] na implementação: não vai no corpo
}
```

- `Authenticate.Response` a implementa; o filtro `[RetryAfterHeader]` (Api) copia o valor para o cabeçalho
  `Retry-After` (segundos, arredondado para cima).

## Registro de DI (manual, por `#region`)

Em `UseCasesExtensions.AddAccountContext()`, dentro de `#region Authenticate`:
`IPasswordVerifier → PasswordVerifier` (Singleton), `ILoginThrottle → InMemoryLoginThrottle` (Singleton),
`TimeProvider.System` (Singleton, `TryAdd`). `ISessionValidator → SessionValidator` (Scoped) em
`AddJwtAuthentication`, no lugar do checker.
