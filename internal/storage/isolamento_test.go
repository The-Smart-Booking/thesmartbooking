package storage

import (
	"context"
	"errors"
	"os"
	"testing"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgconn"
)

// Teste de isolamento da 0.14 (guia §Seed e teste de isolamento). Os dados são
// gravados pelo dono das tabelas (GOOSE_DBSTRING) e lidos pelo app_user
// (DATABASE_URL), como a API faz. Sem as duas variáveis, pula; no CI (CI definida), falha.

// UUIDs próprios do teste, fora da faixa do seed (0.16), para os dois conviverem.
const (
	tenantAlfa  = "f1111111-1111-1111-1111-111111111111"
	tenantBeta  = "f2222222-2222-2222-2222-222222222222"
	usuarioAlfa = "fa000000-0000-0000-0000-000000000001"
	usuarioBeta = "fb000000-0000-0000-0000-000000000001"
)

var tabelasComRLS = []string{"clientes", "servicos", "disponibilidades", "agendamentos", "notificacoes"}

func conectar(t *testing.T, variavel string) *pgx.Conn {
	t.Helper()
	url := os.Getenv(variavel)
	if url == "" {
		if os.Getenv("CI") != "" {
			t.Fatalf("%s não definida no CI: teste de banco não pode pular", variavel)
		}
		t.Skipf("%s não definida: teste de banco pulado", variavel)
	}
	conn, err := pgx.Connect(context.Background(), url)
	if err != nil {
		t.Fatalf("conectar com %s: %v", variavel, err)
	}
	t.Cleanup(func() { conn.Close(context.Background()) })
	return conn
}

// comTenant abre transação com set_config(..., true), como o ComTenant do guia, e
// sempre desfaz: o teste não deixa rastro. tenant vazio = sem tenant definido.
func comTenant(t *testing.T, conn *pgx.Conn, tenant string, fn func(tx pgx.Tx) error) error {
	t.Helper()
	ctx := context.Background()
	tx, err := conn.Begin(ctx)
	if err != nil {
		t.Fatal(err)
	}
	defer tx.Rollback(ctx)
	if tenant != "" {
		if _, err := tx.Exec(ctx, "SELECT set_config('app.tenant_id', $1, true)", tenant); err != nil {
			t.Fatal(err)
		}
	}
	return fn(tx)
}

func contar(t *testing.T, tx pgx.Tx, sql string, args ...any) int {
	t.Helper()
	var n int
	if err := tx.QueryRow(context.Background(), sql, args...).Scan(&n); err != nil {
		t.Fatalf("%s: %v", sql, err)
	}
	return n
}

func exigirSQLState(t *testing.T, err error, codigo string) {
	t.Helper()
	var pgErr *pgconn.PgError
	if !errors.As(err, &pgErr) || pgErr.Code != codigo {
		t.Fatalf("esperava SQLSTATE %s, veio %v", codigo, err)
	}
}

func apagarDados(t *testing.T, dono *pgx.Conn) {
	t.Helper()
	ctx := context.Background()
	// O CASCADE de tenants leva clientes, agendamentos, notificações etc.
	if _, err := dono.Exec(ctx, "DELETE FROM tenants WHERE id IN ($1, $2)", tenantAlfa, tenantBeta); err != nil {
		t.Fatal(err)
	}
	if _, err := dono.Exec(ctx, "DELETE FROM usuarios WHERE id IN ($1, $2)", usuarioAlfa, usuarioBeta); err != nil {
		t.Fatal(err)
	}
}

