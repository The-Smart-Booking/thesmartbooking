# Cards — Smart Booking

Cards que existem no board **Smart Booking DevOps** (GitHub Projects), por fatia.
Só a lista: status, responsável e sprint ficam no board. Atualize este arquivo
quando cards forem criados ou apagados (uma fatia por vez), num PR `docs`.

Dependências copiadas do "Depende de" de cada issue — o corpo da issue é o contrato.

Conferido em 02/10/2026.

## Mudanças de 02/10/2026 (backlog v2.2)

Decisões de papéis, agenda e financeiro (`requisitos.md` v0.8). Títulos e
dependências não mudaram; mudou o corpo destes cards, já editado no board
(rodapé "Atualizado em 02/10/2026"):

| Card | Item | O que mudou |
|---|---|---|
| #65 | 0.9 | `CHECK` de papel só com administrador e prestador; `ALTER TABLE tenants ADD COLUMN criado_por` (a 002 não muda) |
| #67 | 0.11 | `servicos.preco_centavos` sem `DEFAULT`, com `CHECK >= 0` |
| #68 | 0.12 | `valor_centavos` com `CHECK >= 0`; índice `idx_agendamentos_prestador_inicio`; administrador como prestador |
| #70 | 0.14 | `REVOKE DELETE ON agendamentos FROM app_user`; cinco tabelas fora do RLS; sem policy por prestador |
| #72 | 0.16 | Ordem `usuarios` → `tenants` → `memberships`; no Alfa, administrador criador que atende + 1 prestador; preços |
| #52 | 1.5 | Grava `valor_centavos` copiado do serviço; só prestador com membership ativo |
| #53 | 1.6 | `prestador_id` opcional, sem regra de sessão (fica na 2.7) |
| #57 | 1.10 | Continua lista simples; o calendário semanal é a 7.1 |

Itens novos do backlog, sem card ainda: 2.16 a 2.20, 5.8, 6.5a e 6.5b (a 6.5 foi
dividida) e 7.1 a 7.7 (Fatia 7 Acompanhar). Entregar passou a ser a Fatia 8 (8.1
a 8.7).

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

**Prioridade:** 0.7 -> 0.8 -> 0.9 -> 0.10 -> 0.11 -> 0.12 -> 0.13 -> 0.15 -> 0.16 -> 0.14 -> 0.18

0.17 (em paralelo, depois de 0.7). A 0.16 vem antes da 0.14 porque o teste de isolamento precisa dos dois tenants do seed.

Itens 0.1 a 0.6 não têm card: as issues da numeração antiga foram apagadas.

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

**Prioridade:** 1.1 -> 1.2 -> 1.3 -> 1.4 -> 1.6 -> 1.5 -> 1.10 -> 1.11 -> 1.12

1.7 -> 1.8 -> 1.9 (em paralelo, junto com o fim da Fatia 0). A 1.6 vem antes da 1.5 porque destrava a 1.10.
