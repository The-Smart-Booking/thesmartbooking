package storage

import (
	"context"
	"testing"

	"github.com/jackc/pgx/v5"
)

// Teste da 0.18: agendamentos referencia clientes, servicos e memberships com
// RESTRICT, e todos cascateiam de tenants. DELETE FROM tenants não pode quebrar
// pela ordem em que o Postgres processa o CASCADE.
//
// Roda como dono das tabelas (GOOSE_DBSTRING), como no guia (§009): o app_user não
// tem DELETE em agendamentos e excluir empresa não é operação da API.

// UUIDs próprios, fora das faixas do seed (0.16) e do teste de isolamento (0.14).
const (
	tenantExcluido  = "f3333333-3333-3333-3333-333333333333"
	tenantMantido   = "f4444444-4444-4444-4444-444444444444"
	usuarioExclusao = "fc000000-0000-0000-0000-000000000001"
)

func apagarDadosExclusao(t *testing.T, dono *pgx.Conn) {
	t.Helper()
	ctx := context.Background()
	if _, err := dono.Exec(ctx, "DELETE FROM tenants WHERE id IN ($1, $2)", tenantExcluido, tenantMantido); err != nil {
		t.Fatal(err)
	}
	if _, err := dono.Exec(ctx, "DELETE FROM usuarios WHERE id = $1", usuarioExclusao); err != nil {
		t.Fatal(err)
	}
}

// criarDadosExclusao grava uma linha em cada tabela com tenant_id nos dois tenants.
// O usuário é o mesmo nos dois (criador, administrador e prestador): usuarios é
// global, então o teste também confere que ele sobrevive à exclusão de um tenant.
func criarDadosExclusao(t *testing.T, dono *pgx.Conn) {
	t.Helper()
	ctx := context.Background()
	if _, err := dono.Exec(ctx, "INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, 'teste-exclusao@teste.invalid', 'x', 'Teste exclusão')", usuarioExclusao); err != nil {
		t.Fatal(err)
	}
	u := usuarioExclusao
	for i, tenant := range []string{tenantExcluido, tenantMantido} {
		slug := []string{"teste-exclusao-sai", "teste-exclusao-fica"}[i]
		tx, err := dono.Begin(ctx)
		if err != nil {
			t.Fatal(err)
		}
		defer tx.Rollback(ctx)
		lote := []struct {
			sql  string
			args []any
		}{
			{"SELECT set_config('app.tenant_id', $1, true)", []any{tenant}},
			{"INSERT INTO tenants (id, nome, slug, criado_por) VALUES ($1, $2, $2, $3)", []any{tenant, slug, u}},
			{"INSERT INTO memberships (usuario_id, tenant_id, papel) VALUES ($1, $2, 'administrador')", []any{u, tenant}},
			{"INSERT INTO clientes (tenant_id, nome) VALUES ($1, 'Cliente')", []any{tenant}},
			{"INSERT INTO servicos (tenant_id, nome, duracao_minutos, preco_centavos) VALUES ($1, 'Serviço', 60, 5000)", []any{tenant}},
			{"INSERT INTO disponibilidades (tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim) VALUES ($1, $2, 1, '09:00', '18:00')", []any{tenant, u}},
			{`INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
			  SELECT $1, c.id, s.id, $2, '2026-10-05 12:00Z', '2026-10-05 13:00Z', s.preco_centavos
			  FROM clientes c, servicos s WHERE c.tenant_id = $1 AND s.tenant_id = $1`, []any{tenant, u}},
			{"INSERT INTO notificacoes (tenant_id, agendamento_id, tipo) SELECT $1, id, 'confirmacao' FROM agendamentos WHERE tenant_id = $1", []any{tenant}},
		}
		for _, q := range lote {
			if _, err := tx.Exec(ctx, q.sql, q.args...); err != nil {
				t.Fatalf("%s: %v", q.sql, err)
			}
		}
		if err := tx.Commit(ctx); err != nil {
			t.Fatal(err)
		}
	}
}

func TestExclusaoDeTenant(t *testing.T) {
	dono := conectar(t, "GOOSE_DBSTRING")
	ctx := context.Background()

	apagarDadosExclusao(t, dono)
	criarDadosExclusao(t, dono)
	t.Cleanup(func() { apagarDadosExclusao(t, dono) })

	// Toda tabela com tenant_id entra sozinha: tabela nova não precisa mexer no teste.
	rows, err := dono.Query(ctx, `SELECT table_name FROM information_schema.columns
		WHERE table_schema = 'public' AND column_name = 'tenant_id'`)
	if err != nil {
		t.Fatal(err)
	}
	tabelas, err := pgx.CollectRows(rows, pgx.RowTo[string])
	if err != nil {
		t.Fatal(err)
	}

	// Conta com o tenant na transação: com FORCE, nem o dono lê sem ele.
	linhas := func(tenant string) map[string]int {
		n := map[string]int{}
		comTenant(t, dono, tenant, func(tx pgx.Tx) error {
			for _, tabela := range tabelas {
				n[tabela] = contar(t, tx, "SELECT count(*) FROM "+tabela+" WHERE tenant_id = $1", tenant)
			}
			return nil
		})
		return n
	}

	for tabela, n := range linhas(tenantExcluido) {
		if n == 0 {
			t.Fatalf("%s sem linha do tenant a excluir: o teste não cobriria a tabela", tabela)
		}
	}
	antes := linhas(tenantMantido)

	if _, err := dono.Exec(ctx, "DELETE FROM tenants WHERE id = $1", tenantExcluido); err != nil {
		t.Fatalf("DELETE FROM tenants: %v", err)
	}

	for tabela, n := range linhas(tenantExcluido) {
		if n != 0 {
			t.Errorf("%s: sobraram %d linhas do tenant excluído", tabela, n)
		}
	}
	for tabela, n := range linhas(tenantMantido) {
		if n != antes[tabela] {
			t.Errorf("%s: o outro tenant tinha %d linhas, ficou com %d", tabela, antes[tabela], n)
		}
	}
	var usuarios int
	if err := dono.QueryRow(ctx, "SELECT count(*) FROM usuarios WHERE id = $1", usuarioExclusao).Scan(&usuarios); err != nil {
		t.Fatal(err)
	}
	if usuarios != 1 {
		t.Error("usuário sumiu com o tenant: usuarios é global, só o membership devia sair")
	}
}
