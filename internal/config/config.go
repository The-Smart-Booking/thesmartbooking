// Package config é o único lugar que lê variáveis de ambiente (1.1). Os outros
// pacotes recebem a Config pronta, nunca chamam os.Getenv.
package config

import (
	"errors"
	"fmt"
	"io/fs"
	"os"
	"strconv"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/joho/godotenv"
)

// Config da API, lida uma vez na inicialização. Variáveis em .env.example.
type Config struct {
	// DatabaseURL conecta como app_user, sem BYPASSRLS (RNF07), nunca como o dono
	// das tabelas (GOOSE_DBSTRING).
	DatabaseURL string
	PortaHTTP   string

	// Sessão da Fatia 1, enquanto não há login (docs/requisitos.md §12).
	TenantFixo    uuid.UUID // TODO(T2): remover (2.12)
	PrestadorFixo uuid.UUID // TODO(T2): remover (2.12)
	// Serviço da Fatia 1, enquanto não há escolha de serviço.
	ServicoFixo uuid.UUID // TODO(T6): remover (6.6)
}

const (
	usuarioAPI  = "app_user" // criado por db/init/app_user.sh
	portaPadrao = "8080"     // a do proxy do Vite (web/vite.config.ts)
)

// Load lê o .env do diretório atual, se existir, e depois o ambiente. Variável já
// definida no ambiente vence o .env (no CI não há .env). Devolve todos os problemas
// de uma vez, cada um com o nome da variável.
func Load() (Config, error) {
	if err := godotenv.Load(); err != nil && !errors.Is(err, fs.ErrNotExist) {
		return Config{}, fmt.Errorf("config: lendo .env: %w", err)
	}

	var errs []error
	obrigatoria := func(nome string) string {
		v := os.Getenv(nome)
		if v == "" {
			errs = append(errs, fmt.Errorf("%s ausente (ver .env.example)", nome))
		}
		return v
	}
	id := func(nome string) uuid.UUID {
		v := obrigatoria(nome)
		if v == "" {
			return uuid.Nil
		}
		u, err := uuid.Parse(v)
		switch {
		case err != nil:
			errs = append(errs, fmt.Errorf("%s não é um UUID válido: %q", nome, v))
		case u == uuid.Nil:
			errs = append(errs, fmt.Errorf("%s não pode ser o UUID nulo", nome))
		}
		return u
	}

	c := Config{
		DatabaseURL:   obrigatoria("DATABASE_URL"),
		PortaHTTP:     os.Getenv("PORTA_HTTP"),
		TenantFixo:    id("TENANT_FIXO"),
		PrestadorFixo: id("PRESTADOR_FIXO"),
		ServicoFixo:   id("SERVICO_FIXO"),
	}

	if c.DatabaseURL != "" {
		// O erro do pgx já esconde a senha.
		if pc, err := pgx.ParseConfig(c.DatabaseURL); err != nil {
			errs = append(errs, fmt.Errorf("DATABASE_URL inválida: %w", err))
		} else if pc.User != usuarioAPI {
			errs = append(errs, fmt.Errorf("DATABASE_URL conecta como %q; a API usa só o %s (RNF07)", pc.User, usuarioAPI))
		}
	}

	if c.PortaHTTP == "" {
		c.PortaHTTP = portaPadrao
	} else if p, err := strconv.Atoi(c.PortaHTTP); err != nil || p < 1 || p > 65535 {
		errs = append(errs, fmt.Errorf("PORTA_HTTP inválida: %q", c.PortaHTTP))
	}

	if err := errors.Join(errs...); err != nil {
		return Config{}, fmt.Errorf("config:\n%w", err)
	}
	return c, nil
}
