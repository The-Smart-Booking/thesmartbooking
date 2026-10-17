package api

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
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

func TestCriarAgendamento(t *testing.T) {
	h, tx := comBanco(t, seedAlfa)
	ctx := context.Background()
	criar := func(corpo string) *httptest.ResponseRecorder {
		t.Helper()
		return chamar(t, h, "POST", "/api/agendamentos", corpo)
	}

	// Sem fim: duração do Corte (30 min). Preço do Corte no seed: 4500.
	w := criar(`{"cliente_id":"` + seedClienteAlfa.String() + `","inicio":"2099-02-01T06:00:00-03:00"}`)
	var a agendamentoJSON
	if w.Code != http.StatusCreated || json.Unmarshal(w.Body.Bytes(), &a) != nil {
		t.Fatalf("criar: %d %s", w.Code, w.Body)
	}
	if !a.Inicio.Equal(time.Date(2099, 2, 1, 9, 0, 0, 0, time.UTC)) || a.Fim.Sub(a.Inicio) != 30*time.Minute ||
		a.ValorCentavos != 4500 || a.PrestadorID != seedPrestador || a.ClienteNome == "" || a.Inicio.Location() != time.UTC {
		t.Errorf("criado = %+v", a)
	}

	// Consecutivo: 9h30–10h logo depois de 9h–9h30.
	if w := criar(`{"cliente_id":"` + seedClienteAlfa.String() + `","inicio":"2099-02-01T09:30:00Z","fim":"2099-02-01T10:00:00Z"}`); w.Code != http.StatusCreated {
		t.Errorf("consecutivo: %d %s", w.Code, w.Body)
	}

	// Preço congelado: mudar o serviço depois não mexe no agendamento.
	if _, err := tx.Exec(ctx, "UPDATE servicos SET preco_centavos = 9999 WHERE id = $1", seedServicoAlfa); err != nil {
		t.Fatal(err)
	}
	var valor int32
	if err := tx.QueryRow(ctx, "SELECT valor_centavos FROM agendamentos WHERE id = $1", a.ID).Scan(&valor); err != nil || valor != 4500 {
		t.Errorf("valor depois de mudar o preço: %d, err %v", valor, err)
	}

	// Cliente do Beta: o Alfa nem enxerga.
	exigirErro(t, criar(`{"cliente_id":"`+seedClienteBeta.String()+`","inicio":"2099-02-02T09:00:00Z"}`), http.StatusNotFound, CodigoNaoEncontrado)

	// Por último: a violação 23P01 aborta a transação.
	exigirErro(t, criar(`{"cliente_id":"`+seedClienteAlfa.String()+`","inicio":"2099-02-01T09:15:00Z"}`), http.StatusConflict, CodigoConflitoHorario)
}

func TestCriarAgendamentoPrestadorRemovido(t *testing.T) {
	h, tx := comBanco(t, seedAlfa)
	if _, err := tx.Exec(context.Background(), "UPDATE memberships SET removido_em = now() WHERE usuario_id = $1 AND tenant_id = $2", seedPrestador, seedAlfa); err != nil {
		t.Fatal(err)
	}
	w := chamar(t, h, "POST", "/api/agendamentos", `{"cliente_id":"`+seedClienteAlfa.String()+`","inicio":"2099-02-03T09:00:00Z"}`)
	exigirErro(t, w, http.StatusNotFound, CodigoNaoEncontrado)
}

func TestCriarAgendamentoInvalido(t *testing.T) {
	c := `"cliente_id":"` + seedClienteAlfa.String() + `"`
	for nome, corpo := range map[string]string{
		"sem cliente":         `{"inicio":"2099-02-01T09:00:00Z"}`,
		"cliente não uuid":    `{"cliente_id":"1","inicio":"2099-02-01T09:00:00Z"}`,
		"sem inicio":          `{` + c + `}`,
		"inicio sem fuso":     `{` + c + `,"inicio":"2099-02-01T09:00:00"}`,
		"fim antes do inicio": `{` + c + `,"inicio":"2099-02-01T09:00:00Z","fim":"2099-02-01T08:00:00Z"}`,
		"fim igual ao inicio": `{` + c + `,"inicio":"2099-02-01T09:00:00Z","fim":"2099-02-01T09:00:00Z"}`,
		"mais de 24h":         `{` + c + `,"inicio":"2099-02-01T09:00:00Z","fim":"2099-02-02T10:00:00Z"}`,
		"valor no payload":    `{` + c + `,"inicio":"2099-02-01T09:00:00Z","valor_centavos":1}`,
	} {
		t.Run(nome, func(t *testing.T) {
			exigirErro(t, chamar(t, semBanco(t), "POST", "/api/agendamentos", corpo), http.StatusBadRequest, CodigoRequisicaoInvalida)
		})
	}
}
