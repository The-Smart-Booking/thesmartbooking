-- +goose Up
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS app;

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
