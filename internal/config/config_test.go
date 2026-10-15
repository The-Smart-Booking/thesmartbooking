package config

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/google/uuid"
)

// Testes da 1.1. Cada teste monta o próprio ambiente: no CI não há .env nem as
// variáveis do config. t.Setenv e t.Chdir restauram tudo no fim.

var variaveis = []string{"DATABASE_URL", "PORTA_HTTP", "TENANT_FIXO", "PRESTADOR_FIXO", "SERVICO_FIXO"}

// UUIDs do Alfa no seed (db/seed.sql, tabela no README).
const (
	urlAppUser    = "postgres://app_user:x@localhost:5467/example?sslmode=disable"
	tenantAlfa    = "11111111-1111-1111-1111-111111111111"
	prestadorAlfa = "cccccccc-cccc-cccc-cccc-cccccccccccc"
	servicoCorte  = "11111111-0005-0000-0000-000000000001"
)

// semAmbiente roda o teste num diretório sem .env e sem as variáveis do config.
// O t.Setenv antes do Unsetenv faz o valor original voltar no fim do teste.
func semAmbiente(t *testing.T) {
	t.Helper()
	t.Chdir(t.TempDir())
	for _, v := range variaveis {
		t.Setenv(v, "")
		os.Unsetenv(v)
	}
}

func ambienteValido(t *testing.T) {
	t.Helper()
	semAmbiente(t)
	t.Setenv("DATABASE_URL", urlAppUser)
	t.Setenv("TENANT_FIXO", tenantAlfa)
	t.Setenv("PRESTADOR_FIXO", prestadorAlfa)
	t.Setenv("SERVICO_FIXO", servicoCorte)
}

func escreverEnv(t *testing.T, conteudo string) {
	t.Helper()
	if err := os.WriteFile(".env", []byte(conteudo), 0o600); err != nil {
		t.Fatal(err)
	}
}

// erroCom falha se Load não der erro ou se a mensagem não citar cada trecho.
func erroCom(t *testing.T, trechos ...string) {
	t.Helper()
	_, err := Load()
	if err == nil {
		t.Fatal("Load sem erro, queria erro")
	}
	for _, s := range trechos {
		if !strings.Contains(err.Error(), s) {
			t.Errorf("erro não cita %q:\n%v", s, err)
		}
	}
}

func TestLoadValido(t *testing.T) {
	ambienteValido(t)
	c, err := Load()
	if err != nil {
		t.Fatal(err)
	}
	if c.DatabaseURL != urlAppUser || c.PortaHTTP != "8080" {
		t.Errorf("DatabaseURL = %q, PortaHTTP = %q", c.DatabaseURL, c.PortaHTTP)
	}
	if c.TenantFixo != uuid.MustParse(tenantAlfa) || c.PrestadorFixo != uuid.MustParse(prestadorAlfa) || c.ServicoFixo != uuid.MustParse(servicoCorte) {
		t.Errorf("UUIDs = %v %v %v", c.TenantFixo, c.PrestadorFixo, c.ServicoFixo)
	}
}

func TestLoadSemTenantFixo(t *testing.T) {
	ambienteValido(t)
	os.Unsetenv("TENANT_FIXO")
	erroCom(t, "TENANT_FIXO ausente")
}

func TestLoadListaTodasAsAusentes(t *testing.T) {
	semAmbiente(t)
	erroCom(t, "DATABASE_URL ausente", "TENANT_FIXO ausente", "PRESTADOR_FIXO ausente", "SERVICO_FIXO ausente")
}

func TestLoadUUIDInvalido(t *testing.T) {
	for _, v := range []string{"abc", " " + tenantAlfa, "00000000-0000-0000-0000-000000000000"} {
		t.Run(v, func(t *testing.T) {
			ambienteValido(t)
			t.Setenv("PRESTADOR_FIXO", v)
			erroCom(t, "PRESTADOR_FIXO")
		})
	}
}

func TestLoadDatabaseURLDoDono(t *testing.T) {
	ambienteValido(t)
	t.Setenv("DATABASE_URL", "postgres://example:example@localhost:5467/example?sslmode=disable")
	erroCom(t, "DATABASE_URL", "app_user")
}

func TestLoadPorta(t *testing.T) {
	ambienteValido(t)
	t.Setenv("PORTA_HTTP", "9090")
	c, err := Load()
	if err != nil || c.PortaHTTP != "9090" {
		t.Fatalf("PortaHTTP = %q, err = %v", c.PortaHTTP, err)
	}
	for _, v := range []string{"abc", "0", "70000"} {
		t.Run(v, func(t *testing.T) {
			ambienteValido(t)
			t.Setenv("PORTA_HTTP", v)
			erroCom(t, "PORTA_HTTP")
		})
	}
}

func TestLoadLeArquivoEnv(t *testing.T) {
	semAmbiente(t)
	escreverEnv(t, "DATABASE_URL="+urlAppUser+"\nTENANT_FIXO="+tenantAlfa+
		"\nPRESTADOR_FIXO="+prestadorAlfa+"\nSERVICO_FIXO="+servicoCorte+"\nPORTA_HTTP=8081\n")
	c, err := Load()
	if err != nil {
		t.Fatal(err)
	}
	if c.TenantFixo != uuid.MustParse(tenantAlfa) || c.PortaHTTP != "8081" {
		t.Errorf("config = %+v", c)
	}
}

func TestLoadAmbienteVenceArquivo(t *testing.T) {
	ambienteValido(t)
	escreverEnv(t, "TENANT_FIXO=22222222-2222-2222-2222-222222222222\n")
	c, err := Load()
	if err != nil {
		t.Fatal(err)
	}
	if c.TenantFixo != uuid.MustParse(tenantAlfa) {
		t.Errorf("TenantFixo = %v, o .env sobrescreveu o ambiente", c.TenantFixo)
	}
}

// Critério da 1.1: só internal/config lê o ambiente. Testes ficam de fora (os de
// internal/storage leem DATABASE_URL e CI para decidir se pulam).
func TestSoConfigLeAmbiente(t *testing.T) {
	raiz, err := filepath.Abs(filepath.Join("..", ".."))
	if err != nil {
		t.Fatal(err)
	}
	config := filepath.Join(raiz, "internal", "config")
	err = filepath.WalkDir(raiz, func(p string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if d.IsDir() {
			switch d.Name() {
			case ".git", "node_modules", "web":
				return filepath.SkipDir
			}
			if p == config {
				return filepath.SkipDir
			}
			return nil
		}
		if !strings.HasSuffix(p, ".go") || strings.HasSuffix(p, "_test.go") {
			return nil
		}
		b, err := os.ReadFile(p)
		if err != nil {
			return err
		}
		if s := string(b); strings.Contains(s, "os.Getenv(") || strings.Contains(s, "os.LookupEnv(") {
			t.Errorf("%s lê o ambiente direto; use internal/config", p)
		}
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
}
