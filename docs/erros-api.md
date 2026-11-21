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
| 400    | `requisicao_invalida` | JSON malformado, corpo acima de 1 MiB, campo obrigatório ausente, valor inválido ou campo desconhecido no corpo. Exceção: `POST /api/publico/{slug}/agendamentos` ignora campo desconhecido, e nada além dos cinco campos da rota chega ao banco. |
| 401    | `nao_autenticado`     | Sem sessão válida: cookie ausente, expirado ou revogado. Por enquanto só no `POST /api/sessao/empresa`; nas demais rotas, com o middleware (2.5). |
| 401    | `credencial_invalida` | Login com e-mail inexistente, senha errada ou sem membership ativo; mesma mensagem nos três casos. |
| 404    | `nao_encontrado`      | Recurso não existe (ou não pertence ao tenant), ou rota inexistente. |
| 405    | `metodo_nao_permitido` | Rota existe, mas não com esse método. O cabeçalho `Allow` lista os métodos aceitos. |
| 409    | `conflito_horario`    | Agendamento sobrepõe outro (SQLSTATE `23P01`). |
| 409    | `email_em_uso`        | Cadastro ou criar conta pelo convite (2.22) com e-mail que já tem conta. |
| 409    | `slug_em_uso`         | Cadastro com slug de empresa já usado. |
| 429    | `muitas_tentativas`   | Acima do limite de requisições. Rotas públicas: 60 leituras e 10 envios por minuto, por IP. Login (entra na 2.8): 5 falhas por e-mail ou 20 por IP em 15 minutos. Cadastro: 5 por hora por IP. Cabeçalho `Retry-After` em segundos. |
| 500    | `erro_interno`        | Falha inesperada. Mensagem sempre genérica; o detalhe fica só no log do servidor. |

Código novo entra aqui e em `src/Api/Erros.cs` no mesmo PR.

## Códigos previstos

Decididos em 02/10/2026 e 23/10/2026; ainda não existem em `src/Api/Erros.cs`. Cada um
sobe para a tabela de cima, junto com a constante no `Erros.cs`, no PR do card
que o introduz.

| Status | `codigo`           | Quando | Entra em |
|--------|--------------------|--------|----------|
| 403    | `sem_permissao`    | Agendamento de outro prestador do mesmo tenant; `prestador_id` de outro pedido por um prestador; rota só de administrador; remover o criador da empresa; administrador que não é o criador removendo administrador; aceite de convite com sessão de outro e-mail. | 2.7 (aceite: 2.19; remoções: 5.8) |
| 409    | `status_invalido`  | Transição de status inválida: concluir antes do início; cancelar, remarcar ou trocar o serviço de agendamento cancelado ou concluído. | 5.1 |
| 409    | `convite_invalido` | Convite expirado ou já aceito, ou para e-mail que já tem membership ativo no tenant. | 2.18, 2.19, 2.22 |

`conflito_horario` passa a valer também no `PATCH` que remarca ou troca o serviço
(5.1), que dispara a constraint de sobreposição de novo.

Agendamento de **outro tenant** continua `404 nao_encontrado` (o RLS nem deixa
ver); de outro prestador do **mesmo** tenant, para quem não é administrador,
`403 sem_permissao`. Token de convite inexistente também é 404.

## Cliente HTTP (1.9)

- Resposta não-2xx: ler `erro.codigo` e `erro.mensagem`.
- Corpo fora desse formato com status 502/503/504: tratar como erro de rede (`ErroRede`),
  porque é o proxy sem alcançar a API (o do `ng serve` responde 502 com ela fora do ar).
- Corpo fora desse formato com outro status (HTML de erro etc.): tratar como `erro_interno`.
- Requisição sem resposta (status 0 no `HttpClient`): erro de rede (`ErroRede`).
- Código desconhecido: exibir `mensagem`.

## Backend

Em `src/Api/Erros.cs`:

```csharp
return Erros.Responder(StatusCodes.Status404NotFound, Erros.NaoEncontrado, "Cliente não encontrado.");
return Erros.CampoInvalido("nome", "Informe o nome (até 200 caracteres)."); // 400 com detalhes.campo
Erros.Interno(ctx, erro); // loga o erro, responde 500 genérico
```

O handler não precisa capturar exceção para responder 500: o middleware de transação
(`src/Api/Transacao.cs`) captura, desfaz a transação e responde com `Erros.Interno`.

Nunca passe `e.Message` como mensagem: erros do Postgres/Npgsql vão só para o log.
