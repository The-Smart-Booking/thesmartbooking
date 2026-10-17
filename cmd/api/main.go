package main

import (
	"context"
	"fmt"
	"log/slog"
	"net/http"
	"os"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/The-Smart-Booking/thesmartbooking/internal/api"
	"github.com/The-Smart-Booking/thesmartbooking/internal/config"
	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		// Sem config válida a API não sobe (1.1): a mensagem diz qual variável.
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	// Pool do app_user: o config já recusou outro usuário (RNF07).
	pool, err := pgxpool.New(context.Background(), cfg.DatabaseURL)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer pool.Close()

	mux := http.NewServeMux()
	(&api.Handlers{TenantID: cfg.TenantFixo, Tx: api.TxDaRequisicao}).Rotas(mux)
	// Toda requisição numa transação com o tenant; na Fatia 2 ele vem da sessão.
	tenant := func(*http.Request) uuid.UUID { return cfg.TenantFixo }
	addr := ":" + cfg.PortaHTTP
	slog.Info("API no ar", "addr", addr, "tenant_fixo", cfg.TenantFixo)
	if err := http.ListenAndServe(addr, api.ComTransacao(storage.NovoStore(pool), tenant, mux)); err != nil {
		slog.Error("servidor parou", "err", err)
		os.Exit(1)
	}
}
