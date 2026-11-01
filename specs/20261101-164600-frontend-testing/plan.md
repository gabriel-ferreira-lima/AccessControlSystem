# Plano de Implementação: Base de testes do frontend

**Branch**: `20261101-164600-frontend-testing` | **Data**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação em `/specs/20261101-164600-frontend-testing/spec.md`

## Resumo

Criar a base de testes automatizados do console administrativo (`src/Frontend`) e cobrir com **testes
de caracterização** o comportamento que as telas têm hoje, sem alterar nenhuma linha do código de
produção (FR-015, SC-007). A abordagem, validada num protótipo descartável (ver
[research.md](research.md)):

- **Vitest + Testing Library + user-event + jest-dom + jsdom + MSW**, configurados no bloco `test`
  do `vite.config.ts`, com um `setup.ts` que registra o servidor MSW, reprova qualquer chamada de
  API não prevista e cobre as lacunas do jsdom (`<dialog>`, `matchMedia`, `DOMException`).
- **Handlers do MSW** com o envelope real da API, uma fábrica por endpoint (8), usando os tipos de
  `src/types/api.ts`; os módulos de `src/api/` **não** são mockados.
- **Navegação e jornadas** testadas com o `App` real (sem tocar em `App.tsx`); telas isoladas com
  `MemoryRouter` e providers reais.
- **CI** `frontend-ci.yml`: `npm ci` → `typecheck` → `test` → `build`, só quando o front (ou o
  próprio workflow) muda. `npm audit` fica de fora por decisão da clarificação (spec 04).
- Comportamentos estranhos encontrados ficam registrados em [research.md](research.md) (seção
  "Achados"), sem correção.

## Contexto Técnico

**Linguagem/Versão**: TypeScript ~5.8.3 (`strict`), React 19, React Router 7, Vite 6.4.3 (versões que
o projeto já tem; nenhuma é atualizada). Node: mínimo documentado **20.19+**, CI em **Node 22**.

**Dependências Principais**: já existentes (`react`, `react-dom`, `react-router-dom`). Novas, somente
`devDependencies` (justificativa na tabela abaixo, Princípio II): `vitest`, `jsdom`, `msw`,
`@testing-library/react`, `@testing-library/dom`, `@testing-library/user-event`,
`@testing-library/jest-dom`.

**Armazenamento**: N/A (o `sessionStorage` e o `localStorage` são os do jsdom, esvaziados a cada
teste).

**Testes**: Vitest 4 (`vitest run`), Testing Library, MSW (`msw/node`). Ver
[contracts/test-helpers.md](contracts/test-helpers.md).

**Plataforma Alvo**: Node (ambiente jsdom) na máquina de quem desenvolve e no GitHub Actions
(`ubuntu-latest`). Nenhum código novo roda no navegador do operador.

**Tipo de Projeto**: aplicação web, **somente o frontend** (`src/Frontend`) e um workflow em
`.github/workflows/`. Backend intocado.

**Metas de Desempenho**: as metas do Princípio XIII (p95 ≤ 300 ms, p99 ≤ 1 s, 5xx < 0,1%) valem para a
Api e **não são afetadas** (nenhum código de runtime muda; não há cenário k6 novo). Meta desta spec:
suíte completa em **menos de 2 minutos** (SC-001); o protótipo executou 2 arquivos em ~3,7 s.

**Restrições**: nenhuma chamada de rede real; execução determinística (10 execuções idênticas,
SC-004); nenhuma linha de código de produção alterada (FR-015); nenhuma biblioteca de teste além das
listadas; nenhum teste apagado, pulado ou enfraquecido (Integridade das entregas).

**Escala/Escopo**: 8 endpoints consumidos; 7 páginas; 6 componentes de `ui/`, 2 guardas de rota,
`ThemeToggle`, `Sidebar`, `Topbar`; 2 contextos; 4 módulos de `lib/`; estimativa de ~25 arquivos de
teste e 49 cenários de aceitação rastreados. Escala de 150 mil tenants (Princípio XIII) não se aplica:
o front é um cliente e não guarda dado de tenant.

### Pacotes npm novos (Princípio II: justificar no plano)

| Pacote | Faixa | Por quê é necessário | Alternativa rejeitada |
|---|---|---|---|
| `vitest` | `^4.1.0` | Executor de testes; reaproveita a configuração do Vite. Exigido pela constituição | Jest (segundo transformador, configuração duplicada) |
| `jsdom` | `^27.4.0` | Ambiente de DOM fixado pela diretriz | `happy-dom` (fora da diretriz) |
| `msw` | `^2.14.0` | Simula a API na camada de rede com o envelope real; exigido pela constituição | Mock de `fetch`/módulos (proibido pelo Princípio VII) |
| `@testing-library/react` | `^16.3.0` | Renderiza e consulta como o usuário; exigido pela constituição | — |
| `@testing-library/dom` | `^10.4.0` | Peer obrigatório de `react` e `jest-dom`; listado para a versão ficar estável | — |
| `@testing-library/user-event` | `^14.6.0` | Interação como o usuário; exigido pela constituição | `fireEvent` (menos fiel) |
| `@testing-library/jest-dom` | `~6.9.1` | Matchers de DOM; `~` porque a 6.10 exige Node >= 22 | — |

