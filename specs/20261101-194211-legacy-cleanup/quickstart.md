# Guia de validação: Limpeza de legado

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Passo a passo para provar, fase a fase, que a entrega funciona. Os comandos são para o Git Bash (como
os do `README.md`), a partir da **raiz do repositório**, salvo indicação. Rodam com Docker ligado.
Nenhuma etapa faz commit.

## Pré-requisitos

- .NET SDK 10, Node 20.19+ (o dono usa 24), Docker Desktop.
- `cd src/Frontend && npm ci` já feito.
- Linha de base **antes de mexer em qualquer coisa** (V0).

## V0 — Linha de base (antes da fase A)

```bash
(cd src/Backend && dotnet build Backend.slnx && dotnet test Backend.slnx)
(cd src/Frontend && npm run typecheck && npm test)
```

Esperado (medido em 2026-11-01): build com **0 avisos e 0 erros**; backend **429 aprovados**; front
typecheck ok e **238 aprovados** (26 arquivos). Se os números forem outros, anote-os: são a
referência de SC-001 desta execução.

## V1 — Fase A: `User.Activate()` e `PasswordSaltKey`

**1a. Teste do FR-007 (Red por não compilar → Green)**

```bash
# 1) criar Tests/Domain/Entities/UserTests.cs com Activate_reativa_o_usuario_desativado
(cd src/Backend && dotnet build Backend.slnx)          # Red: erro CS1061 ('User' não contém 'Activate')
# 2) renomear User.Active() -> Activate()
(cd src/Backend && dotnet test Backend.slnx --filter "FullyQualifiedName~UserTests")   # Green: 1 aprovado
```

**1b. `PasswordSaltKey` (Red → Green), com um arquivo temporário feito de `.env.example`**

```bash
TMP=$(mktemp -d)
{ grep -vE '^(PASSWORD_SALT_KEY|JWT_PRIVATE_KEY)=' .env.example
  echo "JWT_PRIVATE_KEY=chave-de-teste-apenas-para-validar-o-compose-0123456789abcdef0123456789abcdef"; } > "$TMP/env.sem-salt"
{ cat "$TMP/env.sem-salt"; echo "PASSWORD_SALT_KEY=qualquer"; } > "$TMP/env.com-salt"

docker compose --env-file "$TMP/env.sem-salt" config --quiet; echo "sem a variável: exit=$?"
docker compose --env-file "$TMP/env.com-salt" config --quiet; echo "com a variável: exit=$?"
rm -rf "$TMP"
```

Esperado **antes** de remover a chave: `sem a variável: exit=1` (mensagem `required variable
PASSWORD_SALT_KEY is missing a value`) e `com a variável: exit=0`. Esperado **depois**: `exit=0` nos
dois (a variável sobrando é ignorada).

**1c. Busca (só arquivos versionados)**

```bash
git grep -nE "PasswordSalt|PASSWORD_SALT" -- . ':!specs' ':!.specify'     # depois da fase A: nenhuma linha
```

Gate da fase: build 0/0, `dotnet test` **430** aprovados, `npm test` **238**.

## V2 — Fase B: renomeações de repositório (B1 a B5)

Em cada passo: testes primeiro (a solução deixa de compilar = Red), depois contrato,
implementação e handler (Green); então:

```bash
(cd src/Backend && dotnet build Backend.slnx && dotnet test Backend.slnx)   # 0 avisos, 0 erros, 430 aprovados
```

**2a. Prova de que os testes só mudaram de nome (reversão).** Rodar **antes** da fase C (a C
acrescenta asserções em dois dos arquivos). `BASE` é o commit anterior à fase B, ou `main` se nada foi
commitado desde que a branch saiu dela.

```bash
BASE=$(git merge-base HEAD main)
for f in $(git diff --name-only --diff-filter=M "$BASE" -- 'src/Backend/AccessControlSystem.Tests/*Tests.cs'); do
  case "$f" in
    */Create/*)              extra='s/EmailExistsAsync/AnyAsync/g' ;;
    */Update/*|*/UpdateMe/*) extra='s/EmailExistsAsync/CheckIfEmailIsNotUsed/g' ;;
    *)                       extra='' ;;
  esac
  diff --strip-trailing-cr \
       <(sed -E "s/GetOperatorByIdAsync/GetUserById/g; s/GetOperatorByEmailAsync/GetUserByEmailAsync/g; s/GetOperatorListAsync/GetOperatorList/g${extra:+; $extra}" "$f") \
       <(git show "$BASE:$f") > /dev/null || echo "MUDOU ALÉM DO NOME: $f"
done
```

