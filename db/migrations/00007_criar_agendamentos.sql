-- +goose Up
CREATE TABLE agendamentos (
  id             uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id      uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  cliente_id     uuid NOT NULL,
  servico_id     uuid NOT NULL,
  -- Aponta para memberships, não para o papel: o administrador que atende é prestador.
  prestador_id   uuid NOT NULL,
  inicio         timestamptz NOT NULL,
  fim            timestamptz NOT NULL,
  status         text NOT NULL DEFAULT 'confirmado',
  -- Copiado de servicos.preco_centavos na criação; o preço do serviço pode mudar.
  -- Sem DEFAULT: valor esquecido falha no INSERT, não vira zero.
  valor_centavos integer NOT NULL,
  observacoes    text,
  criado_em      timestamptz NOT NULL DEFAULT now(),
  atualizado_em  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  -- Alvo da FK composta de notificacoes (008).
  CONSTRAINT agendamentos_id_tenant UNIQUE (id, tenant_id),
  -- FKs compostas: cliente, serviço e prestador têm que ser do mesmo tenant.
  -- RESTRICT: apagar cliente não apaga o histórico dele (exclusão é lógica).
  FOREIGN KEY (cliente_id, tenant_id)
    REFERENCES clientes (id, tenant_id) ON DELETE RESTRICT,
  FOREIGN KEY (servico_id, tenant_id)
    REFERENCES servicos (id, tenant_id) ON DELETE RESTRICT,
  FOREIGN KEY (prestador_id, tenant_id)
    REFERENCES memberships (usuario_id, tenant_id) ON DELETE RESTRICT,
  CONSTRAINT status_valido      CHECK (status IN ('confirmado', 'cancelado', 'concluido')),
  CONSTRAINT intervalo_valido   CHECK (fim > inicio),
  CONSTRAINT valor_nao_negativo CHECK (valor_centavos >= 0)
);

-- '[)': fim exclusivo, então 9h-10h não conflita com 10h-11h. Cancelado libera o horário.
-- Violação chega no Go como SQLSTATE 23P01 e vira HTTP 409 (1.5).
ALTER TABLE agendamentos ADD CONSTRAINT sem_sobreposicao
EXCLUDE USING gist (
  tenant_id    WITH =,
  prestador_id WITH =,
  tstzrange(inicio, fim, '[)') WITH &&
) WHERE (status <> 'cancelado');

CREATE INDEX idx_agendamentos_tenant_inicio    ON agendamentos (tenant_id, inicio);
CREATE INDEX idx_agendamentos_cliente          ON agendamentos (tenant_id, cliente_id);
-- Escopo por prestador, calendário e financeiro; cobre também a FK do prestador.
CREATE INDEX idx_agendamentos_prestador_inicio ON agendamentos (tenant_id, prestador_id, inicio);

-- Trigger em vez de disciplina no repositório: ninguém esquece de tocar atualizado_em.
-- +goose StatementBegin
CREATE FUNCTION app.tocar_atualizado_em() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.atualizado_em = now();
  RETURN NEW;
END;
$$;
-- +goose StatementEnd

CREATE TRIGGER trg_agendamentos_atualizado_em
BEFORE UPDATE ON agendamentos
FOR EACH ROW EXECUTE FUNCTION app.tocar_atualizado_em();

-- +goose Down
DROP TABLE agendamentos;
-- A função mora no schema app: sem este DROP, o Down da 001 não apaga o schema.
DROP FUNCTION app.tocar_atualizado_em();
