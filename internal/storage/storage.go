// Package storage é a única porta para o banco (requisitos §6.2, camada b): toda
// função de acesso a dados recebe tenantID logo depois do ctx, e o código gerado
// pelo sqlc (internal/storage/db) não é importado fora daqui.
//
// Os repositórios recebem a transação já aberta com set_config('app.tenant_id',
// ..., true) (helper da 1.3), nunca o pool: pgx.Tx em vez de db.DBTX para o pool
// não caber no lugar por engano. Os construtores (Novo*Repo) não acessam o banco:
// a regra (ctx, tenantID, ...) vale para os métodos.
package storage

import (
	"context"
	"errors"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgtype"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage/db"
)

// ErrNaoEncontrado: registro inexistente ou de outro tenant (o RLS nem deixa ver).
// A API responde 404 nao_encontrado (docs/erros-api.md).
var ErrNaoEncontrado = errors.New("storage: não encontrado")

// Tipos do código gerado, para quem está fora de storage não importar db.
// Cliente (com vinculo_token) só sai do Criar; as leituras devolvem os *Row, sem ele.
type (
	Cliente           = db.Cliente
	ListarClientesRow = db.ListarClientesRow
	BuscarClienteRow  = db.BuscarClienteRow
	Agendamento       = db.Agendamento
)

func naoEncontrado(err error) error {
	if errors.Is(err, pgx.ErrNoRows) {
		return ErrNaoEncontrado
	}
	return err
}

type ClientesRepo struct{ q *db.Queries }

func NovoClientesRepo(tx pgx.Tx) *ClientesRepo { return &ClientesRepo{db.New(tx)} }

func (r *ClientesRepo) Listar(ctx context.Context, tenantID uuid.UUID) ([]ListarClientesRow, error) {
	return r.q.ListarClientes(ctx, tenantID)
}

func (r *ClientesRepo) Buscar(ctx context.Context, tenantID, id uuid.UUID) (BuscarClienteRow, error) {
	c, err := r.q.BuscarCliente(ctx, db.BuscarClienteParams{TenantID: tenantID, ID: id})
	return c, naoEncontrado(err)
}

func (r *ClientesRepo) Criar(ctx context.Context, tenantID uuid.UUID, nome string, telefone *string) (Cliente, error) {
	return r.q.CriarCliente(ctx, db.CriarClienteParams{TenantID: tenantID, Nome: nome, Telefone: telefone})
}

type AgendamentosRepo struct{ q *db.Queries }

func NovoAgendamentosRepo(tx pgx.Tx) *AgendamentosRepo { return &AgendamentosRepo{db.New(tx)} }

// Listar devolve os agendamentos com inicio em [de, ate). prestadorID nil = a
// empresa toda: quem monta o filtro é o middleware de escopo (2.7), nunca o handler.
func (r *AgendamentosRepo) Listar(ctx context.Context, tenantID uuid.UUID, de, ate time.Time, prestadorID *uuid.UUID) ([]Agendamento, error) {
	return r.q.ListarAgendamentos(ctx, db.ListarAgendamentosParams{
		TenantID:    tenantID,
		De:          pgtype.Timestamptz{Time: de, Valid: true},
		Ate:         pgtype.Timestamptz{Time: ate, Valid: true},
		PrestadorID: prestadorID,
	})
}

func (r *AgendamentosRepo) Buscar(ctx context.Context, tenantID, id uuid.UUID) (Agendamento, error) {
	a, err := r.q.BuscarAgendamento(ctx, db.BuscarAgendamentoParams{TenantID: tenantID, ID: id})
	return a, naoEncontrado(err)
}
