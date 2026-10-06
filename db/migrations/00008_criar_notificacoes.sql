-- +goose Up
-- Outbox: a API grava a linha na mesma transação do agendamento e o worker envia depois.
CREATE TABLE notificacoes (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  agendamento_id      uuid NOT NULL,
  tipo                text NOT NULL,
  -- Reagendou: versao + 1, e o lembrete novo não bate na UNIQUE do antigo.
  versao              int  NOT NULL DEFAULT 1,
  status              text NOT NULL DEFAULT 'pendente',
  agendar_para        timestamptz,        -- NULL = enviar imediatamente
  tentativas          int  NOT NULL DEFAULT 0,
  -- Backoff: sem ela, o worker reprocessa a falha em todo ciclo e queima o rate limit.
  proxima_tentativa   timestamptz,
  enviado_em          timestamptz,
  telegram_message_id bigint,
  erro                text,
  criado_em           timestamptz NOT NULL DEFAULT now(),
  -- CASCADE, ao contrário de agendamentos: notificação sem agendamento é órfã.
  FOREIGN KEY (agendamento_id, tenant_id)
    REFERENCES agendamentos (id, tenant_id) ON DELETE CASCADE,
  CONSTRAINT tipo_valido   CHECK (tipo   IN ('confirmacao', 'lembrete', 'cancelamento')),
  CONSTRAINT status_valido CHECK (status IN ('pendente', 'enviado', 'falhou', 'descartado')),
  -- Idempotência (RNF03): o mesmo aviso não entra duas vezes na fila.
  CONSTRAINT notificacao_unica UNIQUE (agendamento_id, tipo, versao)
);

-- Parcial: enviadas saem do índice, que fica pequeno para sempre.
CREATE INDEX idx_notificacoes_fila
  ON notificacoes (agendar_para NULLS FIRST)
  WHERE status = 'pendente';

-- O Postgres não indexa a coluna que referencia: sem este índice, descartar as
-- pendentes de um agendamento e o CASCADE da exclusão de tenant varrem a tabela.
CREATE INDEX idx_notificacoes_agendamento
  ON notificacoes (agendamento_id, tenant_id);

-- +goose Down
DROP TABLE notificacoes;
