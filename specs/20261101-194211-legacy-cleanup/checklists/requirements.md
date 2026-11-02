# Checklist de Qualidade da Especificação: Limpeza de legado

**Propósito**: Validar a completude e a qualidade da especificação antes de avançar para o planejamento
**Criada em**: 2026-11-01
**Feature**: [spec.md](../spec.md)

## Qualidade do Conteúdo

- [X] Sem detalhes de implementação (linguagens, frameworks, APIs)
- [X] Focada no valor para o usuário e nas necessidades do negócio
- [X] Escrita para stakeholders não técnicos
- [X] Todas as seções obrigatórias preenchidas

## Completude dos Requisitos

- [X] Nenhum marcador [NEEDS CLARIFICATION] restante
- [X] Requisitos testáveis e sem ambiguidade
- [X] Critérios de sucesso mensuráveis
- [X] Critérios de sucesso agnósticos de tecnologia (sem detalhes de implementação)
- [X] Todos os cenários de aceitação definidos
- [X] Casos limite identificados
- [X] Escopo claramente delimitado
- [X] Dependências e premissas identificadas

## Prontidão da Feature

- [X] Todos os requisitos funcionais têm critérios de aceitação claros
- [X] Os cenários de usuário cobrem os fluxos principais
- [X] A feature atende aos resultados mensuráveis definidos nos Critérios de Sucesso
- [X] Nenhum detalhe de implementação vaza para a especificação

## Notas

- Validação em 1 iteração; nenhum item reprovado.
- Esta spec é de natureza interna (refactor de alinhamento): os "usuários" são quem mantém o
  código, quem opera o console e quem sobe o ambiente. Os **nomes de método** (`GetOperatorByIdAsync`,
  `Activate()`...), os textos de tela e o nome da configuração removida são o **objeto** da
  mudança, ou seja, escopo e contrato, e não escolha de implementação; por isso aparecem na spec.
  Como fazer a renomeação (ordem das tarefas, ferramentas) fica para o `plan.md`.
- O inventário A (nomes) e o inventário B (textos) foram levantados no código de 2026-11-01 e
  declarados como **ponto de partida**: a varredura completa (FR-005 e FR-009) pode ampliá-los.
- Pontos do roteiro (`03-legacy-cleanup.md`, "Pontos para o clarify") resolvidos no
  `/speckit-clarify` de 2026-11-01 (ver "Clarificações" na spec):
  - **Texto do backend sem acento**: a conferência encontrou 2 além do "Requsição inválida"
    ("Sessão invalida - faça o login novamente" em minha conta e atualizar a própria conta;
    "Usuario atualizado com sucesso" em atualizar a própria conta). Decisão: **os dois também são
    corrigidos**, só na grafia (FR-012); o teste de contrato muda 2 expectativas (FR-013).
  - **Achados A-11, A-12 e A-14 da spec 02**: mudam comportamento, então **ficam de fora**; só o
    A-04 (acentuação) é desta spec.
- Decisões de escopo: `SaveAsync` (criar, confirmado no clarify) e `IsLastActiveAdminAsync`
  (desativar) já seguem a letra da convenção e não são renomeados; o registro histórico das specs
  anteriores e a constituição não são reescritos; as entradas do "Legado conhecido" que esta spec
  resolve saem da lista por emenda do dono, depois do merge.
- Teste novo previsto: só o do FR-007 (`User.Active()` não tem chamador nem teste hoje, então a
  renomeação para `Activate()` ganha um teste de caracterização). O FR-021 não cria teste: acrescenta
  asserções a 4 testes existentes (o `401` de minha conta e de atualizar a própria conta, o texto de
  apoio do login e o cabeçalho "Ações" da lista), todas escritas antes e vistas falhando.
