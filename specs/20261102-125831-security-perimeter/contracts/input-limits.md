# Contrato: tamanho máximo na entrada da Api

**Spec**: FR-001 a FR-008 | **Plano**: Fase A | **Princípios**: IX ("Entrada"), V

## Regra

Todo campo de texto de todo `Request` tem um máximo, validado em `Specification.Ensure` **antes**
de qualquer verificação de formato, de hash ou de acesso ao banco.

- **Contagem**: caracteres Unicode (pontos de código), não bytes nem unidades UTF-16. Limite
  **inclusivo**: o texto com exatamente o máximo é aceito. `null` está dentro do limite.
- **Aplicação**: ao texto **como recebido** (antes de qualquer `Trim`).
- **Ordem**: todos os tamanhos primeiro; se algum falhar, a `Specification` devolve sem avaliar o
  formato. Uma notificação por campo excedido.

## Resposta

Mesma de qualquer recusa de validação (Princípio V):

```json
{
  "message": "Requisição inválida",
  "status": 400,
  "isSuccess": false,
  "notifications": [
    { "key": "Password", "message": "Senha deve ter no máximo 128 caracteres" }
  ],
  "data": null
}
```

- A `key` é o nome da propriedade do `Request` (a mesma chave das notificações de formato já
  existentes: `Email`, `Password`, …).
- A mensagem **não** ecoa o valor recebido.
- O `400` sai antes do limitador por conta e da verificação de senha: o login com senha enorme não
  calcula hash, não conta tentativa e não consulta o banco.

## Tabela de limites

| Rota (caso de uso) | Campo | Máx. | Mensagem |
|---|---|---|---|
| `POST v1/account/login` (Authenticate) | `Email` | 255 | E-mail deve ter no máximo 255 caracteres |
| | `Password` | 128 | Senha deve ter no máximo 128 caracteres |
| `POST v1/account/create` | `Email`, `Password` | 255, 128 | idem |
| `PUT v1/account/update/{id}` | `Email`, `Password` (se informados) | 255, 128 | idem |
| `PUT v1/account/update-me` | `Email`, `Password` (se informados) | 255, 128 | idem |
| *(não exposto)* `UserContext/Create` | `Name` | 100 | Campo nome deve ter no máximo 100 caracteres *(existente)* |
| | `Observation` | 100 | Campo observação deve ter no máximo 100 caracteres *(existente)* |
| | `Telephone` | 20 | Telefone deve ter no máximo 20 caracteres |
| | `Rg` | 20 | RG deve ter no máximo 20 caracteres |
| | `Cpf` | 14 | CPF deve ter no máximo 14 caracteres |
| | `Password` (PIN) | 6 | Senha deve ter no máximo 6 caracteres *(a regra de PIN pede 4 a 6 dígitos)* |

Nota de compatibilidade (Princípio VI): nenhum campo, rota nem status existente muda; é uma nova
recusa `400` para entrada que antes era aceita (ou, no login, recusada com `401` depois de custo
alto). O console é o único cliente do login.

## Testes que garantem o contrato

| Teste | O que prova |
|---|---|
| `…SpecificationTests` de cada use case | `exatamente no limite` passa; `+1` falha com a notificação do campo; vários campos excedidos → uma notificação por campo; texto acima do limite **e** de formato inválido → só a de tamanho |
| `ContractExtensionsTests` | contagem por pontos de código (acentos, emojis), inclusividade, `null`, curto-circuito |
| `RequestTextLimitsTests` (reflexão) | todo campo `string` de todo `Request` gera notificação com texto de 100 000 caracteres; `Request` sem `Specification` falha |
| `AuthenticateHandlerTests` | senha de 1 MB → `400`; `IPasswordVerifier`, `IRepository` e `ILoginThrottle` **não** são chamados |
| `InputLimitsApiTests` | os 4 endpoints com texto devolvem `400` + "Requisição inválida" + notificação do campo; login com senha de exatamente 128 → não é `400` |
