# AccessControlSystem.Tests

Estrutura espelha a das outras camadas, por contexto / use case:

```
Domain/
  ValueObjects/        EmailTests, PasswordTests
  Entities/            OperatorTests
  SharedContext/       EntityTests
Application/
  AccountContext/Create/        CreateSpecificationTests, CreateHandlerTests
  AccountContext/Authenticate/  AuthenticateSpecificationTests, AuthenticateHandlerTests
Infrastructure/
  Fixtures/            PostgresContainerFixture   (Testcontainers, 1 container por run)
  AccountContext/Create/        CreateRepositoryTests
  AccountContext/Authenticate/  AuthenticateRepositoryTests
  AccountContext/               OperatorMappingTests
Api/                  GlobalExceptionHandlerTests, TokenServiceTests
```

## Rodando

**Rápidos** (Domain + Application + Api, sem Docker):

```
dotnet test --filter "Category!=Integration"
```

**Tudo**, incluindo os testes de infraestrutura (precisam do Docker ligado — sobem um
PostgreSQL real via Testcontainers e aplicam as migrations `v1` + `SeedAdmin`):

```
dotnet test
```

## Ferramentas

- **xUnit** — runner
- **NSubstitute** — fakes de `IRepository` / `ITokenService` nos testes de handler
- **Testcontainers.PostgreSql** — Postgres efêmero para os testes de repositório e mapeamento
