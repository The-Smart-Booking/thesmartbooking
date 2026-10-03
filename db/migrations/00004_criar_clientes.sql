-- +goose Up
CREATE TABLE clientes (
  id                uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id         uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  nome              text NOT NULL,
  -- Contato manual, não canal de notificação: texto livre, sem validação E.164.
  telefone          text,
  -- bigint: IDs do Telegram já passam de 2^31.
  telegram_chat_id  bigint,
  -- Credencial de uso único com prazo: o /start <token> resolve cliente e tenant.
  vinculo_token     uuid DEFAULT gen_random_uuid(),
  vinculo_expira_em timestamptz NOT NULL DEFAULT now() + interval '30 days',
  vinculado_em      timestamptz,
  -- Desligado no 403 do Telegram (bot bloqueado); distinto de "nunca vinculou".
  notificavel       boolean NOT NULL DEFAULT true,
  -- Exclusão lógica: agendamentos referencia clientes com RESTRICT (007).
  removido_em       timestamptz,
  criado_em         timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  -- Alvo da FK composta de agendamentos (007).
  CONSTRAINT clientes_id_tenant       UNIQUE (id, tenant_id),
  -- Sem NULLS NOT DISTINCT: com ele, cada tenant aceitaria um único cliente sem Telegram.
  CONSTRAINT chat_id_unico_por_tenant UNIQUE (tenant_id, telegram_chat_id),
  -- Único global: é o que permite o bot único.
  CONSTRAINT vinculo_token_unico      UNIQUE (vinculo_token),
  CONSTRAINT vinculo_coerente CHECK (
       (telegram_chat_id IS NULL     AND vinculado_em IS NULL)
    OR (telegram_chat_id IS NOT NULL AND vinculado_em IS NOT NULL)
  )
);

CREATE INDEX idx_clientes_tenant ON clientes (tenant_id);

-- +goose Down
DROP TABLE clientes;
