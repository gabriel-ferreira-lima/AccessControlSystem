# AccessControlSystem

Sistema de controle de acesso multi-tenant. As regras do projeto estão na constituição:
`.specify/memory/constitution.md`. Ela prevalece sobre qualquer outra orientação.

## Idioma

- **Converse com o usuário em português do Brasil.**
- **Artefatos do Spec Kit em português do Brasil** (`spec.md`, `plan.md`, `tasks.md`,
  `research.md`, `data-model.md`, `quickstart.md`, `contracts/`, `checklists/`), incluindo os
  títulos de seção, mesmo que o template esteja em inglês. Preserve os marcadores
  `[NEEDS CLARIFICATION: ...]`, `- [ ]` / `- [X]`, `T001`, `FR-001`, `SC-001`, `[P]` e `[US1]`.
- **Código e banco em inglês**, com a nomenclatura da constituição: PascalCase/camelCase do
  .NET no C#, camelCase/PascalCase no TypeScript, `snake_case` em tabelas e colunas.
- **Em português**: nomes de métodos de teste (frases com underscore), comentários, títulos de
  `#region` e todo texto exibido ao usuário, com acentuação.

## Estrutura

- `src/Backend/` — .NET 10 (Domain, Application, Infrastructure, Api, Tests). Solução:
  `src/Backend/Backend.slnx`.
- `src/Frontend/` — Vite + React + TypeScript.
- `specs/` — specs geradas pelo Spec Kit (`/speckit-specify`).

## Comandos

```bash
# Backend (em src/Backend)
dotnet build Backend.slnx
dotnet test Backend.slnx                                  # inclui integração (Docker ligado)
dotnet test Backend.slnx --filter "Category!=Integration" # só testes rápidos

# Frontend (em src/Frontend)
npm run typecheck
npm run build

# Tudo via Docker (na raiz): catálogo, bases, migrator e uma API por tenant de teste
docker compose up --build

# Migrator (na raiz, via Docker): único caminho que aplica migrations
docker compose run --rm migrator migrate     # atualiza o catálogo e todas as bases de tenant
docker compose run --rm migrator seed-dev    # tenants e operadores sintéticos (só em Development)
```

**Migrations só pelo Migrator.** A Api não aplica migrations ao iniciar. Nunca rode
`dotnet ef database update` (nem bundle, nem script de `dotnet ef migrations script`) numa base
nova: a migration `SeedAdmin` criaria o administrador de senha fixa. `dotnet ef migrations add`
continua normal, só para **gerar** migrations.

## Fluxo

Cada spec na sua própria branch, entrando na `main` só por pull request. Siga
`/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` →
`/speckit-analyze` → `/speckit-implement` → `/speckit-converge`.

Nunca apague, pule ou afrouxe um teste ou threshold para uma entrega passar (constituição,
"Integridade das entregas"): corrija a causa ou pare e reporte a falha.
