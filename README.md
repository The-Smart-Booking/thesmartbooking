# 📅 Smart Booking

Plataforma multi-tenant de agendamentos com notificações automáticas via Telegram.

Cada organização (barbearia, clínica, estúdio) tem seus próprios usuários, serviços, horários e clientes, isolados das demais. São dois papéis: o **administrador** gerencia a empresa (convida membros e remove prestadores, define serviços e preços, vê o financeiro de todos) e também pode atender; o **prestador** vê e opera só a própria agenda. O cliente final recebe confirmação e lembrete pelo Telegram, sem precisar de conta no sistema.

> 🚧 Em desenvolvimento. Andamento no GitHub Projects (Smart Booking DevOps).

---

## 🛠️ Stack

| Camada | Tecnologia |
|---|---|
| API e worker | Go |
| Frontend | React + Vite + TypeScript |
| Banco | PostgreSQL 16 |
| Migrations | goose |
| Acesso a dados | pgx v5 + sqlc |
| Notificações | Telegram Bot API |
| Ambiente | Docker Compose |

---

## 📁 Estrutura

Hoje:

```
thesmartbooking/
├── .github/
│   ├── workflows/ci.yml        # Go (gofmt, vet, build, test) e web (lint, build) em push e PR para a main
│   └── pull_request_template.md
├── cmd/api/                    # entrada da API
├── internal/api/               # handlers e formato de erro (docs/erros-api.md)
├── db/migrations/              # goose, SQL puro
├── web/                        # frontend React + Vite
├── docs/
├── compose.yml                 # PostgreSQL 16 + Adminer
├── .env.example
├── CLAUDE.md                   # regras para o Claude Code
└── CONTRIBUTING.md
```

Estrutura-alvo (cada pasta nasce no card que a usa):

```
cmd/worker/            # consumidor da fila de notificações (T3)
internal/config/       # leitura das variáveis de ambiente (T1)
internal/storage/      # repositórios — toda função recebe tenantID (T1)
internal/storage/db/   # código gerado pelo sqlc — não editar à mão (T1)
internal/notificador/  # interface Notificador + implementação Telegram (T3)
db/queries/            # queries SQL anotadas para o sqlc (T1)
sqlc.yaml              # configuração do sqlc (T1)
db/seed.sql            # dois tenants fictícios (T0)
```

---

## 🚀 Rodando localmente

**Pré-requisitos:** Docker com Compose, Go 1.27+ e Node (para o `web/`).

```bash
git clone git@github.com:The-Smart-Booking/thesmartbooking.git
cd thesmartbooking

cp .env.example .env      # ajuste as variáveis se necessário
docker compose up -d      # PostgreSQL em localhost:5467, Adminer em localhost:8088

go run ./cmd/api          # deve imprimir "Hello World"
go test ./...
```

Frontend:

```bash
cd web
npm install
npm run dev     # http://localhost:5173 (abre em /agendamentos)
npm run lint    # o CI roda lint e build em todo PR
npm run build   # tsc em modo strict + build do Vite
```

As rotas ficam em `web/src/route.ts` (React Router); `App.tsx` é o layout com a
navegação e o `<Outlet />` onde cada tela aparece. Em dev, o Vite repassa tudo que
começa com `/api` para a API em `localhost:8080`, sem reescrever o caminho: a SPA chama
`/api/agendamentos` na mesma origem e não precisa de CORS.

### 🗄️ Migrations

goose **v3.28.0**, a mesma versão para todos. Ele lê `GOOSE_DRIVER`, `GOOSE_DBSTRING`
e `GOOSE_MIGRATION_DIR` do `.env` sozinho (copie as três do `.env.example`), então
os comandos abaixo rodam contra o Postgres do `compose.yml`, com ele de pé:

```bash
go install github.com/pressly/goose/v3/cmd/goose@v3.28.0   # instala em ~/go/bin, que precisa estar no PATH

goose status                  # o que já rodou e o que falta
goose up                      # aplica todas as pendentes
goose down                    # desfaz só a última
goose down-to 0               # desfaz todas
goose -s create nome sql      # nova migration: 0000N_nome.sql (-s = numeração sequencial)
```

Toda migration tem `Up` e `Down`, e os dois são testados: `goose up`, `goose down-to 0`, `goose up`.

### Seed e isolamento (a partir da Fatia 0)

sqlc na versão fixada em `docs/guia-banco-de-dados.md`.

```bash
sqlc generate                 # a partir da T1: regenera internal/storage/db
set -a; source .env; set +a   # exporta as variáveis para o psql
goose up                      # o seed exige o banco migrado
psql "$GOOSE_DBSTRING" -f db/seed.sql

# sem psql instalado, pelo container do compose:
docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < db/seed.sql
```

O seed roda numa transação só e para no primeiro erro. Pode rodar de novo: o que já
existe (mesmo `id`) é pulado. No CI, com o Postgres do workflow (0.17), o passo é o
mesmo, depois do `goose up`: `psql "$GOOSE_DBSTRING" -f db/seed.sql`.

Duas conexões, de propósito: `GOOSE_DBSTRING` é o dono das tabelas (migrations e seed) e ignora o RLS; `DATABASE_URL` é o `app_user`, o único usuário que a API usa.

O seed cria dois tenants fictícios (`alfa` e `beta`). São dois de propósito: com um só não é possível testar isolamento entre tenants. UUIDs fixos usados no config da T1 (1.1):

