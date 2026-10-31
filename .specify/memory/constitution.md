# AccessControlSystem Constitution

Sistema de controle de acesso multi-tenant, projetado para operar de algumas centenas até
**150 mil tenants**. Esta constituição rege o código **interno de um tenant** (o que roda sobre
os dados de um único cliente) e a forma como ele conversa com o **catálogo**. O sistema de
provisionamento (que cria tenants, aloca bases e cria o primeiro operador) é um sistema
separado, fora do escopo atual.

Termos: **DEVE / NÃO DEVE** = obrigatório; **DEVERIA** = padrão esperado, exceção exige
justificativa no `plan.md` (seção Complexity Tracking); **PODE** = opcional.

## Core Principles

### I. Isolamento de Tenant e Catálogo (NÃO NEGOCIÁVEL)

O modelo é **híbrido**: por padrão, vários tenants compartilham uma mesma base PostgreSQL,
separados por coluna `tenant_id` e por Row-Level Security (RLS); um tenant PODE ganhar base
dedicada (por volume ou contrato) sem nenhuma mudança no código dos use cases. Existem duas
camadas de dados:

- **Catálogo**: base do sistema de provisionamento. Guarda os tenants (`Id` Guid, CNPJ, nome,
  situação e **em qual base de dados ele mora**) e o **diretório de operadores** (e-mail →
  `tenantId`, ativo/inativo). O catálogo NUNCA DEVE guardar senha nem hash de senha.
- **Bases de dados de tenant**: compartilhadas ou dedicadas, guardam todos os dados de negócio
  dos clientes, incluindo as credenciais. O mesmo e-mail PODE existir em vários tenants, cada um
  com a sua própria senha.

Regras:

- Todo use case DEVE operar sobre exatamente um tenant, o resolvido para a requisição corrente.
  Nenhum código interno de tenant PODE consultar, unir ou copiar dados de outro tenant. A única
  exceção é o login (ver abaixo).
- **Toda tabela de dados de tenant DEVE ter a coluna `tenant_id`** (NOT NULL), mapeada como
  shadow property do EF na Infrastructure. O `TenantId` NÃO DEVE aparecer em entidades de
  domínio, `Request`, `Response` nem ser informado por quem chama: ele é preenchido pela
  Infrastructure a partir do tenant resolvido.
- O isolamento DEVE ter duas camadas independentes: **RLS** no PostgreSQL (com `FORCE ROW LEVEL
  SECURITY`, e o usuário da aplicação não pode ser dono das tabelas nem ter `BYPASSRLS`) **e**
  filtro global de consulta do EF por tenant. Uma camada sozinha não basta.
- O tenant corrente DEVE ser informado ao banco por transação (`SET LOCAL` / `set_config(...,
  true)`), nunca por sessão, para continuar correto atrás de um pooler em modo transação
  (PgBouncer).
- Índices únicos e chaves de busca DEVEM incluir `tenant_id` (ex.: e-mail único **por tenant**,
  não global), e todo índice de tabela de tenant DEVERIA começar por `tenant_id`.
- O `AppDbContext` DEVE ser resolvido por requisição (scoped) via DI. Código novo NÃO DEVE ler a
  connection string de estado estático ou global: a base é escolhida pelo resolvedor de tenant a
  partir do catálogo, e o código de Application/Infrastructure não pode depender de saber se a
  base é compartilhada ou dedicada.
- NÃO DEVE existir estado estático, singleton ou cache em memória que guarde dados de tenant.
  Cache é regido pelo Princípio XIII e DEVE ter o tenant na chave.
- Mover um tenant de uma base compartilhada para uma dedicada (ou o contrário) DEVE ser possível
  copiando as linhas com o seu `tenant_id`, sem transformar dados.
- Migrations DEVEM ser aplicáveis a N bases de forma idempotente e independente, e seguir as
  regras de compatibilidade do Princípio XIII. Nenhuma migration PODE criar operador ou usuário
  com valores fixos: o primeiro operador de cada tenant,
  com a role nativa Administrador (Princípio XII), é criado pelo provisionamento, com o e-mail e
  a senha informados na criação do tenant.
- Identificadores DEVEM ser `Guid` gerados pela aplicação (`Entity.Id`), para que registros
  continuem únicos se dados de tenants forem movidos ou consolidados.
- **Acesso ao catálogo**: o código de tenant DEVE acessar o catálogo apenas por interfaces
  declaradas na Application (ex.: `ITenantCatalog`, `ITenantDirectory`) e implementadas na
  Infrastructure. Ele NÃO DEVE ler tabelas do catálogo diretamente, e a única escrita permitida
  são as entradas do diretório de operadores.
- **Sincronização do diretório**: todo use case que cria operador, altera e-mail, ativa ou
  desativa operador DEVE atualizar o diretório no mesmo fluxo. A estratégia para falha entre o
  catálogo e a base do tenant (compensação ou retry/outbox) é definida na spec do diretório e,
  a partir daí, DEVE ser seguida por todos esses use cases.
- **Resolução de tenant**: em requisição autenticada, o tenant vem da claim `tenant` (Guid) do
  token. Todo token emitido DEVE carregar essa claim. A assinatura usa uma chave global única;
  por isso a Api DEVE rejeitar com `401` qualquer token cuja claim `tenant` esteja ausente ou
  não corresponda a um tenant existente e ativo no catálogo.
