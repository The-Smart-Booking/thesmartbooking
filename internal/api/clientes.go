package api

import (
	"net/http"
	"strings"
	"time"
	"unicode/utf8"

	"github.com/google/uuid"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

// clienteJSON é o que a API mostra do cliente. Sem vinculo_token: é credencial
// do Telegram (guia de banco, §004) e só sai pela rota de vínculo (Fatia 4).
type clienteJSON struct {
	ID       uuid.UUID `json:"id"`
	Nome     string    `json:"nome"`
	Telefone *string   `json:"telefone"`
	CriadoEm time.Time `json:"criado_em"`
}

const (
	maxNome     = 200
	maxTelefone = 30
)

func (h *Handlers) criarCliente(w http.ResponseWriter, r *http.Request) {
	var in struct {
		Nome     string  `json:"nome"`
		Telefone *string `json:"telefone"`
	}
	if !lerJSON(w, r, &in) {
		return
	}
	in.Nome = strings.TrimSpace(in.Nome)
	if in.Nome == "" || utf8.RuneCountInString(in.Nome) > maxNome {
		campoInvalido(w, "nome", "Informe o nome (até 200 caracteres).")
		return
	}
	if in.Telefone != nil {
		t := strings.TrimSpace(*in.Telefone)
		if utf8.RuneCountInString(t) > maxTelefone {
			campoInvalido(w, "telefone", "Telefone com mais de 30 caracteres.")
			return
		}
		in.Telefone = &t
		if t == "" {
			in.Telefone = nil // vazio = sem telefone
		}
	}

	c, err := storage.NovoClientesRepo(h.Tx(r)).Criar(r.Context(), h.TenantID, in.Nome, in.Telefone)
	if err != nil {
		responderErroInterno(w, r, err)
		return
	}
	responderJSON(w, http.StatusCreated, clienteJSON{c.ID, c.Nome, c.Telefone, c.CriadoEm.Time})
}

func (h *Handlers) listarClientes(w http.ResponseWriter, r *http.Request) {
	cs, err := storage.NovoClientesRepo(h.Tx(r)).Listar(r.Context(), h.TenantID)
	if err != nil {
		responderErroInterno(w, r, err)
		return
	}
	out := make([]clienteJSON, len(cs)) // lista vazia sai [], não null
	for i, c := range cs {
		out[i] = clienteJSON{c.ID, c.Nome, c.Telefone, c.CriadoEm.Time}
	}
	responderJSON(w, http.StatusOK, out)
}
