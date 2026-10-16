package api

import (
	"net/http"
	"time"

	"github.com/google/uuid"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

// agendamentoJSON: datas sempre em UTC ("...Z"); a conversão para o fuso do
// tenant é da borda (6.4).
type agendamentoJSON struct {
	ID            uuid.UUID `json:"id"`
	ClienteID     uuid.UUID `json:"cliente_id"`
	ClienteNome   string    `json:"cliente_nome,omitempty"`
	ServicoID     uuid.UUID `json:"servico_id"`
	PrestadorID   uuid.UUID `json:"prestador_id"`
	Inicio        time.Time `json:"inicio"`
	Fim           time.Time `json:"fim"`
	Status        string    `json:"status"`
	ValorCentavos int32     `json:"valor_centavos"`
}

const (
	periodoPadrao = 7 * 24 * time.Hour
	periodoMaximo = 92 * 24 * time.Hour // um trimestre; o calendário (7.1) pede uma semana
)

// listarAgendamentos: GET /api/agendamentos?de=&ate=&prestador_id=, período
// [de, ate) pelo inicio, em RFC 3339. Sem de, hoje; sem ate, de + 7 dias (e
// vice-versa). Sem prestador_id, a empresa toda: restringir ao prestador da
// sessão é da 2.7.
func (h *Handlers) listarAgendamentos(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	de, ok := lerData(w, q.Get("de"), "de")
	if !ok {
		return
	}
	ate, ok := lerData(w, q.Get("ate"), "ate")
	if !ok {
		return
	}
	switch {
	case de.IsZero() && ate.IsZero():
		// TODO(T6): "hoje" em UTC; passa a ser no fuso do tenant na 6.4.
		de = time.Now().UTC().Truncate(24 * time.Hour)
		ate = de.Add(periodoPadrao)
	case de.IsZero():
		de = ate.Add(-periodoPadrao)
	case ate.IsZero():
		ate = de.Add(periodoPadrao)
	}
	if ate.Before(de) {
		campoInvalido(w, "ate", "A data final é anterior à inicial.")
		return
	}
	if ate.Sub(de) > periodoMaximo {
		campoInvalido(w, "ate", "Período maior que 92 dias.")
		return
	}
	var prestadorID *uuid.UUID
	if v := q.Get("prestador_id"); v != "" {
		id, err := uuid.Parse(v)
		if err != nil {
			campoInvalido(w, "prestador_id", "prestador_id não é um UUID válido.")
			return
		}
		prestadorID = &id
	}

	as, err := storage.NovoAgendamentosRepo(h.Tx(r)).Listar(r.Context(), h.TenantID, de, ate, prestadorID)
	if err != nil {
		responderErroInterno(w, r, err)
		return
	}
	out := make([]agendamentoJSON, len(as)) // lista vazia sai [], não null
	for i, a := range as {
		out[i] = agendamentoJSON{a.ID, a.ClienteID, a.ClienteNome, a.ServicoID, a.PrestadorID,
			a.Inicio.Time.UTC(), a.Fim.Time.UTC(), a.Status, a.ValorCentavos}
	}
	responderJSON(w, http.StatusOK, out)
}

// lerData lê uma data RFC 3339 (ISO 8601 com fuso). Vazia devolve o zero. Em
// erro, já respondeu 400 e devolve false.
func lerData(w http.ResponseWriter, v, campo string) (time.Time, bool) {
	if v == "" {
		return time.Time{}, true
	}
	t, err := time.Parse(time.RFC3339, v)
	if err != nil {
		campoInvalido(w, campo, campo+" deve estar em ISO 8601 com fuso, ex.: 2026-10-16T09:00:00-03:00.")
		return time.Time{}, false
	}
	return t, true
}
