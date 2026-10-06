# 📅 Smart Booking

![Go](https://img.shields.io/badge/Go-00ADD8?style=for-the-badge&logo=go&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL%2016-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![React](https://img.shields.io/badge/React-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![Telegram](https://img.shields.io/badge/Telegram%20Bot%20API-26A5E4?style=for-the-badge&logo=telegram&logoColor=white)

[![CI](https://img.shields.io/github/actions/workflow/status/The-Smart-Booking/thesmartbooking/ci.yml?branch=main&style=for-the-badge&label=CI)](https://github.com/The-Smart-Booking/thesmartbooking/actions)
![Status](https://img.shields.io/badge/status-em%20desenvolvimento-yellow?style=for-the-badge)

**Plataforma multi-tenant de agendamentos com notificações automáticas via Telegram.**

Cada organização (barbearia, clínica, estúdio) tem seus próprios usuários, serviços, horários e clientes, isolados das demais. O cliente final recebe confirmação e lembrete pelo Telegram, sem precisar de conta no sistema.

> 🚧 Em desenvolvimento. Andamento no GitHub Projects (Smart Booking DevOps).

---

**Índice**

- [🎯 Sobre o projeto](#-sobre-o-projeto)
- [🛠️ Stack](#️-stack)
- [🚀 Começando](#-começando)
  - [Pré-requisitos](#pré-requisitos)
  - [Clonando](#clonando)
  - [Variáveis de ambiente](#variáveis-de-ambiente)
  - [Subindo o ambiente](#subindo-o-ambiente)
  - [Frontend](#frontend)
- [🗄️ Banco de dados](#️-banco-de-dados)
  - [Migrations](#migrations)
  - [Seed e isolamento](#seed-e-isolamento-a-partir-da-fatia-0)
- [📁 Estrutura](#-estrutura)
- [📍 API](#-api)
- [📐 Convenções](#-convenções)
- [🗺️ Roteiro](#️-roteiro)
- [📚 Documentação](#-documentação)
- [🔐 Dados pessoais](#-dados-pessoais)
- [🤝 Equipe](#-equipe)
- [📫 Contribuindo](#-contribuindo)

---

## 🎯 Sobre o projeto

São dois papéis dentro de cada organização:

| Papel | O que faz |
| --- | --- |
| **Administrador** | Gerencia a empresa — convida membros, remove prestadores, define serviços e preços, vê o financeiro de todos. Também pode atender. |
| **Prestador** | Vê e opera apenas a própria agenda. |

O cliente final não tem conta. Ele se vincula ao bot por um link `t.me/<bot>?start=<token>` e passa a receber confirmação, lembrete e aviso de cancelamento no Telegram.

O isolamento entre organizações é garantido no banco, com Row Level Security, e não apenas na aplicação.

---

## 🛠️ Stack

| Camada | Tecnologia |
| --- | --- |
| API e worker | Go |
| Frontend | React + Vite + TypeScript |
| Banco | PostgreSQL 16 |
| Migrations | goose v3.28.0 |
| Acesso a dados | pgx v5 + sqlc |
| Notificações | Telegram Bot API |
| Ambiente | Docker Compose |

---

## 🚀 Começando

### Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) com Compose
- [Go](https://go.dev/dl/) 1.27+
- [Node.js](https://nodejs.org/) — para o `web/`
- [goose](https://github.com/pressly/goose) v3.28.0

```bash
go install github.com/pressly/goose/v3/cmd/goose@v3.28.0
# instala em ~/go/bin, que precisa estar no PATH
```

### Clonando

```bash
git clone git@github.com:The-Smart-Booking/thesmartbooking.git
cd thesmartbooking
```

### Variáveis de ambiente

Use o `.env.example` como referência:

```bash
cp .env.example .env
```

| Variável | Para quê |
| --- | --- |
| `GOOSE_DBSTRING` | Conexão do **dono das tabelas** — migrations e seed. Ignora o RLS. |
| `DATABASE_URL` | Conexão do **`app_user`** — a única que a API usa. Sujeita ao RLS. |
| `GOOSE_DRIVER`, `GOOSE_MIGRATION_DIR` | Lidas pelo goose automaticamente |

> São duas conexões de propósito. Se a API conectar como dono das tabelas, todas as policies de RLS viram decoração e o isolamento entre organizações deixa de existir.

### Subindo o ambiente

```bash
docker compose up -d      # PostgreSQL em localhost:5467, Adminer em localhost:8088

go run ./cmd/api          # deve imprimir "Hello World"
go test ./...
```

### Frontend

```bash
cd web
npm install
npm run dev     # http://localhost:5173 (abre em /agendamentos)
npm run lint    # o CI roda lint e build em todo PR
npm run build   # tsc em modo strict + build do Vite
```

As rotas ficam em `web/src/route.ts` (React Router); `App.tsx` é o layout com a navegação e o `<Outlet />` onde cada tela aparece.

Em dev, o Vite repassa tudo que começa com `/api` para a API em `localhost:8080`, sem reescrever o caminho: a SPA chama `/api/agendamentos` na mesma origem e não precisa de CORS.

---

## 🗄️ Banco de dados

### Migrations

O goose lê `GOOSE_DRIVER`, `GOOSE_DBSTRING` e `GOOSE_MIGRATION_DIR` do `.env` sozinho, então os comandos abaixo rodam contra o Postgres do `compose.yml`, com ele de pé:

| Comando | O que faz |
| --- | --- |
| `goose status` | O que já rodou e o que falta |
| `goose up` | Aplica todas as pendentes |
| `goose down` | Desfaz só a última |
| `goose down-to 0` | Desfaz todas |
| `goose -s create nome sql` | Nova migration `0000N_nome.sql` (`-s` = numeração sequencial) |

Toda migration tem `Up` e `Down`, e os dois são testados: `goose up`, `goose down-to 0`, `goose up`.

### Seed e isolamento (a partir da Fatia 0)

sqlc na versão fixada em `docs/guia-banco-de-dados.md`.

```bash
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

Estrutura-alvo — cada pasta nasce no card que a usa:

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

## 📍 API

Os endpoints nascem na fatia T1 e são documentados conforme entram. O que já está definido é o **formato de erro**, usado por todos os handlers:

```json
{
  "erro": {
    "codigo": "horario_indisponivel",
    "mensagem": "Já existe agendamento nesse horário para este prestador.",
    "campos": { "inicio": "conflita com agendamento existente" }
  }
}
```

| Código | HTTP | Quando |
| --- | --- | --- |
| `validacao` | 400 | Campo faltando ou malformado |
| `nao_encontrado` | 404 | ID inexistente — ou de outro tenant |
| `horario_indisponivel` | 409 | Violação da constraint de sobreposição (`23P01`) |
| `erro_interno` | 500 | Qualquer coisa não prevista |

Nenhum erro de banco vaza para a resposta: nome de constraint, coluna e tabela ficam no log. Registro de outro tenant retorna 404 e não 403, para não confirmar que o ID existe.

Especificação completa em [`docs/erros-api.md`](docs/erros-api.md).

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
| --- | --- |
| **T0** — Fundação | Repositório, Docker, CI, migrations, RLS, seed com dois tenants |
| **T1** — Agendar | Criar e listar agendamentos no navegador (tenant fixo, sem login) |
| **T2** — Entrar | Signup, login, sessão, isolamento real entre tenants, papéis e convites |
| **T3** — Avisar | Vincular cliente ao Telegram e enviar confirmação |
| **T4** — Lembrar | Lembrete automático X horas antes |
| **T5** — Cancelar | Cancelar, reagendar e concluir, inclusive pelo bot; remover membro |
| **T6** — Configurar | Serviços com preço, disponibilidades, slots livres e fuso por tenant |
| **T7** — Acompanhar | Calendário semanal, resumo financeiro do mês e exportação `.ics` |
| **T8** — Entregar | Deploy e documentação final |

Backlog detalhado em [`docs/backlog.md`](docs/backlog.md); cards já criados em [`docs/cards.md`](docs/cards.md). Status de card, só no GitHub Projects.

---

## 📚 Documentação

| Arquivo | Conteúdo |
| --- | --- |
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

## 🤝 Equipe

Projeto acadêmico — Análise e Desenvolvimento de Sistemas, FAESA.

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/igorhsalgado">
        <img src="https://github.com/igorhsalgado.png" width="100px" alt="Igor Salgado"/><br>
        <sub><b>Igor Salgado</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/thisdev-davi">
        <img src="https://github.com/thisdev-davi.png" width="100px" alt="Davi Oliveira"/><br>
        <sub><b>Davi Oliveira</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/J0aoCunha">
        <img src="https://github.com/J0aoCunha.png" width="100px" alt="João Victor da Silva Cunha"/><br>
        <sub><b>João Victor Cunha</b></sub>
      </a>
    </td>
  </tr>
</table>

---

## 📫 Contribuindo

O fluxo completo está em [`CONTRIBUTING.md`](CONTRIBUTING.md). Em resumo:

1. Pegue um card no GitHub Projects e se atribua a ele
2. `git checkout -b <tipo>/t<fatia>-<descrição>` a partir da `main`
3. Commits no padrão `<tipo> - <descrição>`
4. Abra o PR com `Closes #N` na descrição
5. Aguarde aprovação de outro integrante e CI verde

Nenhum item entra em "Feito" sem estar mergeado na `main`, com PR aprovado, CI verde e critérios de aceite verificados.

