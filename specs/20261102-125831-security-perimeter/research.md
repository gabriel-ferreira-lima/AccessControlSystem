# Pesquisa e Decisões: Segurança do perímetro

**Spec**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Data**: 2026-11-02

Não restou nenhum ponto em aberto: as decisões do `/speckit-clarify` (HSTS sem checar a
origem, SC-003 sem tempo, provas por base, exceções por aviso) já estão na spec. Abaixo, o que foi
verificado no código e a escolha de cada ponto técnico.

## Estado verificado (2026-11-02)

- `src/Frontend/nginx.conf`: sem cabeçalho algum; `location /assets/` tem `add_header Cache-Control`
  (por isso **não herda** nada do `server`); `/v1/` faz `proxy_pass http://backend:8080` com
  `X-Forwarded-Proto $scheme`.
- `src/Frontend/index.html` tem **um** script escrito na página (o do tema) e nenhum outro. O build
  (`dist/index.html`) mantém o script idêntico e acrescenta `<script type="module" src="/assets/…">`
  e `<link rel="stylesheet" href="/assets/…">`, ambos de mesma origem. O CSS gerado não tem `url(…)`
  nem `@import`; o código do front não usa `style=` fixo, `setAttribute("style")`, `cssText`,
  `dangerouslySetInnerHTML` nem `eval`. Estilo definido por `element.style.x = …` (o que o React
  faz com a prop `style`) é permitido pela CSP.
- `Request`s com texto: `Authenticate` (Email, Password), `Create` (Email, Password), `Update`
  (Email?, Password?), `UpdateMe` (Email?, Password?) e `UserContext/Create` (Name, Telephone,
  Password, Rg, Cpf, Observation). `Get`, `GetMe`, `Activate`, `Deactivate` não têm texto. Hoje só o
  `UserContext/Create` limita (Name 100, Observation 100); nenhum `Specification` do `AccountContext`
  limita e-mail ou senha, e o login nem valida a senha.
- `AccountContext/UseCases/Authenticate/Handler.cs` já faz validação → normalização → throttle →
  busca → hash → decisão; o `Specification` é o passo 01.
- Banco: `roles.sql` cria `acs_app` com `NOBYPASSRLS NOSUPERUSER NOCREATEDB NOCREATEROLE`;
  `tenant-db-grants.sql` dá `SELECT, INSERT, UPDATE` (sem `DELETE`); `catalog-db-grants.sql` dá só
  `SELECT`. `SchemaContractTests` já cobre, na base compartilhada: matriz de privilégios do
  `operators`, `CREATE TABLE` negado (42501), atributos de `acs_app` e propriedade do `operators`.
  `RowLevelSecurityTests` já cobre, na compartilhada: leitura sem filtro do EF, conexão sem tenant,
  `INSERT` de outro tenant, `UPDATE` do `tenant_id` e `set_config` local.
- `NU1903` (SSH.NET 2024.2.0, transitivo do Testcontainers, 2 avisos: GHSA-q939-rpr3-3284 e
  GHSA-mggc-4xg6-vcxf) é suprimido por `NoWarn` geral no `.csproj` de testes. Os demais projetos
  não têm pacote vulnerável.
- `npm audit`: 2 `high` (`vite <=6.4.3` direto, `source-map-js` 1.0.0–1.2.1 transitivo). O `vite`
  tem versões 6.4.4 e 6.4.5 publicadas, dentro do `^6.3.5` do `package.json`: não precisa trocar de
  major.
- `.gitattributes` só trata `*.sh` (LF) e o Git local tem `core.autocrlf=true`: no Windows os
  demais arquivos saem em CRLF no checkout (ver R-07).

## R-01 — Onde mora o limite

**Decisão**: constante `public const int MaxLength` no value object do Domain quando o campo tem
um (`Email` 255, `Password` 128, `Name` 100, `Telephone` 20, `Rg` 20, `Cpf` 14, `PasswordAccess` 6),
ao lado do `Pattern`; a `Specification` usa a constante. `Observation` (100) não tem value object e
fica como constante privada na `Specification` do `UserContext/Create`. A checagem em si é um método
de extensão `IsMaxLength` no `SharedContext` da Application.