Esperado: **nenhuma** linha `MUDOU ALÉM DO NOME`. Qualquer saída aponta um teste com asserção, dado ou
cenário alterado: a fase não passa. O `--strip-trailing-cr` é necessário: os arquivos do projeto são
CRLF e o `sed` do Git Bash descarta o `CR`, o que faria o `diff` acusar o arquivo inteiro.

**2b. Condição dos handlers sem `!` novo**

```bash
git diff -U0 "$BASE" -- src/Backend/AccessControlSystem.Application | grep -E '^[-+].*(CheckIfEmailIsNotUsed|EmailExistsAsync|AnyAsync)'
```

Esperado: em `Update/Handler.cs` e `UpdateMe/Handler.cs`, um par `-`/`+` idêntico além do nome do
método (`if (await _repository.… (newEmail.Address, request.Id, cancellationToken)) {`), e em
`Create/Handler.cs` o `var exists = await _Repository.… (email, cancellationToken);`. **Nenhum `!`**
aparece nas linhas `+`.

**2c. Nada de nome antigo sobrando**

```bash
git grep -nE "GetUserById|GetUserByEmailAsync|CheckIfEmailIsNotUsed|GetOperatorList\b|\.Active\(\)|void Active\(" -- src
git grep -nE "AnyAsync" -- src/Backend | grep -vE "AnyAsync\([a-z]+ =>"
```

Esperado: **0 linhas** nos dois. O segundo comando descarta os `AnyAsync` do EF Core (todos levam
lambda, como `.AnyAsync(x => ...)`, e **permanecem**: são 12 hoje); o que sobra é o método do
repositório de `Create`. Hoje ele acusa 11 linhas (3 de produção, 3 em `CreateHandlerTests` e 5 em
`CreateRepositoryTests`), que devem chegar a zero ao fim do passo B4.

## V3 — Fase C: mensagens da API

```bash
# 1) ajustar as 2 expectativas do ApiContractCompatTests e acrescentar a asserção do 401 nos 2 testes de handler
(cd src/Backend && dotnet test Backend.slnx --filter "FullyQualifiedName~ApiContractCompatTests|FullyQualifiedName~GetMeHandlerTests|FullyQualifiedName~UpdateMeHandlerTests")
```

Esperado **antes** de mexer nos handlers (Red): **4 falhas**: as linhas da teoria
`Contrato_dos_8_endpoints_nao_mudou` de `update-me: 200 conta atualizada` e `update-me: 400 nenhum
campo informado`, mais `Operador_nao_encontrado_devolve_401` (GetMe) e
`Sessao_sem_operador_correspondente_devolve_401` (UpdateMe). Depois das 4 linhas nos 2 handlers
(Green): tudo passa, **430** no total.

Conferência ao vivo (opcional, com a stack no ar, V7): `update-me` sem campos devolve
`400` com `"message": "Requisição inválida"`.

```bash
git grep -nE "Requsi|Usuario atualizado|Sessão invalida" -- src      # 0 linhas
```

## V4 — Fase D: textos do front

```bash
cd src/Frontend
# 1) ajustar os 10 arquivos de teste (e os 2 títulos do Sidebar.test.tsx) para a grafia correta
#    e acrescentar as 2 asserções do FR-021 (login e lista) — ver contracts/ui-texts.md, seção 3
npm test            # Red: anote a lista de testes que falham (é a tabela "Testes existentes afetados")
# 2) corrigir os 10 arquivos de produção
npm run typecheck && npm test && npm run build      # Green: 238 aprovados
```

**Varredura (SC-004)**: nenhuma forma sem acento nos textos de interface.

```bash
git grep -nE "Nao foi poss|Sua sessao|Faca login|e limitado|>Inicio<|\"Inicio\"|informacoes desta|Pagina nao|nao existe ou foi|Operacao\"|Navegacao principal|o conteudo|Encerrar sessao|>Acoes<|por pagina|pagina \{|Proxima|deixara de|Salvar alteracoes" -- 'src/Frontend/src/**/*.ts' 'src/Frontend/src/**/*.tsx'
```

Esperado: **0 linhas** (testes incluídos). Não casa com `href="#conteudo"` nem com a mensagem técnica
de `main.tsx`, que ficam.

**Completude (FR-009)**: rodar também a varredura por radicais do item (b) da tarefa T090 do
[tasks.md](tasks.md), que acha um texto sem acento fora do inventário. Esperado: só sobra
`src/Frontend/src/main.tsx:9`.

**Conferência visual** (complementar: os 17 textos já são afirmados por teste, mas vale ver a tela;
`npm run dev` ou a stack do V7):

