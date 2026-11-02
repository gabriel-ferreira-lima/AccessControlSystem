# Contrato: auditoria de pacotes no CI

**Spec**: FR-024 a FR-030 | **Plano**: Fase D | **Princípio**: IX ("Dependências")

## Regra

> `dotnet list package --vulnerable` **não** é gate: sai com código 0 mesmo com vulnerabilidade e não
> respeita o `NuGetAuditSuppress`. O gate é o NuGet Audit no *restore* (research R-12). A emenda
> PATCH da constituição que alinha o texto a isso está aprovada e será aplicada pelo revisor logo
> após o merge desta spec.

O CI falha quando há pacote com vulnerabilidade conhecida, direto ou transitivo, em **qualquer
severidade**, exceto exceções registradas. Se a base de avisos não puder ser consultada, o CI
falha (nunca "passa em silêncio").

## Backend (NuGet)

| Item | Valor |
|---|---|
| Onde | `src/Backend/Directory.Build.props` (vale para todos os projetos) |
| Auditoria | `NuGetAudit=true`, `NuGetAuditMode=all` (inclui transitivos), `NuGetAuditLevel=low` |
| Falha | `WarningsAsErrors`: `NU1901;NU1902;NU1903;NU1904` sempre; `NU1900` (falha ao consultar os avisos) quando `CI=true` |
| Quando roda | em todo *restore*: `dotnet restore`, `dotnet build`, `dotnet test`, `docker build` das imagens |
| Dockerfiles | a Api e o Migrator copiam **só os `.csproj`** antes do `dotnet restore`; por isso ambos passam a ter `COPY Directory.Build.props ./` **antes** do restore (sem isso a auditoria não valeria nas imagens). Travado por `PackageAuditConfigTests`. Dentro do `docker build` não há `CI`, então o `NU1900` não é erro ali |
| Passo do CI | `dotnet restore Backend.slnx` explícito antes do build (nome claro no log) |

### Exceção (única hoje)

No `AccessControlSystem.Tests.csproj`, e só nele:

```xml
<!-- SSH.NET 2024.2.0 vem transitivo do Testcontainers (recurso "Docker over SSH", nunca usado aqui);
     só existe no projeto de testes e não vai para as imagens de produção. -->
<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-q939-rpr3-3284" />
<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-mggc-4xg6-vcxf" />
```

Substitui o `<NoWarn>$(NoWarn);NU1903</NoWarn>` geral, que sai. **Não existe fallback para
`NoWarn`**: se o `NuGetAuditSuppress` por URL do GHSA não silenciar os avisos do `SSH.NET`, o
implementador **para e reporta a saída real** (a FR-027 proíbe a supressão geral). Aviso novo (outro GHSA), mesmo no
`SSH.NET`, falha. Se uma versão corrigida do Testcontainers existir, a exceção é removida em vez de
mantida.

### Como registrar uma exceção nova

1. Colocar o `NuGetAuditSuppress` com a **URL exata do aviso** no `.csproj` do projeto afetado
   (o menor escopo possível).
2. Escrever a justificativa em comentário XML logo acima (por que o risco não se aplica, onde o
   pacote vive, quando reavaliar).
3. A revisão do PR confere a presença e a qualidade da justificativa. O CI **não** a valida
   (decisão de 2026-11-02).

## Frontend (npm)

| Item | Valor |
|---|---|
| Passo | `npm audit` em `frontend-ci.yml`, depois do `npm ci` |
| Severidade | qualquer (sem `--audit-level`); dependências de desenvolvimento incluídas |
| Rede | sem acesso ao registro, o comando sai com erro e o passo falha |
| Exceções hoje | nenhuma |

Exceção futura (se um dia for preciso): allowlist por GHSA (por exemplo com `audit-ci`) em arquivo
versionado, com a justificativa ao lado e a mesma conferência na revisão do PR. Introduzir o
`audit-ci` exige justificar o pacote novo no `plan.md` da spec que o trouxer (Princípio II).

## Correção das vulnerabilidades de hoje

| Pacote | Antes | Depois | Como |
|---|---|---|---|
| `vite` (direto, dev) | ≤ 6.4.3 | ≥ 6.4.4 (mesma major) | `npm audit fix`; piso em `package.json` sobe para `^6.4.5` |
| `source-map-js` (transitivo) | 1.0.0–1.2.1 | versão corrigida, definida pelo `npm audit fix` | `package-lock.json` |

O `tasks.md` lista as versões reais depois da execução. Typecheck, testes e build do front precisam
continuar verdes sem alterar teste algum. Se o `npm audit fix` pedir `--force` (troca de major),
parar e reportar.

## Prova de que a verificação funciona (FR-030)

PR em **rascunho**, nunca integrado, por ecossistema, com um pacote vulnerável conhecido; o CI do
passo de auditoria precisa ficar **vermelho**; depois o PR é fechado sem merge. Registrar os links
dos dois PRs e das duas execuções no `tasks.md`. Exige push e abertura de PR no GitHub: **é
validação do revisor depois do push (V-01 e V-02), não tarefa do implementador**. O implementador
prova o equivalente localmente: sem a exceção, o `dotnet build` falha com `NU1903`; com um pacote
vulnerável, o `npm audit` sai com código 1.