**Por quê**: o Princípio IV manda expor o `Pattern` como `const` pública para a `Specification`
reutilizar; o mesmo vale para o tamanho. Um número só (`Email.MaxLength`) casa a validação com o
`varchar(255)` do `OperatorMap` e com o valor que o teste usa como "exatamente no limite". O
auxiliar fica no `SharedContext` porque é usado por dois contextos (Princípio III).

**Alternativas**: literal em cada `Specification` (espalha o 255 e o 128; descartada); ler o
`MaxLength` do mapeamento do EF (acopla Application à Infrastructure; proibido); validar no
value object/construtor (o `Email` já é construído só depois da `Specification`; a recusa tem que
ser anterior e ficar no contrato `400` + notificações).

## R-02 — Como contar

**Decisão**: o limite conta **caracteres Unicode** (pontos de código, via `EnumerateRunes`), é
inclusivo, e tem curto-circuito por tamanho da `string`: `Length <= max` → dentro; `Length > 2×max`
→ fora (cada ponto de código ocupa no máximo 2 unidades UTF-16); só no meio é que conta runes. Texto
`null` está dentro do limite (campos opcionais ausentes continuam ausentes).

**Por quê**: FR-005 pede caracteres, não bytes; o `varchar(n)` do PostgreSQL também conta pontos de
código, então o teste "exatamente 255" e a coluna concordam. O curto-circuito limita o trabalho a
O(max) mesmo para uma senha de 1 MB (o contrário seria varrer o megabyte para recusá-lo).

**Alternativas**: `string.Length` puro (conta unidades UTF-16: um emoji vale 2 e a recusa fica
estranha; mais simples, mas contraria o "contar caracteres" da spec); contar bytes UTF-8 (contraria
a spec; descartada).

## R-03 — Senha acima de 128 no login

**Decisão**: o login recusa senha com mais de 128 caracteres com `400` "Requisição inválida" +
notificação `Password`, **antes** do throttle, da busca e do hash (Handler passo 01). O `Specification`
do `Authenticate` passa a validar a senha só quanto ao tamanho (o formato da senha **não** é
validado no login: o login nunca revela regra de complexidade).

**Riscos e por que são aceitáveis**:

- Uma conta antiga com senha > 128 ficaria sem conseguir entrar. Antes desta spec, `Create`/`Update`
  aceitavam senha de qualquer tamanho, então existir uma dessas é possível em tese. Hoje só existe o
  ambiente de desenvolvimento (memória do projeto: homologação e produção não existem), as senhas do
  seed são curtas, e a spec 05 só cria ambientes depois desta. Se aparecer, a correção é o
  administrador trocar a senha (`update`), que já passa a ter o limite.
- Não é "mudança incompatível" do Princípio VI: nenhum cliente fora o console usa o login
  (aparelhos têm credencial própria, Princípio IX).

**Alternativa**: devolver o `401` genérico para senha longa, sem dizer que é tamanho. Rejeitada
porque a spec escolheu `400` com notificações (FR-002 e Premissas, "Senha no login"), e esconderia
do operador legítimo por que a senha foi recusada. Não vaza e-mail existente: o tamanho é checado
antes de saber se o e-mail existe.

## R-04 — Ordem e forma da validação

