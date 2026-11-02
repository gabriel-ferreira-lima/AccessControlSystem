# Plano de Implementação: Segurança do perímetro (console web e entrada de dados da Api)

**Branch**: `20261102-125831-security-perimeter` | **Data**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `/specs/20261102-125831-security-perimeter/spec.md`

## Resumo

Fecha o perímetro (Princípio IX) em **4 fases pequenas** (A, B, C e D), cada uma com build e testes
verdes ao final, para revisão e commit separados (o agente não faz commits). Cada fase segue
Red → Green: primeiro o teste que falha, depois a mudança. A Fase B é a exceção honesta: ela
**prova** um comportamento que já existe, então o "Red" dela é um teste-canário (ver a tabela).

Abordagem técnica (decisões e alternativas em [research.md](research.md)):

- **Tamanho máximo na entrada**: um auxiliar de validação no `SharedContext` da Application
  (`IsMaxLength`, conta caracteres Unicode, limite inclusivo) e constantes `MaxLength` nos value
  objects do Domain (`Email` 255, `Password` 128, e as do `UserContext`). Cada `Specification.Ensure`
  checa **todos** os tamanhos primeiro e devolve cedo, antes de qualquer `Matches` (regex), o que
  fecha o custo de regex e de hash para entrada gigante. Um teste genérico por reflexão percorre
  todos os `Request` e falha se algum campo `string` não gerar notificação ao receber texto enorme.
- **Cabeçalhos do console**: `nginx.conf` inclui um trecho versionado (`security-headers.conf`) no
  `server` e nos `location` que têm `add_header` próprio, tudo com `always`. CSP sem
  `'unsafe-inline'`: o script do tema é liberado por `sha256`, e um teste do Vitest falha (com o
  hash novo na mensagem) quando o script muda. HSTS por `map` do `X-Forwarded-Proto`. O
  `/v1/` esconde (`proxy_hide_header`) os cabeçalhos da Api que o nginx repõe, para não duplicar.
- **Verificação real dos cabeçalhos**: `scripts/verify-security-headers.sh` constrói a imagem do
  console, sobe-a ao lado de um upstream simulado (alias `backend`) e confere os cabeçalhos em
  página, arquivo estático, 404 e rota encaminhada, com e sem `X-Forwarded-Proto: https`. Roda no
  `frontend-ci.yml` e localmente.
- **Prova do usuário de banco da aplicação**: testes de integração, com `acs_app`, nas três bases
  (DDL negado por instrução, atributos/propriedade/associação, catálogo só leitura) e RLS na base
  dedicada (a compartilhada já é coberta por `RowLevelSecurityTests`). Um auxiliar
  `AppRoleProbe` concentra as consultas de privilégio e é exercitado também contra **papéis-canário**
  com privilégio a mais (FR-023: a prova falha quando a permissão sobra).
- **Pacotes**: NuGet Audit nativo em `Directory.Build.props` (`NuGetAuditMode=all`,
  `NuGetAuditLevel=low`, NU1901–NU1904 como erro, NU1900 como erro no CI); a exceção do `SSH.NET`
  vira dois `NuGetAuditSuppress` por GHSA só no projeto de testes. No front, `npm audit` (qualquer
  severidade) no `frontend-ci.yml`, e `vite` sobe dentro do `^6` (sem troca de major).

| Fase | Escopo | Red (como falha primeiro) | Green |
|---|---|---|---|
| **A** | Tamanhos máximos (FR-001 a FR-008) | os testes do auxiliar e das 5 `Specification` não compilam; o teste genérico de reflexão lista todos os campos sem limite; o handler do login chama o hash/repositório com senha de 1 MB; as 4 chamadas da Api com campo gigante devolvem `401`/`201`/`200` em vez de `400` | `IsMaxLength`, constantes `MaxLength`, as 5 `Specification` com tamanho antes de formato |
| **B** | Prova do usuário de banco (FR-019 a FR-023) | os testes de sensibilidade (`AppRoleProbe` contra papéis-canário com `CREATE` no esquema, `DELETE`, `BYPASSRLS`, associação ao migrator) **não** acusam violação enquanto o auxiliar não existe; depois de existir, o canário prova que a prova falha quando a permissão sobra | `AppRoleProbe`, `AppRolePrivilegeTests` (3 bases), `DedicatedRowLevelSecurityTests`, `CatalogReadOnlyTests` |
| **C** | Cabeçalhos do console + HSTS + teste (FR-009 a FR-018) | o teste do Vitest não acha `security-headers.conf`; `verify-security-headers.sh` falha (sem cabeçalhos) | `security-headers.conf`, `nginx.conf` com `map`/`include`/`proxy_hide_header`, `Dockerfile` copia o trecho, passo no `frontend-ci.yml`, README |
| **D** | Pacotes e CI (FR-024 a FR-030) | `npm audit` sai com código 1 (2 high); com `Directory.Build.props` e sem o suppress o `dotnet build` falha com `NU1903` no `SSH.NET` | `npm audit fix` (`vite` 6.4.x), `package.json` com piso novo, suppress por GHSA no projeto de testes, passos de auditoria nos dois workflows, PRs-rascunho de prova |

