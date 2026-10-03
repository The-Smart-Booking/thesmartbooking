-- +goose Up
-- Sem tenant_id, de propósito: o mesmo e-mail pode pertencer a mais de um tenant,
-- e o vínculo mora em memberships.
CREATE TABLE usuarios (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  email      text NOT NULL,
  senha_hash text NOT NULL,
  nome       text NOT NULL,
  criado_em  timestamptz NOT NULL DEFAULT now()
);

-- lower(email): UNIQUE direto na coluna deixaria Davi@x.com e davi@x.com criarem duas contas.
CREATE UNIQUE INDEX usuarios_email_unico ON usuarios (lower(email));

-- Criador da empresa. A 002 já foi mergeada, então a coluna entra aqui.
-- RESTRICT: com CASCADE, apagar a conta do criador apagaria a empresa.
-- Banco de dev com linhas em tenants faz este NOT NULL falhar: goose down-to 0 antes.
ALTER TABLE tenants
  ADD COLUMN criado_por uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT;

CREATE TABLE memberships (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  usuario_id  uuid NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  tenant_id   uuid NOT NULL REFERENCES tenants(id)  ON DELETE CASCADE,
  -- text + CHECK, não ENUM: CHECK se altera com um ALTER TABLE.
  papel       text NOT NULL,
  -- Exclusão lógica: agendamentos referencia memberships com RESTRICT (007).
  removido_em timestamptz,
  criado_em   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT papel_valido CHECK (papel IN ('owner', 'prestador')),
  -- Impede membership duplicado e é alvo da FK composta de agendamentos (007).
  CONSTRAINT membership_unico UNIQUE (usuario_id, tenant_id)
);

CREATE INDEX idx_memberships_usuario ON memberships (usuario_id);
CREATE INDEX idx_memberships_tenant  ON memberships (tenant_id);

-- +goose Down
DROP TABLE memberships;
ALTER TABLE tenants DROP COLUMN criado_por;
DROP TABLE usuarios;
