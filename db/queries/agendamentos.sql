-- WHERE tenant_id explícito em toda query, mesmo com RLS: é a camada (b) de
-- defesa (requisitos §6.2).

-- Período pelo inicio, [de, ate). prestador_id nulo = a empresa toda (guia,
-- §Escopo por prestador).
-- Com o nome do cliente, para a tela não fazer uma chamada por linha (1.6).
-- name: ListarAgendamentos :many
SELECT a.*, c.nome AS cliente_nome
FROM agendamentos a
JOIN clientes c ON c.id = a.cliente_id AND c.tenant_id = a.tenant_id
WHERE a.tenant_id = @tenant_id
  AND a.inicio >= @de AND a.inicio < @ate
  AND (sqlc.narg('prestador_id')::uuid IS NULL OR a.prestador_id = sqlc.narg('prestador_id'))
ORDER BY a.inicio;

-- name: BuscarAgendamento :one
SELECT * FROM agendamentos
WHERE tenant_id = $1 AND id = $2;