**Decisão**: em cada `Specification.Ensure`, primeiro todos os `IsMaxLength` (uma notificação por
campo excedido, chave = nome da propriedade, como as já existentes: `Email`, `Password`, …); se o
contrato já está inválido, **devolve** sem avaliar `Matches`, `IsValid` do CPF etc. Só então o
formato. Mensagem: `"{Campo} deve ter no máximo {n} caracteres"` (por exemplo, "E-mail deve ter no
máximo 255 caracteres", "Senha deve ter no máximo 128 caracteres"); a mensagem **não** ecoa o valor.
As mensagens existentes de `Name` e `Observation` do `UserContext/Create` ficam como estão.

**Por quê**: FR-003 e o caso de borda "limite e formato juntos". O padrão de e-mail
(`^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$`) tem grupos repetidos que podem custar muito em
entrada longa e malformada; avaliar o tamanho antes remove o problema, sem trocar o padrão.

**Espaços nas pontas**: o login normaliza o e-mail; o limite é aplicado ao texto **como recebido**
(antes do `Trim`), para espaços em excesso não driblarem o limite (caso de borda da spec).

## R-05 — Teste genérico por reflexão (FR-007)

**Decisão**: `Tests/Architecture/RequestTextLimitsTests` (xUnit `Theory` com `MemberData`):

1. Descobre, no assembly da Application, todo tipo `Request` que implementa `IRequest` e a classe
   `Specification` irmã (mesmo namespace) com `static Ensure(Request)`; **falha** se algum
   `Request` não tiver `Specification` (campo novo sem validação não passa em silêncio).
2. Para cada propriedade `string`/`string?` do `Request`, monta uma instância pelo construtor
   (valor do parâmetro se tiver padrão; `"a"` para string; `Guid.NewGuid()`; primeiro valor definido
   do enum; `null` para anuláveis; zero para números) com **apenas aquela propriedade** preenchida
   com 100 000 caracteres.
3. Chama `Ensure` e exige uma notificação cuja chave seja o nome da propriedade **e** cuja mensagem
   contenha `no máximo` (convenção das mensagens de tamanho). Só a chave não basta: a notificação de
   **formato** (ex.: "E-mail inválido") tem a mesma chave e faria um campo sem limite passar (achado
   durante a Fase A). As linhas do `Theory` usam duas colunas curtas (caso de uso e campo), porque o
   xUnit trunca textos longos no nome de exibição e linhas com o mesmo nome truncado seriam fundidas.
4. Trava de sanidade: o conjunto descoberto contém, no mínimo, as propriedades conhecidas
   (`Authenticate.Password`, `Create.Email`, `UserContext.Create.Cpf` …), para a reflexão não
   "passar" por não achar nada.

**Por quê**: a spec pede que um campo novo sem limite quebre o teste sem cadastro manual. Cem mil
caracteres está "bem acima de qualquer limite" razoável de campo de formulário.

**Limite do teste**: ele prova que *há* um limite, não que o número está certo; os números são
fixados nos testes de `Specification` de cada use case (casos `exatamente no limite` e `+1`).

## R-06 — Diretivas da CSP

**Decisão** (valor completo em [contracts/security-headers.md](contracts/security-headers.md)):

`default-src 'self'; script-src 'self' 'sha256-<hash>'; style-src 'self'; img-src 'self' data:;
connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`.

- `style-src 'self'`: o build gera um `.css` externo e não há estilo escrito na página (verificado).
  Se o build do futuro gerar `<style>`, a CSP quebra o teste do CI e a decisão é revista (spec,
  Premissas: "o plano decide como liberá-los sem abrir a política de scripts").
- `img-src 'self' data:` e `connect-src 'self'`: o console só chama a própria origem (a base da
  Api é `/`; `VITE_API_BASE_URL=/` no build).
- `object-src 'none'` (FR-009: sem plugins), `base-uri 'self'`, `form-action 'self'`.
- **Sem** `upgrade-insecure-requests`: no ambiente local (HTTP) o navegador tentaria subir os
  subrecursos para HTTPS e o console quebraria.
- **Sem** `Cross-Origin-*-Policy` (COOP/COEP/CORP) e sem `Report-To`: fora do pedido da spec e com
  risco de quebrar o console sem ganho para os riscos listados. Podem entrar em spec própria.

## R-07 — Script do tema por hash

**Decisão**: o hash `sha256` do texto do script do tema fica no `security-headers.conf`. Um teste
do Vitest (`src/lib/security-headers.test.ts`) importa `index.html` e `security-headers.conf` com o
sufixo `?raw`, extrai o conteúdo do único `<script>` sem `src`, normaliza `\r\n`→`\n`, calcula
SHA-256 em base64 com `crypto.subtle`, no ambiente **jsdom padrão** do projeto (sem pragma de
ambiente: o `src/test/setup.ts` usa `window` e `HTMLDialogElement` em todo teste e **não** é
alterado; se `crypto.subtle` não existir nesse ambiente, **parar e reportar**, sem trocar o ambiente
nem tocar o `setup.ts`), e exige que `'sha256-<hash>'` apareça na CSP; a mensagem de falha traz o hash **novo** para copiar
(é essa a documentação de "como recalcular", repetida no README do front). O teste também falha se
houver **mais de um** script sem `src` (um segundo script escrito na página seria bloqueado).

**Por que normalizar a quebra de linha**: o checkout no Windows (`core.autocrlf=true`) deixa o
`index.html` em CRLF, enquanto o CI no Linux o tem em LF. O navegador normaliza `CRLF`→`LF` ao
interpretar o HTML antes de calcular o hash da CSP; portanto o hash correto é o do texto em LF, nas
duas plataformas. O script de verificação do CI (R-10) também confere o hash do script **servido**.

**Alternativas**: `nonce` por requisição (exige gerar e injetar o valor a cada resposta: o nginx
serve arquivos estáticos; com `sub_filter` fica frágil; descartada); `'unsafe-inline'` (proibido
pela spec); mover o script para um arquivo (`public/theme-init.js`, `<script src>` síncrono):
elimina o hash e o teste, ao custo de uma requisição bloqueante a mais antes do primeiro desenho.
**Fica como saída** se o hash virar incômodo, mas a spec (FR-010/FR-014) e o pedido já decidiram
pelo hash.

## R-08 — HSTS

**Decisão**: `map $http_x_forwarded_proto $hsts_value { default ""; "https" "max-age=31536000"; }`
no escopo `http` (o `default.conf` é incluído lá) e `add_header Strict-Transport-Security
$hsts_value always;` no trecho comum. Valor vazio → o nginx **não** envia o cabeçalho (o script de
verificação prova, com e sem o cabeçalho de entrada). Sem `includeSubDomains`, sem `preload`.

**Por quê**: decisão do `/speckit-clarify`: sem checar a origem do cabeçalho (o navegador ignora
HSTS recebido por HTTP, RFC 6797 §8.1; quem forjar só afeta o próprio navegador; o ALB sobrescreve
o cabeçalho). `preload` é praticamente irreversível e depende de decisão do dono do produto;
`includeSubDomains` poderia prender subdomínios sem TLS.

**Detalhe**: o `map` compara `https` exato, em minúsculas (valor que o ALB envia). Uma lista
(`https, http`) ou maiúsculas não ativam a instrução; é o lado seguro (sem HSTS, nunca com HSTS
indevido). O `proxy_set_header X-Forwarded-Proto $scheme` do `/v1/` **não muda**: ele diz o que o
nginx recebeu na conexão dele (HTTP, hoje) e é o que a Api usa; ver R-13.

## R-09 — Herança de `add_header`, `always` e respostas encaminhadas

**Decisão**:

- `security-headers.conf` com todos os `add_header … always` (a `always` faz os cabeçalhos
  saírem também em 4xx/5xx, inclusive as páginas de erro geradas pelo nginx).
- `include` no `server` **e** em cada `location` que declara `add_header` próprio (hoje só
  `/assets/`, por causa do `Cache-Control`). Regra a documentar no `nginx.conf`: qualquer
  `add_header` novo num `location` obriga repetir o `include` ali.
- No `/v1/`, `proxy_hide_header` para os cabeçalhos que o nginx repõe (CSP, `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Strict-Transport-Security`): se a
  Api (ou uma versão futura dela) enviar um deles, não há duplicata nem conflito. A resposta da Api
  continua com os demais cabeçalhos dela (`Retry-After`, `Content-Type`, …).
- Trecho em arquivo próprio (`/etc/nginx/snippets/security-headers.conf`), copiado pelo
  `Dockerfile`, para o `include` não repetir a lista.

**Alternativa**: repetir os `add_header` em cada `location` (3 cópias do CSP, mais fácil de
divergir; descartada). Imagem `nginx` com módulo `headers-more` (outra imagem; descartada).

## R-10 — Verificação real dos cabeçalhos (FR-013)

**Decisão**: `src/Frontend/scripts/verify-security-headers.sh` (LF, já garantido pelo
`.gitattributes`):

1. `docker build` da imagem do console (a mesma do `docker compose`).
2. Sobe o upstream simulado: imagem `nginx:1.27-alpine` com um `stub.conf` que escuta em `8080` e
   responde JSON em `/v1/` **enviando de propósito** `X-Content-Type-Options: nosniff` (para provar
   que o `proxy_hide_header` evita a duplicata). Construído por `docker build -f stub.Dockerfile`
   (sem bind mount, que no Git Bash do Windows exige tratamento de caminho).
3. Cria uma rede Docker, sobe o stub com `--network-alias backend` e o console na mesma rede (o
   `proxy_pass http://backend:8080` só resolve assim).
4. Faz `curl -sS -D -` em: `/` (página), o `.js` real citado no `index.html` (arquivo estático),
   `/assets/nao-existe.js` (404 do nginx) e `/v1/ping` (encaminhada), **cada um sem e com**
   `X-Forwarded-Proto: https`. Confere: cada cabeçalho obrigatório presente **uma única vez**; HSTS
   ausente sem o cabeçalho de entrada e presente (≥ 31 536 000) com ele; e que o hash `sha256` da CSP
   é o do script escrito na página **servida** (CRLF→LF normalizado).
5. `trap` de limpeza (contêineres, rede e imagens do teste) em qualquer saída.

**Por quê**: `nginx -t` só prova sintaxe; o pedido é provar o que sai pela rede, em 4 tipos de
resposta. O CI do front não tem o backend, e o nginx **não sobe** se o host do `proxy_pass` não
resolver, então o upstream simulado é necessário. Fica fora do `docker compose` do dia a dia (não
sobe Api nem banco).

**Alternativas**: teste de integração do Vitest com `testcontainers` (pacote novo; descartada);
Playwright para ver erros de CSP no console do navegador (pacote novo, Princípio II; o tema é
conferido manualmente no quickstart e o hash é provado por dois testes).

## R-11 — Testes do usuário de banco da aplicação

**Decisão** (garantias em [contracts/app-role-guarantees.md](contracts/app-role-guarantees.md)):

- `AppRoleProbe` (em `Fixtures`) reúne as consultas ao catálogo do PostgreSQL, executadas com a
  conexão do migrator: atributos do papel (`rolsuper`, `rolcreatedb`, `rolcreaterole`, `rolbypassrls`,
  `rolreplication`), tabelas do esquema `public` cujo dono é o papel, dono do banco e do esquema,
  associação a outros papéis (`pg_has_role(papel,'acs_migrator','member')`; fecha `SET ROLE`),
  `CREATE`/`USAGE` no esquema e privilégios por tabela (comparados com o conjunto permitido da base:
  tenant = `SELECT, INSERT, UPDATE`; catálogo = `SELECT`). Devolve a **lista de violações**.
- `AppRolePrivilegeTests` (`Theory` × {catálogo, compartilhada, dedicada}): (a) `AppRoleProbe` do
  `acs_app` devolve lista vazia; (b) cada instrução proibida, executada pelo `acs_app`, falha com
  `42501`: `CREATE TABLE`, `ALTER TABLE … ADD COLUMN`, `ALTER TABLE … DROP COLUMN`, `DROP TABLE`,
  `CREATE INDEX`, `DROP INDEX`, `CREATE SCHEMA`, `TRUNCATE`, `DELETE`, `ALTER TABLE … DISABLE ROW
  LEVEL SECURITY` e `NO FORCE …`, `DROP POLICY`, `ALTER ROLE acs_app BYPASSRLS SUPERUSER`,
  `CREATE ROLE`, `SET ROLE acs_migrator`, `SET SESSION AUTHORIZATION acs_migrator`. Cada instrução em
  conexão nova (a transação aborta no erro). Os SQLSTATE exatos são conferidos na implementação; se
  alguma instrução devolver outro código de "permissão", o teste registra o código real.
- `CatalogReadOnlyTests`: para cada tabela do catálogo, `INSERT`, `UPDATE` e `DELETE` (com `WHERE
  false`/valores mínimos, pois a checagem de privilégio vem antes de qualquer restrição) falham com
  `42501`, e `has_table_privilege` confirma só `SELECT`.
- `DedicatedRowLevelSecurityTests`: o equivalente de `RowLevelSecurityTests` na base dedicada
  (leitura sem filtro do EF só vê o tenant informado; sem tenant não vê nada; `INSERT` e `UPDATE` do
  `tenant_id` para outro tenant são recusados). **A base compartilhada não é duplicada**: já é
  coberta pelos testes da spec 01, que ficam como estão. Também sobrepõe-se, deliberadamente, **uma
  única instrução** (`CREATE TABLE` na compartilhada) à de `SchemaContractTests`, porque o teste
  novo é parametrizado por base; o teste antigo mantém o que é único dele (matriz do `operators`,
  índices).
- **Papéis-canário (FR-023)**: em banco isolado **com as migrations reais** (catálogo por
  `CreateIsolatedCatalogAsync`; tenant por `CreateMigratedTenantDatabaseAsync`, novo auxiliar da
  fixture que reaproveita o `EfDatabaseMigrationRunner`; nada de `CREATE TABLE` à mão), um papel de teste recebe cada privilégio indevido (`CREATE` no esquema, `DELETE`,
  `TRUNCATE`, `BYPASSRLS`, propriedade de uma tabela, associação a outro papel) e o teste exige que
  `AppRoleProbe` devolva a violação correspondente. Assim a prova **falha** quando a permissão
  sobra, sem alterar o `acs_app` do cluster compartilhado dos testes. **Papéis pertencem ao
  cluster inteiro, não ao banco isolado**: cada canário tem **nome único por execução**
  (`canary_<tipo>_<Guid:N>`), é criado `NOLOGIN` e é removido no `DisposeAsync` mesmo se a
  asserção falhar, com `REASSIGN OWNED BY` e `DROP OWNED BY` em cada banco onde recebeu algo
  (sem isso o `DROP ROLE` falha por objetos dependentes) seguidos de `DROP ROLE`. Cada teste
  confere que o **seu** papel sumiu, e o `DisposeAsync` da fixture confere, antes de descartar o
  contêiner, que nenhum `canary_%` sobrou na suíte inteira (falha a coleção se sobrar).

**Por quê**: o requisito é "provado por teste" e "protege contra regressão". Atributos de papel
são do cluster e o `acs_app` é compartilhado pela suíte; mexer nele para "ver falhar" quebraria os
outros testes. O canário em papel/banco próprio resolve sem esse risco.

## R-12 — NuGet Audit nativo em vez de interpretar `dotnet list package --vulnerable`

**Decisão**: novo `src/Backend/Directory.Build.props`:

- `NuGetAudit=true`, `NuGetAuditMode=all` (inclui transitivos), `NuGetAuditLevel=low` (qualquer
  severidade);
- `WarningsAsErrors` com `NU1901;NU1902;NU1903;NU1904` (vulnerabilidade, por severidade), sempre;
- `NU1900` (falha ao consultar a base de avisos) como erro **só no CI** (condição `'$(CI)' ==
  'true'`; o GitHub Actions define `CI=true`), para o desenvolvedor sem internet continuar
  compilando; o CI nunca "passa" sem ter consultado (FR-028).
- Exceção do `SSH.NET`: sai o `NoWarn NU1903` geral do `AccessControlSystem.Tests.csproj` e entram
  dois itens `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-q939-rpr3-3284" />` e
  `…GHSA-mggc-4xg6-vcxf`, **só nesse projeto**, com o comentário de justificativa já existente
  (recurso "Docker over SSH" do Testcontainers, nunca usado; o pacote só existe no projeto de
  testes e não vai para as imagens de produção). Um aviso novo no mesmo pacote falha, porque o
  suppress é por URL de aviso.
- O `backend-ci.yml` ganha um `dotnet restore Backend.slnx` explícito antes do build, só para a
  falha da auditoria aparecer com nome próprio no log; `dotnet build`/`dotnet test`/`docker build`
  também auditam, pois o *restore* é quem audita.

**Efeito colateral aceito**: um aviso novo num pacote já usado quebra o build e o `docker compose
up --build` até corrigir ou justificar (é o objetivo).

**Correção (Dockerfiles)**: os Dockerfiles da Api e do Migrator (contexto de build `src/Backend`,
`WORKDIR /src`) copiam **só os `.csproj`** antes do `RUN dotnet restore`; o `COPY . .` que traz o
resto vem depois, e o `publish` usa `--no-restore`. Sem outra mudança, o `Directory.Build.props`
**não estaria presente** no restore das imagens e a auditoria nativa não valeria nelas. Por isso a
Fase D acrescenta `COPY Directory.Build.props ./` **antes** do `RUN dotnet restore` nos dois
Dockerfiles (tarefas T059 e T060), e o `PackageAuditConfigTests` exige essa linha antes do primeiro
`dotnet restore` (a regressão fica travada por teste). Dentro do `docker build` a variável `CI` não
existe, então `NU1900` não é erro ali (o `NU1901`–`NU1904` é); a falha de consulta aos avisos é
barrada pelo `restore` do CI. O `.dockerignore` de `src/Backend` não exclui o arquivo.

**Verificação a fazer na implementação**: confirmar que `NuGetAuditSuppress` por GHSA silencia
os 2 avisos do `SSH.NET` transitivo e que `dotnet build` fica verde sem `NoWarn`; se o
suppress não casar com a URL exata, **parar e reportar a saída real**. **Não há fallback para
`NoWarn`** (nem para `WarningsAsErrors` afrouxado): seria a supressão geral que a FR-027 proíbe e
contradiz o `PackageAuditConfigTests` (item 2).

**Por que o `dotnet list package --vulnerable` não é gate**: ele imprime texto e sai com código 0
mesmo com vulnerabilidade, e **não respeita o `NuGetAuditSuppress`** (o `SSH.NET` continuaria
aparecendo, sem distinguir exceção justificada de aviso novo). Por isso o `tasks.md` **não** o
inclui como gate; o gate é o NuGet Audit no *restore*.

**Emenda (PATCH) à constituição: APROVADA pelo dono do projeto (2026-11-02)**, a ser aplicada pelo
revisor logo após o merge desta spec (esta spec não edita a constituição, Integridade das
entregas): nos textos do Princípio IX e do Fluxo ("`dotnet list package --vulnerable`,
`npm audit`"), trocar pelo mecanismo em uso ("NuGet Audit como erro no *restore*, `npm audit`"), e
retirar do "Legado conhecido" os itens do `nginx.conf` e do CI sem verificação de pacotes.

## R-13 — `npm audit`, `vite` e pendências para a spec 05

**`npm audit` no front**: `npm audit` sem `--audit-level` (qualquer severidade, dependências de
desenvolvimento incluídas, FR-025), depois do `npm ci`. Sem rede, o `npm audit` sai com erro e o
passo falha (FR-028). **Exceção por aviso** fica como opção futura, via allowlist por GHSA com
`audit-ci`; **não** se instala agora (Princípio II, sem exceção hoje).

**Correção**: o `npm audit` informa correção sem `--force`. Plano: `npm audit fix` (atualiza
`package-lock.json`), depois subir o piso em `package.json` (`"vite": "^6.4.5"`, hoje `^6.3.5`) para
um `npm install` futuro não voltar a uma versão vulnerável. Esperado: `vite` 6.4.x e `source-map-js`
≥ 1.2.2, sem troca de major. Se `npm audit fix` pedir `--force`, **parar e reportar**. O `tasks.md`
lista cada pacote que mudou (nome, de → para) e qualquer ajuste de configuração que a atualização
pedir. Typecheck, testes e build precisam continuar verdes sem tocar em teste.

**Validação de verdade (FR-030)**: PR em rascunho, nunca integrado, uma vez por ecossistema
(backend: um `PackageReference` com vulnerabilidade conhecida e já corrigida, por exemplo
`Newtonsoft.Json 12.0.3`; front: `npm install lodash@4.17.15 --save-dev`), esperando CI vermelho
no passo de auditoria, e depois fechado sem merge. **Envolve push e PR no GitHub, ação externa:
é validação do revisor depois do push (V-01 e V-02 no `tasks.md`), não tarefa do implementador.** O
implementador entrega a configuração que torna o CI vermelho e prova o equivalente localmente
(`dotnet build` falhando com `NU1903` sem a exceção, T057; `npm audit` saindo com código 1 diante de
um pacote vulnerável, T070).

**Fora do escopo, sugestão**: os workflows só rodam quando `src/**` muda (filtro de caminhos); um
aviso novo sobre pacote *inalterado* não acenderia o CI. Uma execução agendada (semanal) cobriria
isso; entra na spec 05 (pipeline), não aqui.

**Pendências para a spec 05** (registrar lá, não aqui):

1. O console só pode ser alcançado pelo load balancer (premissa da decisão do HSTS); o grupo de
   segurança do serviço precisa impedir acesso direto.
2. O redirecionamento HTTP→HTTPS é do ALB (FR-018).
3. O `proxy_set_header X-Forwarded-Proto $scheme` do nginx reflete a conexão do nginx (HTTP) e não o
   esquema original do cliente; se a Api precisar do esquema original atrás do ALB, a spec 05
   decide como repassá-lo, junto com `KnownProxies` e o IP real do cliente.
4. A `Permissions-Policy` desliga câmera e microfone; a spec 15 (QR code) revisa se o console passar
   a ler QR pela câmera.
