package api

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"net/http"
	"runtime/debug"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage"
)

type chaveTx struct{}

// TxDaRequisicao devolve a transação aberta pelo ComTransacao (o Handlers.Tx da API).
func TxDaRequisicao(r *http.Request) pgx.Tx {
	tx, _ := r.Context().Value(chaveTx{}).(pgx.Tx)
	return tx
}

var errRespostaDeErro = errors.New("api: handler respondeu erro")

// ComTransacao abre uma transação por requisição (1.3), com o tenant de
// tenant(r): TENANT_FIXO na Fatia 1, a sessão na Fatia 2. Resposta 4xx/5xx ou
// panic = ROLLBACK; o resto, COMMIT.
//
// A resposta do handler só sai depois do COMMIT: se ele falhar, o cliente recebe
// 500, não um 201 de algo que não foi gravado.
func ComTransacao(s *storage.Store, tenant func(*http.Request) uuid.UUID, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		adiada := &respostaAdiada{ResponseWriter: w}
		err := s.ComTenant(r.Context(), tenant(r), func(tx pgx.Tx) (err error) {
			defer func() {
				if p := recover(); p != nil {
					if p == http.ErrAbortHandler {
						panic(p) // aborto pedido pelo handler; o ROLLBACK sai no defer do ComTenant
					}
					err = fmt.Errorf("panic: %v\n%s", p, debug.Stack())
				}
			}()
			next.ServeHTTP(adiada, r.WithContext(context.WithValue(r.Context(), chaveTx{}, tx)))
			if adiada.status >= 400 {
				return errRespostaDeErro
			}
			return nil
		})
		if err != nil && !errors.Is(err, errRespostaDeErro) {
			clear(w.Header()) // o 500 não leva Location, Set-Cookie etc. do handler
			responderErroInterno(w, r, err)
			return
		}
		if adiada.status == 0 {
			adiada.status = http.StatusOK
		}
		w.WriteHeader(adiada.status)
		w.Write(adiada.corpo.Bytes())
	})
}

// ComTransacaoNasRotas é o handler da API: requisição que casa com uma rota do
// mux vai pelo ComTransacao; rota inexistente (404) e método não permitido (405)
// respondem no formato da API sem abrir transação (1.13).
func ComTransacaoNasRotas(s *storage.Store, tenant func(*http.Request) uuid.UUID, mux *http.ServeMux) http.Handler {
	comTx := ComTransacao(s, tenant, mux)
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if h, padrao := mux.Handler(r); padrao == "" {
			h.ServeHTTP(erroDoMux{w}, r) // o handler de 404/405 do próprio mux, que seta o Allow
			return
		}
		comTx.ServeHTTP(w, r)
	})
}

// erroDoMux troca o 404/405 text/plain do ServeMux pelo formato da API; os
// cabeçalhos que o mux setou (o Allow do 405) ficam.
type erroDoMux struct{ http.ResponseWriter }

func (e erroDoMux) WriteHeader(status int) {
	if status == http.StatusMethodNotAllowed {
		responderErro(e.ResponseWriter, status, CodigoMetodoNaoPermitido, "Método não permitido nesta rota.", nil)
		return
	}
	responderErro(e.ResponseWriter, http.StatusNotFound, CodigoNaoEncontrado, "Rota não encontrada.", nil)
}

func (erroDoMux) Write(b []byte) (int, error) { return len(b), nil } // descarta o corpo text/plain

// respostaAdiada guarda status e corpo até o fim da transação. Os cabeçalhos vão
// direto para o w (só saem no WriteHeader dele); no 500, o ComTransacao os limpa.
type respostaAdiada struct {
	http.ResponseWriter
	status int
	corpo  bytes.Buffer
}

func (a *respostaAdiada) WriteHeader(status int) {
	if a.status == 0 {
		a.status = status
	}
}

func (a *respostaAdiada) Write(b []byte) (int, error) {
	a.WriteHeader(http.StatusOK)
	return a.corpo.Write(b)
}
