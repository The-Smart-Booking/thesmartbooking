package api

import (
	"encoding/json"
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestResponderErro(t *testing.T) {
	w := httptest.NewRecorder()
	responderErro(w, http.StatusConflict, CodigoConflitoHorario, "Horário ocupado.", map[string]any{"profissional_id": 7})

	if w.Code != http.StatusConflict {
		t.Fatalf("status = %d", w.Code)
	}
	if ct := w.Header().Get("Content-Type"); !strings.HasPrefix(ct, "application/json") {
		t.Fatalf("content-type = %q", ct)
	}
	var c corpoErro
	if err := json.Unmarshal(w.Body.Bytes(), &c); err != nil {
		t.Fatal(err)
	}
	if c.Erro.Codigo != CodigoConflitoHorario || c.Erro.Mensagem != "Horário ocupado." || c.Erro.Detalhes["profissional_id"] != float64(7) {
		t.Fatalf("corpo = %s", w.Body)
	}
}

func TestResponderErroInternoNaoVazaMensagem(t *testing.T) {
	w := httptest.NewRecorder()
	r := httptest.NewRequest(http.MethodPost, "/agendamentos", nil)
	responderErroInterno(w, r, errors.New(`ERROR: relation "agendamentos" does not exist (SQLSTATE 42P01)`))

	if w.Code != http.StatusInternalServerError {
		t.Fatalf("status = %d", w.Code)
	}
	body := w.Body.String()
	if strings.Contains(body, "SQLSTATE") || strings.Contains(body, "relation") || strings.Contains(body, "detalhes") {
		t.Fatalf("vazou erro interno: %s", body)
	}
	if !strings.Contains(body, `"codigo":"erro_interno"`) {
		t.Fatalf("corpo = %s", body)
	}
}
