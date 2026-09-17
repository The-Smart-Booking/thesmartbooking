# 📅 Smart Booking

Plataforma multi-tenant de agendamentos com notificações automáticas via Telegram.

Cada organização (barbearia, clínica, estúdio) tem seus próprios usuários, serviços, horários e clientes, isolados das demais. O cliente final recebe confirmação e lembrete pelo Telegram, sem precisar de conta no sistema.

> 🚧 **Status:** Fase 0 — fundação, em fechamento. Já existem o ambiente Docker (PostgreSQL) e o CI. Schema e RLS entram na Fase 1.

---

## 🛠️ Stack

| Camada | Tecnologia |
|---|---|
| API e worker | Go |
| Frontend | React + Vite + TypeScript |
| Banco | PostgreSQL 16 |
| Migrations | goose |
| Notificações | Telegram Bot API |
| Ambiente | Docker Compose |

---

## 📁 Estrutura

Hoje:

```
thesmartbooking/
├── .github/
│   ├── workflows/ci.yml        # go build, go vet, go test em PR para a main
│   ├── ISSUE_TEMPLATE/card.md
│   └── pull_request_template.md
├── cmd/api/                    # entrada da API
├── web/                        # frontend React + Vite
├── docs/
├── compose.yml                 # PostgreSQL 16 + Adminer
├── .env.example
└── CONTRIBUTING.md
```

Estrutura-alvo (cada pasta nasce no card que a usa):

```
cmd/worker/            # consumidor da fila de notificações (F6)
internal/config/       # leitura das variáveis de ambiente (F2)
internal/api/          # handlers e middlewares (F2)
internal/storage/      # repositórios — toda função recebe tenantID (F1)
internal/notificador/  # interface Notificador + implementação Telegram (F5)
db/migrations/         # goose, SQL puro (F1)
db/seed.sql            # dois tenants fictícios (F1)
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

### ⏳ A partir da Fase 1: migrations, seed e isolamento

```bash
set -a; source .env; set +a   # exporta DATABASE_URL para o shell
goose -dir db/migrations postgres "$DATABASE_URL" up
psql "$DATABASE_URL" -f db/seed.sql
```

O seed cria dois tenants fictícios (`alfa` e `beta`). São dois de propósito: com um só não é possível testar isolamento entre tenants.

Conectado como `app_user` (nunca como o dono das tabelas):

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

📦 **Repositórios.** Nenhuma função de acesso a dados aceita query sem receber `tenantID`. O RLS é a rede de segurança; a camada de repositório é a primeira barreira.

🕐 **Fuso horário.** Tudo em UTC no banco (`timestamptz`). Conversão apenas na borda, usando `tenants.fuso_horario`.

🌿 **Commits, branches e PR.** Commit `<tipo> - <descrição>`, branch `<tipo>/f<fase>-<descrição>` a partir da `main`, um PR por card com `Closes #N`, aprovação de outro integrante e CI verde. Todo PR move o card no GitHub Projects e atualiza `docs/kanban.md`. Detalhes em [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

## 🗺️ Roteiro

O backlog é dividido em fases; cada sprint fecha uma fase.

| Fase | Entrega | Status |
|---|---|---|
| F0 — Fundação | Repositório, Docker, CI, padrões do time | 🚧 |
| F1 — Banco e isolamento | Migrations, RLS, seed com dois tenants | ⏳ |
| F2 — Autenticação e contas | Signup, login, sessão, middleware de tenant | ⏳ |
| F3 — API de agendamentos | CRUD e regras de agendamento | ⏳ |
| F4 — Frontend | Telas do prestador | ⏳ |
| F5 — Bot: vinculação | Vincular cliente ao Telegram | ⏳ |
| F6 — Worker de notificações | Confirmação e lembrete automáticos | ⏳ |
| F7 — Interação pelo bot | Opcional no MVP | ⏳ |
| F8 — Entrega | Deploy e documentação final | ⏳ |

Legenda: ✅ concluída · 🚧 em andamento · ⏳ não iniciada

Backlog detalhado em `docs/backlog.md`. Acompanhamento no GitHub Projects, espelhado em `docs/kanban.md`.

---

## 📚 Documentação

| Arquivo | Conteúdo |
|---|---|
| `docs/requisitos.md` | Requisitos, arquitetura e decisões técnicas |
| `docs/backlog.md` | Backlog por fatias verticais |
| `docs/guia-banco-de-dados.md` | Migrations comentadas e armadilhas do RLS |
| `docs/kanban.md` | Status de cada card, espelho do GitHub Projects |
| `docs/decisoes.md` | Decisões em aberto e seus prazos |
| `CONTRIBUTING.md` | Padrão de commit, branch, PR e kanban |

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
