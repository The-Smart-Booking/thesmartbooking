package api

import (
	"encoding/json"
	"log/slog"
	"net/http"
)

// Códigos de erro estáveis. O frontend decide pelo código, nunca pela mensagem.
// Formato documentado em docs/erros-api.md.
const (
	CodigoRequisicaoInvalida = "requisicao_invalida" // 400
	CodigoNaoEncontrado      = "nao_encontrado"      // 404
	CodigoConflitoHorario    = "conflito_horario"    // 409
	CodigoErroInterno        = "erro_interno"        // 500
)

type corpoErro struct {
	Erro struct {
		Codigo   string         `json:"codigo"`
		Mensagem string         `json:"mensagem"`
		Detalhes map[string]any `json:"detalhes,omitempty"`
	} `json:"erro"`
}

// responderErro escreve o erro no formato padrão. detalhes pode ser nil.
func responderErro(w http.ResponseWriter, status int, codigo, msg string, detalhes map[string]any) {
	var c corpoErro
	c.Erro.Codigo, c.Erro.Mensagem, c.Erro.Detalhes = codigo, msg, detalhes
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(c)
}

// responderErroInterno loga err e responde 500 genérico: mensagem do Postgres
// (ou qualquer erro interno) nunca vai para o cliente.
func responderErroInterno(w http.ResponseWriter, r *http.Request, err error) {
	slog.Error("erro interno", "metodo", r.Method, "rota", r.URL.Path, "err", err)
	responderErro(w, http.StatusInternalServerError, CodigoErroInterno, "Erro interno. Tente novamente mais tarde.", nil)
}