- **Requisições de aparelhos**: o tenant de uma requisição vinda de um aparelho (Princípio IX,
  Dispositivos) DEVE ser resolvido pela identidade autenticada do aparelho, nunca por dado
  enviado no corpo ou na rota.
- **Login**: é o único use case que roda antes da resolução de tenant e que PODE ler dados de
  mais de um tenant. Ele DEVE consultar o diretório para obter os tenants candidatos do e-mail e
  acessar só esses, cada um no seu próprio contexto de tenant, em modo leitura.
- **Segurança do login**: um tenant só PODE aparecer na resposta depois que a senha conferir
  naquele tenant e o operador estiver ativo nele. Tenants onde a senha não confere ou o operador
  está inativo NUNCA DEVEM ser revelados, e toda falha devolve o mesmo `401` genérico. Quando o
  cliente escolhe um tenant, a senha DEVE ser verificada de novo no tenant escolhido antes da
  emissão do token.

**Por quê:** uma base por tenant não escala para 150 mil tenants (conexões, custo fixo por
base, migrations e backups multiplicados). O modelo híbrido mantém o custo baixo para a maioria e
o isolamento físico para quem precisa. Como o código nunca sabe onde o tenant mora, a mesma
regra de negócio serve aos dois casos. O catálogo existe só para localizar tenants e operadores;
dados de negócio e credenciais nunca vão para ele.

### II. Clean Architecture com dependências para dentro

- Projetos e dependências: `Domain` ← `Application` ← `Infrastructure`; a `Api` compõe tudo
  (referencia `Application` e `Infrastructure`). Nenhuma referência PODE apontar para fora.
- `Domain` NÃO DEVE depender de nenhum pacote além de `SecureIdentity`. `Application` só
  depende de `Domain` e `Flunt`. EF Core e Npgsql ficam só em `Infrastructure` (e na `Api`
  para migrations).
- Interfaces que a Application precisa (repositórios, `ITokenService`, `IActiveSessionChecker`)
  DEVEM ser declaradas na Application e implementadas na Infrastructure ou na Api.
- Pacotes NuGet ou npm novos DEVEM ser justificados no `plan.md`.

### III. Bounded Context → Use Case (vertical slice)

- O código DEVE ser organizado por contexto e use case, espelhado em todas as camadas:
  - `Domain/Contexts/{Contexto}/{Entities|ValueObjects|Enums}`
  - `Application/Context/{Contexto}/UseCases/{UseCase}/` com `Request.cs`, `Response.cs`,
    `Specification.cs`, `Handler.cs` e `Contracts/IRepository.cs`
  - `Infrastructure/Context/{Contexto}/UseCases/{UseCase}/Repository.cs`
  - `Infrastructure/Context/{Contexto}/Mapping/{Entidade}Map.cs`
  - `Tests/{Camada}/{Contexto}/{UseCase}/`
- Cada use case DEVE ter o seu próprio `IRepository`, com só os métodos de que precisa. NÃO
  DEVE existir repositório genérico por entidade nem `IRepository` compartilhado entre use cases.
- Código só é promovido para `SharedContext` quando já for usado por dois ou mais contextos.
- Um use case NÃO DEVE chamar o Handler de outro use case.
- Métodos de repositório DEVEM seguir a convenção: `Get{Entidade}By{Campo}Async`,
  `Get{Entidade}ListAsync`, `{Algo}ExistsAsync` / `Is{Algo}Async` para perguntas e
  `{Ação}Async` para escrita (`CreateAsync`, `UpdateAsync`), sempre com sufixo `Async`. Um método
  `bool` DEVE responder exatamente o que o nome pergunta (`EmailExistsAsync` devolve `true`
  quando o e-mail existe).
- O registro de DI DEVE ser manual, em `UseCasesExtensions.Add{Contexto}Context()`, com um
  `#region {UseCase}` por use case registrando o `IRepository` e o `IHandler<Request, Response>`,
  todos `Scoped`. Cada contexto novo ganha o seu método `Add{Contexto}Context()` chamado no
  `Program.cs`.

### IV. Domínio rico e sempre válido

- Entidades herdam de `Entity`, têm setters `private`, um construtor `protected` vazio para o EF
  e só mudam de estado por métodos com nome de intenção, com verbo no imperativo em inglês
  (`UpdateEmail`, `Activate`, `Deactivate`). Transições de ativação DEVEM usar sempre o par
  `Activate()` / `Deactivate()`.
- Value objects herdam de `ValueObject`, validam no construtor e lançam `DomainException` com
  mensagem em português. Regex DEVE ser `[GeneratedRegex]` com o pattern exposto como `const`
  pública, para a `Specification` reutilizar o mesmo pattern.
- Invariantes que envolvem só a própria entidade (ex.: data final ≥ data inicial) DEVEM estar
  na entidade. Regras que dependem de outros registros (ex.: último Administrador ativo, e-mail
  único por tenant)
  ficam no Handler, consultando o repositório.
- Exclusão é sempre lógica (`IsActive`). NÃO DEVE existir exclusão física de entidades de negócio.
  A eliminação de dados pessoais é feita por anonimização (Princípio X), nunca por `DELETE`.
- Senhas DEVEM ser guardadas só como hash (`SecureIdentity.PasswordHasher`) e verificadas pelo
  value object; nunca trafegam de volta em `Response`.

### V. Contrato de Use Case (Specification → Handler → Response)

