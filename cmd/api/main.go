package main

import (
	"fmt"
	"log/slog"
	"net/http"
	"os"

	"github.com/The-Smart-Booking/thesmartbooking/internal/config"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		// Sem config válida a API não sobe (1.1): a mensagem diz qual variável.
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	// Rotas entram a partir da 1.3; por ora, toda rota responde 404.
	mux := http.NewServeMux()
	addr := ":" + cfg.PortaHTTP
	slog.Info("API no ar", "addr", addr, "tenant_fixo", cfg.TenantFixo)
	if err := http.ListenAndServe(addr, mux); err != nil {
		slog.Error("servidor parou", "err", err)
		os.Exit(1)
	}
}
