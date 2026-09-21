# Formato de erro da API

Toda resposta de erro (status 4xx/5xx) tem `Content-Type: application/json` e este corpo:

```json
{
  "erro": {
    "codigo": "conflito_horario",
    "mensagem": "Já existe um agendamento nesse horário.",
    "detalhes": { "profissional_id": 7 }
  }
}
```

| Campo      | Tipo   | Obrigatório | Uso |
|------------|--------|-------------|-----|
| `codigo`   | string | sim | Identificador estável. **O frontend decide o comportamento por ele.** |
| `mensagem` | string | sim | Texto em português, pode ser exibido ao usuário. Pode mudar sem aviso — não compare. |
| `detalhes` | objeto | não | Contexto extra, depende do código. Ausente quando não há. |

## Códigos

| Status | `codigo`              | Quando |
|--------|-----------------------|--------|
| 400    | `requisicao_invalida` | JSON malformado, campo obrigatório ausente, valor inválido. |
| 404    | `nao_encontrado`      | Recurso não existe (ou não pertence ao tenant). |
| 409    | `conflito_horario`    | Agendamento sobrepõe outro (SQLSTATE `23P01`). |
| 500    | `erro_interno`        | Falha inesperada. Mensagem sempre genérica; o detalhe fica só no log do servidor. |

Código novo entra aqui e em `internal/api/erros.go` no mesmo PR.

## Cliente HTTP (1.9)

- Resposta não-2xx: ler `erro.codigo` e `erro.mensagem`.
- Corpo fora desse formato (proxy, rede, HTML de erro): tratar como `erro_interno`.
- Código desconhecido: exibir `mensagem`.

## Backend

Em `internal/api`:

```go
responderErro(w, http.StatusNotFound, CodigoNaoEncontrado, "Cliente não encontrado.", nil)
responderErroInterno(w, r, err) // loga err, responde 500 genérico
```

Nunca passe `err.Error()` como mensagem: erros do Postgres/pgx vão só para o log.