- `Request` é um `record` que implementa `IRequest`. `Response` herda de
  `SharedContext.UseCases.Response`, com `Message`, `Status`, `Notifications` e `Data`; os dados de
  retorno ficam num `record ResponseData` do próprio use case.
- A validação de formato DEVE estar em `Specification.Ensure(request)` com Flunt. Se falhar, o
  Handler DEVE devolver `400` com a mensagem `"Requisição inválida"` e as `notifications`, sem
  tocar no repositório.
- O Handler DEVE ser dividido em `#region 01 - {passo}`, `#region 02 - ...` na ordem: validação →
  busca → regras de negócio → persistência → resposta.
- Resultados esperados de negócio DEVEM virar `Response` com status HTTP explícito (400, 401,
  404, 409); exceções ficam para o inesperado. Status usados hoje: `200`, `201` (criação), `400`,
  `401`, `404`, `409`.
- Handler que instancia value objects ou persiste dados sujeitos a restrição de unicidade DEVE
  capturar `DomainException` → `400` e `ConflictException` → `409`. Repositórios DEVEM traduzir
  violação de unicidade do Postgres (`SqlState 23505`) em `ConflictException`.
- Todo método assíncrono DEVE receber e repassar `CancellationToken`.

### VI. Api fina e autorização no servidor

- Controller NÃO DEVE conter lógica: recebe a requisição, injeta o handler via
  `[FromServices] IHandler<Request, Response>`, chama `HandleAsync` e devolve
  `StatusCode(result.Status, result)`.
- Identificadores de quem chama (`User.Id()`) DEVEM vir do token, nunca do corpo; quando o
  `Request` tem `Id`, a Api sobrescreve com `request with { Id = ... }` (da rota ou do token).
- Autorização DEVE ser declarada por permissão no endpoint (Princípio XII), nunca por nome de
  role. O backend é a única fonte de verdade de autorização e de regras de negócio.
- Todo endpoint DEVE ter comentário XML (`<summary>`, `<param>`, um `<response code>` por status
  possível) para alimentar o Swagger.
- Rotas DEVEM seguir o padrão por ação já usado no `AccountContext`: `v1/{contexto}/{acao}`, com
  o contexto no singular e em minúsculas (`v1/account`, `v1/user`). Ações e métodos padrão:
  `POST create`, `GET get-list`, `GET get-me`, `PUT update/{id:guid}`, `PUT update-me`,
  `PUT activate/{id:guid}`, `PUT deactivate/{id:guid}`. Ação nova DEVE usar verbo em inglês no
  mesmo formato `kebab-case`.
- Erros fora do fluxo de use case (401 de autenticação, 403, 429, 500) DEVEM usar o mesmo
  envelope `ErrorResponse`.
- **Compatibilidade de contrato**: clientes que não são atualizados junto com a Api (aparelhos e
  integrações externas) DEVEM continuar funcionando depois de um deploy. Mudança incompatível em
  endpoint usado por eles (remover ou renomear campo, mudar tipo, status ou rota) exige uma nova
  versão de rota (`v2`), e a versão anterior continua no ar até não haver mais cliente usando.
  Acrescentar campo opcional não é mudança incompatível.
- A exceção a este princípio são os endpoints de aparelho (Princípio IX, Dispositivos), que
  seguem o contrato do fabricante.

### VII. Test-First (NÃO NEGOCIÁVEL)

- TDD é obrigatório: os testes de um comportamento DEVEM ser escritos e executados **falhando**
  antes da implementação desse comportamento (Red → Green → Refactor). No `tasks.md`, as tarefas
  de teste de cada use case DEVEM vir antes das tarefas de implementação e não são opcionais,
  mesmo que o template do Spec Kit as marque assim.
- Todo use case novo ou alterado DEVE ter, no mínimo:
  - `{UseCase}SpecificationTests` — validações de entrada;
  - `{UseCase}HandlerTests` — regras e status, com `IRepository` falso via NSubstitute;
  - `{UseCase}RepositoryTests` — contra Postgres real via Testcontainers, com
    `[Collection(PostgresCollection.Name)]` e `[Trait("Category", "Integration")]`.
- Entidades e value objects novos DEVEM ter testes em `Tests/Domain/`.
- Todo endpoint DEVE ter testes de autorização: `401` sem token, `403` com role sem a permissão
  exigida e `401` com token emitido para outro tenant.
- Testes de integração compartilham o mesmo banco: DEVEM gerar dados únicos (ex.: e-mail com
  `Guid`) e NÃO DEVEM assumir tabela vazia.
- Todo `{UseCase}RepositoryTests` DEVE ter um caso de isolamento: dados de outro tenant, na mesma
  base, não são lidos nem alterados. A suíte DEVE ter também um teste que prova que o RLS bloqueia
  o acesso mesmo sem o filtro do EF.
- Nomes de teste DEVEM ser frases em português com underscore, descrevendo o comportamento
  (`Ultimo_admin_ativo_nao_pode_ser_desativado`).
- Ferramentas do backend: xUnit, NSubstitute, Testcontainers.PostgreSql. Nenhuma outra
  biblioteca de teste ou de assertion sem justificativa.