Linha de base: a spec 04 fechou a suíte com o total registrado no `tasks.md` dela; **a primeira
tarefa do `tasks.md` desta spec mede e anota** os números de backend e de front antes de qualquer
mudança (SC-011: nenhum teste anterior é apagado, pulado ou enfraquecido).

## Contexto Técnico

**Linguagem/Versão**: C# / .NET 10; TypeScript ~5.8.3 (`strict`), React 19; nginx 1.27 (imagem do
console). Nenhuma versão muda, exceto o `vite` (hoje ≤ 6.4.3 → 6.4.4 ou superior, mesma major) e o
`source-map-js` transitivo.

**Dependências Principais**: Flunt 2.0.5 (já em uso), SecureIdentity (já em uso), NuGet Audit
(recurso do SDK), `npm audit` (recurso do npm). **Nenhum pacote NuGet ou npm novo** (Princípio II):
a conferência dos cabeçalhos usa `curl` e `docker`; o hash do script do tema usa o `crypto` do
Node dentro do Vitest; o allowlist do `audit-ci` fica só como opção futura, citada no research.

**Armazenamento**: PostgreSQL, **sem nenhuma mudança de esquema nem migration**. Os testes novos
só leem o catálogo do próprio banco (`pg_roles`, `pg_class`, `pg_auth_members`) e tentam instruções
proibidas com o usuário da aplicação.

**Testes**: backend xUnit + NSubstitute + Testcontainers.PostgreSql; front Vitest + Testing
Library + MSW. Verificação dos cabeçalhos: script de shell (Docker + curl) no CI. Nenhuma
biblioteca nova.

**Plataforma de Destino**: Api em container Linux atrás do nginx do console; ambiente local por
`docker compose`. Homologação e produção não existem (spec 05).

**Tipo de Projeto**: web-service (Api REST) + console web (SPA) + configuração de CI.

**Metas de Desempenho** (Princípio XIII, as do projeto): endpoints comuns p95 ≤ 300 ms e
p99 ≤ 1 s; polling de aparelho p95 ≤ 150 ms; taxa de erro 5xx < 0,1%. Esta spec **não acrescenta
nenhuma ida ao banco** nem altera o pipeline de autenticação; a checagem de tamanho é O(n) em
memória, no máximo O(2×limite) para texto acima do limite (ver research R-02), e **reduz** o custo
do pior caso (senha enorme não chega mais ao hash). Os cabeçalhos do nginx não têm custo mensurável.
Esta spec não cria nem altera caminho crítico além de **recusar mais cedo** no login; por isso não
entrega cenário k6 novo (justificativa em Verificação da Constituição, Princípio XIII).

**Restrições**: nenhum teste, threshold ou aviso pode ser apagado, pulado ou suprimido para a
entrega passar (Integridade das entregas). Sem `'unsafe-inline'` em `script-src`. O código novo
segue o estilo K&R do arquivo vizinho, comentários em português, identificadores em inglês.

**Escala/Escopo**: até 150 mil tenants (Princípio XIII); o que muda aqui é independente de
tenant. Alcance: 5 `Specification` (4 de `AccountContext` com texto + 1 de `UserContext/Create`),
1 `nginx.conf` + 1 trecho, 1 script de verificação, 2 workflows, 1 `Directory.Build.props`,
~4 classes novas de teste de banco e 1 de reflexão.

## Verificação da Constituição

*GATE: passou antes da pesquisa; reavaliada depois do design (resultado abaixo).*

