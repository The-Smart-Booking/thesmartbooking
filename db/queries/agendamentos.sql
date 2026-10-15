-- WHERE tenant_id explícito em toda query, mesmo com RLS: é a camada (b) de
-- defesa (requisitos §6.2).

-- Período pelo inicio, [de, ate). prestador_id nulo = a empresa toda (guia,
-- §Escopo por prestador).
-- name: ListarAgendamentos :many
SELECT * FROM agendamentos
WHERE tenant_id = @tenant_id
  AND inicio >= @de AND inicio < @ate
  AND (sqlc.narg('prestador_id')::uuid IS NULL OR prestador_id = sqlc.narg('prestador_id'))
ORDER BY inicio;

-- name: BuscarAgendamento :one
SELECT * FROM agendamentos
WHERE tenant_id = $1 AND id = $2;