- **Frontend**: o TDD vale também para o front.
  - Toda página e todo componente com comportamento DEVEM ter testes com Vitest + Testing
    Library. Os testes interagem como o usuário (`user-event`) e consultam por papel, rótulo
    ou texto acessível, nunca por classe CSS ou estrutura do DOM.
  - Chamadas HTTP DEVEM ser simuladas na camada de rede com MSW, devolvendo o envelope real da
    API (`message`, `status`, `notifications`, `data`), e não com mock dos módulos de
    `src/api/`.
  - Funções de `src/lib/` DEVEM ter testes unitários.
  - Arquivos de teste ficam ao lado do arquivo testado (`*.test.ts` / `*.test.tsx`), com
    descrições em português (`it("mostra as notifications quando a api devolve 400")`).

### VIII. Frontend é só apresentação

- O frontend NÃO DEVE reproduzir regra de negócio nem de segurança: não valida senha, e-mail ou
  permissão antes de enviar, e não desabilita ações para "adivinhar" a resposta da API.
- Toda chamada HTTP DEVE ser uma função em `src/api/` (uma por endpoint) usando `apiRequest`.
- Todo erro de API exibido DEVE passar por `toFormError`, mostrando `message` **e**
  `notifications` como vieram do backend.
- `RequireSession` e `RequireRole` são conveniência de navegação, não controle de acesso. Menus
  e rotas DEVEM ser exibidos conforme as permissões do operador (vindas da Api), nunca por nome
  de role fixo no código do front.
- Stack: Vite + React + TypeScript `strict` + React Router, CSS com tokens de tema, sem
  bibliotecas de UI de terceiros.

### IX. Segurança (NÃO NEGOCIÁVEL)

O sistema controla acesso físico a locais de clientes e roda em produção. Toda decisão de
design DEVE partir do pressuposto de que a Api está exposta a atacantes.

- **Fechado por padrão**: a Api DEVE usar uma fallback policy que exige usuário autenticado.
  Endpoint anônimo só com `[AllowAnonymous]` explícito e justificado no `plan.md` (hoje: login e
  health checks).
- **JWT**: a validação DEVE checar assinatura, expiração, `iss` e `aud`, com `ClockSkew` curto. A
  chave de assinatura DEVE ter pelo menos 256 bits, e a Api NÃO DEVE iniciar com chave ausente
  ou menor. O token DEVE carregar uma claim de versão de segurança do operador, incrementada na
  troca de senha, na mudança de role e na desativação, e conferida a cada requisição (pelo
  `IActiveSessionChecker`). Token com versão antiga recebe `401`.
- **Login**: a verificação de hash DEVE ser executada sempre, mesmo quando o e-mail não existe,
  para o tempo de resposta não revelar quais e-mails estão cadastrados. O login DEVE ter limite
  de tentativas por IP **e** por conta, com backoff, e mensagem de erro genérica.
- **Rede**: a Api DEVE confiar em `X-Forwarded-*` só de proxies conhecidos (`KnownProxies` /
  `KnownIPNetworks` configurados) e NÃO DEVE ficar exposta direto em produção, só atrás do
  proxy. Produção exige HTTPS com HSTS e cabeçalhos de segurança (CSP, `X-Content-Type-Options`,
  `X-Frame-Options`/`frame-ancestors`, `Referrer-Policy`) no nginx. Swagger só em Development.
- **Entrada**: toda string de `Request` DEVE ter tamanho máximo validado na `Specification`,
  inclusive senha e PIN (evita DoS no cálculo de hash).
- **Criptografia e chaves**: dado criptografado DEVE usar criptografia autenticada (AES-GCM) e
  guardar junto a versão da chave usada, para permitir rotação. Chaves NÃO DEVEM ficar no banco
  nem no repositório.
- **PIN de acesso** (`PasswordAccess`): o aparelho precisa do valor original, então o PIN é
  criptografado, não transformado em hash.
  - NUNCA em texto puro na base, em `Response` ou em log.
  - Descriptografado só para enviar ao aparelho e para verificar um PIN recebido.
  - A verificação descriptografa o valor guardado e compara em tempo constante
    (`CryptographicOperations.FixedTimeEquals`). O texto cifrado nunca é comparado nem usado
    em busca.
  - Quando o PIN identifica a pessoa sozinho (ex.: modo PIN da ControlID, único por aparelho),
    a busca e a unicidade DEVEM usar um índice HMAC do PIN, com chave própria e separada da
    chave de criptografia.
- **Dispositivos**: cada fabricante (ex.: ControlID) DEVE ser um adaptador, e as regras
  específicas dele (modos de identificação, unicidade de PIN por aparelho, formato de mensagens)
  ficam no adaptador e na spec, nunca no domínio genérico.
  - **Sistema chamando o aparelho**: adaptador na Infrastructure, atrás de uma interface
    declarada na Application.
  - **Aparelho chamando o sistema** (modo push, em que o aparelho pergunta periodicamente se há
    comandos e envia eventos): controllers próprios por fabricante na Api (ex.:
    `Api/Devices/ControlId/`). Esses controllers seguem rotas e payloads do fabricante (exceção
    aos Princípios V e VI), são finos, e só traduzem o protocolo do fabricante para use cases da
    Application.
  - Endpoints de aparelho DEVEM usar um esquema de autenticação próprio, com credencial
    individual por aparelho (nunca o JWT de operador), revogável. O aparelho é autorizado só
    para as ações do seu protocolo.
  - Endpoints de aparelho DEVEM ter rate limit por aparelho, e a sua comunicação DEVE usar canal
    autenticado e criptografado (HTTPS).
- **Logs**: NUNCA logar senha, token, PIN, chave, CPF ou RG completos.
- **Banco**: o usuário de banco da aplicação NÃO DEVE ter permissão de DDL. Migrations rodam em
  processo separado, com usuário próprio: o passo de migrations do pipeline de entrega (seção
  "Branches, ambientes e entrega"), e o provisionamento ao criar uma base nova.
