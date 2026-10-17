// Package storage é a única porta para o banco (requisitos §6.2, camada b): toda
// função de acesso a dados recebe tenantID logo depois do ctx, e o código gerado
// pelo sqlc (internal/storage/db) não é importado fora daqui.
//
// Os repositórios recebem a transação já aberta com set_config('app.tenant_id',
// ..., true) pelo Store.ComTenant, nunca o pool: pgx.Tx em vez de db.DBTX para o pool
// não caber no lugar por engano. Os construtores (Novo*Repo) não acessam o banco:
// a regra (ctx, tenantID, ...) vale para os métodos.
package storage

import (
	"context"
	"errors"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgconn"
	"github.com/jackc/pgx/v5/pgtype"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/The-Smart-Booking/thesmartbooking/internal/storage/db"
)

// ErrNaoEncontrado: registro inexistente ou de outro tenant (o RLS nem deixa ver).
// A API responde 404 nao_encontrado (docs/erros-api.md).
var ErrNaoEncontrado = errors.New("storage: não encontrado")

// ErrHorarioIndisponivel: a constraint sem_sobreposicao barrou (SQLSTATE 23P01).
// A API responde 409 conflito_horario.
var ErrHorarioIndisponivel = errors.New("storage: horário indisponível")

// Tipos do código gerado, para quem está fora de storage não importar db.
// Cliente (com vinculo_token) só sai do Criar; as leituras devolvem os *Row, sem ele.
type (
	Cliente               = db.Cliente
	ListarClientesRow     = db.ListarClientesRow
	BuscarClienteRow      = db.BuscarClienteRow
	Agendamento           = db.Agendamento
	AgendamentoComCliente = db.ListarAgendamentosRow
)

// Store abre as transações com tenant sobre o pool do app_user (DATABASE_URL).
type Store struct{ pool *pgxpool.Pool }

func NovoStore(pool *pgxpool.Pool) *Store { return &Store{pool} }

// ComTenant roda fn numa transação com set_config('app.tenant_id', ..., true):
// o tenant morre com a transação e não vaza para quem pegar a conexão depois
// (guia, armadilha 2). fn com erro ou panic = ROLLBACK; sem erro, COMMIT.
func (s *Store) ComTenant(ctx context.Context, tenantID uuid.UUID, fn func(tx pgx.Tx) error) error {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return err
	}
	defer tx.Rollback(ctx) // no-op depois do Commit

	if _, err := tx.Exec(ctx, "SELECT set_config('app.tenant_id', $1, true)", tenantID.String()); err != nil {
		return err
	}
	if err := fn(tx); err != nil {
		return err
	}
	return tx.Commit(ctx)
}

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
func (r *AgendamentosRepo) Listar(ctx context.Context, tenantID uuid.UUID, de, ate time.Time, prestadorID *uuid.UUID) ([]AgendamentoComCliente, error) {
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

// NovoAgendamento: o que vem da requisição. Fim nil = inicio + duração do serviço.
type NovoAgendamento struct {
	ClienteID, ServicoID, PrestadorID uuid.UUID
	Inicio                            time.Time
	Fim                               *time.Time
}

// Criar grava com valor_centavos copiado do serviço. ErrNaoEncontrado: prestador
// sem membership ativo ou serviço inexistente/inativo. ErrHorarioIndisponivel:
// sobreposição com outro agendamento do mesmo prestador.
func (r *AgendamentosRepo) Criar(ctx context.Context, tenantID uuid.UUID, n NovoAgendamento) (Agendamento, error) {
	p := db.CriarAgendamentoParams{
		TenantID:    tenantID,
		ClienteID:   n.ClienteID,
		ServicoID:   n.ServicoID,
		PrestadorID: n.PrestadorID,
		Inicio:      pgtype.Timestamptz{Time: n.Inicio, Valid: true},
	}
	if n.Fim != nil {
		p.Fim = pgtype.Timestamptz{Time: *n.Fim, Valid: true}
	}
	a, err := r.q.CriarAgendamento(ctx, p)
	if pgErr, ok := errors.AsType[*pgconn.PgError](err); ok && pgErr.Code == "23P01" {
		return a, ErrHorarioIndisponivel
	}
	return a, naoEncontrado(err)
}
