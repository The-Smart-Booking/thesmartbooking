package auth

import "testing"

func TestNormalizarEmail(t *testing.T) {
	casos := []struct {
		nome, entrada, quer string
	}{
		{"exemplo do card", " Ana@Exemplo.com ", "ana@exemplo.com"},
		{"já normalizado", "ana@exemplo.com", "ana@exemplo.com"},
		{"tab e quebra de linha", "\tANA@EXEMPLO.COM\n", "ana@exemplo.com"},
		{"vazio", "   ", ""},
	}
	for _, c := range casos {
		t.Run(c.nome, func(t *testing.T) {
			if got := NormalizarEmail(c.entrada); got != c.quer {
				t.Errorf("NormalizarEmail(%q) = %q, quer %q", c.entrada, got, c.quer)
			}
		})
	}
}