- **Dependências**: o CI DEVE falhar com pacote vulnerável conhecido (`dotnet list package
  --vulnerable`, `npm audit`). Suprimir um alerta exige comentário com justificativa, como o
  `NU1903` em `AccessControlSystem.Tests.csproj`.

**Por quê:** num sistema de controle de acesso, uma falha de segurança na Api abre portas
físicas. Cada regra acima corresponde a um problema real encontrado no código atual (ver
"Legado conhecido").

### X. Dados pessoais (LGPD)

- O tenant (empresa cliente) é o **controlador** dos dados, e o sistema é o **operador**. O
  sistema DEVE oferecer ao tenant as ferramentas para cumprir a LGPD; quem decide prazos e
  bases legais é o tenant.
- Nome, CPF, RG, telefone, PIN e registros de acesso são dados pessoais. Uma spec só PODE coletar
  um dado pessoal novo se justificar a finalidade.
- CPF e RG DEVEM aparecer sempre mascarados em listas e respostas paginadas
  (ex.: `***.456.789-**`). O valor completo só PODE aparecer no endpoint e na tela de detalhe,
  para quem tiver a permissão específica de ver documentos completos (Princípio XII).
- **Eliminação**: o pedido de eliminação DEVE desativar o titular imediatamente (ele sai dos
  aparelhos). Os dados ficam retidos até o fim do prazo de retenção configurado pelo tenant e
  então DEVEM ser **anonimizados** de forma automática e irreversível: nome, CPF, RG, telefone e
  PIN substituídos ou apagados no cadastro, mantendo o `Id`.
- Histórico e auditoria DEVEM referenciar pessoas **só pelo `Id`** e NÃO DEVEM copiar nome, CPF,
  RG ou telefone. Assim, anonimizar o cadastro deixa todo o histórico sem identificação de uma
  vez, sem precisar varrer tabelas de eventos; a interface passa a mostrar "Usuário anonimizado".
- O prazo de retenção, sua configuração por tenant e o job de anonimização são definidos em spec
  própria.

### XI. Auditoria

- Todo use case de escrita e todo login (com sucesso ou falha) DEVE gerar um evento de auditoria
  com: quem (`Id`), quando (UTC), ação, entidade-alvo (`Id`) e valores antes/depois dos campos
  alterados. Eventos NÃO DEVEM conter dados pessoais em texto (Princípio X) nem segredos
  (Princípio IX).
- Os eventos ficam junto dos dados do tenant (com `tenant_id` e RLS, Princípio I), numa tabela
  só de inclusão (append-only). Nenhum use case PODE alterar ou apagar um evento.
- Falha de login é registrada em cada tenant candidato em que o e-mail existe. Falha sem nenhum
  tenant identificável (e-mail fora do diretório) vai só para o log estruturado, sem o e-mail em
  texto.
- O evento DEVE ser gravado na mesma transação da escrita auditada: ou os dois são persistidos,
  ou nenhum.
- Os testes de handler DEVEM verificar que o evento esperado é gerado.

### XII. Autorização por permissões e roles dinâmicas (NÃO NEGOCIÁVEL)

- **Permissões são fixas no código**: cada ação protegida tem uma permissão nomeada
  `{contexto}.{acao}` (ex.: `account.create`, `user.deactivate`), declarada num catálogo único na
  Application. Permissão nova só nasce junto com a funcionalidade, por spec. Todo endpoint DEVE
  exigir exatamente a permissão da sua ação.
- **Roles são dados do tenant**: criadas, editadas e desativadas dentro do sistema, como um
  conjunto de sim/não sobre o catálogo de permissões. Cada operador tem **exatamente uma** role.
  Nenhum código PODE testar nome de role para decidir acesso.
- **Role nativa Administrador**: existe em todo tenant, NÃO PODE ser editada nem desativada, tem
  todas as permissões e é a **única** que pode criar, editar ou desativar roles. O primeiro
  operador do tenant a recebe no provisionamento. O tenant NUNCA PODE ficar sem ao menos um
  operador ativo com essa role.
- **Anti-escalada**: quem não tem a role Administrador NÃO PODE atribuir a role Administrador a
  ninguém, e só PODE atribuir (ao criar ou editar um operador) roles cujas permissões estejam
  todas contidas nas suas. Ninguém PODE alterar a própria role.
- **Efeito imediato**: mudar a role de um operador ou as permissões de uma role DEVE invalidar
  os tokens afetados pela versão de segurança (Princípio IX). A permissão é conferida com o
  estado atual, e não com um snapshot antigo do token.
- "Apagar" operador, usuário ou role significa desativar (Princípio IV).

### XIII. Escalabilidade e desempenho (NÃO NEGOCIÁVEL)

O sistema DEVE suportar de algumas centenas até 150 mil tenants sem mudança de arquitetura,
crescendo pela adição de instâncias da Api, de bases compartilhadas e de bases dedicadas.

- **Api sem estado**: qualquer instância DEVE poder atender qualquer requisição de qualquer
  tenant. Estado que precisa ser coerente entre instâncias (rate limit, cache, locks de jobs)
  DEVE ficar no Redis, nunca na memória da instância.
