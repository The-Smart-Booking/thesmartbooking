-- WHERE tenant_id explícito em toda query, mesmo com RLS: é a camada (b) de
-- defesa (requisitos §6.2). Cliente removido (exclusão lógica) não aparece.
-- Leitura sem vinculo_token: é credencial (guia, §004); só o CriarCliente devolve.

-- name: ListarClientes :many
SELECT id, tenant_id, nome, telefone, telegram_chat_id, vinculo_expira_em,
       vinculado_em, notificavel, criado_em
FROM clientes
WHERE tenant_id = $1 AND removido_em IS NULL
ORDER BY nome;

-- name: BuscarCliente :one
SELECT id, tenant_id, nome, telefone, telegram_chat_id, vinculo_expira_em,
       vinculado_em, notificavel, criado_em
FROM clientes
WHERE tenant_id = $1 AND id = $2 AND removido_em IS NULL;

-- name: CriarCliente :one
INSERT INTO clientes (tenant_id, nome, telefone)
VALUES ($1, $2, $3)
RETURNING *;
