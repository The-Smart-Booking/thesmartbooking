package api

import (
	"encoding/json"
	"net/http"
	"strings"
	"testing"
)

func TestCriarEListarClientes(t *testing.T) {
	h, _ := comBanco(t, seedAlfa)

	w := chamar(t, h, "POST", "/api/clientes", `{"nome":"  Carla Teste ","telefone":"11 90000-0000"}`)
	var criado clienteJSON
	if w.Code != http.StatusCreated || json.Unmarshal(w.Body.Bytes(), &criado) != nil {
		t.Fatalf("criar: %d %s", w.Code, w.Body)
	}
	if criado.Nome != "Carla Teste" || criado.Telefone == nil || *criado.Telefone != "11 90000-0000" {
		t.Errorf("criado = %+v", criado)
	}
	if strings.Contains(w.Body.String(), "vinculo_token") {
		t.Error("vinculo_token é credencial e não pode sair na resposta")
	}

	w = chamar(t, h, "GET", "/api/clientes", "")
	var lista []clienteJSON
	if w.Code != http.StatusOK || json.Unmarshal(w.Body.Bytes(), &lista) != nil {
		t.Fatalf("listar: %d %s", w.Code, w.Body)
	}
	ids := map[string]bool{}
	for _, c := range lista {
		ids[c.ID.String()] = true
	}
	if !ids[criado.ID.String()] || !ids[seedClienteAlfa.String()] {
		t.Errorf("listagem sem o cliente criado ou sem o do seed: %s", w.Body)
	}
	if ids[seedClienteBeta.String()] {
		t.Error("cliente do Beta na listagem do Alfa")
	}
}

func TestCriarClienteInvalido(t *testing.T) {
	for nome, corpo := range map[string]string{
		"json malformado":    `{"nome":`,
		"sem nome":           `{"telefone":"1199"}`,
		"nome só espaços":    `{"nome":"   "}`,
		"nome longo":         `{"nome":"` + strings.Repeat("a", 201) + `"}`,
		"telefone longo":     `{"nome":"Ana","telefone":"` + strings.Repeat("9", 31) + `"}`,
		"campo desconhecido": `{"nome":"Ana","email":"a@b.c"}`,
		"dois objetos":       `{"nome":"Ana"}{"nome":"Bia"}`,
	} {
		t.Run(nome, func(t *testing.T) {
			exigirErro(t, chamar(t, semBanco(t), "POST", "/api/clientes", corpo), http.StatusBadRequest, CodigoRequisicaoInvalida)
		})
	}
}
