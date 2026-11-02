# Modelo de dados: Limpeza de legado

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Esta feature **não cria nem altera dado persistido**: nenhuma tabela, coluna, índice, constraint,
mapeamento EF Core (`*Map`), migration, `tenant_id`, política de RLS ou formato de JSON muda. O
"modelo" aqui é o que **muda de nome** e a única entidade cujo estado é exercitado por um teste
novo. Tudo o que não está listado abaixo permanece idêntico.

## Entidade afetada: `User` (`Domain/Contexts/UserContext/Entities/User.cs`)

| Campo | Tipo | Observação |
|---|---|---|
| `IsActive` | `bool`, `private set`, padrão `true` | exclusão é sempre lógica (Princípio IV) |

Transições de estado de ativação (os efeitos **não mudam**, só o nome do primeiro método):

| De | Método | Para |
|---|---|---|
| inativo | `Activate()` (antes `Active()`) | ativo |
| ativo | `Deactivate()` | inativo |

`Operator` (`AccountContext`) já usa o par `Activate()` / `Deactivate()`; `User` passa a espelhá-lo
(Princípio IV: "Transições de ativação DEVEM usar sempre o par"). Nenhum chamador de `Active()`
existe em `src/`.

**Invariantes de `User` que não mudam** (construtor): observação com no máximo 100 caracteres; data
final não anterior à data inicial; setters `private`; construtor `protected` vazio para o EF.

## Contratos de repositório depois da renomeação (`AccountContext`)

Cada use case continua com o **seu próprio** `IRepository`, só com o que precisa (Princípio III).
Em **negrito**, o que muda de nome. Retornos e `CancellationToken` iguais aos de hoje.

| Use case | Membros do `IRepository` |
|---|---|
| Activate | **`GetOperatorByIdAsync`**(`Guid id`) → `Operator?` · `ActivateAsync`(`Operator`) |
| Deactivate | **`GetOperatorByIdAsync`**(`Guid id`) → `Operator?` · `DeactivateAsync`(`Operator`) · `IsLastActiveAdminAsync`(`Guid exceptId`) → `bool` |
| GetMe | **`GetOperatorByIdAsync`**(`Guid id`) → `Operator?` |
| Authenticate | **`GetOperatorByEmailAsync`**(`string email`) → `Operator?` |
| Get | **`GetOperatorListAsync`**(`int page`, `int size`, `EActiveFilter isActive`) → `PagedResponse<OperatorItem>` |
| Create | **`EmailExistsAsync`**(`string email`) → `bool` · `SaveAsync`(`Operator`) |
| Update | **`GetOperatorByIdAsync`**(`Guid id`) → `Operator?` · `UpdateAsync`(`Operator`) → `Operator` · **`EmailExistsAsync`**(`string email`, `Guid exceptId`) → `bool` |
| UpdateMe | os mesmos três membros do Update |

### Semântica dos dois `bool` (conferida no código)

| Método | Responde | Devolve `true` quando |
|---|---|---|
| `Create.EmailExistsAsync(email)` | "este e-mail já existe neste tenant?" | existe um operador com o e-mail |
| `Update/UpdateMe.EmailExistsAsync(email, exceptId)` | "este e-mail pertence a **outro** operador?" | existe um operador com o e-mail e `Id` diferente de `exceptId` |

É a resposta de hoje (`AnyAsync` com `x.Id != id`); só o nome passa a dizê-la. O isolamento por
tenant continua vindo do filtro global do EF e do RLS, não do método.

## Configuração (`Secrets`)

| Chave | Antes | Depois |
|---|---|---|
| `Secrets:JwtPrivateKey` | obrigatória (`[Required]`), validada na inicialização | idêntica |
| `Secrets:PasswordSaltKey` | presente em `appsettings.json` e exigida pelo compose; **nenhum código a lê** | **removida** |

`SecretsOptions` (`Api/Options/SecretsOptions.cs`) não muda: já só tem `JwtPrivateKey`.

## Respostas da API

O envelope (`message`, `status`, `isSuccess`, `notifications`, `data`) e os `ResponseData` de todos
os use cases são idênticos. Mudam apenas 3 textos de `message`
([contracts/api-messages.md](contracts/api-messages.md)).

## Dados de teste (única adição)

Para o teste `UserTests.Activate_reativa_o_usuario_desativado` (FR-007), um `User` válido monta-se com
os valores padrão dos value objects do contexto, que aceitam vazio, e um nome de 3 a 100 caracteres:

| Value object | Valor usado |
|---|---|
| `Name` | `"Maria Silva"` (sintético) |
| `Telephone`, `PasswordAccess`, `Rg`, `Cpf` | construtor padrão (vazio) |

Nenhum dado pessoal real (Princípio X).