Versões mais novas (`vitest 5`, `msw 3`, `jsdom 30`, `jest-dom 7`) foram descartadas por exigirem
Node >= 22 e, no caso do `msw 3`, TypeScript >= 5.9 ([research.md](research.md), item 1).

## Verificação da Constituição

*PORTÃO: deve passar antes da pesquisa (Fase 0). Reavaliado depois do design (Fase 1).*

| Princípio | Aplica? | Situação (antes e depois do design) |
|---|---|---|
| I. Isolamento de Tenant | Não | Nenhuma tabela, use case ou dado de tenant. Dados de teste são sintéticos |
| II. Clean Architecture | Parcial | Sem mudança de camadas. Pacotes novos justificados acima. ✅ |
| III. Use Case vertical | Não | Só backend. Estrutura do front preservada; testes ficam ao lado do código ✅ |
| IV. Domínio rico | Não | — |
| V. Contrato de Use Case | Não | O envelope `message/status/isSuccess/notifications/data` é o que as fábricas reproduzem ✅ |
| VI. Api fina | Não | Backend intocado |
| VII. Test-First (frontend) | **Sim** | Vitest + Testing Library + `user-event` ✅; consultas por papel/rótulo/texto ✅; MSW com envelope real, sem mock de `src/api/` ✅; testes unitários de `src/lib/` ✅; arquivos ao lado do testado, descrições em português ✅. Estes são testes de **caracterização** (passam desde o início); o TDD estrito vale para toda mudança de front **a partir** desta spec ✅ |
| VIII. Frontend só apresentação | **Sim** | Os testes **fixam** o que a tela faz hoje, inclusive não validar nada no cliente e exibir `message` + `notifications` como vieram ✅. `RequireRole` por nome de role é legado conhecido: fixado como está (spec 08 o muda) ✅ |
| IX. Segurança | Parcial | Sem endpoint, token ou segredo novos; fixtures sem segredo real. **Desvio**: `npm audit` não entra no CI desta spec — ver Complexity Tracking |
| X. LGPD | Parcial | Sem dado pessoal real: e-mails `@example.test` ✅ |
| XI. Auditoria | Não | Nenhum use case de escrita novo |
| XII. Permissões | Não | Nenhuma permissão nova; o teste de menu/rotas descreve o comportamento atual por role |
| XIII. Escalabilidade | Não | Sem caminho crítico novo; sem cenário k6 (a spec não altera login, autenticação, validação de aparelho, listas ou jobs **no servidor**) |

**Quality gates** (Constituição, "Fluxo de Desenvolvimento"): `npm run typecheck`, `npm test` e
`npm run build` — todos ficam no CI (passos 4 a 6). `dotnet build/test` não se aplicam (backend não
muda). **`npm audit` não roda** (desvio justificado abaixo).

**Resultado do portão**: aprovado, com 1 desvio justificado. Reavaliado após o design: sem novas
violações.

## Estrutura do Projeto

### Documentação (esta feature)

```text
specs/20261101-164600-frontend-testing/
├── plan.md                         # Este arquivo
├── research.md                     # Fase 0: decisões validadas + achados de caracterização
├── data-model.md                   # Fase 1: dados sintéticos e estados exercitados
├── quickstart.md                   # Fase 1: guia de validação
├── contracts/
│   ├── test-helpers.md             # API de src/test/ para quem escreve testes
│   ├── frontend-ci.md              # Contrato do workflow
│   └── cobertura-cenarios.md       # Rastreabilidade cenário -> teste
├── checklists/requirements.md
└── tasks.md                        # Fase 2 (/speckit-tasks — NÃO criado aqui)
```

### Código-fonte (raiz do repositório)

```text
.github/workflows/
└── frontend-ci.yml                 # NOVO

src/Frontend/
├── package.json                    # + scripts "test" e "test:watch"; + devDependencies
├── package-lock.json               # atualizado pelo npm
├── vite.config.ts                  # + bloco "test" (defineConfig de "vitest/config")
├── README.md                       # + seção "Testes"; Node 20.19+
└── src/
    ├── test/                       # NOVO: infraestrutura (fora do bundle de produção)
    │   ├── setup.ts
    │   ├── server.ts               # + server.test.ts
    │   ├── envelope.ts
    │   ├── handlers.ts
    │   ├── fixtures.ts
    │   └── render.tsx
    ├── App.test.tsx                # NOVO: navegação e jornadas com o App real
    ├── lib/                        # api-client, form-error, labels, session-storage  (+ *.test.ts)
    ├── context/                    # AuthContext, ThemeContext                        (+ *.test.tsx)
    ├── components/
    │   ├── RequireSession.test.tsx, RequireRole.test.tsx, ThemeToggle.test.tsx
    │   ├── layout/                 # Sidebar.test.tsx, Topbar.test.tsx
    │   └── ui/                     # Alert, Button, ConfirmDialog, PasswordField,
    │                               # SelectField, TextField  (+ *.test.tsx)
    └── pages/                      # as 7 páginas                                     (+ *Page.test.tsx)
```