- **Cache**: toda chave DEVE conter o tenant e todo item DEVE ter TTL explícito. Escrita que
  afeta segurança (sessão, versão de segurança, role, permissões, situação do tenant) DEVE
  invalidar a entrada correspondente no mesmo fluxo; o TTL é só a rede de proteção e NÃO DEVE
  passar de 60 segundos para esses dados. A falha do Redis DEVE degradar para o banco, nunca
  liberar acesso.
- **Custo por requisição**: o pipeline de autenticação (sessão, versão de segurança, tenant e
  permissões) DEVE custar no máximo uma ida ao banco por requisição, ou nenhuma quando atendido
  pelo cache.
- **Consultas**: toda consulta DEVE ser servida por índice (que começa por `tenant_id`), listas
  DEVEM ser paginadas, e NÃO DEVE haver consulta dentro de laço (N+1) nem carga sem limite em
  memória. O `plan.md` DEVE listar as consultas novas e os índices que as atendem.
- **Rate limit por tenant**: além dos limites por IP e por conta (Princípio IX), DEVE existir
  limite por tenant, para que um cliente não degrade os outros (noisy neighbor).
- **Migrations compatíveis**: como a atualização de N bases não é atômica, toda migration DEVE
  ser compatível com a versão anterior da aplicação (expand/contract): adicionar antes de usar,
  parar de usar antes de remover, e nenhuma mudança destrutiva no mesmo deploy que a introduz. O
  passo de migrations DEVE rodar em lotes, ser retomável e registrar o estado por base.
- **Jobs**: todo job em background DEVE processar os tenants em lotes, com concorrência limitada,
  lock distribuído (Redis) para não rodar duplicado entre instâncias, e ser idempotente e
  retomável. Um tenant com erro NÃO DEVE interromper os outros.
- **Metas (SLO)**, medidas no servidor sob a carga de referência:
  - endpoints comuns de leitura e escrita: p95 ≤ 300 ms e p99 ≤ 1 s;
  - validação de acesso vinda de aparelho e resposta ao polling de aparelho: p95 ≤ 150 ms;
  - taxa de erro (5xx) < 0,1%.
- **Testes de carga**: feitos com k6, com os scripts versionados no repositório.
  - A carga de referência (número de tenants, distribuição entre bases compartilhadas e
    dedicadas, requisições por segundo) DEVE ser definida e versionada junto dos scripts.
  - O polling dos aparelhos (aparelhos por tenant × frequência) DEVE fazer parte da carga de
    referência: tende a ser o maior volume do sistema. A resposta "não há nada pendente" DEVERIA
    ser servida sem ida ao banco.
  - Toda spec que cria ou altera um caminho crítico (login, pipeline de autenticação, validação
    de aparelho, listas e jobs) DEVE entregar um cenário k6 com *thresholds* iguais às metas
    acima. Thresholds não atingidos reprovam a entrega.

**Por quê:** num sistema de controle de acesso, lentidão é falha de produto: tem gente parada
na porta. Escalabilidade não se adiciona depois. Ela precisa estar nas regras desde a primeira
spec, porque mudar o modelo de dados ou tirar estado da memória com 150 mil tenants no ar custa
muito mais.

## Padrões Técnicos e de Código

- **Stack backend:** .NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL (com RLS), Redis,
  JWT Bearer, Flunt, OpenTelemetry. Testes de carga com k6.
- **Infraestrutura:** AWS com serviços gerenciados (containers em ECS Fargate, PostgreSQL em RDS,
  Redis em ElastiCache, Secrets Manager, CloudWatch). Os serviços exatos e a ferramenta de
  infraestrutura como código são definidos na spec de ambientes; trocar um deles exige emenda.
- **Idioma:** identificadores (classes, métodos, propriedades, rotas) em inglês; mensagens ao
  usuário, títulos de `#region`, comentários e nomes de teste em português. Todo texto exibido ao
  usuário (mensagens da API, telas e erros do frontend) DEVE estar em português com acentuação
  correta; comentários de código PODEM ficar sem acento.
- **Estilo C#:** chave na mesma linha (K&R), `Nullable` habilitado, namespaces em bloco
  espelhando as pastas, campos privados `_camelCase`, `@operator` para variáveis com nome
  reservado. Código novo DEVE seguir o estilo do arquivo vizinho.
- **Persistência:** mapeamento só via `IEntityTypeConfiguration` (`{Entidade}Map`), tabelas e
  colunas em `snake_case`, value objects como owned types. Migrations ficam no assembly
  `AccessControlSystem.Api`; uma migration já aplicada NÃO DEVE ser editada — mudanças viram
  migration nova.
- **Leitura:** consultas só de leitura DEVEM usar `AsNoTracking()`; listas DEVEM ser paginadas
  (`PagedResponse<T>`, `Size` de 1 a 100) e projetadas direto para o `record` de resposta.
- **Segredos:** nenhum segredo em `appsettings.json` versionado nem em log. Valores vêm de
  variáveis de ambiente / user-secrets / `.env` (não versionado).

### Operação

- A Api DEVE expor `/health/live` (processo de pé) e `/health/ready` (catálogo e Redis
  acessíveis; as bases de tenant não entram, porque são muitas), anônimos e sem detalhes
  internos na resposta.
- Logs DEVEM ser estruturados, com `tenantId`, `operatorId` e `correlationId` no escopo de toda
  requisição.
- Datas e horas DEVEM ser em UTC (`DateTime` com `Kind = Utc` ou `DateTimeOffset`; colunas
  `timestamptz`). A conversão para o fuso local é feita só na apresentação.
