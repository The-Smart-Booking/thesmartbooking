package auth

import (
	"crypto/rand"
	"crypto/subtle"
	"encoding/base64"
	"errors"
	"fmt"
	"strings"

	"golang.org/x/crypto/argon2"
)

// Mínimo recomendado pela OWASP para Argon2id. Os parâmetros vão gravados no
// próprio hash (formato PHC): subir o custo depois não invalida senha antiga.
const (
	argonMemoriaKiB = 19 * 1024
	argonTempo      = 2
	argonThreads    = 1
	argonSaltBytes  = 16
	argonChaveBytes = 32
)

// ErrHashInvalido indica senha_hash fora do formato PHC do Argon2id: dado
// corrompido no banco, não senha errada.
var ErrHashInvalido = errors.New("auth: hash de senha em formato inválido")

var b64 = base64.RawStdEncoding

// GerarHash devolve o hash Argon2id da senha, com salt aleatório, no formato
// $argon2id$v=19$m=19456,t=2,p=1$<salt>$<chave> que vai para usuarios.senha_hash.
func GerarHash(senha string) string {
	salt := make([]byte, argonSaltBytes)
	rand.Read(salt) // desde o Go 1.24 nunca devolve erro: sem entropia, o programa encerra
	chave := argon2.IDKey([]byte(senha), salt, argonTempo, argonMemoriaKiB, argonThreads, argonChaveBytes)
	return fmt.Sprintf("$argon2id$v=%d$m=%d,t=%d,p=%d$%s$%s",
		argon2.Version, argonMemoriaKiB, argonTempo, argonThreads,
		b64.EncodeToString(salt), b64.EncodeToString(chave))
}

// ConferirSenha diz se a senha bate com o hash. Senha errada é (false, nil);
// erro só quando o hash não está no formato esperado (ErrHashInvalido).
func ConferirSenha(senha, hash string) (bool, error) {
	// "", "argon2id", "v=19", "m=...,t=...,p=...", salt, chave
	partes := strings.Split(hash, "$")
	if len(partes) != 6 || partes[0] != "" || partes[1] != "argon2id" ||
		partes[2] != fmt.Sprintf("v=%d", argon2.Version) {
		return false, ErrHashInvalido
	}
	var memoria, tempo uint32
	var threads uint8
	if _, err := fmt.Sscanf(partes[3], "m=%d,t=%d,p=%d", &memoria, &tempo, &threads); err != nil ||
		memoria == 0 || tempo == 0 || threads == 0 { // argon2.IDKey entra em pânico com zero
		return false, ErrHashInvalido
	}
	salt, err := b64.DecodeString(partes[4])
	if err != nil || len(salt) == 0 {
		return false, ErrHashInvalido
	}
	chave, err := b64.DecodeString(partes[5])
	if err != nil || len(chave) == 0 {
		return false, ErrHashInvalido
	}

	calculada := argon2.IDKey([]byte(senha), salt, tempo, memoria, threads, uint32(len(chave)))
	// Tempo constante: com ==, o tempo de resposta vaza quantos bytes bateram.
	return subtle.ConstantTimeCompare(calculada, chave) == 1, nil
}
