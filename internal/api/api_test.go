package api

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"os"
	"strings"
	"testing"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

// IDs do seed (db/seed.sql, 0.16). Alfa = TENANT_FIXO do .env.example.
var (
	seedAlfa        = uuid.MustParse("11111111-1111-1111-1111-111111111111")
	seedClienteAlfa = uuid.MustParse("11111111-0004-0000-0000-000000000001")
	seedClienteBeta = uuid.MustParse("22222222-0004-0000-0000-000000000001")
	seedServicoAlfa = uuid.MustParse("11111111-0005-0000-0000-000000000001") // SERVICO_FIXO
	seedAdminAlfa   = uuid.MustParse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
	seedPrestador   = uuid.MustParse("cccccccc-cccc-cccc-cccc-cccccccccccc") // PRESTADOR_FIXO
)

// urlDoBanco devolve a DATABASE_URL (app_user). Sem ela, pula; no CI, falha.
func urlDoBanco(t *testing.T) string {
	t.Helper()
	url := os.Getenv("DATABASE_URL")
	if url == "" {
		if os.Getenv("CI") != "" {
			t.Fatal("DATABASE_URL não definida no CI: teste de banco não pode pular")
		}
		t.Skip("DATABASE_URL não definida: teste de banco pulado")
	}
	return url
}

// comBanco monta as rotas sobre uma transação do app_user com set_config(...,
// true), como faz o ComTransacao, desfeita no fim do teste.
func comBanco(t *testing.T, tenant uuid.UUID) (http.Handler, pgx.Tx) {
	t.Helper()
	ctx := context.Background()
	conn, err := pgx.Connect(ctx, urlDoBanco(t))
	if err != nil {
		t.Fatal(err)
	}
	tx, err := conn.Begin(ctx)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { tx.Rollback(ctx); conn.Close(ctx) })
	if _, err := tx.Exec(ctx, "SELECT set_config('app.tenant_id', $1, true)", tenant.String()); err != nil {
		t.Fatal(err)
	}
	return rotas(&Handlers{TenantID: tenant, Tx: func(*http.Request) pgx.Tx { return tx }}), tx
}

// semBanco monta as rotas para testes de validação: tocar no banco é falha.
func semBanco(t *testing.T) http.Handler {
	return rotas(&Handlers{TenantID: seedAlfa, Tx: func(*http.Request) pgx.Tx {
		t.Fatal("validação deveria barrar antes do banco")
		return nil
	}})
}

func rotas(h *Handlers) http.Handler {
	mux := http.NewServeMux()
	h.Rotas(mux)
	return mux
}

func chamar(t *testing.T, h http.Handler, metodo, rota, corpo string) *httptest.ResponseRecorder {
	t.Helper()
	w := httptest.NewRecorder()
	h.ServeHTTP(w, httptest.NewRequest(metodo, rota, strings.NewReader(corpo)))
	return w
}

// exigirErro confere status e código no formato de docs/erros-api.md.
func exigirErro(t *testing.T, w *httptest.ResponseRecorder, status int, codigo string) {
	t.Helper()
	var c corpoErro
	if w.Code != status || json.Unmarshal(w.Body.Bytes(), &c) != nil || c.Erro.Codigo != codigo || c.Erro.Mensagem == "" {
		t.Errorf("esperava %d %s, veio %d %s", status, codigo, w.Code, w.Body)
	}
}
