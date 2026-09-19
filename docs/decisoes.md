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
| Tipo de UUID no Go: `pgtype.UUID` (padrão do sqlc/pgx) ou `google/uuid` via `overrides` | Antes da primeira query (1.2) — o card #49 já escreve `uuid.UUID` | | aberta | |
| Serviço do agendamento na Fatia 1: `servico_id` é `NOT NULL`, mas #52 e #58 não pedem serviço (constante no config ou campo no payload?) | Antes da 1.5 | | aberta | |
| Rotas da API sob `/api` ou proxy do Vite reescrevendo o caminho (#55 usa `/api`, #51–#53 usam `/clientes` e `/agendamentos`) | Antes da 1.4 e da 1.8 | | aberta | |
| Como o worker lê a fila de todos os tenants com `FORCE ROW LEVEL SECURITY` (usuário próprio, função `SECURITY DEFINER` ou laço por tenant) | Antes da estrutura do worker (3.12) | | aberta | |
