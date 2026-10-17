package api

import (
	"context"
	"encoding/json"
	"net/http"
	"strings"
	"testing"
	"time"

	"github.com/google/uuid"
)

func TestListarAgendamentos(t *testing.T) {
	h, tx := comBanco(t, seedAlfa)
	// Em 2099, longe das datas relativas do seed.
	dia := time.Date(2099, 1, 10, 0, 0, 0, 0, time.UTC)
	for _, a := range []struct {
		prestador uuid.UUID
		inicio    time.Time
	}{
		{seedPrestador, dia.Add(10 * time.Hour)},
		{seedAdminAlfa, dia.Add(9 * time.Hour)},
		{seedPrestador, dia.Add(10 * 24 * time.Hour)}, // fora do dia
	} {
		if _, err := tx.Exec(context.Background(), `INSERT INTO agendamentos
			(tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
			VALUES ($1, $2, $3, $4, $5, $6, 0)`,
			seedAlfa, seedClienteAlfa, seedServicoAlfa, a.prestador, a.inicio, a.inicio.Add(time.Hour)); err != nil {
			t.Fatal(err)
		}
	}
	listar := func(query string) []agendamentoJSON {
		t.Helper()
		w := chamar(t, h, "GET", "/api/agendamentos?"+query, "")
		var as []agendamentoJSON
		if w.Code != http.StatusOK || json.Unmarshal(w.Body.Bytes(), &as) != nil {
			t.Fatalf("%s: %d %s", query, w.Code, w.Body)
		}
		if strings.Contains(w.Body.String(), "-03:00") {
			t.Errorf("datas fora de UTC: %s", w.Body)
		}
		return as
	}

	// -03:00 de propósito: 2099-01-09T21:00-03:00 = 2099-01-10T00:00Z.
	as := listar("de=2099-01-09T21:00:00-03:00&ate=2099-01-11T00:00:00Z")
	if len(as) != 2 || !as[0].Inicio.Before(as[1].Inicio) {
		t.Fatalf("período do dia 10: esperava 2 em ordem de inicio, veio %+v", as)
	}
	if as[0].ClienteNome == "" || as[0].Inicio.Location() != time.UTC {
		t.Errorf("sem nome do cliente ou fora de UTC: %+v", as[0])
	}

	as = listar("de=2099-01-10T00:00:00Z&ate=2099-01-11T00:00:00Z&prestador_id=" + seedPrestador.String())
	if len(as) != 1 || as[0].PrestadorID != seedPrestador {
		t.Errorf("filtro por prestador: %+v", as)
	}
	if as := listar("de=2099-01-01T00:00:00Z&ate=2099-02-01T00:00:00Z"); len(as) != 3 {
		t.Errorf("mês todo, sem prestador: esperava 3, veio %d", len(as))
	}
	if as := listar("de=2099-03-01T00:00:00Z"); len(as) != 0 {
		t.Errorf("período vazio: %+v", as)
	}
}

func TestListarAgendamentosInvalido(t *testing.T) {
	for nome, query := range map[string]string{
		"de malformado":        "de=2099-01-10",
		"ate antes de de":      "de=2099-01-10T00:00:00Z&ate=2099-01-09T00:00:00Z",
		"período longo demais": "de=2099-01-01T00:00:00Z&ate=2099-06-01T00:00:00Z",
		"prestador não uuid":   "prestador_id=123",
	} {
		t.Run(nome, func(t *testing.T) {
			exigirErro(t, chamar(t, semBanco(t), "GET", "/api/agendamentos?"+query, ""), http.StatusBadRequest, CodigoRequisicaoInvalida)
		})
	}
}
