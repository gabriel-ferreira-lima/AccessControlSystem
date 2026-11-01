# Contrato: interfaces internas de tenant e catálogo

**Plano**: [../plan.md](../plan.md) | **Princípios**: I, II, III

Declaradas em `AccessControlSystem.Application/SharedContext/Tenancy/` e implementadas na
Infrastructure (ou na Api, quando indicado). Só assinaturas; nenhuma carrega `TenantId` para dentro
de `Request`, `Response` ou entidade de domínio.

```csharp
namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>Tenant da requisição corrente. Preenchido uma única vez por requisição.</summary>
    public interface ITenantContext {
        bool IsResolved { get; }

        /// <exception cref="InvalidOperationException">Quando ainda não foi resolvido (falha fechada).</exception>
        Guid TenantId { get; }
    }

    /// <summary>Valida o tenant no catálogo e o torna o tenant corrente da requisição.</summary>
    public interface ITenantResolver {
        /// <returns>
        /// <c>true</c> se o tenant existe e está ativo (o contexto fica resolvido); <c>false</c> se
        /// não existe ou está inativo (sem distinguir). Lança se a base do tenant não tem
        /// connection string configurada (erro de configuração, nunca outra base).
        /// </returns>
        Task<bool> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }

    /// <summary>Leitura do catálogo. Único caminho do código de tenant até o catálogo.</summary>
    public interface ITenantCatalog {
        /// <summary>Tenant e a base em que ele mora, em uma única consulta.</summary>
        Task<TenantInfo?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>Tenants ativos por paginação de chave (id &gt; afterTenantId), ordem por id. size de 1 a 100.</summary>
        Task<IReadOnlyList<TenantInfo>> GetActiveTenantListAsync(
            Guid? afterTenantId, int size, CancellationToken cancellationToken = default);
    }

    /// <summary>Tenant como está no catálogo. <c>Cnpj</c> é texto lido, sem validação aqui.</summary>
    public sealed record TenantInfo(Guid Id, string Cnpj, string Name, bool IsActive, TenantDatabaseInfo Database);

    /// <summary>Onde o tenant mora. Não contém connection string nem segredo.</summary>
    public sealed record TenantDatabaseInfo(string Name, EDatabaseKind Kind);

    public enum EDatabaseKind { Shared = 1, Dedicated = 2 }
}
```

O catálogo pertence ao sistema de provisionamento. Estas interfaces **só leem** e devolvem `record`s;
o modelo de persistência (`CatalogTenant`, `CatalogDatabase`) é interno da Infrastructure e nunca
sai dela. O Domain não muda.

## Implementações e escopos de DI

| Interface | Implementação | Projeto | Escopo |
|---|---|---|---|
| `ITenantContext` | `TenantScope` | Infrastructure | scoped (a mesma instância serve a `ITenantResolver`) |
| `ITenantResolver` | `TenantResolver` | Infrastructure | scoped |
| `ITenantCatalog` | `TenantCatalog` (usa `CatalogDbContext`, `AsNoTracking`) | Infrastructure | scoped |
| `IActiveSessionChecker` | `ActiveSessionChecker` (sem mudança) | Infrastructure | scoped |
| `ITokenService` | `TokenService` (agora recebe `IOptions<SecretsOptions>` e `ITenantContext`) | Api | scoped |

`EDatabaseKind` fica na própria Application (`SharedContext/Tenancy/EDatabaseKind.cs`); a
Infrastructure o usa para mapear a coluna `kind`.

## O que NÃO muda

Os `Request`, `Response`, `Specification`, `Handler`, `Contracts/IRepository` e `Repository` de
todos os use cases do `AccountContext` (incluindo o `GetMe`, usado como referência) permanecem
como estão. O `AppDbContext` continua sendo recebido pelo construtor dos repositórios; só a forma
como ele é criado muda (por requisição, pela base do tenant).
