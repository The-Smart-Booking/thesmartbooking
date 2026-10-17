package api

import (
	"errors"
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

const duracaoMaxima = 24 * time.Hour // a mesma da duração do serviço (guia, §005)

// criarAgendamento: POST /api/agendamentos {cliente_id, inicio, fim?}, datas em
// RFC 3339. Sem fim, vale a duração do serviço. Prestador e serviço são os fixos
// da Fatia 1; o preço vem do serviço, nunca do corpo.
func (h *Handlers) criarAgendamento(w http.ResponseWriter, r *http.Request) {
	var in struct {
		ClienteID string `json:"cliente_id"`
		Inicio    string `json:"inicio"`
		Fim       string `json:"fim"`
	}
	if !lerJSON(w, r, &in) {
		return
	}
	clienteID, err := uuid.Parse(in.ClienteID)
	if err != nil {
		campoInvalido(w, "cliente_id", "Informe o cliente.")
		return
	}
	if in.Inicio == "" {
		campoInvalido(w, "inicio", "Informe o início.")
		return
	}
	inicio, ok := lerData(w, in.Inicio, "inicio")
	if !ok {
		return
	}
	n := storage.NovoAgendamento{ClienteID: clienteID, ServicoID: h.ServicoID, PrestadorID: h.PrestadorID, Inicio: inicio}
	if in.Fim != "" {
		fim, ok := lerData(w, in.Fim, "fim")
		if !ok {
			return
		}
		if !fim.After(inicio) || fim.Sub(inicio) > duracaoMaxima {
			campoInvalido(w, "fim", "O fim deve ser depois do início, em até 24 horas.")
			return
		}
		n.Fim = &fim
	}

	tx := h.Tx(r)
	// Cliente antes, para dizer qual campo falhou: outro tenant ou removido = 404.
	c, err := storage.NovoClientesRepo(tx).Buscar(r.Context(), h.TenantID, clienteID)
	if errors.Is(err, storage.ErrNaoEncontrado) {
		responderErro(w, http.StatusNotFound, CodigoNaoEncontrado, "Cliente não encontrado.", map[string]any{"campo": "cliente_id"})
		return
	}
	if err != nil {
		responderErroInterno(w, r, err)
		return
	}
	a, err := storage.NovoAgendamentosRepo(tx).Criar(r.Context(), h.TenantID, n)
	switch {
	case errors.Is(err, storage.ErrHorarioIndisponivel):
		responderErro(w, http.StatusConflict, CodigoConflitoHorario, "Já existe um agendamento nesse horário.", map[string]any{"prestador_id": h.PrestadorID})
	case errors.Is(err, storage.ErrNaoEncontrado):
		responderErro(w, http.StatusNotFound, CodigoNaoEncontrado, "Prestador ou serviço indisponível.", nil)
	case err != nil:
		responderErroInterno(w, r, err)
	default:
		responderJSON(w, http.StatusCreated, agendamentoJSON{a.ID, a.ClienteID, c.Nome, a.ServicoID, a.PrestadorID,
			a.Inicio.Time.UTC(), a.Fim.Time.UTC(), a.Status, a.ValorCentavos})
	}
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
