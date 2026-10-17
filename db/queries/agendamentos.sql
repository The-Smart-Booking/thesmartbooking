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

-- Uma instrução só (1.5): valor_centavos copiado do serviço, nunca do payload;
-- prestador só com membership ativo e serviço só ativo, senão zero linhas. Sem fim,
-- inicio + duração do serviço. Sobreposição fica com a constraint sem_sobreposicao
-- (23P01): checar antes em Go abre condição de corrida.
-- name: CriarAgendamento :one
INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
SELECT s.tenant_id, @cliente_id::uuid AS cliente_id, s.id, m.usuario_id,
       sqlc.arg('inicio')::timestamptz AS inicio,
       COALESCE(sqlc.narg('fim')::timestamptz,
                sqlc.arg('inicio')::timestamptz + s.duracao_minutos * interval '1 minute') AS fim,
       s.preco_centavos
FROM servicos s
JOIN memberships m
  ON m.tenant_id = s.tenant_id AND m.usuario_id = @prestador_id AND m.removido_em IS NULL
WHERE s.tenant_id = @tenant_id AND s.id = @servico_id AND s.ativo
RETURNING *;