// criarDados grava uma linha em cada tabela com RLS, nos dois tenants. Cada tenant
// numa transação com set_config: com FORCE, nem o dono grava sem tenant (a não ser
// que seja superusuário).
func criarDados(t *testing.T, dono *pgx.Conn) {
	t.Helper()
	ctx := context.Background()
	for i, tenant := range []string{tenantAlfa, tenantBeta} {
		usuario := []string{usuarioAlfa, usuarioBeta}[i]
		slug := []string{"teste-rls-alfa", "teste-rls-beta"}[i]

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
			{"INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, $2, 'x', 'Teste RLS')", []any{usuario, slug + "@teste.invalid"}},
			{"INSERT INTO tenants (id, nome, slug, criado_por) VALUES ($1, $2, $2, $3)", []any{tenant, slug, usuario}},
			{"INSERT INTO memberships (usuario_id, tenant_id, papel) VALUES ($1, $2, 'administrador')", []any{usuario, tenant}},
			{"INSERT INTO clientes (tenant_id, nome) VALUES ($1, 'Cliente')", []any{tenant}},
			{"INSERT INTO servicos (tenant_id, nome, duracao_minutos, preco_centavos) VALUES ($1, 'Serviço', 60, 5000)", []any{tenant}},
			{"INSERT INTO disponibilidades (tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim) VALUES ($1, $2, 1, '09:00', '18:00')", []any{tenant, usuario}},
			{`INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
			  SELECT $1, c.id, s.id, $2, '2026-10-05 12:00Z', '2026-10-05 13:00Z', s.preco_centavos
			  FROM clientes c, servicos s WHERE c.tenant_id = $1 AND s.tenant_id = $1`, []any{tenant, usuario}},
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

func TestIsolamentoPorTenant(t *testing.T) {
	dono := conectar(t, "GOOSE_DBSTRING")
	app := conectar(t, "DATABASE_URL")
	ctx := context.Background()

	// Armadilha 1 do guia: se a API conecta como dono ou com BYPASSRLS, tudo abaixo
	// passaria por acidente.
	var superusuario, bypass bool
	if err := app.QueryRow(ctx, "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user").Scan(&superusuario, &bypass); err != nil {
		t.Fatal(err)
	}
	if superusuario || bypass {
		t.Fatalf("DATABASE_URL conecta com superusuário=%v BYPASSRLS=%v: o RLS não vale para ela", superusuario, bypass)
	}
	var donoDe int
	if err := app.QueryRow(ctx, "SELECT count(*) FROM pg_tables WHERE tableowner = current_user").Scan(&donoDe); err != nil {
		t.Fatal(err)
	}
	if donoDe > 0 {
		t.Fatalf("DATABASE_URL é dona de %d tabelas", donoDe)
	}

	apagarDados(t, dono)
	criarDados(t, dono)
	t.Cleanup(func() { apagarDados(t, dono) })

	for _, tabela := range tabelasComRLS {
		t.Run(tabela, func(t *testing.T) {
			comTenant(t, app, tenantAlfa, func(tx pgx.Tx) error {
				todas := contar(t, tx, "SELECT count(*) FROM "+tabela)
				doAlfa := contar(t, tx, "SELECT count(*) FROM "+tabela+" WHERE tenant_id = $1", tenantAlfa)
				if todas == 0 || todas != doAlfa {
					t.Errorf("sem WHERE: %d linhas, do Alfa: %d", todas, doAlfa)
				}
				if n := contar(t, tx, "SELECT count(*) FROM "+tabela+" WHERE tenant_id = $1", tenantBeta); n != 0 {
					t.Errorf("WHERE tenant_id = Beta devolveu %d linhas", n)
				}
				return nil
			})
			comTenant(t, app, "", func(tx pgx.Tx) error {
				if n := contar(t, tx, "SELECT count(*) FROM "+tabela); n != 0 {
					t.Errorf("sem tenant definido: %d linhas (devia falhar fechado)", n)
				}
				return nil
			})
		})
	}

	t.Run("INSERT com tenant de outro falha no WITH CHECK", func(t *testing.T) {
		err := comTenant(t, app, tenantAlfa, func(tx pgx.Tx) error {
			_, err := tx.Exec(ctx, "INSERT INTO clientes (tenant_id, nome) VALUES ($1, 'invasor')", tenantBeta)
			return err
		})
		exigirSQLState(t, err, "42501")
	})

	t.Run("UPDATE que muda o tenant falha no WITH CHECK", func(t *testing.T) {
		err := comTenant(t, app, tenantAlfa, func(tx pgx.Tx) error {
			_, err := tx.Exec(ctx, "UPDATE clientes SET tenant_id = $1", tenantBeta)
			return err
		})
		exigirSQLState(t, err, "42501")
	})

	t.Run("DELETE em agendamentos é negado", func(t *testing.T) {
		err := comTenant(t, app, tenantAlfa, func(tx pgx.Tx) error {
			_, err := tx.Exec(ctx, "DELETE FROM agendamentos")
			return err
		})
		exigirSQLState(t, err, "42501")
	})

	t.Run("tenant da transação morre com ela", func(t *testing.T) {
		comTenant(t, app, tenantAlfa, func(tx pgx.Tx) error { return nil })
		var tenant *string
		if err := app.QueryRow(ctx, "SELECT app.current_tenant_id()::text").Scan(&tenant); err != nil {
			t.Fatal(err)
		}
		if tenant != nil {
			t.Fatalf("tenant %s sobreviveu à transação na mesma conexão", *tenant)
		}
	})
}