| Princípio | Avaliação |
|---|---|
| **I. Isolamento e catálogo** | Passa. Não há tabela, coluna nem migration. Os testes novos **reforçam** o princípio: RLS na base dedicada (a compartilhada já tem), usuário da aplicação sem BYPASSRLS/propriedade nas três bases, catálogo só leitura. O catálogo continua sem senha nem hash. |
| **II. Clean Architecture** | Passa. Constantes no Domain (`const`, sem pacote); o auxiliar de validação fica em `Application/SharedContext` (usado por 2 contextos: `AccountContext` e `UserContext`, regra do Princípio III); nada novo aponta para fora. **Sem pacote novo**: a justificativa exigida pelo Princípio II é "nenhum". |
| **III. Vertical slice** | Passa. Nenhum use case novo; as 5 `Specification` existentes mudam no lugar. `SharedContext` só recebe o que já é comum. |
| **IV. Domínio rico** | Passa. As constantes `MaxLength` ficam ao lado do `Pattern` nos value objects, no mesmo estilo (`public const`). Os value objects **não** mudam de comportamento (a validação nova é de entrada, na `Specification`). |
| **V. Contrato de use case** | Passa. A recusa por tamanho sai pela `Specification.Ensure` → `400` "Requisição inválida" + `notifications`, sem tocar o repositório; o formato do envelope não muda. |
| **VI. Api fina** | Passa. Nenhum controller muda de lógica. Os `<response code="400">` já existem nos comentários XML; ajusta-se o texto onde disser só "fora do padrão" para citar o tamanho. Sem rota nova, sem mudança de status existente, **sem mudança incompatível** (login com senha > 128 passa a `400`: nenhum cliente fora o console usa o login, ver research R-03). |
| **VII. Test-First** | Passa, com 2 ressalvas justificadas em Complexity Tracking (Fase B é prova de comportamento existente; `UserContext/Create` não tem handler). Testes de Specification/Handler/Api primeiro na Fase A. Nomes em português com underscore. Nenhuma biblioteca de teste nova. |
| **VIII. Front só apresentação** | Passa. O front **não** ganha validação de tamanho (a Api é a fonte). O único teste novo do front lê `index.html` e `security-headers.conf`; não toca `src/api/`. |
| **IX. Segurança** | **É o objeto da spec.** Fecha: cabeçalhos (CSP, `X-Content-Type-Options`, `X-Frame-Options`/`frame-ancestors`, `Referrer-Policy`) e HSTS no nginx; "Entrada: toda string de `Request` com tamanho máximo, inclusive senha e PIN"; "Banco: o usuário da aplicação sem DDL"; "Dependências: o CI falha com pacote vulnerável". Ver a ressalva sobre a ferramenta em Complexity Tracking. |
| **X. LGPD** | Passa. Nenhum dado pessoal coletado, guardado ou logado; os limites do cadastro de pessoas só restringem entrada. |
| **XI. Auditoria** | Passa. Nenhum use case de escrita novo nem alterado em comportamento. Recusas `400` por tamanho não geram evento (são validação de entrada; auditoria de login é da spec 07). |
| **XII. Permissões** | Passa. Nenhuma permissão nova; nenhum endpoint novo. |
| **XIII. Escalabilidade** | Passa. Zero ida ao banco nova, zero estado em memória novo, nenhuma consulta nova (lista de consultas novas: **nenhuma**). Sem caminho crítico novo; o login passa a recusar entrada enorme **antes** do trabalho caro. Não há cenário k6 novo porque nenhum caminho crítico é criado ou piorado; o cenário de login da spec 04 segue valendo. |
| **Idioma/estilo** | Passa. Artefatos em português; código e banco em inglês; mensagens ao usuário em português com acento. |

**Legado conhecido que esta spec resolve** (a retirada do texto da constituição é por emenda do
dono do projeto, fora desta spec, FR-032): `src/Frontend/nginx.conf` sem HSTS nem cabeçalhos de
segurança; CI do backend e do frontend sem verificação de pacotes vulneráveis (e as 2
vulnerabilidades `high` do front).