- Entidades editáveis DEVEM ter concorrência otimista (coluna `xmin` do Postgres como token de
  concorrência). Conflito de versão devolve `409`.
- Métricas e traces DEVEM ser emitidos com OpenTelemetry (latência, taxa de erro e volume por
  endpoint; tempo de banco e de Redis). O `tenantId` DEVE ir em traces e logs, mas NÃO DEVE ser
  dimensão de métrica: com até 150 mil tenants, isso explode a cardinalidade. Métricas por tenant
  só para uma lista explícita de tenants monitorados.
- **Backup e recuperação**:
  - Todas as bases (catálogo, compartilhadas e dedicadas) DEVEM ter backup automático com
    restauração para um ponto no tempo (point-in-time recovery).
  - DEVE existir um procedimento documentado e testado para restaurar **um único tenant** de uma
    base compartilhada sem afetar os outros: restaurar o backup numa base temporária e copiar as
    linhas daquele `tenant_id`, o que o Princípio I permite.
  - Os testes de restauração NÃO PODEM levar dados de produção para fora de produção (LGPD,
    Princípio X).
  - As metas de RPO (quanto dado se aceita perder) e RTO (em quanto tempo o sistema volta) são
    definidas na spec de ambientes, como decisão de negócio, e passam a ser verificadas.

## Fluxo de Desenvolvimento e Quality Gates

- Toda feature segue o fluxo do Spec Kit: `specify` → (`clarify`) → `plan` → `tasks` →
  (`analyze`) → `implement` → `converge`. A spec descreve a mudança, não o sistema inteiro.
- O `plan.md` DEVE passar no Constitution Check antes da pesquisa e de novo depois do design.
  Qualquer violação DEVE estar justificada em Complexity Tracking.
- O `plan.md` DEVE reutilizar a estrutura existente (Princípio III) e apontar o use case de
  referência mais parecido já implementado (ex.: `AccountContext/UseCases/Update`).
- No `plan.md`, os campos *Performance Goals*, *Constraints* e *Scale/Scope* DEVEM ser
  preenchidos com as metas e a escala do Princípio XIII (nunca com números inventados ou
  `NEEDS CLARIFICATION`).
- O `plan.md` DEVE ter uma seção "Segurança e dados pessoais" com: as permissões novas do
  catálogo e a matriz de autorização (endpoint × permissão), os casos de abuso considerados, os
  dados pessoais tocados e os eventos de auditoria gerados.
- Antes de abrir o PR, DEVEM passar (e o CI DEVE repetir no PR):
  - `dotnet build Backend.slnx` sem erros (em `src/Backend`);
  - `dotnet test Backend.slnx` (com Docker ligado para os testes de integração);
  - `npm run typecheck`, `npm test` e `npm run build` em `src/Frontend`, se o frontend mudou;
  - verificação de pacotes vulneráveis (`dotnet list package --vulnerable`, `npm audit`);
  - cenário k6 curto da spec, contra o ambiente local, quando a spec tocar um caminho crítico
    (Princípio XIII).
- Mudança de contrato da API (rota, campos, status) DEVE atualizar no mesmo trabalho: os
  comentários XML, `src/Frontend/src/types/api.ts` e a tabela de endpoints do
  `src/Frontend/README.md`.

### Branches, ambientes e entrega

- **Branches**: cada spec é desenvolvida na sua própria branch e entra na `main` só por pull
  request com o CI verde. A `main` DEVE estar sempre pronta para ir a produção, e NÃO DEVE
  existir outra branch de longa duração. Push direto na `main` é proibido (branch protection).
- **Ambientes**: local (`docker-compose`), **homologação** e **produção**, na AWS, com recursos
  e segredos separados por ambiente.
- **Construir uma vez**: cada merge na `main` gera as imagens de container uma única vez,
  versionadas pelo SHA do commit. A **mesma** imagem é promovida de homologação para produção,
  sem rebuild.
- **Homologação**:
  - recebe deploy automático a cada merge na `main`;
  - as migrations rodam antes da nova versão da aplicação (compatíveis, Princípio XIII);
  - depois do deploy rodam smoke tests e os cenários k6 completos com a carga de referência;
  - falha em qualquer um bloqueia a promoção para produção.
- **Dados fora de produção**: homologação e local DEVEM usar só dados sintéticos, gerados por
  script, com vários tenants em bases compartilhadas e ao menos uma dedicada. NUNCA DEVE existir
  cópia de dados de produção fora de produção (LGPD, Princípio X).
- **Produção**: merge na `main` NÃO altera produção. Produção recebe deploy só por aprovação
  manual (GitHub Environment com revisor), da imagem já validada em homologação, e a aprovação
  leva tudo o que entrou na `main` desde o último deploy. Rollback é reimplantar a versão anterior, o que só é
  possível porque as migrations são compatíveis com a versão anterior.
- **Infraestrutura como código**: todo recurso de nuvem DEVE ser criado e alterado por código
  versionado no repositório, nunca à mão no console da AWS. O CI se autentica na AWS por OIDC,
  sem chaves de acesso de longa duração.
- **Segredos**: por ambiente, no AWS Secrets Manager. Chaves de JWT, de criptografia de PIN e de
  HMAC DEVEM ser diferentes em cada ambiente.

## Governance

- Esta constituição prevalece sobre qualquer outro documento ou costume do repositório. Em
  conflito entre ela e o código existente, vale ela; o código legado é corrigido por spec
  própria, nunca copiado.
