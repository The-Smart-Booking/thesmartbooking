# 📅 Smart Booking

Plataforma multi-tenant de agendamentos com notificações automáticas via Telegram.

Cada organização (barbearia, clínica, estúdio) tem seus próprios usuários, serviços, horários e clientes, isolados das demais. O cliente final recebe confirmação e lembrete pelo Telegram, sem precisar de conta no sistema.

> 🚧 **Status:** Sprint 0 concluída (ambiente Docker, CI e padrões do time). Em andamento: Sprint 1 — fim da Fatia 0 (schema, RLS e seed) e base do frontend.

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
│   ├── workflows/ci.yml        # Go (gofmt, vet, build, test) e web (lint, build) em PR para a main
│   └── pull_request_template.md
├── cmd/api/                    # entrada da API
├── internal/api/               # handlers e formato de erro (docs/erros-api.md)
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
db/migrations/         # goose, SQL puro (T0)
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
npm run dev
```

### ⏳ A partir da Fatia 0: migrations, seed e isolamento

goose e sqlc nas versões fixadas em `docs/guia-banco-de-dados.md`.

```bash
goose up                      # lê GOOSE_DRIVER, GOOSE_DBSTRING e GOOSE_MIGRATION_DIR do .env
sqlc generate                 # a partir da T1: regenera internal/storage/db
set -a; source .env; set +a   # exporta as variáveis para o psql
psql "$GOOSE_DBSTRING" -f db/seed.sql
```

Duas conexões, de propósito: `GOOSE_DBSTRING` é o dono das tabelas (migrations e seed) e ignora o RLS; `DATABASE_URL` é o `app_user`, o único usuário que a API usa.

O seed cria dois tenants fictícios (`alfa` e `beta`). São dois de propósito: com um só não é possível testar isolamento entre tenants.

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

| Fatia | Entrega | Status |
|---|---|---|
| T0 — Fundação | Repositório, Docker, CI, migrations, RLS, seed com dois tenants | 🚧 |
| T1 — Agendar | Criar e listar agendamentos no navegador (tenant fixo, sem login) | ⏳ |
| T2 — Entrar | Signup, login, sessão e isolamento real entre tenants | ⏳ |
| T3 — Avisar | Vincular cliente ao Telegram e enviar confirmação | ⏳ |
| T4 — Lembrar | Lembrete automático X horas antes | ⏳ |
| T5 — Cancelar | Cancelar e reagendar, inclusive pelo bot | ⏳ |
| T6 — Configurar | Serviços, disponibilidades e slots livres | ⏳ |
| T7 — Entregar | Deploy e documentação final | ⏳ |

Legenda: ✅ concluída · 🚧 em andamento · ⏳ não iniciada

Backlog detalhado em `docs/backlog.md`; cards já criados em `docs/cards.md`. Status de card, só no GitHub Projects.

---

## 📚 Documentação

| Arquivo | Conteúdo |
|---|---|
| `docs/requisitos.md` | Requisitos, arquitetura e decisões técnicas |
| `docs/backlog.md` | Backlog por fatias verticais |
| `docs/guia-banco-de-dados.md` | Migrations comentadas, armadilhas do RLS e acesso a dados |
| `docs/erros-api.md` | Formato de erro da API |
| `docs/cards.md` | Cards do GitHub Projects por fatia (sem status) |
| `docs/decisoes.md` | Decisões em aberto e seus prazos |
| `CONTRIBUTING.md` | Padrão de commit, branch, PR e board |
| `CLAUDE.md` | Regras para o Claude Code |

---

## 🔐 Dados pessoais

Projeto acadêmico. O sistema armazena nome, telefone e identificador de Telegram de clientes, com a finalidade única de operar agendamentos e enviar as notificações correspondentes.

O `seed.sql` usa exclusivamente dados fictícios. Não versione nem carregue dados reais de terceiros neste repositório. Para demonstrações, use contatos do próprio grupo, com ciência dos envolvidos.

---

## 👥 Equipe

Projeto acadêmico — FAESA

- Igor Salgado
- Davi Oliveira
- João Victor Cunha

---

## 📄 Licença

A definir.
