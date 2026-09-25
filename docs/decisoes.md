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
| Rotas da API sob `/api` ou proxy do Vite reescrevendo o caminho? O #55 usa `/api`; #51–#53 usam `/clientes` e `/agendamentos`, e `/agendamentos` também é rota do React | Antes da 1.8 (#55) e da 1.4 (#51) | Davi | decidida | A API serve tudo sob `/api` (`/api/clientes`, `/api/agendamentos`); o proxy do Vite repassa `/api` sem reescrever. Mesmo caminho em dev e produção |
| Tipo de UUID no Go: `pgtype.UUID` (padrão do sqlc com pgx) ou `google/uuid` via `overrides`? O #49 escreve `uuid.UUID`; o `ComTenant` do guia, `pgtype.UUID` | Antes da primeira query (1.2, #49) | Davi | decidida | `google/uuid` via `overrides` no `sqlc.yaml` (guia, §Acesso a dados) |
| Serviço do agendamento na Fatia 1: `agendamentos.servico_id` é `NOT NULL`, mas #52 e #58 não pedem serviço. `SERVICO_FIXO` no config (1.1) ou campo no payload? | Antes da 1.1 (#48) | Davi | decidida | `SERVICO_FIXO` no config, junto de `TENANT_FIXO` e `PRESTADOR_FIXO`, apontando para um serviço com UUID fixo do seed (0.16). Sai na 6.6, quando o formulário passa a escolher serviço |
| Como o worker lê a fila de todos os tenants com `FORCE ROW LEVEL SECURITY`: papel próprio, função `SECURITY DEFINER` ou laço por tenant? | Antes da 3.12 | Davi | decidida | Laço por tenant: o worker lista `tenants` (fora do RLS) e consome a fila de cada um dentro de `ComTenant`, como `app_user`. Nenhum papel nem policy nova; revisitar se o número de tenants crescer |
