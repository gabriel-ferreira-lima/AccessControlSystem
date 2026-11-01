# Guia de Validação: Fundação Multi-Tenant

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Contratos**: [contracts/](contracts/)

Roteiro para provar, ponta a ponta, que a feature funciona. Os cenários de aceitação da spec são
cobertos por testes automatizados; este guia mostra como rodá-los e como conferir à mão no ambiente
de desenvolvimento. Não contém código de implementação.

## 1. Pré-requisitos

- .NET 10 SDK e Docker (Docker Desktop ou Engine + Compose v2).
- Branch `20261031-221333-multi-tenant-foundation`.
- `dotnet tool restore` na raiz, para ter o `dotnet-ef` 10.0.12 do manifesto local (necessário só
  para **gerar** migrations).

> **Regra: migrations só são aplicadas pelo Migrator.** Nunca rode `dotnet ef database update`
> (nem `dotnet ef migrations bundle`, nem um script de `dotnet ef migrations script`) numa base
> nova. A migration `SeedAdmin` seria executada e criaria o administrador de e-mail e senha fixos,
> o que o FR-031 proíbe. Quem neutraliza a `SeedAdmin` em bases novas é a `SeedAdminPolicy` do
> Migrator (`docker compose run --rm migrator migrate`). Gerar migrations com
> `dotnet ef migrations add` continua normal; **aplicar**, só pelo Migrator. A Api também não
> aplica migrations ao iniciar.

## 2. Qualidade (antes de abrir o PR)

```bash
# em src/Backend
dotnet build Backend.slnx
dotnet test Backend.slnx --filter "Category!=Integration"   # rápidos, sem Docker
dotnet test Backend.slnx                                    # tudo, com Docker ligado
dotnet list Backend.slnx package --vulnerable --include-transitive
```

Esperado: build sem erros, todos os testes verdes (incluindo os de handler e de repositório que
já existiam, sem alteração de chamada) e nenhum pacote vulnerável novo. Não há alteração no
frontend, então `npm` não é necessário.

## 3. Subir o ambiente de desenvolvimento (um comando)

```bash
# na raiz; copie .env.example para .env e preencha os segredos
docker compose down -v          # só na primeira vez: o volume antigo tem banco em formato legado
docker compose up --build
```

As migrations do catálogo e de todas as bases são aplicadas **somente** pelo container `migrator`
(ver a regra no início deste guia).

Esperado:
- o container `migrator` roda `migrate` e `seed-dev` e termina com código `0`;
- sobem `db`, `backend` (5290), `backend-b` (5291), `backend-dedicated` (5292) e `frontend` (5173).

Conferir o registro de versões por base (FR-023):

```bash
docker compose exec -e PGPASSWORD=$ACS_MIGRATOR_PASSWORD db \
  psql -U acs_migrator -d acs_catalog \
  -c "select name, kind, schema_version, migration_status from tenant_databases"
```

Esperado: `shared_01` e `dedicated_01` com a mesma `schema_version` (a última migration) e
`migration_status = 3` (`Succeeded`).

## 4. Cenários de validação

Senhas: `DEV_ADMIN_PASSWORD` e as de operador do `.env`. Tenants: A (`5290`) e B (`5291`) dividem
`shared_01`; D (`5292`) mora em `dedicated_01`.

