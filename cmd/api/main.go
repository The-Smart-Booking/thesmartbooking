package main

import (
	"context"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/The-Smart-Booking/thesmartbooking/internal/api"
	"github.com/The-Smart-Booking/thesmartbooking/internal/config"
	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

// Fixos no código (1.14); vão para o config só se alguém precisar mudar.
const (
	readHeaderTimeout = 5 * time.Second // corta slowloris
	readTimeout       = 15 * time.Second
	// Conta a requisição inteira, inclusive a transação do ComTransacao:
	// curto demais corta a resposta de query lenta sem erro claro para o cliente.
	writeTimeout    = 30 * time.Second
	idleTimeout     = 60 * time.Second
	pingTimeout     = 5 * time.Second
	shutdownTimeout = writeTimeout // dá tempo de a requisição mais longa terminar
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		// Sem config válida a API não sobe (1.1): a mensagem diz qual variável.
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	// Pool do app_user: o config já recusou outro usuário (RNF07).
	pool, err := pgxpool.New(ctx, cfg.DatabaseURL)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	// O pool conecta sob demanda: sem o Ping, banco fora do ar só aparece como 500.
	pingCtx, cancel := context.WithTimeout(ctx, pingTimeout)
	err = pool.Ping(pingCtx)
	cancel()
	if err != nil {
		slog.Error("não alcançou o banco", "err", err)
		os.Exit(1)
	}

	mux := http.NewServeMux()
	(&api.Handlers{TenantID: cfg.TenantFixo, Tx: api.TxDaRequisicao,
		PrestadorID: cfg.PrestadorFixo, ServicoID: cfg.ServicoFixo}).Rotas(mux)
	// Toda requisição numa transação com o tenant; na Fatia 2 ele vem da sessão.
	tenant := func(*http.Request) uuid.UUID { return cfg.TenantFixo }
	srv := &http.Server{
		Addr:              ":" + cfg.PortaHTTP,
		Handler:           api.ComTransacao(storage.NovoStore(pool), tenant, mux),
		ReadHeaderTimeout: readHeaderTimeout,
		ReadTimeout:       readTimeout,
		WriteTimeout:      writeTimeout,
		IdleTimeout:       idleTimeout,
	}
	slog.Info("API no ar", "addr", srv.Addr, "tenant_fixo", cfg.TenantFixo)
	parou := make(chan error, 1)
	go func() { parou <- srv.ListenAndServe() }()
	select {
	case err := <-parou:
		slog.Error("servidor parou", "err", err)
		os.Exit(1)
	case <-ctx.Done():
	}

	// Shutdown gracioso: para de aceitar conexões e espera as requisições em andamento.
	stop() // um segundo Ctrl+C mata na hora
	slog.Info("desligando a API")
	shutCtx, cancel := context.WithTimeout(context.Background(), shutdownTimeout)
	defer cancel()
	err = srv.Shutdown(shutCtx)
	pool.Close()
	if err != nil {
		slog.Error("shutdown não terminou no prazo", "err", err)
		os.Exit(1)
	}
	slog.Info("API desligada")
}