**Reavaliação depois do design (Fase 1)**: sem novas violações. As duas ressalvas abaixo seguem
justificadas e uma proposta de emenda PATCH é registrada em research R-12.

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261102-125831-security-perimeter/
├── plan.md              # Este arquivo (saída do /speckit-plan)
├── research.md          # Fase 0: decisões e alternativas (R-01 a R-13)
├── data-model.md        # Fase 1: limites, conjunto de cabeçalhos, exceções, matriz de privilégios
├── quickstart.md        # Fase 1: validação ponta a ponta
├── contracts/
│   ├── input-limits.md          # limite por campo, por use case, e a mensagem
│   ├── security-headers.md      # cabeçalhos do console, por tipo de resposta
│   ├── app-role-guarantees.md   # garantias do usuário de banco da aplicação, por base
│   └── dependency-audit.md      # como o CI audita pacotes e como registrar exceção
├── checklists/requirements.md
└── tasks.md             # Fase 2 (saída do /speckit-tasks, NÃO criado aqui)
```

### Código-fonte (raiz do repositório)

Caso de referência mais parecido já implementado: **`AccountContext/UseCases/Update`**
(`Specification` com campos opcionais) e, para os testes de banco, **`SchemaContractTests`** e
**`RowLevelSecurityTests`** (spec 01).

```text
src/Backend/
├── Directory.Build.props                                  # NOVO: NuGetAudit (modo, nível, erros)
├── AccessControlSystem.Domain/Contexts/
│   ├── AccountContext/ValueObjects/{Email,Password}.cs    # + public const int MaxLength
│   └── UserContext/ValueObjects/{Name,Telephone,Rg,Cpf,PasswordAccess}.cs   # + MaxLength
├── AccessControlSystem.Application/
│   ├── SharedContext/UseCases/Validation/ContractExtensions.cs   # NOVO: IsMaxLength (caracteres Unicode)
│   └── Context/
│       ├── AccountContext/UseCases/{Authenticate,Create,Update,UpdateMe}/Specification.cs   # tamanho antes de formato
│       └── UserContext/Create/Specification.cs            # Telephone, Password, Rg, Cpf (Name/Observation já tinham)
├── AccessControlSystem.Api/Controllers/AccountContextController.cs   # só comentários XML (400)
└── AccessControlSystem.Tests/
    ├── AccessControlSystem.Tests.csproj                   # sai NoWarn NU1903; entram 2 NuGetAuditSuppress
    ├── Application/
    │   ├── SharedContext/Validation/ContractExtensionsTests.cs
    │   ├── AccountContext/{Authenticate,Create,Update,UpdateMe}/…SpecificationTests.cs   # + casos de tamanho
    │   ├── AccountContext/Authenticate/AuthenticateHandlerTests.cs                       # + senha de 1 MB
    │   └── UserContext/Create/CreateSpecificationTests.cs                                # NOVO
    ├── Architecture/RequestTextLimitsTests.cs             # NOVO: reflexão sobre todos os Request (FR-007)
    ├── Api/InputLimitsApiTests.cs                         # NOVO: 4 endpoints com campo gigante → 400
    └── Infrastructure/
        ├── Fixtures/AppRoleProbe.cs                       # NOVO: consultas de privilégio + papéis-canário
        └── Tenancy/
            ├── AppRolePrivilegeTests.cs                   # NOVO: DDL/atributos/propriedade nas 3 bases
            ├── CatalogReadOnlyTests.cs                    # NOVO: INSERT/UPDATE/DELETE/TRUNCATE negados
            └── DedicatedRowLevelSecurityTests.cs          # NOVO: RLS na base dedicada

src/Frontend/
├── Dockerfile                          # + COPY do trecho de cabeçalhos
├── nginx.conf                          # + map do HSTS, include, proxy_hide_header
├── security-headers.conf               # NOVO: CSP, nosniff, XFO, Referrer-Policy, Permissions-Policy, HSTS
├── scripts/
│   ├── verify-security-headers.sh      # NOVO: verificação real (docker + curl), LF
│   └── headers-check/{stub.Dockerfile,stub.conf}   # NOVO: upstream simulado (alias backend)
├── src/lib/security-headers.test.ts    # NOVO: hash do script do tema × CSP; pega mudança do script
├── package.json / package-lock.json    # vite ^6.4.x (piso novo) e source-map-js transitivo
└── README.md                           # + cabeçalhos, como recalcular o hash, auditoria

