# Decisões em aberto — Smart Booking

Decisões que ainda não foram tomadas, cada uma com prazo limite. É a lista
oficial — o §12.1 de `docs/requisitos.md` aponta para cá. As decisões já tomadas
estão em §12 daquele documento.

Quando uma decisão for tomada: preencha a coluna **Decisão**, mude o status para
`decidida`, e copie a linha para `docs/requisitos.md` §12 no mesmo PR.

| Tema | Prazo limite para decidir | Responsável | Status | Decisão |
|---|---|---|---|---|
| Nomes e granularidade dos papéis | Antes do middleware de autorização (2.7, Fatia 2) | | aberta | |
| Antecedência padrão do lembrete | Antes de tornar configurável por tenant (4.6, Fatia 4) | | aberta | |
| Agendamento por link público (cliente sem conta) | Antes da Fatia 2 (o frontend já nasce na Fatia 1) | | aberta | |
| Quem entrega o deep link ao cliente | Antes da tela de cliente (3.8, Fatia 3) | | aberta | |
| Hospedagem | Antes da Fatia 7 | | aberta | |
| Rotas da API sob `/api` ou proxy do Vite reescrevendo o caminho? O #55 usa `/api`; #51–#53 usam `/clientes` e `/agendamentos`, e `/agendamentos` também é rota do React | Antes da 1.8 (#55) e da 1.4 (#51) | | aberta | |
| Tipo de UUID no Go: `pgtype.UUID` (padrão do sqlc com pgx) ou `google/uuid` via `overrides`? O #49 escreve `uuid.UUID`; o `ComTenant` do guia, `pgtype.UUID` | Antes da primeira query (1.2, #49) | | aberta | |
| Serviço do agendamento na Fatia 1: `agendamentos.servico_id` é `NOT NULL`, mas #52 e #58 não pedem serviço. `SERVICO_FIXO` no config (1.1) ou campo no payload? | Antes da 1.1 (#48) | | aberta | |
| Como o worker lê a fila de todos os tenants com `FORCE ROW LEVEL SECURITY`: papel próprio, função `SECURITY DEFINER` ou laço por tenant? | Antes da 3.12 — se for papel próprio, ele nasce no script da 0.15 | | aberta | |
