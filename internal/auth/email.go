// Package auth reúne a autenticação da Fatia 2 (cadastro, login e sessão).
package auth

import "strings"

// NormalizarEmail tira espaços das pontas e passa para minúsculas, no cadastro e
// no login (2.15). O índice usuarios_email_unico, em lower(email), garante a
// unicidade mesmo se alguém gravar sem passar por aqui.
func NormalizarEmail(email string) string {
	return strings.ToLower(strings.TrimSpace(email))
}
