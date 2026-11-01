# Contrato: renomeações de repositório e de domínio

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Mapa símbolo a símbolo e arquivo a arquivo da fase **A** (`User.Activate`) e da fase **B** (B1 a B5).
Todos os caminhos de produção são relativos a `src/Backend/`; os de teste, a
`src/Backend/AccessControlSystem.Tests/`. Os números de linha são de 2026-11-01 (antes da mudança).

## 1. Os seis símbolos

| Passo | Antes | Depois | Onde |
|---|---|---|---|
| A | `User.Active()` | `User.Activate()` | `AccessControlSystem.Domain/Contexts/UserContext/Entities/User.cs:81` (nenhum chamador) |
| B1 | `GetUserById(Guid id, CancellationToken)` | `GetOperatorByIdAsync(Guid id, CancellationToken)` | use cases Activate, Deactivate, GetMe, Update, UpdateMe |
| B2 | `GetUserByEmailAsync(string email, CancellationToken)` | `GetOperatorByEmailAsync(string email, CancellationToken)` | use case Authenticate |
| B3 | `GetOperatorList(int page, int size, EActiveFilter isActive, CancellationToken)` | `GetOperatorListAsync(...)` (mesma assinatura) | use case Get |
| B4 | `AnyAsync(string email, CancellationToken)` | `EmailExistsAsync(string email, CancellationToken)` | use case Create |
| B5 | `CheckIfEmailIsNotUsed(string email, Guid id, CancellationToken)` | `EmailExistsAsync(string email, Guid exceptId, CancellationToken)` | use cases Update e UpdateMe |

Os tipos de retorno **não mudam** (`Task<Operator?>`, `Task<PagedResponse<OperatorItem>>`,
`Task<bool>`). Só o nome do método e, em B5, o nome do parâmetro `id` → `exceptId`.

## 2. Linhas de produção (31)

| Passo | Arquivo | Linha(s) |
|---|---|---|
| A | `Domain/.../UserContext/Entities/User.cs` | 81 |
| B1 | `Application/.../Activate/Contracts/IRepository.cs` | 6 |
| B1 | `Application/.../Deactivate/Contracts/IRepository.cs` | 6 |
| B1 | `Application/.../GetMe/Contracts/IRepository.cs` | 6 |
| B1 | `Application/.../Update/Contracts/IRepository.cs` | 8 |
| B1 | `Application/.../UpdateMe/Contracts/IRepository.cs` | 8 |
| B1 | `Application/.../Activate/Handler.cs` | 24 |
| B1 | `Application/.../Deactivate/Handler.cs` | 25 |
| B1 | `Application/.../GetMe/Handler.cs` | 21 |
| B1 | `Application/.../Update/Handler.cs` | 27 |
| B1 | `Application/.../UpdateMe/Handler.cs` | 27 |
| B1 | `Infrastructure/.../Activate/Repository.cs` | 14 |
| B1 | `Infrastructure/.../Deactivate/Repository.cs` | 15 |
| B1 | `Infrastructure/.../GetMe/Repository.cs` | 13 |
| B1 | `Infrastructure/.../Update/Repository.cs` | 17 |
| B1 | `Infrastructure/.../UpdateMe/Repository.cs` | 29 |
| B2 | `Application/.../Authenticate/Contracts/IRepository.cs` | 6 |
| B2 | `Application/.../Authenticate/Handler.cs` | 29 |
| B2 | `Infrastructure/.../Authenticate/Repository.cs` | 13 |
| B3 | `Application/.../Get/Contracts/IRepository.cs` | 6 |
| B3 | `Application/.../Get/Handler.cs` | 24 |
| B3 | `Infrastructure/.../Get/Repository.cs` | 15 |
| B4 | `Application/.../Create/Contracts/IRepository.cs` | 9 |
| B4 | `Application/.../Create/Handler.cs` | 39 |
| B4 | `Infrastructure/.../Create/Repository.cs` | 17 |
| B5 | `Application/.../Update/Contracts/IRepository.cs` | 10 |
| B5 | `Application/.../UpdateMe/Contracts/IRepository.cs` | 10 |
| B5 | `Application/.../Update/Handler.cs` | 52 |
| B5 | `Application/.../UpdateMe/Handler.cs` | 39 |
| B5 | `Infrastructure/.../Update/Repository.cs` | 32 (+ `x.Id != id` no corpo) |
| B5 | `Infrastructure/.../UpdateMe/Repository.cs` | 33 (+ `x.Id != id` no corpo) |

(`.../` = `Context/AccountContext/UseCases/{UseCase}`.)

## 3. O que NÃO muda (conferir a cada passo)

| Item | Por quê fica |
|---|---|
| `AnyAsync` do **EF Core** dentro de `Create/Repository.cs`, `Update/Repository.cs` e `UpdateMe/Repository.cs` | É a consulta; o que muda é o nome do método que a envolve |
| `AnyAsync` do EF em `Infrastructure/Services/ActiveSessionChecker.cs:17` e `Migrator/Commands/SeedDevCommand.cs:149` | Outro assunto; nenhum repositório de use case |
| `AnyAsync` do EF em testes: `InitialTenantRegistrarTests.cs:79-80`, `CatalogMappingTests.cs:198`, `P03QueryShapesPremiseTests.cs:33-34`, `CreateRepositoryTests.cs:88,90` | Consultas dos testes, não chamadas ao repositório |
| `SaveAsync` (Create) | Já é `{Ação}Async`; confirmado no clarify (20 ocorrências ficam) |
| `IsLastActiveAdminAsync`, `ActivateAsync`, `DeactivateAsync`, `UpdateAsync` | Já seguem a convenção |
| `ITenantCatalog.GetTenantByIdAsync` / `GetActiveTenantListAsync`, `IActiveSessionChecker.IsActiveAsync` | Já seguem a convenção |
| Corpo das consultas, `AsNoTracking()`, filtros por `tenant_id`, paginação | Comportamento: nada muda |
| Migrations | Não se tocam |

