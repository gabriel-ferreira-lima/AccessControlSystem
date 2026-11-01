# Plano de Implementação: Limpeza de legado

**Branch**: `20261101-194211-legacy-cleanup` | **Data**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `/specs/20261101-194211-legacy-cleanup/spec.md`

## Resumo

Refactor de alinhamento, **sem mudança de comportamento** além de 3 mensagens da API e 17 textos de
interface com a grafia corrigida. O trabalho é dividido em **5 fases pequenas e independentes**, cada
uma com build e testes verdes ao final, para revisão e commit separados (o agente não faz commits).
Cada fase segue Red → Green: primeiro o teste (ou a verificação) que falha, depois a mudança.

| Fase | Escopo | Red (como falha primeiro) | Green | Testes ao final (backend / front) |
|---|---|---|---|---|
| **A** | `User.Active()` → `Activate()` e remoção da `PasswordSaltKey` | `UserTests` não compila; `docker compose config` sem a variável falha (exit 1, **confirmado hoje**) | renomear o método; remover a chave de 4 arquivos | 430 / 238 |
| **B** | 5 renomeações de repositório (B1 a B5, um símbolo por vez) | testes renomeados primeiro: a solução deixa de compilar | contratos, implementações e handlers | 430 / 238 |
| **C** | 3 mensagens da API | 2 expectativas do teste de contrato + 2 asserções de `401` falham | 4 linhas em 2 handlers | 430 / 238 |
| **D** | 17 textos do front | testes ajustados para o texto acentuado e as 2 asserções acrescentadas (FR-021) falham | 10 arquivos de produção | 430 / 238 |
| **E** | documentação e varredura final | — | README dos testes, mapa de cobertura da spec 02, buscas com 0 resultado | 430 / 238 |

Linha de base medida em 2026-11-01 (antes de qualquer mudança): backend `dotnet build` com 0 avisos
e 0 erros, **429 testes aprovados** (0 falhas, 0 ignorados, integração com Docker ligado); front
`typecheck` ok e **238 testes aprovados** em 26 arquivos. Depois das fases: backend **430** (+1 do
FR-007), front **238**. Detalhes das decisões em [research.md](research.md).

## Contexto Técnico

**Linguagem/Versão**: C# / .NET 10 (SDK 10.0.402); TypeScript ~5.8.3 (`strict`), React 19. Nenhuma
versão muda.

**Dependências Principais**: nenhuma nova e nenhuma atualizada (FR-020, Princípio II). EF Core,
Npgsql, Flunt, SecureIdentity, Vite e as bibliotecas de teste permanecem como estão.

**Armazenamento**: N/A. Nenhuma migration, tabela, coluna ou índice muda; as consultas EF Core não
são tocadas (só os nomes dos métodos que as envolvem).

**Testes**: backend xUnit + NSubstitute + Testcontainers.PostgreSql; front Vitest + Testing Library
+ MSW. Nenhuma biblioteca nova.

**Plataforma Alvo**: a mesma de hoje (containers Linux via `docker compose` em desenvolvimento;
máquina de quem desenvolve).

**Tipo de Projeto**: web service (.NET) + frontend web já existentes. Nenhum projeto novo.

**Metas de Desempenho**: as metas do Princípio XIII (p95 ≤ 300 ms, p99 ≤ 1 s e 5xx < 0,1% nos
endpoints comuns; p95 ≤ 150 ms em aparelho) **valem e não são afetadas**: nenhuma consulta, índice,
cache ou caminho crítico muda, só nomes e textos. Não há cenário k6 novo (a spec não cria nem altera
caminho crítico).

**Restrições**: zero mudança de comportamento exceto os 3 textos de API (FR-012) e os 17 de UI
(FR-009); nenhum teste apagado, pulado ou enfraquecido; testes existentes só trocam nome de método
ou texto esperado, e 4 ganham uma asserção (FR-021: 2 de API e 2 de interface); 1 teste novo
(FR-007); nenhuma migration,
pacote ou edição da constituição (FR-020); sem commits do agente.