- Emendas são feitas pelo dono do projeto via `/speckit-constitution`, com a versão atualizada:
  MAJOR para remoção ou redefinição de princípio, MINOR para princípio ou seção nova, PATCH para
  redação.
- Revisões de código e o `/speckit-analyze` DEVEM verificar conformidade com esta constituição.
- **Integridade das entregas**: o código deste projeto é escrito por IA. Para fazer uma entrega
  passar, é PROIBIDO:
  - apagar, pular (`Skip`) ou enfraquecer um teste que falha;
  - afrouxar thresholds do k6 ou metas do Princípio XIII;
  - suprimir avisos de compilação, de análise ou de vulnerabilidade;
  - marcar uma tarefa do `tasks.md` como concluída sem rodar os quality gates;
  - editar esta constituição.

  Diante de uma falha, o agente DEVE corrigir a causa ou parar e reportar a falha com a saída
  real. Mudar um teste só é permitido quando a spec mudou o comportamento esperado, e isso DEVE
  estar explícito no `tasks.md`.
- Orientação operacional (como rodar, comandos) fica no `README.md` e no `CLAUDE.md`, não aqui.

### Legado conhecido — NÃO copiar

Trechos do código atual que violam esta constituição. Eles NÃO DEVEM servir de referência para
código novo; cada um é corrigido por spec própria.

- `Application/Configuration.cs` + `BuilderExtensions.AddConfiguration/AddDatabase`: connection
  string em campo estático (viola o Princípio I). Será substituído por um resolvedor de tenant
  na primeira spec.
- `TokenService`: o token ainda não carrega a claim `tenant` (viola o Princípio I). Entra na mesma
  spec do resolvedor de tenant.
- `Api/Migrations/20260908171522_SeedAdmin`: cria um admin com e-mail e senha fixos (viola o
  Princípio I). Continua só para desenvolvimento, até o provisionamento existir.
- Repositórios do `AccountContext`: `GetUserById` (devolve `Operator`), `CheckIfEmailIsNotUsed`
  (devolve `true` quando o e-mail está em uso), `GetOperatorList` e `GetUserByEmailAsync` (viola
  o Princípio III). Serão renomeados por spec de refactor.
- `Domain/Contexts/UserContext/Entities/User.Active()`: DEVE ser `Activate()` (viola o
  Princípio IV).
- `Application/Context/UserContext/Create/*`: handler vazio e `ResponseData` copiado do Operator.
  A spec do cadastro de usuário reescreve esse use case, reaproveitando só o domínio
  (`User`, `Name`, `Cpf`, `Rg`, `Telephone`, `PasswordAccess`).
- Textos de UI sem acento no frontend (ex.: `src/Frontend/src/lib/api-client.ts`).
- `Secrets:PasswordSaltKey`: lida na configuração e exigida no `docker-compose.yml`, mas não é
  usada pelo hash de senha. Será removida da configuração, do `.env.example` e do compose.
- `BuilderExtensions.AddForwardedHeaders`: limpa `KnownProxies`/`KnownIPNetworks`, então a Api
  aceita `X-Forwarded-For` de qualquer origem. Com a porta da Api exposta no
  `docker-compose.yml`, o rate limit do login é contornável (viola o Princípio IX).
- `BuilderExtensions.AddJwtAuthentication`: `ValidateIssuer`/`ValidateAudience = false`, sem
  checagem de tamanho de chave e sem versão de segurança no token (viola o Princípio IX).
- `Authenticate/Handler.cs`: quando o e-mail não existe, responde sem calcular o hash, o que
  permite descobrir e-mails pelo tempo de resposta (viola o Princípio IX).
- `AddRateLimiting`: limite só por IP, sem limite por conta nem por tenant, e guardado na memória
  da instância (viola os Princípios IX e XIII).
- `OperatorMap` e as tabelas atuais: sem coluna `tenant_id`, sem RLS, e o índice único de e-mail
  é global em vez de por tenant (viola o Princípio I).
- `Program.cs`: Swagger mapeado em qualquer ambiente (viola o Princípio IX).
- `MigrationExtensions.ApplyMigrations`: migrations no startup, exigindo DDL do usuário da
  aplicação (viola o Princípio IX).
- `PasswordAccess.Value`: PIN em texto puro (viola o Princípio IX).
- `src/Frontend/nginx.conf`: sem HSTS nem cabeçalhos de segurança (viola o Princípio IX).
- `Domain/Contexts/AccountContext/Enums/ERole.cs` + `[Authorize(Roles = "Admin")]` no
  `AccountContextController` + regra "último admin ativo" no `Deactivate`: roles fixas no código
  (viola o Princípio XII). A regra do último admin vira "último operador ativo com a role
  Administrador".
- `src/Frontend/src/components/RequireRole.tsx` e as rotas de `App.tsx`: acesso por nome de role
  (`Admin`/`Operator`) (viola os Princípios VIII e XII).
- `src/Frontend`: nenhum teste automatizado e nenhum workflow de CI (viola o Princípio VII).
- Entrega: não existem ambientes de homologação e produção, pipeline de deploy nem
  infraestrutura como código (o workflow do Azure App Service foi removido no commit `6dcb3e0`)
  (viola a seção "Branches, ambientes e entrega").

**Version**: 1.0.0 | **Ratified**: 2026-10-31 |
**Last Amended**: 2026-10-31
