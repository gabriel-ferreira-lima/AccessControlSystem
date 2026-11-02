# Modelo de Dados: Segurança do perímetro

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

## Banco de dados

**Nenhuma mudança.** Sem tabela, coluna, índice, política de RLS, privilégio nem migration nova. O
`acs_app` continua exatamente como o `roles.sql`, o `tenant-db-grants.sql` e o
`catalog-db-grants.sql` o definem; esta spec **prova** esse estado por teste (ver
[contracts/app-role-guarantees.md](contracts/app-role-guarantees.md)) e só mexe nele se a prova
descobrir uma permissão a mais.

Por isso a seção "Consultas novas e índices que as atendem" (Princípio XIII) é: **nenhuma consulta
nova na aplicação**. Os testes consultam o catálogo do PostgreSQL (`pg_roles`, `pg_class`,
`pg_tables`, `pg_auth_members`, `has_table_privilege`), sempre no ambiente de teste.

## Conceitos governados (do spec, sem persistência)

### Limite de tamanho de campo

Uma regra por campo de texto de cada `Request`. Vive como `const` no value object do Domain (ou na
`Specification`, quando não há value object) e é aplicada na `Specification`.

| Use case | Campo | Máximo (caracteres) | Constante |
|---|---|---|---|
| `AccountContext/Authenticate` | `Email` | 255 | `Email.MaxLength` |
| `AccountContext/Authenticate` | `Password` | 128 | `Password.MaxLength` |
| `AccountContext/Create` | `Email`, `Password` | 255, 128 | idem |
| `AccountContext/Update` | `Email`, `Password` (quando informados) | 255, 128 | idem |
| `AccountContext/UpdateMe` | `Email`, `Password` (quando informados) | 255, 128 | idem |
| `UserContext/Create` | `Name` | 100 (já existia) | `Name.MaxLength` |
| `UserContext/Create` | `Observation` | 100 (já existia) | constante da `Specification` |
| `UserContext/Create` | `Telephone` | 20 | `Telephone.MaxLength` |
| `UserContext/Create` | `Rg` | 20 | `Rg.MaxLength` |
| `UserContext/Create` | `Cpf` | 14 | `Cpf.MaxLength` |
| `UserContext/Create` | `Password` (PIN) | 6 | `PasswordAccess.MaxLength` |

`Get`, `GetMe`, `Activate` e `Deactivate` não têm campo de texto. Detalhes (mensagens, chave da
notificação, regra de contagem) em [contracts/input-limits.md](contracts/input-limits.md).

### Conjunto de cabeçalhos de segurança do console

Definido uma vez em `src/Frontend/security-headers.conf`, incluído em todo `server`/`location` que
responde. O conteúdo exato, por tipo de resposta, está em
[contracts/security-headers.md](contracts/security-headers.md).

Relação: o **hash** da CSP é derivado do conteúdo do script do tema em `index.html`
(`sha256` de base64, com quebras de linha normalizadas para LF). Quando o script muda, o hash
muda; o teste do Vitest e o script de verificação do CI são quem garante a coerência.

### Exceção de vulnerabilidade

| Ecossistema | Onde vive | Forma | Escopo | Exceções hoje |
|---|---|---|---|---|
| NuGet | `AccessControlSystem.Tests.csproj` | `<NuGetAuditSuppress Include="<URL do GHSA>" />` + comentário de justificativa ao lado | só o projeto onde está | 2 (`SSH.NET`: GHSA-q939-rpr3-3284 e GHSA-mggc-4xg6-vcxf) |
| npm | (nenhum arquivo hoje) | opção futura: allowlist por GHSA com justificativa ao lado, via `audit-ci` | o `package.json` do front | 0 |

Regras: por aviso, nunca por pacote; a justificativa é escrita ao lado e conferida na revisão do PR
(o CI não a valida); não há vencimento. Detalhes em
[contracts/dependency-audit.md](contracts/dependency-audit.md).

### Usuário de banco da aplicação (`acs_app`)

Papel do cluster PostgreSQL usado pela Api. Privilégios esperados por base:

| Base | Privilégios de tabela | Atributos do papel | Propriedade |
|---|---|---|---|
| Catálogo | `SELECT` | sem superusuário, criação de banco, criação de papel, replicação nem BYPASSRLS | não é dono de banco, esquema nem tabela |
| Compartilhada | `SELECT, INSERT, UPDATE` (sem `DELETE`, sem `TRUNCATE`), sujeito a RLS com `FORCE` | idem | idem |
| Dedicada | idem compartilhada | idem | idem |

Em nenhuma base: `CREATE` no esquema `public`, associação ao papel `acs_migrator` (que impediria
`SET ROLE`) nem outra associação que dê DDL.

## Transições de estado

Não se aplica: nenhuma entidade de negócio muda de estado.
