# Cards — Smart Booking

Cards que existem no board **Smart Booking DevOps** (GitHub Projects), por fatia.
Só a lista: status, responsável e sprint ficam no board. Atualize este arquivo
quando cards forem criados ou apagados (uma fatia por vez), num PR `docs`.

Dependências copiadas do "Depende de" de cada issue — o corpo da issue é o contrato.

Conferido em 25/09/2026.

## Fatia 0 — Fundação

| Card | Item | Título | Tam. | Área | Depende de |
|---|---|---|---|---|---|
| #63 | 0.7 | Configurar goose + migration 001 (extensões e app.current_tenant_id) | P | banco | — |
| #64 | 0.8 | Migration: tenants | M | banco | 0.7 |
| #65 | 0.9 | Migration: usuarios e memberships | M | banco | 0.8 |
| #66 | 0.10 | Migration: clientes | M | banco | 0.8 |
| #67 | 0.11 | Migration: servicos e disponibilidades | M | banco | 0.9 |
| #68 | 0.12 | Migration: agendamentos + constraint de sobreposição + trigger | M | banco | 0.10, 0.11 |
| #69 | 0.13 | Migration: notificacoes (outbox) com UNIQUE(agendamento_id, tipo, versao) | M | banco | 0.12 |
| #70 | 0.14 | Migration: RLS com FORCE, USING e WITH CHECK em todas as tabelas | G | banco, **caminho crítico** | 0.13, 0.15, 0.16, 0.17 |
| #71 | 0.15 | Script de init: app_user sem BYPASSRLS e grants | P | banco, infra | 0.7 |
| #72 | 0.16 | Seed com dois tenants fictícios e UUIDs fixos | P | banco | 0.13 |
| #73 | 0.17 | Subir Postgres no CI para os testes de banco | M | infra | 0.7 |
| #74 | 0.18 | Teste: exclusão de tenant não quebra em chave estrangeira | P | banco | 0.13, 0.17 |

Itens 0.1 a 0.6 foram feitos na Sprint 0, com a numeração antiga (issues apagadas).

## Fatia 1 — Agendar

| Card | Item | Título | Tam. | Área | Depende de |
|---|---|---|---|---|---|
| #48 | 1.1 | Config com TENANT_FIXO e PRESTADOR_FIXO (temporário, remover na T2) | P | backend | — |
| #49 | 1.2 | Camada de repositório: toda função recebe tenantID como 1º parâmetro | M | backend, banco | migrations e seed da Fatia 0 |
| #50 | 1.3 | Abrir transação por requisição com set_config('app.tenant_id', ...) | M | backend, banco, **caminho crítico** | 1.1, 1.2 |
| #51 | 1.4 | CRUD mínimo de clientes (criar e listar) | M | backend | 1.2, 1.3, 1.7 |
| #52 | 1.5 | POST /agendamentos traduzindo SQLSTATE 23P01 para HTTP 409 | M | backend, banco | 1.2, 1.3, 1.4, 1.7 |
| #53 | 1.6 | GET /agendamentos com filtro por período | M | backend | 1.2, 1.3, 1.7 |
| #54 | 1.7 | Padronizar formato de resposta de erro da API | P | backend | — |
| #55 | 1.8 | Setup Vite + React + TypeScript + roteamento | P | frontend | — |
| #56 | 1.9 | Cliente HTTP com tratamento centralizado de erro | M | frontend | 1.7, 1.8 |
| #57 | 1.10 | Tela de listagem de agendamentos | M | frontend | 1.6, 1.9 |
| #58 | 1.11 | Formulário de novo agendamento (data e hora digitadas, sem slots) | M | frontend | 1.4, 1.5, 1.9 |
| #59 | 1.12 | Estados de carregamento e exibição de erro | M | frontend | 1.9, 1.10, 1.11 |
