-- +goose Up
CREATE TABLE tenants (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  nome               text NOT NULL,
  slug               text NOT NULL UNIQUE,
  -- Nome IANA (America/Sao_Paulo), nunca offset (-3): offset quebra no horário de verão.
  -- O CHECK barra offsets; se o nome existe, a aplicação confere em pg_timezone_names.
  fuso_horario       text NOT NULL DEFAULT 'America/Sao_Paulo',
  -- NULL = usa o bot da plataforma. tenants fica fora do RLS, então o token
  -- ficaria visível a todas as empresas: sai para tabela própria com policy
  -- no dia do bot por tenant, e o CHECK impede preencher antes disso.
  telegram_bot_token text,
  criado_em          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT slug_formato CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$'),
  CONSTRAINT fuso_horario_iana CHECK (fuso_horario = 'UTC' OR fuso_horario ~ '^[A-Z][A-Za-z_]+(/[A-Za-z0-9_+-]+)+$'),
  CONSTRAINT telegram_bot_token_nulo CHECK (telegram_bot_token IS NULL)
);

-- +goose Down
DROP TABLE tenants;
