# Checklist de Qualidade da Especificação: Base de entrega sem nuvem (proteção da principal, verificações confiáveis, saúde da Api e dados de homologação)

**Propósito**: Validar a completude e a qualidade da especificação antes de seguir para o planejamento

**Criado em**: 2026-11-02

**Feature**: [spec.md](../spec.md)

## Qualidade do Conteúdo

- [X] Sem detalhes de implementação (linguagens, frameworks, APIs)
- [X] Focada em valor para o usuário e necessidades do negócio
- [X] Escrita para partes interessadas não técnicas
- [X] Todas as seções obrigatórias preenchidas

## Completude dos Requisitos

- [X] Nenhum marcador [NEEDS CLARIFICATION] restante
- [X] Requisitos testáveis e sem ambiguidade
- [X] Critérios de sucesso mensuráveis
- [X] Critérios de sucesso agnósticos de tecnologia (sem detalhes de implementação)
- [X] Todos os cenários de aceitação definidos
- [X] Casos de borda identificados
- [X] Escopo claramente delimitado
- [X] Dependências e premissas identificadas

## Prontidão da Feature

- [X] Todos os requisitos funcionais têm critérios de aceitação claros
- [X] Os cenários de usuário cobrem os fluxos principais
- [X] A feature atende aos resultados mensuráveis definidos nos Critérios de Sucesso
- [X] Nenhum detalhe de implementação vaza para a especificação

## Notas

- **Validação feita em 2026-11-02 e refeita no mesmo dia depois de 2 ajustes do dono** (o
  administrador fixo é identificado por qualquer dado fixo; os ensaios das proteções só depois da
  conferência) **e depois do `/speckit-clarify`**: 0 marcadores `[NEEDS CLARIFICATION]`, 0
  placeholders do template, FR-001 a FR-039 mais FR-005a, FR-013a e FR-037a (42 requisitos) e
  SC-001 a SC-016 sem repetição, 8 histórias. Os IDs com sufixo seguem o precedente da 04b
  (`FR-020a`), para não renumerar. Todos os requisitos têm ao menos um cenário de aceitação ou
  critério de sucesso que os prova (FR-005a com a História 1 cenário 6 e SC-016; FR-013a com a
  História 2 cenário 10 e SC-014; FR-037 e FR-037a com a História 8 e SC-015). O FR-023 (comentário
  de documentação nos endpoints de saúde) é conferido por inspeção na revisão.
- **Origem**: esta spec é a 05a, a metade sem nuvem da antiga spec 05 (dividida em 2026-11-02 por
  causa do custo da AWS). A spec gerada antes do corte foi descartada e está guardada fora do
  repositório (`05b-referencia-spec-rascunho.md`) como referência para a 05b.
- **Termos que aparecem por serem o próprio objeto do pedido ou da constituição**: pull request,
  branch principal, `/health/live` e `/health/ready` (nomes de contrato da Api, seção Operação),
  Migrator (componente da spec 01) e a migration de desenvolvimento (`SeedAdmin` é citada só pelo
  papel, não pelo nome). Nomes de ferramentas de CI, de scripts de proteção e de bibliotecas de
  teste **não** aparecem no corpo da spec: ficam para o `plan.md`. O GitHub só é citado no
  contexto e nas Premissas.
- **Nenhum marcador foi usado de propósito**: as especificações anteriores deixaram os pontos em
  aberto como padrões nas Premissas, para o `/speckit-clarify` confirmar. **Clarify de 2026-11-02
  concluído** (seção "Clarificações" da spec, 10 respostas): 6 padrões confirmados pelo dono
  (proteção por script mais guia, **usando rulesets**; dados fictícios só em `Staging`; verificação
  do administrador no Migrator, recusada em desenvolvimento; aviso pelo e-mail do GitHub; branch sem
  precisar estar atualizada; 2 segundos para a "pronta") e 4 perguntas respondidas (o que conta
  como documentos: `specs/`, `.md`, `.specify/` e `.claude/`; verificações também sobre a principal
  depois do aceite; varredura de segredos como proteção; conferência das proteções à mão e também
  automática, sem credencial guardada).
- **Itens que o `plan.md` deve conferir** (verificados em 2026-11-02 no código):
  - o `seed-dev` e a migration `SeedAdmin` usam o **mesmo e-mail** (`admin@admin.com.br`); a
    migration também fixa o identificador e a senha. A verificação (FR-031) reprova se **qualquer**
    dado fixo bater (o identificador **ou** o e-mail, sem diferenciar maiúsculas de minúsculas), e o
    comando de homologação não pode usar esse e-mail (FR-028); o plano define onde o e-mail é
    normalizado na comparação e cobre os casos do FR-035 (só o e-mail alterado; só o identificador
    trocado);
  - `SeedAdminPolicy` do Migrator só impede base **nova** de receber o administrador (marca a
    migration como aplicada quando a base não tem histórico); a nova verificação é independente
    dela e pega base antiga ou alterada à mão;
  - `backend-ci.yml` e `frontend-ci.yml` têm filtro de `paths` e rodam em `push` e `pull_request`;
    nenhum tem agenda. A verificação agregadora precisa tratar "pulada" como reprovação (FR-009) e
    ter um nome estável, porque a proteção exige a verificação por nome (caso de borda);
  - o segundo ponto de risco é um pull request que altera o próprio arquivo da verificação
    agregadora (limite aceito nas Premissas);
  - a ordem de ativação: a proteção só é ligada depois de a verificação agregadora existir na
    principal, e a Api e os workflows desta spec entram antes, por pull request comum;
  - os ensaios de envio direto, envio forçado e exclusão da principal (SC-001) só rodam depois de a
    conferência das proteções (FR-037) confirmar que estão ativas; o guia traz o cuidado (SHA da
    principal anotado antes, mudança desfazível no envio forçado), e o plano define como a
    conferência e o ensaio se encadeiam sem alterar a principal de verdade;
  - conferência automática (FR-037/FR-037a): o plano confirma **pela documentação do GitHub** que o
    endpoint de regras da branch funciona só com a permissão de leitura do `GITHUB_TOKEN` (em
    2026-11-02 ele devolveu HTTP 200 sem nenhuma credencial; hoje a lista é vazia, então o formato
    da resposta com regras ainda precisa ser visto); o workflow roda só em agenda e sob demanda,
    nunca por pull request, com `permissions` mínimas e sem segredo; a lógica de comparação é
    testada com respostas simuladas (regra ausente, nome trocado, envio forçado liberado);
  - o limite de 60 dias de inatividade de repositório público passa a afetar **dois** workflows
    agendados (auditoria e conferência), e a conferência automática depende de o repositório ser
    público (se voltar a privado, precisa de credencial e deve ser reavaliada);
  - a varredura de segredos com bloqueio no envio (FR-005a) é configuração da plataforma, fora do
    alcance do endpoint de regras: entra no script e na conferência à mão;
  - `TrustedProxyOptions` já aceita `KnownProxies` e `KnownNetworks`: a 05b só configura, esta spec
    não mexe;
  - o comando de dados fictícios deve espelhar o desenho do `seed-dev` (três tenants: dois em
    `shared_01`, um em `dedicated_01`), com identificadores estáveis próprios de homologação;
  - os health checks são anônimos e exigem justificativa da exceção ao fallback de autenticação
    (Princípio IX) e aos testes de autorização 401/403 (Princípio VII) na seção "Segurança e dados
    pessoais" do plano.
