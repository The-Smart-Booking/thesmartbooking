# 📅 Smart Booking

Plataforma multi-tenant de agendamentos com notificações automáticas via Telegram.

Cada organização (barbearia, clínica, estúdio) tem seus próprios usuários, serviços, horários e clientes, isolados das demais. O cliente final recebe confirmação e lembrete pelo Telegram, sem precisar de conta no sistema.

> 🚧 **Status:** em desenvolvimento inicial (Fatia 0 — fundação). Ainda não há aplicação executável; por enquanto o repositório contém o schema do banco e a infraestrutura de desenvolvimento.

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

```
smartbooking/
├── cmd/
│   ├── api/          # servidor HTTP
│   └── worker/       # consumidor da fila de notificações
├── internal/
│   ├── config/
│   ├── db/           # repositórios (toda função recebe tenantID)
│   ├── http/         # handlers e middlewares
│   └── notificador/  # interface Notificador + implementação Telegram
├── db/
│   ├── migrations/   # goose, SQL puro
│   └── seed.sql
├── web/              # frontend React
├── docs/
└── docker-compose.yml
```

---

## 🚀 Rodando localmente

**Pré-requisitos:** Docker, Docker Compose, Go 1.22+ e goose.

```bash
git clone git@github.com:<org>/smartbooking.git
cd smartbooking

cp .env.example .env      # ajuste as variáveis se necessário
docker compose up -d      # sobe o PostgreSQL

goose -dir db/migrations postgres "$DATABASE_URL" up
psql "$DATABASE_URL" -f db/seed.sql
```

O seed cria dois tenants fictícios (`alfa` e `beta`). São dois de propósito: com um só não é possível testar isolamento entre tenants.

### 🔒 Verificando o isolamento

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

🌿 **Branches e PR.** Trabalho em branch a partir da `main`, PR com aprovação de outro integrante e CI verde antes do merge. Descrição do PR com `Closes #N` para fechar a issue automaticamente.

---

## 🗺️ Roteiro

O projeto é desenvolvido em fatias verticais: cada fatia atravessa banco, API, frontend e bot, e termina em algo demonstrável.

| Fatia | Entrega | Status |
|---|---|---|
| 0 — Fundação | Schema, RLS, CI, ambiente local | 🚧 |
| 1 — Agendar | Criar e listar agendamento na tela | ⏳ |
| 2 — Entrar | Login e isolamento real entre tenants | ⏳ |
| 3 — Avisar | Vinculação ao Telegram e confirmação | ⏳ |
| 4 — Lembrar | Lembrete automático antes do horário | ⏳ |
| 5 — Cancelar | Cancelamento e reagendamento | ⏳ |
| 6 — Configurar | Serviços, disponibilidades e slots | ⏳ |
| 7 — Entregar | Deploy e documentação | ⏳ |

Legenda: ✅ concluída · 🚧 em andamento · ⏳ não iniciada

Backlog detalhado em `docs/backlog.md`. Acompanhamento no GitHub Projects.

---

## 📚 Documentação

| Arquivo | Conteúdo |
|---|---|
| `docs/requisitos.md` | Requisitos, arquitetura e decisões técnicas |
| `docs/backlog.md` | Backlog por fatias verticais |
| `docs/guia-banco-de-dados.md` | Migrations comentadas e armadilhas do RLS |

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
