-- +goose Up
-- Regra semanal recorrente no fuso do tenant ("segunda das 9h às 18h"), não um
-- instante: time sem fuso e dia_semana numérico. A conversão para instante fica
-- no cálculo de slots, com tenants.fuso_horario.
CREATE TABLE disponibilidades (
  id           uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  prestador_id uuid NOT NULL,
  dia_semana   smallint NOT NULL,     -- 0 = domingo ... 6 = sábado
  hora_inicio  time NOT NULL,
  hora_fim     time NOT NULL,
  PRIMARY KEY (id),
  -- FK composta: só aceita prestador que tem membership neste tenant.
  -- FK simples para usuarios(id) deixaria passar prestador de outra empresa.
  FOREIGN KEY (prestador_id, tenant_id)
    REFERENCES memberships (usuario_id, tenant_id) ON DELETE CASCADE,
  CONSTRAINT dia_valido     CHECK (dia_semana BETWEEN 0 AND 6),
  CONSTRAINT horario_valido CHECK (hora_fim > hora_inicio)
);

CREATE INDEX idx_disponibilidades_prestador
  ON disponibilidades (tenant_id, prestador_id, dia_semana);

-- +goose Down
DROP TABLE disponibilidades;