.github/workflows/
├── backend-ci.yml                      # + restore/auditoria explícitos (CI=true → NU1900 erro)
└── frontend-ci.yml                     # + npm audit + verify-security-headers.sh
```

**Decisão de estrutura**: aplicação web com backend e frontend já existentes; tudo reaproveita a
estrutura do Princípio III. Os testes de banco novos ficam em `Tests/Infrastructure/Tenancy/`, ao
lado dos que já provam o isolamento.

## Segurança e dados pessoais

**Permissões novas do catálogo**: nenhuma. **Matriz de autorização (endpoint × permissão)**:
nenhum endpoint criado ou alterado em autorização; os 8 endpoints de `v1/account` seguem como no
`EndpointCatalog` (login anônimo; `get-me`/`update-me` autenticados; os demais só Administrador). A
mudança é de **validação de entrada** (400) e vale **antes** da autorização de dados, sem alterar
401/403.

**Casos de abuso considerados**:

| Abuso | Defesa nesta spec |
|---|---|
| Senha/e-mail de megabytes no login para esgotar CPU (hash, regex) | tamanho antes do formato e do hash; 400 sem tocar hash, throttle nem banco (Handler test) |
| Regex em entrada gigante (custo super-linear do padrão de e-mail) | tamanho checado antes do `Matches` |
| Campo novo esquecido sem limite | teste de reflexão sobre todos os `Request` |
| Console embutido em outro site (clickjacking) | `frame-ancestors 'none'` + `X-Frame-Options: DENY` |
| Script injetado/de terceiro | CSP sem `'unsafe-inline'`; só `'self'` e o hash do script do tema |
| Rebaixamento para HTTP | HSTS de 1 ano quando `X-Forwarded-Proto: https` |
| Cabeçalho esquecido em 404/5xx ou em `location` com `add_header` próprio | `always` + `include` no `server` e nos `location`; teste real em 4 tipos de resposta |
| Aplicação comprometida alterando o esquema ou furando o RLS | `acs_app` sem DDL, sem BYPASSRLS, sem propriedade, sem associação ao migrator; prova em 3 bases e canários |
| Permissão a mais concedida por engano ao `acs_app` | `AppRoleProbe` + canários: a prova falha |
| Dependência com vulnerabilidade conhecida (front e back) | NuGet Audit como erro; `npm audit`; exceção só por GHSA, no projeto de testes |
| CI "verde" porque a base de avisos estava fora do ar | NU1900 como erro no CI; `npm audit` falha sem rede |

**Dados pessoais tocados**: nenhum novo. Os campos do cadastro de pessoas (nome, telefone, RG, CPF,
PIN, observação) só ganham **limite de tamanho** na entrada; nada é coletado, guardado, logado ou
exposto a mais. Mensagens de erro não ecoam o valor recebido (só o nome do campo e o limite).

**Eventos de auditoria gerados**: nenhum (nenhum use case de escrita novo ou alterado em efeito).

## Rastreamento de Complexidade

> Violações da constituição que precisam de justificativa.

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| **IX / Fluxo**: a constituição cita `dotnet list package --vulnerable` como verificação do CI; este plano usa o **NuGet Audit nativo** (`NuGetAuditMode=all`, avisos de segurança como erro) | O NuGet Audit falha no *restore* (portanto em `dotnet build`, em `dotnet test` e nos `docker build`), cobre todos os projetos e transitivos sem interpretar texto, e a exceção é por GHSA com comentário ao lado. O `dotnet list package --vulnerable` só imprime texto e sai com 0 mesmo com vulnerabilidade, exigindo parsing frágil da saída | Fazer parsing da saída do `dotnet list` é frágil (idioma da saída: o próprio ambiente imprime em português) e cobre menos. **O requisito (CI falha com pacote vulnerável) é atendido por mecanismo equivalente e mais forte**; proposta de emenda PATCH em research R-12 |
| **VII**: os testes da Fase B não nascem falhando | Não há comportamento novo: o `acs_app` já está correto. Os testes **provam** e protegem contra regressão (FR-023) | Mudar as permissões só para o teste falhar seria artificial; em vez disso, os **papéis-canário** (privilégio a mais de propósito, em banco isolado) provam que cada sonda falha quando a permissão sobra, e isso é o "Red" |
| **VII**: `UserContext/Create` ganha só `SpecificationTests`, sem `HandlerTests` nem `RepositoryTests` | O `Handler` dele é uma classe vazia (legado; a spec 11 reescreve o use case) e o use case não está exposto na Api: não há handler nem repositório para testar | Criar handler/repositório aqui seria escopo da spec 11 e contrariaria "specs curtas" |
