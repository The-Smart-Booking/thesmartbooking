# Formato de erro da API

Toda resposta de erro (status 4xx/5xx) tem `Content-Type: application/json` e este corpo:

```json
{
  "erro": {
    "codigo": "conflito_horario",
    "mensagem": "Já existe um agendamento nesse horário.",
    "detalhes": { "prestador_id": "5f0c2a9e-3b7d-4c1e-9a8f-2d6b1e7c4a90" }
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

## Códigos previstos

Decididos em 02/10/2026; ainda não existem em `internal/api/erros.go`. Cada um
sobe para a tabela de cima, junto com a constante no `erros.go`, no PR do card
que o introduz.

| Status | `codigo`           | Quando | Entra em |
|--------|--------------------|--------|----------|
| 401    | `nao_autenticado`  | Sem sessão válida: cookie ausente, expirado ou revogado. | 2.5 |
| 403    | `sem_permissao`    | Agendamento de outro prestador do mesmo tenant; `prestador_id` de outro pedido por um prestador; rota só de administrador; remover o criador da empresa; administrador que não é o criador removendo administrador. | 2.7 (remoções: 5.8) |
| 409    | `status_invalido`  | Transição de status inválida: concluir antes do início; cancelar, remarcar ou trocar o serviço de agendamento cancelado ou concluído. | 5.1 |
| 409    | `convite_invalido` | Convite expirado ou já aceito, ou para e-mail que já tem membership ativo no tenant. | 2.18, 2.19 |

`conflito_horario` passa a valer também no `PATCH` que remarca ou troca o serviço
(5.1), que dispara a constraint de sobreposição de novo.

Agendamento de **outro tenant** continua `404 nao_encontrado` (o RLS nem deixa
ver); de outro prestador do **mesmo** tenant, para quem não é administrador,
`403 sem_permissao`. Token de convite inexistente também é 404.

## Cliente HTTP (1.9)

- Resposta não-2xx: ler `erro.codigo` e `erro.mensagem`.
- Corpo fora desse formato com status 502/503/504: tratar como erro de rede (`ErroRede`),
  porque é o proxy sem alcançar a API (o do Vite responde 502 com ela fora do ar).
- Corpo fora desse formato com outro status (HTML de erro etc.): tratar como `erro_interno`.
- Falha do próprio `fetch` (sem resposta): erro de rede (`ErroRede`).
- Código desconhecido: exibir `mensagem`.

## Backend

Em `internal/api`:

```go
responderErro(w, http.StatusNotFound, CodigoNaoEncontrado, "Cliente não encontrado.", nil)
responderErroInterno(w, r, err) // loga err, responde 500 genérico
```

Nunca passe `err.Error()` como mensagem: erros do Postgres/pgx vão só para o log.
