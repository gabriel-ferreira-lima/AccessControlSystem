# Checklist de Qualidade da Especificação: Segurança do perímetro (console web e entrada de dados da Api)

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

- Os nomes de mecanismos de segurança (política de conteúdo, HTTPS obrigatório, RLS) aparecem
  porque são o próprio objeto do pedido, não uma escolha de implementação. Nomes de ferramentas
  (servidor web, gerenciador de pacotes, framework de testes) ficam para o `plan.md`.
- Sessão de esclarecimentos de 2026-11-02 aplicada (decisões do Gabriel, registradas na seção
  "Clarificações" da spec): HSTS emitido por `X-Forwarded-Proto` = `https` sem verificar a origem
  (FR-017 removido); SC-003 sem critério de tempo, provado por teste que o hash e o banco não são
  chamados; provas do usuário de banco divididas por base (FR-019 a FR-021, nova FR-020a do
  catálogo só leitura, US4 cenário 7 e SC-008); exceções de vulnerabilidade conferidas na revisão
  do PR, sem validação automática da justificativa (FR-026) e sem regra de vencimento.
- Pontos ainda decididos por padrão (Premissas, revisáveis no plano): limites dos campos do
  cadastro de pessoas; severidade da verificação de vulnerabilidades (qualquer); validade do HTTPS
  obrigatório (1 ano, sem `preload` e sem subdomínios); tratamento da senha acima do limite no
  login (`400`).
- Itens do "Estado atual do código" que o `plan.md` deve conferir: testes de banco já existentes
  em `SchemaContractTests` e `RowLevelSecurityTests` (reaproveitar, FR-022); `NoWarn NU1903` no
  projeto de testes (migrar para exceção explícita, FR-027).