| # | O que fazer | Esperado | História / critério |
|---|---|---|---|
| V1 | `POST /v1/account/login` com `admin@admin.com.br` em cada uma das 3 portas | `200` nas três, cada token com a claim `tenant` do respectivo tenant (decodifique o JWT) | H2, SC-008 |
| V2 | Com o token de A: `GET /v1/account/get-list` na porta 5290 | só os operadores de A (admin e `operador@exemplo.com`) | H1, SC-001 |
| V3 | Idem com o token de B na porta 5291 | só os de B; os ids são diferentes dos de A, apesar do mesmo e-mail | H1, H4, SC-005 |
| V4 | Token de B usado na Api de A (`5290`) em `get-me` | `200` com os dados do operador **de B**: a Api é sem estado e o tenant vem sempre do token; o tenant padrão só vale para o login. (O caso de `401` com `Id` de um tenant e `tenant` de outro é automatizado em `JwtTenantValidationTests`.) | H1, H2 |
| V5 | `GET /v1/account/get-list` com o token de D na porta 5292 | só o admin de D | H1 (base dedicada) |
| V6 | `POST /v1/account/create` com e-mail já existente no **mesmo** tenant | `409` (`E-mail já está em uso`) | H4, SC-005 |
| V7 | `POST /v1/account/create` com e-mail que existe só em **outro** tenant | `201` | H4 |
| V8 | `GET /v1/account/get-me` sem header `Authorization` | `401` | H2 |
| V9 | `GET /v1/account/get-me` com token sem a claim `tenant` (gerado em teste) | `401`, mesma mensagem | H2, SC-002 |
| V10 | Inativar o tenant A (`update tenants set is_active = false` no catálogo como `acs_migrator`) e repetir `get-me` com um token válido de A | `401` na requisição seguinte; reativar e voltar a `200` | H2, SC-002 |
| V11 | Conectar como `acs_app` ao `acs_shared_01` **sem** informar o tenant e rodar `select count(*) from operators` | `0`, mesmo havendo operadores | H1, G2 |
| V12 | Conectar como `acs_app` e tentar `delete from operators` ou `create table x(a int)` | permissão negada | G5 |
| V13 | `docker compose run --rm -e DOTNET_ENVIRONMENT=Production migrator seed-dev` | recusado, código de saída `3`, nenhum dado escrito | H6, SC-007 |
| V14 | Rodar `docker compose up` de novo (ou `docker compose run --rm migrator`) | `migrate` e `seed-dev` terminam com `0` sem duplicar tenants, bases ou operadores | H6, FR-036 |

## 5. Atualização em lotes: falha e retomada (H5)

Automatizado em `Migrator/MigrationOrchestratorTests`. À mão: remova temporariamente a
`TenantDatabases__dedicated_01__MigrationConnectionString` do serviço `migrator` e rode
`docker compose run --rm migrator migrate`. Esperado: `shared_01` continua `Succeeded`,
`dedicated_01` fica `Failed` com o motivo (sem connection string no texto) e o código de saída é
`1`. Restaure a variável e rode de novo: só `dedicated_01` é processada.

## 6. Adoção de uma base legada (H3) e base nova sem admin de seed (FR-031)

Ambos automatizados com Testcontainers (`Infrastructure/Migrations/`):

- **Dados atuais → tenant inicial**: uma base nos formatos `v1` + `SeedAdmin` + `AddIsActiveToOperator`
  com operadores recebe as duas migrations novas com `InitialTenant` configurado. Esperado: mesma
  contagem de linhas, todas com o `tenant_id` do tenant inicial, login funcionando com as senhas
  de antes.
- **Base nova**: criada do zero e migrada **pelo Migrator** em ambiente que não é de
  desenvolvimento. Esperado: 0 operadores e a `SeedAdmin` presente no histórico como já aplicada.
  (Com `dotnet ef database update` direto, o resultado seria o oposto: o admin de seed existiria.
  Por isso essa forma é proibida.)

## 7. Mobilidade (H7)

Automatizado em `Infrastructure/Tenancy/TenantMobilityTests`: copia as linhas de um tenant de uma
base compartilhada para uma dedicada vazia, sem transformar dados, aponta o catálogo para ela e
verifica login, listagem e alteração; depois copia de volta para uma compartilhada com outros
tenants, sem conflito de `id`.

## 8. Limpeza

```bash
docker compose down        # mantém os dados
docker compose down -v     # apaga o volume pg-data (recria tudo no próximo up)
```
