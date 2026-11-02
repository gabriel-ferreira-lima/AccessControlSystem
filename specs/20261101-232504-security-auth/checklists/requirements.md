# Checklist de Qualidade da Especificação: Segurança da autenticação e da sessão

**Propósito**: Validar a completude e a qualidade da especificação antes de seguir para o planejamento

**Criado em**: 2026-11-01

**Feature**: [spec.md](../spec.md)

## Qualidade do Conteúdo

- [X] Sem detalhes de implementação (linguagens, frameworks, APIs)
- [X] Focada no valor para o usuário e nas necessidades do negócio
- [X] Escrita para partes interessadas não técnicas
- [X] Todas as seções obrigatórias preenchidas

## Completude dos Requisitos

- [X] Nenhum marcador [NEEDS CLARIFICATION] restante
- [X] Requisitos testáveis e sem ambiguidade
- [X] Critérios de sucesso mensuráveis
- [X] Critérios de sucesso independentes de tecnologia (sem detalhes de implementação)
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

- Os nomes `X-Forwarded-For`, Swagger, docker compose e nginx aparecem porque são o próprio
  assunto pedido (o que o atacante forja, o que fica exposto e onde); a spec descreve o
  comportamento exigido, não como implementá-lo. Como o token passa a ser invalidado (marca de
  validade da sessão), a forma de armazenar e de contar ficam para o `plan.md`.
- Nenhum marcador [NEEDS CLARIFICATION] foi aberto. A sessão de `/speckit-clarify` de 2026-11-01
  confirmou: regra do bloqueio por conta (5 falhas, espera de 1 a 15 minutos), troca de e-mail não
  derruba o token, console encerra a sessão depois da troca da própria senha e edição sem mudança
  efetiva não invalida a sessão. Tolerância de relógio (30 s) e chave mínima (32 bytes) ficaram
  como estavam nas Premissas.
- O SC-004 tem duas partes: a prova automatizada (a verificação de senha roda nos três casos de
  falha e no sucesso, e o hash fictício tem o mesmo algoritmo e custo de um hash real) e a medição
  de tempo (mediana com diferença de até 10%), feita na validação ao vivo e registrada no
  `quickstart.md`, fora do CI.
- Ajustes de 2026-11-01 depois do clarify: reativação não revalida tokens antigos (FR-006, cenário
  4a), senha informada sempre invalida e só a senha em branco não (FR-004, FR-007, cenário 5a) e
  descarte do registro de falhas após 15 minutos (FR-032).
- Violações temporárias do Princípio XIII registradas nas Premissas (limite em memória e ausência de
  cenário k6) devem ir para Complexity Tracking do `plan.md`.
