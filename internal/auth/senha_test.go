package auth

import (
	"encoding/base64"
	"errors"
	"strings"
	"testing"

	"golang.org/x/crypto/argon2"
)

func TestGerarHashEConferirSenha(t *testing.T) {
	const senha = "senha-de-teste-123"
	hash := GerarHash(senha)

	if !strings.HasPrefix(hash, "$argon2id$v=19$m=19456,t=2,p=1$") {
		t.Errorf("hash fora do formato PHC esperado: %q", hash)
	}
	if strings.Contains(hash, senha) {
		t.Error("hash contém a senha em texto puro")
	}
	if outro := GerarHash(senha); outro == hash {
		t.Error("dois hashes da mesma senha saíram iguais: o salt não é aleatório")
	}

	casos := []struct {
		nome, senha string
		quer        bool
	}{
		{"senha certa", senha, true},
		{"senha errada", "senha-errada", false},
		{"vazia", "", false},
		{"maiúscula muda a senha", strings.ToUpper(senha), false},
	}
	for _, c := range casos {
		t.Run(c.nome, func(t *testing.T) {
			ok, err := ConferirSenha(c.senha, hash)
			if err != nil {
				t.Fatalf("erro inesperado: %v", err)
			}
			if ok != c.quer {
				t.Errorf("ConferirSenha(%q) = %v, quer %v", c.senha, ok, c.quer)
			}
		})
	}
}

// Hash gravado com outros parâmetros continua conferindo: é o que permite subir
// o custo depois sem migration.
func TestConferirSenhaUsaParametrosDoHash(t *testing.T) {
	salt := []byte("salt-de-16-bytes")
	chave := argon2.IDKey([]byte("antiga"), salt, 1, 64, 1, 32)
	b := base64.RawStdEncoding
	hash := "$argon2id$v=19$m=64,t=1,p=1$" + b.EncodeToString(salt) + "$" + b.EncodeToString(chave)

	ok, err := ConferirSenha("antiga", hash)
	if err != nil || !ok {
		t.Errorf("ConferirSenha = (%v, %v), quer (true, nil)", ok, err)
	}
}

func TestConferirSenhaHashInvalido(t *testing.T) {
	partes := strings.Split(GerarHash("x"), "$")
	trocando := func(i int, v string) string {
		p := append([]string(nil), partes...)
		p[i] = v
		return strings.Join(p, "$")
	}

	casos := map[string]string{
		"vazio":            "",
		"bcrypt":           "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW",
		"argon2i":          trocando(1, "argon2i"),
		"versão antiga":    trocando(2, "v=16"),
		"parâmetros ruins": trocando(3, "m=abc"),
		"threads zero":     trocando(3, "m=19456,t=2,p=0"),
		"tempo zero":       trocando(3, "m=19456,t=0,p=1"),
		"salt sem base64":  trocando(4, "!!!"),
		"chave vazia":      trocando(5, ""),
	}
	for nome, hash := range casos {
		t.Run(nome, func(t *testing.T) {
			ok, err := ConferirSenha("x", hash)
			if ok || !errors.Is(err, ErrHashInvalido) {
				t.Errorf("ConferirSenha(%q) = (%v, %v), quer (false, ErrHashInvalido)", hash, ok, err)
			}
		})
	}
}