| Tela | O que conferir |
|---|---|
| Login | rodapé "O uso deste sistema **é** limitado a operadores autorizados." |
| Lista de operadores | cabeçalho de tabela para leitor de tela "**Ações**" (inspecionar o DOM ou usar leitor de tela); "N operador(es) · **página** X de Y"; "Itens por **página**"; botão "**Próxima**"; confirmação de desativar com "**deixará**" |
| Início, menu, topo | "Início", grupo "Operação", "Encerrar sessão", link "Ir para o conteúdo" (Tab na primeira vez) |
| Endereço inexistente | "Página não encontrada" e "O recurso solicitado não existe ou foi movido." |
| Editar operador e Minha conta | botão "Salvar alterações" |
| API fora do ar | "Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente." |

## V5 — Fase E: documentação e varredura final

- `AccessControlSystem.Tests/README.md`: a árvore cita `UserTests` em `Domain/Entities/`.
- `specs/20261101-164600-frontend-testing/contracts/cobertura-cenarios.md`: linhas 21, 70 e 124 em
  sincronia com os textos e títulos novos.
- Buscas das fases V1c, V2c, V3 e V4 repetidas: **0 linhas** em todas.
- `git status`: só os arquivos previstos no [plan.md](plan.md); **nenhum** commit feito pelo agente.

## V6 — Gates finais (Constituição, "Fluxo de Desenvolvimento")

```bash
(cd src/Backend && dotnet build Backend.slnx && dotnet test Backend.slnx)          # 0 avisos, 0 erros; 430 aprovados
(cd src/Backend && dotnet list Backend.slnx package --vulnerable --include-transitive)   # igual à linha de base
(cd src/Frontend && npm run typecheck && npm test && npm run build)                # 238 aprovados
(cd src/Frontend && npm audit --audit-level=high)                                  # igual à linha de base (2 high, spec 04)
```

Sem pacote novo ou atualizado: `git diff --stat main -- '*.csproj' src/Frontend/package*.json`
deve ficar vazio (compara com a `main`, porque há um commit por checkpoint).

## V7 — Ambiente local sobe sem `PASSWORD_SALT_KEY` e o login funciona (US4)

A stack local do dono já existe (contêineres `acs-*` com nome fixo e o volume
`access-control-system_pg-data`), então este passo **reaproveita o mesmo projeto e os mesmos
volumes**, só trocando o arquivo de variáveis por uma cópia do `.env` sem a linha da chave. Não use
`down -v`.

```bash
TMP=$(mktemp -d)                                              # fora do repositório
trap 'rm -rf "$TMP"' EXIT                                     # a cópia tem segredos reais: some mesmo se o up falhar
grep -v '^PASSWORD_SALT_KEY=' .env > "$TMP/env.sem-salt"     # cópia do .env real, sem a chave

docker compose --env-file "$TMP/env.sem-salt" up --build -d
docker compose --env-file "$TMP/env.sem-salt" ps               # migrator concluído; backend, backend-b, backend-dedicated e frontend no ar

PORT=$(grep '^BACKEND_PORT=' "$TMP/env.sem-salt" | cut -d= -f2-); PORT=${PORT:-5290}
PASS=$(grep '^DEV_ADMIN_PASSWORD=' "$TMP/env.sem-salt" | cut -d= -f2-)
curl -s -X POST "http://localhost:$PORT/v1/account/login" -H 'Content-Type: application/json' \
     -d "{\"email\":\"admin@admin.com.br\",\"password\":\"$PASS\"}"

docker compose --env-file "$TMP/env.sem-salt" down            # sem -v; o trap remove o temporário
```

Se o `up` falhar, rodar `docker compose down` (sem `-v`) com o `.env` normal.

Esperado: `up` termina sem o erro `required variable PASSWORD_SALT_KEY is missing a value`; o login
devolve `"status": 200`, `"isSuccess": true` e um token. Repetir com o `.env` normal (que ainda tem a
linha) também sobe: a variável sobrando é ignorada.

## V8 — Rastreio dos critérios de sucesso

| Critério | Verificação |
|---|---|
| SC-001 (testes passam; contagem igual + 1) | V0 e V6: 429 → **430** (backend), 238 → **238** (front) |
| SC-002 (nomes antigos: 0) | V2c |
| SC-003 (0 métodos fora da convenção; `bool` correto) | V2b, e o contrato [repository-renames.md](contracts/repository-renames.md) |
| SC-004 (0 textos sem acento) | V4 (varredura e conferência visual) |
| SC-005 (exatamente 2 expectativas do contrato alteradas) | V3 e `git diff` de `ApiContractCompatTests.cs` (2 linhas de expectativa + 1 de comentário) |
| SC-006 (sobe sem a chave; 0 ocorrências) | V1b, V1c e V7 |
| SC-007 (0 alterações de teste não declaradas) | V2a para renomeações; lista do Red (V4) e [contracts/api-messages.md](contracts/api-messages.md) para textos e asserções |
| SC-008 (quality gates) | V6 |
