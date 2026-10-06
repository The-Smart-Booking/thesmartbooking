#!/bin/sh
# Cria o app_user (a API): LOGIN, sem BYPASSRLS, sem superusuário, dono de nada.
# Roda no initdb do compose (só com volume vazio) e, no CI (0.17), via sh antes do
# goose up, com PGHOST/PGPASSWORD do dono. Quem roda é o dono das tabelas
# (POSTGRES_USER), o mesmo das migrations: é a ele que o DEFAULT PRIVILEGES se aplica.
set -eu
: "${APP_USER_PASSWORD:?defina APP_USER_PASSWORD (.env.example)}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v senha="$APP_USER_PASSWORD" -v dono="$POSTGRES_USER" -v banco="$POSTGRES_DB" <<'SQL'
CREATE ROLE app_user LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD :'senha';

-- O schema app nasce na 001, que roda depois deste script: criado aqui para o GRANT.
-- A 001 usa IF NOT EXISTS e passa direto. Dono: POSTGRES_USER, o mesmo da 001.
CREATE SCHEMA IF NOT EXISTS app;

GRANT CONNECT ON DATABASE :"banco" TO app_user;
GRANT USAGE   ON SCHEMA public, app TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
-- Sem sequences: toda PK é uuid com gen_random_uuid(). Com serial/identity,
-- acrescente GRANT USAGE ON SEQUENCES aqui.
ALTER DEFAULT PRIVILEGES FOR ROLE :"dono" IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
SQL
