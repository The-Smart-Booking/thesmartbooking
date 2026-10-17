// Package api tem os handlers HTTP. Toda rota fica sob /api (requisitos §12) e
// todo erro sai no formato de docs/erros-api.md.
package api

import (
	"encoding/json"
	"io"
	"net/http"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

// Handlers guarda o que os handlers precisam por requisição.
type Handlers struct {
	// TenantID é o TENANT_FIXO na Fatia 1; passa a vir da sessão na Fatia 2.
	TenantID uuid.UUID
	// Tx devolve a transação da requisição, já com set_config('app.tenant_id',
	// ..., true). Quem abre é o middleware da 1.3; nos testes, o próprio teste.
	Tx func(*http.Request) pgx.Tx
	// PRESTADOR_FIXO e SERVICO_FIXO (1.1): a Fatia 1 agenda sempre com eles.
	PrestadorID, ServicoID uuid.UUID
}

// Rotas registra as rotas no mux.
func (h *Handlers) Rotas(mux *http.ServeMux) {
	mux.HandleFunc("POST /api/clientes", h.criarCliente)
	mux.HandleFunc("GET /api/clientes", h.listarClientes)
	mux.HandleFunc("GET /api/agendamentos", h.listarAgendamentos)
	mux.HandleFunc("POST /api/agendamentos", h.criarAgendamento)
}

const limiteCorpo = 1 << 20 // 1 MiB

// lerJSON decodifica o corpo em dst. Em erro, já respondeu 400 e devolve false.
func lerJSON(w http.ResponseWriter, r *http.Request, dst any) bool {
	dec := json.NewDecoder(http.MaxBytesReader(w, r.Body, limiteCorpo))
	dec.DisallowUnknownFields() // campo com nome errado vira 400, não some calado
	// O segundo Decode barra lixo depois do objeto, ex.: {"a":1}{"a":2}.
	if err := dec.Decode(dst); err != nil || dec.Decode(&struct{}{}) != io.EOF {
		responderErro(w, http.StatusBadRequest, CodigoRequisicaoInvalida, "Corpo da requisição não é um JSON válido.", nil)
		return false
	}
	return true
}

func responderJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(v)
}

// campoInvalido responde 400 apontando o campo, para o formulário marcar.
func campoInvalido(w http.ResponseWriter, campo, msg string) {
	responderErro(w, http.StatusBadRequest, CodigoRequisicaoInvalida, msg, map[string]any{"campo": campo})
}
