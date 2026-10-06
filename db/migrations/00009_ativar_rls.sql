-- +goose Up
-- Rede de segurança do isolamento: mesmo que uma query esqueça o WHERE tenant_id,
-- o banco só devolve e só aceita linhas do tenant da transação.
-- Fora do RLS, de propósito: usuarios, memberships, tenants, sessoes e convites são
-- lidas antes de existir tenant (login, cookie, token de convite). Nelas o WHERE
-- manual é obrigatório (guia §009).
--
-- Precisa do app_user (script da 0.15) antes do goose up, por causa do REVOKE.

-- ENABLE liga o RLS; FORCE faz valer também para o dono das tabelas, que por
-- padrão ignora as policies. A outra defesa é a API conectar como app_user.
ALTER TABLE clientes         ENABLE ROW LEVEL SECURITY;
ALTER TABLE servicos         ENABLE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades ENABLE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     ENABLE ROW LEVEL SECURITY;
ALTER TABLE notificacoes     ENABLE ROW LEVEL SECURITY;

ALTER TABLE clientes         FORCE ROW LEVEL SECURITY;
ALTER TABLE servicos         FORCE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades FORCE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     FORCE ROW LEVEL SECURITY;
ALTER TABLE notificacoes     FORCE ROW LEVEL SECURITY;

-- USING filtra o que é lido (e o que UPDATE/DELETE alcançam); WITH CHECK barra
-- gravar linha de outro tenant. Sem tenant definido, app.current_tenant_id() é
-- NULL e nenhuma linha passa: falha fechada.
-- Sem policy por prestador: o escopo dentro da empresa é filtro na query (2.7).
CREATE POLICY isolamento_tenant ON clientes
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

CREATE POLICY isolamento_tenant ON servicos
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

CREATE POLICY isolamento_tenant ON disponibilidades
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

CREATE POLICY isolamento_tenant ON agendamentos
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

CREATE POLICY isolamento_tenant ON notificacoes
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

-- Agendamento nunca é apagado, só cancelado ou concluído. Aqui e não na 007: o
-- DEFAULT PRIVILEGES da 0.15 concede DELETE quando a tabela nasce.
REVOKE DELETE ON agendamentos FROM app_user;

-- O script da 0.15 já concede, mas o Down da 001 apaga o schema app e o Up o
-- recria sem o GRANT: depois de um goose down-to 0, o app_user perderia o acesso
-- a app.current_tenant_id(). Repetir aqui deixa o goose up sempre completo.
-- O Down não revoga: quem concedeu primeiro foi o script.
GRANT USAGE ON SCHEMA app TO app_user;

-- +goose Down
GRANT DELETE ON agendamentos TO app_user;

DROP POLICY isolamento_tenant ON notificacoes;
DROP POLICY isolamento_tenant ON agendamentos;
DROP POLICY isolamento_tenant ON disponibilidades;
DROP POLICY isolamento_tenant ON servicos;
DROP POLICY isolamento_tenant ON clientes;

-- DISABLE não desfaz o FORCE: são duas flags. Sem o NO FORCE, o Down deixa a
-- tabela marcada como FORCE.
ALTER TABLE notificacoes     NO FORCE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     NO FORCE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades NO FORCE ROW LEVEL SECURITY;
ALTER TABLE servicos         NO FORCE ROW LEVEL SECURITY;
ALTER TABLE clientes         NO FORCE ROW LEVEL SECURITY;

ALTER TABLE notificacoes     DISABLE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     DISABLE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades DISABLE ROW LEVEL SECURITY;
ALTER TABLE servicos         DISABLE ROW LEVEL SECURITY;
ALTER TABLE clientes         DISABLE ROW LEVEL SECURITY;