**Escala/Escopo**: 6 símbolos renomeados em **42 arquivos C#** (cerca de 150 ocorrências, mais 20
testes cujo nome cita o método); 4 linhas de mensagem em 2 handlers; 4 arquivos de configuração e
documentação (6 linhas); 10 arquivos de produção do front e 10 de teste. A escala de 150 mil
tenants (Princípio XIII) não se aplica: nada aqui guarda ou lê dado de tenant de forma nova.

## Verificação da Constituição

*PORTÃO: deve passar antes da pesquisa (Fase 0). Reavaliado depois do design (Fase 1).*

| Princípio | Aplica? | Situação (antes e depois do design) |
|---|---|---|
| I. Isolamento de Tenant | Parcial | Nenhuma tabela, `tenant_id`, RLS ou filtro muda. Os testes de isolamento (`TenantIsolationTests`, `DedicatedTenantIsolationTests` e os casos de isolamento dos `*RepositoryTests`) só trocam nome de método e continuam provando o isolamento ✅ |
| II. Clean Architecture | Sim | Sem mudança de camadas nem de dependências; nenhum pacote novo ✅ |
| III. Use Case vertical | **Sim** | Esta spec **aplica** a convenção de nomes: `Get{Entidade}By{Campo}Async`, `Get{Entidade}ListAsync`, `{Algo}ExistsAsync`, sufixo `Async`; `bool` responde o que o nome pergunta. Cada use case segue com o seu próprio `IRepository` (o `EmailExistsAsync` aparece em 3 interfaces distintas, nunca compartilhado) ✅ |
| IV. Domínio rico | **Sim** | `User` passa a usar o par `Activate()` / `Deactivate()` ✅ |
| V. Contrato de Use Case | Sim | Specification → Handler → Response intactos; status, `CancellationToken` e fluxo dos handlers não mudam. Só 3 textos de `Response` ✅ |
| VI. Api fina | Sim | Controllers intocados. Compatibilidade de contrato: texto de mensagem não é campo, tipo, status nem rota; os 2 endpoints afetados são usados só pelo console; XML comments, `types/api.ts` e a tabela do `src/Frontend/README.md` não citam os textos (verificado) ✅ |
| VII. Test-First | **Sim** | Red → Green em toda fase (ver "Fases"). Nenhum teste apagado/pulado/enfraquecido. Nas renomeações os testes só mudam de nome, o que é **provado por reversão** ([quickstart.md](quickstart.md), V2). Nome do teste novo: frase em português com underscore ✅. **Sem exceção**: todos os 20 textos corrigidos (3 de API e 17 de interface) passam a ser afirmados por teste; onde nenhum teste os afirmava hoje (o `401`, o texto de apoio do login e o cabeçalho "Ações"), uma asserção é acrescentada a um teste existente, antes da correção (FR-021; [research.md](research.md), itens 6 e 7) ✅ |
| VIII. Frontend só apresentação | Sim | Só texto exibido muda; nenhuma regra, rota, habilitação ou chamada muda ✅ |
| IX. Segurança | Parcial | Sem endpoint, token ou segredo novo; **remove** um segredo exigido e inútil, que confundia. Sem log novo. `.env` local do dono **não é tocado**. Baseline de pacotes inalterada (ver Acompanhamento de Complexidade) ✅ |
| X. LGPD | Não | Nenhum dado pessoal coletado, movido ou exibido de forma nova |
| XI. Auditoria | Não | Nenhum use case de escrita muda de comportamento (a ausência de auditoria nos de `AccountContext` continua sendo legado, spec 07) |
| XII. Permissões | Não | Nenhuma permissão nova; nenhuma autorização muda |
| XIII. Escalabilidade | Não | Nenhuma consulta, índice, cache ou job muda; sem cenário k6 (justificado em Metas de Desempenho) |
| Padrões (idioma, nomenclatura) | **Sim** | Código em inglês; nomes de teste em português com underscore; textos de UI acentuados ✅ |
| Governança / Integridade das entregas | **Sim** | Toda alteração em teste existente será listada no `tasks.md` (tabela "Testes existentes afetados") com o motivo ✅ |

