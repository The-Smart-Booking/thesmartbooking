-- +goose Up
-- btree_gist: a constraint de sobreposição de agendamentos (007) mistura = em uuid com && em intervalo.
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS app;

-- Tenant da transação, lido pelas policies de RLS. O `true` devolve NULL em vez de erro
-- quando app.tenant_id não foi definido: sem tenant, nenhuma linha passa (falha fechada).
-- +goose StatementBegin
CREATE FUNCTION app.current_tenant_id() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;
-- +goose StatementEnd

-- +goose Down
DROP FUNCTION IF EXISTS app.current_tenant_id();
DROP SCHEMA IF EXISTS app;
DROP EXTENSION IF EXISTS btree_gist;
