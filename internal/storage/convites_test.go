package storage

import (
	"context"
	"testing"

	"github.com/jackc/pgx/v5"
)

// Teste da 2.17: o CHECK de convites só aceita administrador e prestador (guia §011).
// Roda como dono (GOOSE_DBSTRING): convites está fora do RLS e a transação é desfeita.
func TestConvitePapelInvalido(t *testing.T) {
	dono := conectar(t, "GOOSE_DBSTRING")
	ctx := context.Background()

	const usuario = "fd000000-0000-0000-0000-000000000001"
	const tenant = "f5555555-5555-5555-5555-555555555555"
	comTenant(t, dono, "", func(tx pgx.Tx) error {
		if _, err := tx.Exec(ctx, "INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, 'teste-convite@teste.invalid', 'x', 'Teste convite')", usuario); err != nil {
			t.Fatal(err)
		}
		if _, err := tx.Exec(ctx, "INSERT INTO tenants (id, nome, slug, criado_por) VALUES ($1, 'Teste convite', 'teste-convite', $2)", tenant, usuario); err != nil {
			t.Fatal(err)
		}
		for _, papel := range []string{"administrador", "prestador"} {
			if _, err := tx.Exec(ctx, "INSERT INTO convites (tenant_id, email, papel, criado_por) VALUES ($1, 'convidado@teste.invalid', $2, $3)", tenant, papel, usuario); err != nil {
				t.Fatalf("papel %s recusado: %v", papel, err)
			}
		}
		_, err := tx.Exec(ctx, "INSERT INTO convites (tenant_id, email, papel, criado_por) VALUES ($1, 'convidado@teste.invalid', 'owner', $2)", tenant, usuario)
		exigirSQLState(t, err, "23514")
		return nil
	})
}
