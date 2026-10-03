-- +goose Up
CREATE TABLE servicos (
  id              uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  nome            text NOT NULL,
  duracao_minutos int  NOT NULL,
  -- Centavos em integer (R$ 45,90 = 4590), nunca money nem float.
  -- Sem DEFAULT: com DEFAULT 0, serviço sem preço ficaria grátis em silêncio.
  preco_centavos  integer NOT NULL,
  -- Exclusão lógica: DELETE quebraria o histórico de agendamentos (007).
  ativo           boolean NOT NULL DEFAULT true,
  criado_em       timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  -- Alvo da FK composta de agendamentos (007): serviço e agendamento no mesmo tenant.
  CONSTRAINT servicos_id_tenant UNIQUE (id, tenant_id),
  CONSTRAINT duracao_positiva CHECK (duracao_minutos > 0 AND duracao_minutos <= 1440),
  CONSTRAINT preco_nao_negativo CHECK (preco_centavos >= 0)
);

CREATE INDEX idx_servicos_tenant ON servicos (tenant_id);

-- +goose Down
DROP TABLE servicos;