## 4. Condição dos handlers: continua idêntica (sem `!` novo)

`UpdateMe/Handler.cs` (linha 39) e `Update/Handler.cs` (linha 52):

```csharp
// antes
if (await _repository.CheckIfEmailIsNotUsed(newEmail.Address, request.Id, cancellationToken)) {
    return new Response("Email já está em uso", 409);
}
// depois (só o nome do método muda)
if (await _repository.EmailExistsAsync(newEmail.Address, request.Id, cancellationToken)) {
    return new Response("Email já está em uso", 409);
}
```

`Create/Handler.cs` (linha 39): `var exists = await _Repository.AnyAsync(email, ...); if (exists) 409` passa a
`EmailExistsAsync(email, ...)`, também sem inverter a condição.

## 5. Testes por passo

**Regra**: nos testes só mudam **nomes** (chamada, `Substitute`, `Received`, `DidNotReceive` e o
nome do método de teste). Nenhuma asserção, dado ou cenário muda; é provado por reversão
([quickstart.md](../quickstart.md), V2).

| Passo | Arquivos de teste (18 no total na fase B) |
|---|---|
| B1 | `Application/AccountContext/{Activate,Deactivate,GetMe,Update,UpdateMe}/…HandlerTests.cs`; `Infrastructure/AccountContext/{Activate,Deactivate,GetMe,Update,UpdateMe}/…RepositoryTests.cs`; `Infrastructure/Tenancy/TenantIsolationTests.cs` (linhas 55 e 101) e `DedicatedTenantIsolationTests.cs` (linhas 49, 50, 73 e 74) |
| B2 | `Application/AccountContext/Authenticate/AuthenticateHandlerTests.cs`; `Infrastructure/AccountContext/Authenticate/AuthenticateRepositoryTests.cs` |
| B3 | `Application/AccountContext/Get/GetHandlerTests.cs`; `Infrastructure/AccountContext/Get/GetRepositoryTests.cs`; `TenantIsolationTests.cs` (linha 38) e `DedicatedTenantIsolationTests.cs` (linhas 30, 47 e 48) |
| B4 | `Application/AccountContext/Create/CreateHandlerTests.cs` (linhas 18, 45, 84); `Infrastructure/AccountContext/Create/CreateRepositoryTests.cs` (linhas 26, 31, 35, 43, 113) |
| B5 | `UpdateHandlerTests.cs`, `UpdateMeHandlerTests.cs`, `UpdateRepositoryTests.cs`, `UpdateMeRepositoryTests.cs` |

Os 18 arquivos únicos: 8 de handler + 8 de repositório + 2 de isolamento de tenant. Alguns entram em
mais de um passo (por exemplo `UpdateMe*Tests`, em B1 e B5).

### Os 20 métodos de teste cujo nome cita o método (FR-006)

O prefixo troca; o resto da frase (o comportamento) fica **exatamente** como está.

| Arquivo | Antes (prefixo) → depois |
|---|---|
| `Activate/ActivateRepositoryTests.cs` (2) | `GetUserById_` → `GetOperatorByIdAsync_` (`devolve_o_operador`, `null_quando_nao_existe`) |
| `Deactivate/DeactivateRepositoryTests.cs` (2) | `GetUserById_` → `GetOperatorByIdAsync_` (`devolve_o_operador`, `null_quando_nao_existe`) |
| `GetMe/GetMeRepositoryTests.cs` (2) | `GetUserById_` → `GetOperatorByIdAsync_` (`devolve_o_operador_com_dados_completos`, `null_quando_nao_existe`) |
| `Update/UpdateRepositoryTests.cs` (5) | `GetUserById_` → `GetOperatorByIdAsync_` (2); `CheckIfEmailIsNotUsed_` → `EmailExistsAsync_` (`true_quando_outro_operador_tem_o_email`, `false_para_o_proprio_email`, `considera_so_o_tenant_corrente`) |
| `UpdateMe/UpdateMeRepositoryTests.cs` (5) | os mesmos 5 do `Update` |
| `Create/CreateRepositoryTests.cs` (2) | `AnyAsync_` → `EmailExistsAsync_` (`false_quando_o_email_nao_existe`, `true_depois_de_salvar`) |
| `Authenticate/AuthenticateRepositoryTests.cs` (2) | `GetUserByEmailAsync_` → `GetOperatorByEmailAsync_` (`null_quando_nao_existe`, `devolve_o_operador_com_a_senha_utilizavel`) |

Os métodos `SaveAsync_...` de `CreateRepositoryTests` **não** mudam. Nenhum teste de handler cita o
método no nome.

## 6. Critérios de aceite do contrato

- A solução compila (`0 avisos, 0 erros`) e os testes passam depois de **cada** passo.
- Busca final sem resultado para `GetUserById`, `GetUserByEmailAsync`, `CheckIfEmailIsNotUsed`,
  `GetOperatorList\b`, `\.Active\(\)` e `void Active\(` em `src/`, e para `AnyAsync` **de
  repositório de use case** (o do EF permanece).
- Reversão dos testes (V2): diferença vazia em todos os arquivos de teste da fase B.