| Variável | UUID | O que é |
|---|---|---|
| `TENANT_FIXO` | `11111111-1111-1111-1111-111111111111` | tenant Barbearia Alfa |
| `PRESTADOR_FIXO` | `cccccccc-cccc-cccc-cccc-cccccccccccc` | Alfa Prestador (papel `prestador`) |
| `SERVICO_FIXO` | `11111111-0005-0000-0000-000000000001` | serviço Corte do Alfa |

O administrador do Alfa é `aaaaaaaa-…` e o da Beta, `bbbbbbbb-…`; o padrão dos demais
UUIDs está no cabeçalho de `db/seed.sql`. Os usuários do seed não fazem login: o
`senha_hash` é falso.

Conectado como `app_user` (`psql "$DATABASE_URL"`, nunca como o dono das tabelas):

```sql
BEGIN;
SELECT set_config('app.tenant_id', '11111111-1111-1111-1111-111111111111', true);

SELECT count(*) FROM clientes;   -- só clientes do tenant Alfa

SELECT count(*) FROM clientes    -- deve retornar 0
WHERE tenant_id = '22222222-2222-2222-2222-222222222222';

ROLLBACK;
```

⚠️ Se o segundo `SELECT` retornar alguma linha, o Row Level Security não está ativo. Ver `docs/guia-banco-de-dados.md`.

---

## 📐 Convenções

🗄️ **Banco.** Toda alteração de schema entra como migration versionada. Nenhum `ALTER TABLE` rodado à mão, em nenhuma circunstância.

🔑 **Contexto de tenant.** Toda requisição abre transação e define `app.tenant_id` via `set_config(..., true)`. Nunca `SET` sem escopo local — com pool de conexões, a variável vaza para a requisição seguinte e vira vazamento de dados entre organizações.

📦 **Repositórios.** SQL mora em `db/queries/` e vira Go pelo sqlc (driver pgx v5). O código gerado nunca é chamado direto dos handlers: passa por `internal/storage`, onde nenhuma função aceita query sem receber `tenantID`. O RLS é a rede de segurança; a camada de repositório é a primeira barreira.

🕐 **Fuso horário.** Tudo em UTC no banco (`timestamptz`). Conversão apenas na borda, usando `tenants.fuso_horario`.

🌿 **Commits, branches e PR.** Commit `<tipo> - <descrição>`, branch `<tipo>/t<fatia>-<descrição>` a partir da `main`, um PR por card com `Closes #N`, aprovação de outro integrante e CI verde. Todo PR move o card no GitHub Projects. Detalhes em [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

## 🗺️ Roteiro

O backlog é dividido em fatias verticais: cada uma atravessa banco, API e frontend e termina em algo demonstrável. Uma fatia pode levar mais de uma sprint.

| Fatia | Entrega |
|---|---|
| T0 — Fundação | Repositório, Docker, CI, migrations, RLS, seed com dois tenants |
| T1 — Agendar | Criar e listar agendamentos no navegador (tenant fixo, sem login) |
| T2 — Entrar | Signup, login, sessão, isolamento real entre tenants, papéis administrador/prestador e convites |
| T3 — Avisar | Vincular cliente ao Telegram e enviar confirmação |
| T4 — Lembrar | Lembrete automático X horas antes |
| T5 — Cancelar | Cancelar, reagendar e concluir, inclusive cancelar pelo bot; remover membro |
| T6 — Configurar | Serviços com preço, disponibilidades, slots livres e fuso por tenant |
| T7 — Acompanhar | Calendário semanal, resumo financeiro do mês e exportação para o calendário do celular (`.ics`) |
| T8 — Entregar | Deploy e documentação final |

Backlog detalhado em `docs/backlog.md`; cards já criados em `docs/cards.md`. Status de card, só no GitHub Projects.

---

## 📚 Documentação

| Arquivo | Conteúdo |
|---|---|
| [`docs/requisitos.md`](docs/requisitos.md) | Requisitos, arquitetura e decisões técnicas tomadas (§12) |
| [`docs/especificacao-fluxo.md`](docs/especificacao-fluxo.md) | Papéis e permissões, ciclo de vida do agendamento, telas e modelo de dados |
| [`docs/backlog.md`](docs/backlog.md) | Backlog por fatias verticais |
| [`docs/guia-banco-de-dados.md`](docs/guia-banco-de-dados.md) | Migrations comentadas, armadilhas do RLS e acesso a dados |
| [`docs/erros-api.md`](docs/erros-api.md) | Formato de erro da API |
| [`docs/cards.md`](docs/cards.md) | Cards do GitHub Projects por fatia (sem status) |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Padrão de commit, branch, PR e board |
| [`CLAUDE.md`](CLAUDE.md) | Regras para o Claude Code |

---

## 🔐 Dados pessoais

Projeto acadêmico. O sistema armazena nome, telefone e identificador de Telegram de clientes, com a finalidade única de operar agendamentos e enviar as notificações correspondentes.

A exportação `.ics` é um download feito pelo prestador: o arquivo leva nome do cliente e horário de cada agendamento para o calendário do aparelho dele (e para o iCloud, se o calendário sincroniza). A finalidade é a mesma: o prestador consultar a própria agenda. O arquivo é uma fotografia; depois de baixado, o sistema não o atualiza nem o apaga.

O `seed.sql` usa exclusivamente dados fictícios. Não versione nem carregue dados reais de terceiros neste repositório. Para demonstrações, use contatos do próprio grupo, com ciência dos envolvidos.

---

## 👥 Equipe

Projeto para estudo

- Igor Salgado
- Davi Oliveira
- João Victor Cunha
