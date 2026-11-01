# Rascunho da descrição do PR: Fundação Multi-Tenant

> Rascunho (T148). Não faz parte da entrega funcional: copie o texto para a descrição do PR na
> `main` e apague este arquivo antes de commitar, se preferir.

## O que muda

Prepara o AccessControlSystem para atender de centenas a 150 mil tenants no modelo híbrido (bases
compartilhadas isoladas por `tenant_id`, filtro do EF e RLS do PostgreSQL; bases dedicadas
opcionais), **sem mudar o que o operador vê**: o contrato HTTP dos 8 endpoints de `v1/account/*` é
o mesmo (teste `ApiContractCompatTests`).

- Catálogo de tenants e bases (`ITenantCatalog`), tenant resolvido por requisição a partir da claim
  `tenant` do JWT (consulta o catálogo a cada requisição, sem cache).
- `tenant_id` em `operators` (propriedade sombra), RLS `ENABLE` + `FORCE`, papéis `acs_migrator` e
  `acs_app` (sem DDL, sem DELETE, sem `BYPASSRLS`).
- Novo projeto `AccessControlSystem.Migrator` (`migrate` e `seed-dev`): a Api **não aplica
  migrations ao iniciar**.
- Ambiente de desenvolvimento em um comando: catálogo, base compartilhada, base dedicada, migrator
  e uma Api por tenant de teste.
- Domain e use cases do `AccountContext` sem nenhuma mudança.

## Exceções constitucionais (Complexity Tracking do `plan.md`)

1. **Cenário k6 e SC-010 verificados na spec 06, fora desta entrega.** Esta spec altera um caminho
   crítico (autenticação) e o Princípio XIII exige o gate de carga. A diretriz da feature registra
   que o k6 só passa a existir na spec 06, que mede a baseline cobrindo este pipeline. Criar um
   cenário agora exigiria inventar a carga de referência. **Nenhum número de desempenho foi
   inventado** e nenhum threshold foi afrouxado: as metas continuam as da constituição.
2. **2 idas ao banco por requisição autenticada** (catálogo + base do tenant), no lugar de no
   máximo 1. Catálogo e base do tenant são bancos diferentes; cache em memória viola o Princípio I e
   Redis está fora do escopo. A spec de escala resolve com cache no Redis (TTL ≤ 60 s).
3. **Migração fundadora sem compatibilidade com a Api anterior** (expand/contract). O RLS torna as
   linhas invisíveis à Api antiga, então ela não pode conviver com as bases novas. Não existe
   ambiente fora de desenvolvimento, logo nenhuma instância antiga fica no ar. O conteúdo segue
   expand/contract em duas migrations.

Demais itens do Complexity Tracking: projeto Migrator; migrations aplicadas em processo (e não por
bundle do EF); pré-marcação da `SeedAdmin` em bases novas; índice `(tenant_id, email)` criado só na
migration; `Microsoft.AspNetCore.Mvc.Testing`; `dotnet-ef` em manifesto local; e dois pacotes
oficiais de opções (`Options.ConfigurationExtensions` e `Options.DataAnnotations`) na Infrastructure.

## Achados da implementação (registrados em `research.md`)

- **R-06, retomada após falha**: uma 1ª execução sem tenant inicial deixava a base sem saída (a 2ª
  execução, já com o tenant, falhava de novo). A `EnforceTenantIsolationOnOperators` agora repete o
  backfill, de forma idempotente. Teste que reproduz o cenário incluído.
- **R-07, conexões do Migrator**: o pool do Npgsql por base deixaria uma conexão ociosa por base no
  servidor; o executor abre cada base com `Pooling=false`.
- **R-11, `seed-dev`**: no primeiro `up` o `migrate` encontra o catálogo vazio; o `seed-dev` passa a
  atualizar as bases que acabou de cadastrar antes de criar os operadores.

## Antes de mergear

- `git update-index --chmod=+x docker/postgres/init/01-init.sh` (o script funciona mesmo sem o bit,
  porque o entrypoint do Postgres o executa com `source`, mas ele deve ser executável).
- Na primeira vez em cada máquina: `docker compose down -v` (o volume antigo `db-data` tem banco no
  formato legado) e criar o `.env` a partir do `.env.example`.
- Sugestões de fluxo: `/speckit-analyze` e, depois, `/speckit-converge`.
