# Contrato: mensagens da API corrigidas

**Feature**: [spec.md](../spec.md) | **Plano**: [plan.md](../plan.md)

Fase **C**. Três textos de `message` mudam **só na grafia**; o resto da resposta (status,
`isSuccess`, `notifications`, `data`) é idêntico. Caminhos de produção relativos a `src/Backend/`;
os de teste, a `src/Backend/AccessControlSystem.Tests/`.

## 1. As três mensagens

| # | Endpoint | Status | Antes | Depois | Handler (linha de 2026-11-01) |
|---|---|---|---|---|---|
| 1 | `PUT v1/account/update-me` | `400` (validação) | `Requsição inválida` | `Requisição inválida` | `Application/Context/AccountContext/UseCases/UpdateMe/Handler.cs:22` |
| 2 | `PUT v1/account/update-me` | `200` | `Usuario atualizado com sucesso` | `Usuário atualizado com sucesso` | `UpdateMe/Handler.cs:58` |
| 3 | `PUT v1/account/update-me` e `GET v1/account/get-me` | `401` (operador não encontrado) | `Sessão invalida - faça o login novamente` | `Sessão inválida - faça o login novamente` | `UpdateMe/Handler.cs:30` e `GetMe/Handler.cs:24` |

Total: **4 linhas** de produção em 2 arquivos. O `400` do update-me passa a ser igual ao dos demais
endpoints (`Create`, `Update`, `Get`, etc. já devolvem `Requisição inválida`).

## 2. Como cada texto é protegido por teste (TDD: o teste muda primeiro e falha)

| # | Teste | Mudança | Motivo na tabela do `tasks.md` |
|---|---|---|---|
| 1 | `Api/ApiContractCompatTests.cs`, cenário `"update-me: 400 nenhum campo informado"` | expectativa da linha ~159: `"Requsição inválida"` → `"Requisição inválida"`; o comentário da linha ~158 (que chama o erro de "contrato de hoje") passa a registrar que a grafia foi corrigida pela spec 03 | texto esperado mudou por esta spec |
| 2 | `ApiContractCompatTests.cs`, cenário `"update-me: 200 conta atualizada"` | expectativa da linha ~153: `"Usuario atualizado com sucesso"` → `"Usuário atualizado com sucesso"` | texto esperado mudou por esta spec |
| 3a | `Application/AccountContext/UpdateMe/UpdateMeHandlerTests.cs`, `Sessao_sem_operador_correspondente_devolve_401` | **asserção acrescentada**: `Assert.Equal("Sessão inválida - faça o login novamente", response.Message)` depois da asserção de status | asserção acrescentada pelo FR-021 |
| 3b | `Application/AccountContext/GetMe/GetMeHandlerTests.cs`, `Operador_nao_encontrado_devolve_401` | a mesma asserção acrescentada | asserção acrescentada pelo FR-021 |

**Exatamente duas expectativas** do teste de contrato mudam (SC-005): as linhas ~153 e ~159. Nenhum
outro cenário daquela classe muda. Nenhum teste **novo** é criado para o `401`: os dois casos já
existem e ganham a asserção do texto (que hoje ninguém afirma).

Red esperado antes de tocar nos handlers: **4 falhas** (2 cenários do teste de contrato, que é de
integração e precisa de Docker, e os 2 testes de handler). Green depois das 4 linhas: todos passam.

## 3. Invariantes do contrato (não podem mudar)

| Item | Estado |
|---|---|
| Rotas, métodos HTTP, status, campos JSON (`message`, `status`, `isSuccess`, `notifications`, `data`) | idênticos |
| `data` do `200` do update-me (`id`, `email`) e do get-me | idênticos |
| `notifications` do `400` do update-me | idênticas (vêm da `Specification`) |
| Comentários XML dos endpoints (Swagger) | **não citam** nenhuma das três mensagens (verificado em 2026-11-01): nada a atualizar |
| `src/Frontend/src/types/api.ts` e tabela de endpoints do `src/Frontend/README.md` | **não citam** as mensagens: nada a atualizar |
| `"Email já está em uso"` (`409` de Create, Update e UpdateMe) | **não muda** (divergência de redação com o `E-mail` dos repositórios é o achado F-02, não de acento) |
| Demais mensagens (`Conta não encontrada`, `Credenciais inválidas`, etc.) | já corretas; não se tocam |

## 4. Compatibilidade de contrato (Princípio VI)

Texto de mensagem não é "remover ou renomear campo, mudar tipo, status ou rota", então **não exige
`v2`**. Quem consome os dois endpoints é só o console, que exibe a `message` como veio e, no `401`,
decide pelo **status** (`api-client.ts`: `response.status === 401` aciona o encerramento de sessão),
não pelo texto. Aparelhos e integrações externas não usam `get-me` nem `update-me`.
