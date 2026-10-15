package storage

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

// Teste da 1.2: os repositórios, lendo como app_user (DATABASE_URL) com o tenant
// Alfa, não enxergam o Beta. Usa os dados do seed (db/seed.sql, 0.16), sem gravar
// nada. Sem DATABASE_URL, pula; no CI (CI definida), falha.

var (
	seedAlfa            = uuid.MustParse("11111111-1111-1111-1111-111111111111")
	seedBeta            = uuid.MustParse("22222222-2222-2222-2222-222222222222")
	seedClienteBeta     = uuid.MustParse("22222222-0004-0000-0000-000000000001")
	seedAgendamentoBeta = uuid.MustParse("22222222-0007-0000-0000-000000000001")
	seedAgendamentoAlfa = uuid.MustParse("11111111-0007-0000-0000-000000000001")
	// Período largo: o seed grava datas relativas ao dia em que rodou.
	periodoDe, periodoAte = time.Date(2000, 1, 1, 0, 0, 0, 0, time.UTC), time.Date(2100, 1, 1, 0, 0, 0, 0, time.UTC)
)

func TestRepositorioAlfaNaoEnxergaBeta(t *testing.T) {
	app := conectar(t, "DATABASE_URL")
	ctx := context.Background()

	comTenant(t, app, seedAlfa.String(), func(tx pgx.Tx) error {
		clientes, agendamentos := NovoClientesRepo(tx), NovoAgendamentosRepo(tx)

		cs, err := clientes.Listar(ctx, seedAlfa)
		if err != nil {
			t.Fatal(err)
		}
		if len(cs) == 0 {
			t.Fatal("nenhum cliente do Alfa: o seed (db/seed.sql) rodou?")
		}
		for _, c := range cs {
			if c.TenantID != seedAlfa {
				t.Errorf("cliente %s do tenant %s na listagem do Alfa", c.ID, c.TenantID)
			}
		}

		as, err := agendamentos.Listar(ctx, seedAlfa, periodoDe, periodoAte, nil)
		if err != nil {
			t.Fatal(err)
		}
		if len(as) == 0 {
			t.Fatal("nenhum agendamento do Alfa no período: o seed (db/seed.sql) rodou?")
		}
		for _, a := range as {
			if a.TenantID != seedAlfa {
				t.Errorf("agendamento %s do tenant %s na listagem do Alfa", a.ID, a.TenantID)
			}
		}
		if _, err := agendamentos.Buscar(ctx, seedAlfa, seedAgendamentoAlfa); err != nil {
			t.Errorf("buscar agendamento do próprio tenant: %v", err)
		}

		// Registro do Beta por id: não encontrado, pelo WHERE e pelo RLS.
		if _, err := clientes.Buscar(ctx, seedAlfa, seedClienteBeta); !errors.Is(err, ErrNaoEncontrado) {
			t.Errorf("buscar cliente do Beta: esperava ErrNaoEncontrado, veio %v", err)
		}
		if _, err := agendamentos.Buscar(ctx, seedAlfa, seedAgendamentoBeta); !errors.Is(err, ErrNaoEncontrado) {
			t.Errorf("buscar agendamento do Beta: esperava ErrNaoEncontrado, veio %v", err)
		}

		// tenantID do Beta com a transação no Alfa: o RLS barra o que o WHERE deixaria.
		if cs, err := clientes.Listar(ctx, seedBeta); err != nil || len(cs) != 0 {
			t.Errorf("listar clientes do Beta na transação do Alfa: %d linhas, err %v", len(cs), err)
		}
		if as, err := agendamentos.Listar(ctx, seedBeta, periodoDe, periodoAte, nil); err != nil || len(as) != 0 {
			t.Errorf("listar agendamentos do Beta na transação do Alfa: %d linhas, err %v", len(as), err)
		}

		// Escrita como app_user passa no WITH CHECK; o comTenant desfaz no fim.
		if c, err := clientes.Criar(ctx, seedAlfa, "Cliente do teste", nil); err != nil || c.TenantID != seedAlfa {
			t.Errorf("criar cliente no Alfa: tenant %s, err %v", c.TenantID, err)
		}
		return nil
	})
}
