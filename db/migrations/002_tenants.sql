-- +goose Up
CREATE TABLE tenants (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  nome               text NOT NULL,
  slug               text NOT NULL UNIQUE,
  fuso_horario       text NOT NULL DEFAULT 'America/Sao_Paulo',
  telegram_bot_token text,          -- NULL = usa o bot da plataforma
  criado_em          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT slug_formato CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$')
);

-- +goose Down
DROP TABLE tenants;