Nenhum arquivo de produção (`*.ts`/`*.tsx` sem `.test`) é alterado.
Arquivos de configuração tocados: `package.json`, `package-lock.json`, `vite.config.ts`, `README.md`
do front, e `CLAUDE.md` (acrescentar `npm test` em "Comandos").

**Decisão de Estrutura**: web app, só frontend. Testes ao lado do arquivo testado (Princípio VII);
infraestrutura compartilhada em `src/test/`. `tsconfig.app.json` (`include: ["src"]`) já inclui os
testes no `typecheck` e no `build`; o `vite build` não os empacota (validado no protótipo).
**Referência existente mais próxima**: não há use case de backend envolvido. A referência é a própria
estrutura de `src/Frontend/src` (uma função por endpoint em `src/api/`, `toFormError` para todo erro).

## Segurança e dados pessoais

- **Permissões novas / matriz endpoint × permissão**: nenhuma. Nenhum endpoint criado ou alterado;
  o catálogo de permissões (Princípio XII) não muda.
- **Casos de abuso considerados**:
  - *Vazamento de segredo pelos testes*: fixtures usam tokens e senhas fictícios; nenhum `.env` real
    é lido (o `vite.config.ts` fixa `VITE_API_BASE_URL` no teste).
  - *Teste que chama a API real*: impossível por construção — chamada sem handler reprova o teste e
    não há rede no CI.
  - *Dependência de teste vulnerável*: no protótipo, `npm audit` antes/depois da instalação dá o
    mesmo resultado (2 *high* em `vite`, preexistentes); as ferramentas novas não acrescentam alerta.
    A conferência final é repetida na implementação.
  - *Workflow com permissões excessivas*: o `frontend-ci.yml` não usa segredos nem `permissions`
    amplas; só leitura do repositório.
- **Dados pessoais tocados**: nenhum real. E-mails sintéticos `@example.test` (Princípio X).
- **Eventos de auditoria gerados**: nenhum (não há use case de escrita novo).

## Consultas novas e índices (Princípio XIII)

N/A: nenhuma consulta nova. Nenhuma tabela nem índice envolvido.

## Acompanhamento de Complexidade

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| **Princípio IX / quality gates**: o CI do front **não** roda `npm audit`, embora a constituição exija falha do CI com pacote vulnerável conhecido | Decisão do dono do projeto na clarificação de 2026-11-01. Hoje `npm audit` em `src/Frontend` aponta 2 vulnerabilidades *high* (`vite <=6.4.3` e `source-map-js`); ligar a verificação agora deixaria o primeiro PR vermelho, e resolver exige atualizar o Vite (toolchain de produção, fora desta spec). A spec 04 (segurança) já prevê `npm audit --audit-level=high` no `frontend-ci.yml` junto com o backend e deve resolver as vulnerabilidades ao ligá-la | Ligar `npm audit` já nesta spec; ou suprimir o alerta com comentário (esconderia um risco real num cliente que roda no navegador do operador). Ambas contrariam o escopo escolhido |

**Aceito pelo dono do projeto em 2026-11-01.** Após o merge desta spec, uma emenda PATCH da constituição (via `/speckit-constitution`, feita pelo dono; o agente não edita a constituição) retira do "Legado conhecido" o item "`src/Frontend` sem testes e sem CI" e acrescenta "CI do backend e do frontend sem verificação de pacotes vulneráveis, corrigido pela spec 04".

## Pendências fora do código desta spec

- **Dono do repositório**: (a) decidido em 2026-11-01: `frontend-build-and-test` **não** será
  marcada como verificação obrigatória na `main` até a spec 05, por causa do filtro de `paths`
  ([research.md](research.md), item 10); até lá, o revisor não mescla pull request com o CI
  vermelho; (b) após o merge, emendar a constituição (PATCH, via `/speckit-constitution`) conforme
  o Acompanhamento de Complexidade acima. O agente não edita a constituição.
- **Spec 03 (limpeza)** e **spec 08 (roles)** atualizam os testes que fixam textos sem acento e as
  regras por nome de role; a mudança de teste deve ser explícita no `tasks.md` delas.

## Ordem sugerida de implementação (insumo para o `/speckit-tasks`)

1. Dependências, scripts e bloco `test` do `vite.config.ts`; `setup.ts`, `server.ts` (+ teste),
   `envelope.ts`, `handlers.ts`, `fixtures.ts`, `render.tsx`.
2. `src/lib/` (4 arquivos), `context/`, guardas, `ThemeToggle` e componentes de `ui/`.
3. Páginas: Login → Lista de operadores → Criar → Editar → Minha conta → Home/NotFound.
4. `App.test.tsx` (navegação e jornadas) e `Sidebar`/`Topbar`.
5. `frontend-ci.yml`, `README.md` do front e `CLAUDE.md`.
6. Registro dos achados em `research.md` e conferência de `contracts/cobertura-cenarios.md`.

Como são testes de caracterização, cada teste deve **passar** sobre o código atual. Se um teste
precisar de uma mudança no código de produção para passar, isso é um achado (FR-015), não uma
alteração.
