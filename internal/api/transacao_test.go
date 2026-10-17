package api

import (
	"context"
	"errors"
	"net/http"
	"net/http/httptest"
	"slices"
	"testing"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

// poolDeUmaConexao: com MaxConns = 1, toda requisição reusa a mesma conexão,
// que é onde um SET sem escopo local vazaria o tenant.
func poolDeUmaConexao(t *testing.T) *pgxpool.Pool {
	t.Helper()
	cfg, err := pgxpool.ParseConfig(urlDoBanco(t))
	if err != nil {
		t.Fatal(err)
	}
	cfg.MaxConns = 1
	pool, err := pgxpool.NewWithConfig(context.Background(), cfg)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(pool.Close)
	return pool
}

// tenantDoCabecalho: no teste, cada requisição escolhe o seu tenant.
func tenantDoCabecalho(r *http.Request) uuid.UUID { return uuid.MustParse(r.Header.Get("Tenant")) }

func requisicao(t *testing.T, h http.Handler, tenant uuid.UUID) *httptest.ResponseRecorder {
	t.Helper()
	w := httptest.NewRecorder()
	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Tenant", tenant.String())
	h.ServeHTTP(w, r)
	return w
}

func TestTransacaoMesmaConexaoTenantsDiferentes(t *testing.T) {
	pool := poolDeUmaConexao(t)
	ctx := context.Background()

	var pid uint32
	var tenants []uuid.UUID
	h := ComTransacao(storage.NovoStore(pool), tenantDoCabecalho, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		tx := TxDaRequisicao(r)
		if err := tx.QueryRow(r.Context(), "SELECT pg_backend_pid()").Scan(&pid); err != nil {
			t.Fatal(err)
		}
		// Sem WHERE: quem filtra é só o RLS, pelo tenant da transação.
		rows, _ := tx.Query(r.Context(), "SELECT DISTINCT tenant_id FROM clientes")
		var err error
		if tenants, err = pgx.CollectRows(rows, pgx.RowTo[uuid.UUID]); err != nil {
			t.Fatal(err)
		}
	}))

	if w := requisicao(t, h, seedAlfa); w.Code != http.StatusOK {
		t.Fatalf("requisição do Alfa: %d %s", w.Code, w.Body)
	}
	pidAlfa := pid
	if !slices.Equal(tenants, []uuid.UUID{seedAlfa}) {
		t.Errorf("requisição do Alfa viu os tenants %v", tenants)
	}
	if w := requisicao(t, h, seedBeta); w.Code != http.StatusOK {
		t.Fatalf("requisição do Beta: %d %s", w.Code, w.Body)
	}
	if pid != pidAlfa {
		t.Fatalf("conexões diferentes (%d e %d): o teste não provaria nada", pidAlfa, pid)
	}
	if !slices.Equal(tenants, []uuid.UUID{seedBeta}) {
		t.Errorf("requisição do Beta, na conexão que foi do Alfa, viu os tenants %v", tenants)
	}

	var sobrou *string
	if err := pool.QueryRow(ctx, "SELECT app.current_tenant_id()::text").Scan(&sobrou); err != nil {
		t.Fatal(err)
	}
	if sobrou != nil {
		t.Errorf("tenant %s ficou na conexão depois da requisição", *sobrou)
	}
}

func TestTransacaoCommitERollback(t *testing.T) {
	store := storage.NovoStore(poolDeUmaConexao(t))
	ctx := context.Background()

	for nome, caso := range map[string]struct {
		responder func(w http.ResponseWriter, r *http.Request)
		status    int
		grava     bool
	}{
		"201 grava":    {func(w http.ResponseWriter, r *http.Request) { responderJSON(w, http.StatusCreated, nil) }, http.StatusCreated, true},
		"400 desfaz":   {func(w http.ResponseWriter, r *http.Request) { campoInvalido(w, "nome", "inválido") }, http.StatusBadRequest, false},
		"500 desfaz":   {func(w http.ResponseWriter, r *http.Request) { responderErroInterno(w, r, errors.New("teste")) }, http.StatusInternalServerError, false},
		"panic desfaz": {func(w http.ResponseWriter, r *http.Request) { panic("teste") }, http.StatusInternalServerError, false},
	} {
		t.Run(nome, func(t *testing.T) {
			var id uuid.UUID
			h := ComTransacao(store, tenantDoCabecalho, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				c, err := storage.NovoClientesRepo(TxDaRequisicao(r)).Criar(r.Context(), seedAlfa, "Teste 1.3", nil)
				if err != nil {
					t.Fatal(err)
				}
				id = c.ID
				caso.responder(w, r)
			}))

			if w := requisicao(t, h, seedAlfa); w.Code != caso.status {
				t.Errorf("status %d, esperava %d: %s", w.Code, caso.status, w.Body)
			}
			err := store.ComTenant(ctx, seedAlfa, func(tx pgx.Tx) error {
				_, err := storage.NovoClientesRepo(tx).Buscar(ctx, seedAlfa, id)
				return err
			})
			switch {
			case err != nil && !errors.Is(err, storage.ErrNaoEncontrado):
				t.Fatal(err)
			case (err == nil) != caso.grava:
				t.Errorf("gravou = %v, esperava %v", err == nil, caso.grava)
			}
			if caso.grava {
				store.ComTenant(ctx, seedAlfa, func(tx pgx.Tx) error {
					_, err := tx.Exec(ctx, "DELETE FROM clientes WHERE id = $1", id)
					return err
				})
			}
		})
	}
}

// Banco fora do ar: o BEGIN falha e a resposta é 500 no formato padrão.
func TestTransacaoSemBancoResponde500(t *testing.T) {
	pool, err := pgxpool.New(context.Background(), "postgres://app_user@127.0.0.1:1/x?connect_timeout=1")
	if err != nil {
		t.Fatal(err)
	}
	defer pool.Close()
	h := ComTransacao(storage.NovoStore(pool), tenantDoCabecalho, http.HandlerFunc(func(http.ResponseWriter, *http.Request) {
		t.Error("handler chamado sem transação")
	}))
	exigirErro(t, requisicao(t, h, seedAlfa), http.StatusInternalServerError, CodigoErroInterno)
}