**Quality gates** (Constituição, "Fluxo de Desenvolvimento"): `dotnet build Backend.slnx` (0 avisos,
0 erros), `dotnet test Backend.slnx` com Docker ligado, `npm run typecheck`, `npm test`,
`npm run build`, e as verificações de pacote vulnerável (resultado igual ao da linha de base).
**Resultado do portão**: aprovado, sem violação nova e sem violação a justificar. Reavaliado depois
do design: sem novas violações.

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261101-194211-legacy-cleanup/
├── plan.md                          # Este arquivo
├── research.md                      # Fase 0: decisões, linha de base, achados fora do escopo
├── data-model.md                    # Fase 1: contratos de repositório depois da renomeação, estado de User
├── quickstart.md                    # Fase 1: guia de validação por fase
├── contracts/
│   ├── repository-renames.md        # mapa símbolo a símbolo, arquivo a arquivo
│   ├── api-messages.md              # as 3 mensagens, handler, teste e invariantes do contrato
│   ├── ui-texts.md                  # os 17 textos, arquivo:linha de produção e de teste
│   └── configuration.md             # remoção da PasswordSaltKey
├── checklists/requirements.md
└── tasks.md                         # Fase 2 (/speckit-tasks — NÃO criado aqui)
```

### Código-fonte (raiz do repositório)

```text
src/Backend/
├── AccessControlSystem.Domain/Contexts/UserContext/Entities/User.cs        # A: Active() -> Activate()
├── AccessControlSystem.Application/Context/AccountContext/UseCases/
│   ├── {Activate,Deactivate,GetMe,Update,UpdateMe}/Contracts/IRepository.cs   # B1 (+ B5 em Update e UpdateMe)
│   ├── Authenticate/Contracts/IRepository.cs                                # B2
│   ├── Create/Contracts/IRepository.cs                                      # B4
│   ├── Get/Contracts/IRepository.cs                                         # B3
│   ├── */Handler.cs                                                         # B: chamadas; C: GetMe e UpdateMe (mensagens)
├── AccessControlSystem.Infrastructure/Context/AccountContext/UseCases/*/Repository.cs   # B: os 8
├── AccessControlSystem.Api/appsettings.json                                 # A: remove Secrets:PasswordSaltKey
└── AccessControlSystem.Tests/
    ├── Domain/Entities/UserTests.cs                                         # NOVO (A, FR-007)
    ├── Application/AccountContext/*/…HandlerTests.cs                        # B: 8 arquivos; C: GetMe e UpdateMe (asserção)
    ├── Infrastructure/AccountContext/*/…RepositoryTests.cs                  # B: 8 arquivos
    ├── Infrastructure/Tenancy/{TenantIsolationTests,DedicatedTenantIsolationTests}.cs   # B
    ├── Api/ApiContractCompatTests.cs                                        # C: 2 expectativas + 1 comentário
    └── README.md                                                            # E: acrescenta UserTests na árvore

docker-compose.yml, .env.example, README.md                                  # A: PasswordSaltKey

src/Frontend/src/                                                            # D: 10 de produção + 10 de teste
├── lib/api-client.ts · components/layout/{AppShell,Sidebar,Topbar}.tsx
└── pages/{LoginPage,HomePage,NotFoundPage,OperatorsListPage,EditOperatorPage,MyAccountPage}.tsx

specs/20261101-164600-frontend-testing/contracts/cobertura-cenarios.md       # E: 3 trechos (mapa de rastreabilidade)
```

Nenhum arquivo é criado além de `UserTests.cs`. Nenhuma migration, pacote ou workflow muda.

**Decisão de Estrutura**: web app existente (backend + frontend), sem projeto novo. O trabalho é
transversal, então a organização é **por fase**, não por camada: cada fase toca vários projetos, mas
compila e passa sozinha.
**Referência existente mais próxima**: `Application/Context/AccountContext/UseCases/Update/`
(repositório com leitura por chave, pergunta de existência e escrita) e `Domain/…/Operator.cs`
(par `Activate()` / `Deactivate()` que `User` passa a espelhar). Para o teste novo, o estilo de
`Tests/Domain/Entities/OperatorTests.cs`.

## Segurança e dados pessoais

- **Permissões novas / matriz endpoint × permissão**: nenhuma. Nenhum endpoint é criado ou alterado;
  o catálogo de permissões (Princípio XII) não muda. Os testes de autorização existentes seguem como
  estão (o contrato de `401` sem token, `401` de outro tenant etc. não é tocado).
- **Casos de abuso considerados**:
  - *Inverter a condição de e-mail em uso na renomeação* (abre bug de unicidade): a condição dos
    handlers de `Update` e `UpdateMe` **não pode ganhar nem perder `!`**; conferida por reversão no
    diff dos testes e por revisão das duas linhas ([contracts/repository-renames.md](contracts/repository-renames.md)).
  - *Busca e troca cega confundir `AnyAsync` do repositório com o do EF Core* (mudaria consulta):
    o `AnyAsync` do EF fica; a lista do que **não** muda está no mesmo contrato.
  - *Remover a configuração e quebrar a inicialização da Api*: nenhuma classe a lê (`SecretsOptions`
    só tem `JwtPrivateKey`) e nenhum teste a referencia; validado por `docker compose config` e
    pelo `up --build` sem a variável ([contracts/configuration.md](contracts/configuration.md)).
  - *Um `.env` antigo com `PASSWORD_SALT_KEY` quebrar algo*: o compose passa a não referenciá-la, então
    a variável sobrando é ignorada (testado com e sem).
  - *Mensagem de `401` mudar de forma que o console deixe de derrubar a sessão*: o console decide pelo
    **status** `401`, não pelo texto; o texto não muda de significado.
- **Dados pessoais tocados**: nenhum novo. Os e-mails dos exemplos e dos testes são sintéticos.
- **Eventos de auditoria gerados**: nenhum (nenhum use case de escrita ganha ou perde comportamento).

## Consultas novas e índices (Princípio XIII)

N/A: nenhuma consulta nova e nenhum índice criado ou alterado. As consultas EF Core de
`Update`/`UpdateMe`/`Create` (`AnyAsync` por e-mail, com `tenant_id` aplicado pelo filtro global e
pelo RLS) continuam idênticas.

## Acompanhamento de Complexidade

**Sem violação nova da constituição**; nada a justificar. Duas situações **herdadas** (não
introduzidas aqui, não mudam e não são corrigidas aqui):

| Situação herdada | Estado na linha de base de 2026-11-01 | Quem resolve |
|---|---|---|
| `npm audit` em `src/Frontend` | 2 vulnerabilidades *high* (`vite`, com `launch-editor`); esta spec não altera pacote, o resultado é idêntico | Spec 04 (security-hardening) |
| `dotnet list package --vulnerable` | só o `SSH.NET` transitivo do projeto de testes (*high*), já suprimido por `NU1903` com justificativa em `AccessControlSystem.Tests.csproj` | Spec 04 |

## Pendências fora do código desta spec

- **Dono do repositório (após o merge)**: emendar a constituição (PATCH, via `/speckit-constitution`;
  o agente não a edita) retirando de "Legado conhecido" os itens que esta spec resolve: nomes de
  repositório do `AccountContext`, `User.Active()`, `Secrets:PasswordSaltKey` e, no front, os textos
  sem acento. Os textos de API (`Requsição`, `Sessão invalida`, `Usuario`) não estavam na lista.
- **Premissas do pedido que o código já superou** (para o revisor não procurar o que não existe):
  `Application/Configuration.cs` e `BuilderExtensions.AddConfiguration` não existem mais (a spec 01
  migrou para `IOptions`; a chave só estava no `appsettings.json`, no compose, no `.env.example` e
  no README); e a árvore do `AccessControlSystem.Tests/README.md` já cobre todos os use cases (só
  falta citar `UserTests`).
- **Achados fora do escopo, registrados e não corrigidos**: ver [research.md](research.md), "Achados".

## Integridade: testes afetados (prévia para o `tasks.md`)

O `tasks.md` terá a tabela **"Testes existentes afetados"**, um teste por linha, com o motivo, e a
lista **"Testes novos"** à parte. A origem de cada lista:

| Categoria | Motivo na tabela | Quantidade prevista | Como a lista exata é obtida |
|---|---|---|---|
| Backend: nome de método trocado | renomeação de método | 20 métodos de teste renomeados (nome cita o método) + chamadas e `Substitute` em 18 arquivos | os 18 arquivos estão em [contracts/repository-renames.md](contracts/repository-renames.md) |
| Backend: texto esperado mudou | texto esperado mudou por esta spec | 2 expectativas em `ApiContractCompatTests` (+ o comentário da linha ~158) | [contracts/api-messages.md](contracts/api-messages.md) |
| Backend: asserção acrescentada | asserção acrescentada pelo FR-021 | 2 testes: `GetMeHandlerTests.Operador_nao_encontrado_devolve_401` e `UpdateMeHandlerTests.Sessao_sem_operador_correspondente_devolve_401` | idem |
| Front: asserção acrescentada | asserção acrescentada pelo FR-021 | 2 testes: `LoginPage.test.tsx` "mostra o formulário em branco com os campos de e-mail e senha" (texto de apoio) e `OperatorsListPage.test.tsx` "carrega a primeira página com o filtro Ativos" (cabeçalho "Ações") | [contracts/ui-texts.md](contracts/ui-texts.md) |
| Front: texto esperado mudou | texto esperado mudou por esta spec | 10 arquivos de teste; cada `it` alterado é listado a partir da **saída do Red** (os testes que falham são exatamente os que precisam mudar) | [contracts/ui-texts.md](contracts/ui-texts.md) |
| Front: título de `it` | texto esperado mudou por esta spec | 2 títulos em `Sidebar.test.tsx` (citam "Inicio") | idem |
| **Testes novos** | FR-007 | **1**: `UserTests.Activate_reativa_o_usuario_desativado` | — |
| **Testes novos** | FR-021 | **0**: os 4 casos (2 de `401`, login e lista) já têm teste; ganham asserção (linhas acima) | — |

## Ordem sugerida de implementação (insumo para o `/speckit-tasks`)

1. **Fase A**: `UserTests.cs` (Red por não compilar) → `User.Activate()`; Red do compose
   (`config` sem a variável) → remover a chave de `appsettings.json`, `docker-compose.yml`,
   `.env.example` e das 2 linhas do `README.md`. Gate: build + testes (430 / 238).
2. **Fase B**, um símbolo por vez, cada passo com a solução compilando e testes verdes:
   B1 `GetUserById` → `GetOperatorByIdAsync`; B2 `GetUserByEmailAsync` → `GetOperatorByEmailAsync`;
   B3 `GetOperatorList` → `GetOperatorListAsync`; B4 `AnyAsync` (Create) → `EmailExistsAsync`;
   B5 `CheckIfEmailIsNotUsed` → `EmailExistsAsync(email, exceptId)`. Em cada passo: testes primeiro
   (Red = não compila), depois contrato, implementação e handler.
3. **Fase C** (depois da B, porque `GetMe/Handler.cs` e `UpdateMe/Handler.cs` são os mesmos arquivos):
   testes com o texto novo (Red) → 4 linhas nos 2 handlers.
4. **Fase D**: 10 testes do front para o texto acentuado **e** as 2 asserções do FR-021 (login e
   lista), `npm test` (Red, anotar a lista de falhas) → 10 arquivos de produção (Green) →
   `typecheck`, `test`, `build`.
5. **Fase E**: `README.md` dos testes, `cobertura-cenarios.md` da spec 02, buscas com 0 resultado,
   gates completos e o `docker compose up --build` sem `PASSWORD_SALT_KEY` com login.

As fases A e D são independentes das demais; B e C devem ser nesta ordem. Toda fase termina com
build e testes verdes, e o agente **não** faz commit: o revisor commita cada fase separadamente.
