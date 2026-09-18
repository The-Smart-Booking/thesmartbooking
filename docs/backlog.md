# Backlog — Smart Booking

Versão **3.1** · 17/09/2026 · alinhado ao `docs/requisitos.md` v0.4 e ao
`docs/guia-banco-de-dados.md` v1.1

## Mudanças desde a v3

- Restaurados 4 itens da v1 que a v3 perdeu na consolidação da Fase 3: camada de
  repositório, formato de erro da API, fuso horário por tenant e enfileiramento da
  notificação de confirmação. Os três primeiros estavam na lista de **nunca corte**.
- Fase 3 renumerada para refletir ordem de dependência: os dois itens que são
  pré-requisito de todos os outros (repositório e formato de erro) passaram a 3.1 e
  3.2. Renumeração sem custo — a Fase 3 ainda não tem issue criada.
- Decisão de acesso a dados registrada: **sqlc + pgx**, sem ORM (item 0.10).
- Lista de **nunca corte** corrigida: faltavam o teste de concorrência do worker e
  os itens de isolamento restaurados.

## Mudanças desde a v2

- Numeração alinhada ao repositório: o prefixo do título é a **fase**
  (`[F0]`…`[F8]`), e a sprint mora na **milestone**. A v2 usava `[T0]`.
- Itens marcados com ⭑ são novos, vindos das revisões de arquitetura de 16 e 17/09.
- A coluna **Issue** liga cada item ao card no GitHub (vazio = ainda não criado).

## Como usar este documento

Cada item vira uma issue. O título já está pronto para copiar. O corpo da issue
tem contexto + critérios de aceite.

- **Labels**: `fase-0` … `fase-8`, mais `infra`, `banco`, `backend`, `frontend`,
  `bot`, `docs`, `caminho-critico`
- **Fechamento**: `Closes #N` na descrição do PR
- **Granularidade**: nenhum item passa de ~2 dias. Se passar, quebre em dois.
- **Estimativa**: tamanhos relativos (P / M / G), nunca horas. Grupo de faculdade
  não acerta estimativa em horas, e o número errado gera falsa sensação de
  controle.

---

## FASE 0 — Fundação do repositório

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 0.1 | `[F0] Inicializar módulo Go e estrutura de pastas` | P | #3 |
| 0.2 | `[F0] Branch protection na main (1 aprovação obrigatória)` | P | #4 |
| 0.3 | `[F0] docker-compose com postgres:16-alpine` | P | #5 |
| 0.4 | `[F0] .env.example e carregamento de config na API` | P | #6 |
| 0.5 | `[F0] GitHub Actions: go build, go vet, go test` | M | #7 |
| 0.6 | `[F0] Padrão de commit, template de PR e template de issue` | P | #8 |
| 0.7 ⭑ | `[F0] Trazer requisitos, backlog e guia de BD para docs/` | P | #23 |
| 0.8 ⭑ | `[F0] Registrar as decisões em aberto com prazo em docs/decisoes.md` | P | #24 |
| 0.9 ⭑ | `[F0] Ambiente validado nas três máquinas` | P | #25 |
| 0.10 ⭑ | `[F0] Decidir e instalar sqlc + pgx (sem ORM) e registrar em docs/decisoes.md` | P | |

**Critérios de aceite (0.3):** `docker compose up` sobe o Postgres; a aplicação
conecta usando as variáveis do `.env`; volume persiste dados entre reinícios.

**Critérios de aceite (0.5):** o workflow roda em todo PR para a `main`; PR com
teste quebrado não pode ser mergeado.

**Critérios de aceite (0.10):** `sqlc.yaml` no repositório apontando para
`db/migrations` e `db/queries`; `sqlc generate` roda sem erro; decisão e
justificativa registradas em `docs/decisoes.md`.

> A escolha de não usar ORM decorre do RLS: o contexto de tenant precisa valer na
> mesma transação e conexão da query, e ORM gerencia pool e transação por conta
> própria. Além disso, `EXCLUDE USING gist`, FKs compostas, `FOR UPDATE SKIP
> LOCKED` e `tstzrange` ficam fora do que qualquer ORM Go modela — seria SQL puro
> de todo jeito, com uma camada extra por cima.

## FASE 1 — Banco de dados e isolamento

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 1.1 | `[F1] goose e migration 001 (extensões e app.current_tenant_id)` | M | #9 |
| 1.2 | `[F1] Migration: tenants` | M | #10 |
| 1.3 | `[F1] Migration: usuarios e memberships` | M | #11 |
| 1.4 | `[F1] Migration: clientes` | M | #12 |
| 1.5 | `[F1] Migration: servicos e disponibilidades` | M | #13 |
| 1.6 | `[F1] Migration: agendamentos + constraint de sobreposição + trigger` | M | #14 |
| 1.7 | `[F1] Migration: notificacoes (outbox) com UNIQUE(agendamento_id, tipo, versao)` | M | #15 |
| 1.8 | `[F1] Migration: RLS com FORCE, USING e WITH CHECK em todas as tabelas` | G | #16 |
| 1.9 | `[F1] Script de init: app_user sem BYPASSRLS e grants` | P | #17 |
| 1.10 | `[F1] Seed com dois tenants fictícios e UUIDs fixos` | P | #18 |
| 1.11 ⭑ | `[F1] Subir Postgres no CI para os testes de banco` | M | |
| 1.12 ⭑ | `[F1] Migration: sessoes` | P | |
| 1.13 ⭑ | `[F1] Teste: exclusão de tenant não quebra em chave estrangeira` | P | |
| 1.14 ⭑ | `[F1] sqlc + pgx: sqlc.yaml e helper de transação com tenant` | M | |

**Critérios de aceite (1.6):** tentar inserir dois agendamentos sobrepostos para o
mesmo prestador no mesmo tenant retorna erro do banco (`23P01`), não da aplicação;
inserir dois consecutivos (9–10h e 10–11h) **funciona**; teste automatizado
comprova os dois casos.

**Critérios de aceite (1.8):** com `app.tenant_id` definido para o tenant A, um
`SELECT * FROM agendamentos` sem `WHERE` retorna apenas linhas do tenant A; um
`INSERT` com `tenant_id` do tenant B falha por violação de `WITH CHECK`.

**Critérios de aceite (1.11):** o workflow sobe `postgres:16-alpine` como serviço,
roda as migrations e executa os testes que dependem de banco; o teste de
isolamento roda conectado como `app_user`.

**Critérios de aceite (1.14):** `sqlc.yaml` lê `db/migrations` como schema e
`db/queries` como queries, gerando em `internal/storage/db` com `sql_package:
"pgx/v5"`; `internal/storage` expõe um helper que abre transação, chama
`set_config('app.tenant_id', $1, true)` e só então entrega `Queries.WithTx(tx)`;
o CI roda `sqlc diff` e falha se o código gerado estiver desatualizado.

> A issue 1.8 é a mais importante do backlog inteiro. **Não deve ser atribuída a
> quem estiver aprendendo Postgres no projeto.**

## FASE 2 — Autenticação e contas

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 2.1 | `[F2] Hash de senha com Argon2id (funções de hash e verificação)` | P | |
| 2.2 | `[F2] Endpoint POST /signup: cria usuário + tenant + membership owner` | M | |
| 2.3 | `[F2] Endpoint POST /login com criação de sessão` | M | |
| 2.4 | `[F2] Endpoint POST /logout com revogação da sessão` | P | |
| 2.5 | `[F2] Middleware de autenticação (extrai usuário da sessão)` | M | |
| 2.6 | `[F2] Middleware de tenant: valida membership e faz set_config local` | G | |
| 2.7 | `[F2] Middleware de autorização por papel (owner/prestador/atendente)` | M | |
| 2.8 | `[F2] Rate limit no endpoint de login` | P | |
| 2.9 | `[F2] Endpoint GET /me com tenants do usuário` | P | |
| 2.10 | `[F2] Teste de isolamento: usuário do tenant A não acessa dados do tenant B` | M | |
| 2.11 ⭑ | `[F2] Config com TENANT_FIXO e PRESTADOR_FIXO (temporário)` | P | #19 |
| 2.12 ⭑ | `[F2] Normalizar e-mail no cadastro e no login` | P | |

**Critérios de aceite (2.6):** requisição sem tenant válido retorna 403;
`tenant_id` enviado no corpo da requisição é ignorado; toda transação inicia com
`set_config('app.tenant_id', $1, true)`.

**Critérios de aceite (2.12):** e-mail é gravado e comparado normalizado; cadastrar
`Davi@x.com` depois de `davi@x.com` falha.

## FASE 3 — API de agendamentos

Renumerada na v3.1 para seguir a ordem de dependência: 3.1 e 3.2 são pré-requisito
de todo o resto da fase.

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 3.1 ⭑ | `[F3] Camada de repositório com sqlc: nenhuma função aceita query sem tenantID` | M | |
| 3.2 ⭑ | `[F3] Padronizar respostas de erro da API e documentar em docs/api-erros.md` | P | |
| 3.3 | `[F3] CRUD de serviços` | M | |
| 3.4 | `[F3] CRUD de clientes (com geração de vinculo_token)` | M | |
| 3.5 | `[F3] CRUD de disponibilidades` | M | |
| 3.6 | `[F3] Cálculo de slots livres a partir de disponibilidade e agendamentos` | G | |
| 3.7 | `[F3] Criar agendamento traduzindo 23P01 em HTTP 409` | G | |
| 3.8 | `[F3] Listar agendamentos com filtro por período e prestador` | M | |
| 3.9 | `[F3] Cancelar e reagendar (descartando o lembrete pendente)` | G | |
| 3.10 ⭑ | `[F3] Fuso horário por tenant e conversão na borda` | M | |
| 3.11 ⭑ | `[F3] Enfileirar notificação de confirmação na transação de criação` | M | |

**Critérios de aceite (3.1):** toda função gerada é consumida via `db.New(tx)`,
amarrada à transação que já definiu `app.tenant_id`. Nenhum caminho de código
executa query fora desse contexto.

**Critérios de aceite (3.2):** formato único `{"erro": {"codigo", "mensagem",
"campos?"}}`; nenhum erro de banco vaza para a resposta — `pgErr.Message` vai para
o log e o cliente recebe código traduzido; registro de outro tenant retorna 404, não
403, para não confirmar a existência do ID; tipo correspondente declarado em
`web/src/types/`.

**Critérios de aceite (3.7):** violação da constraint de exclusão vira HTTP 409 com
`codigo: horario_indisponivel`, nunca 500.

**Critérios de aceite (3.9):** cancelar um agendamento marca a notificação de
lembrete pendente como `descartado` **na mesma transação**; reagendar descarta o
lembrete antigo e cria nova notificação com `versao + 1`.

**Critérios de aceite (3.10):** horários gravados em UTC; conversão apenas na
borda, usando `tenants.fuso_horario`; teste com dois fusos diferentes comprova.

**Critérios de aceite (3.11):** a linha em `notificacoes` entra na **mesma
transação** que cria o agendamento, com `agendar_para` nulo. Nunca existe
agendamento sem notificação nem notificação sem agendamento.

> 3.10 parece detalhe e invalida a Fase 6 inteira se estiver errado: fuso errado
> significa lembrete na hora errada.

## FASE 4 — Frontend

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 4.1 | `[F4] Setup Vite + React + TypeScript + roteamento` | M | |
| 4.2 ⭑ | `[F4] Proxy do Vite para a API na mesma origem` | P | |
| 4.3 | `[F4] Cliente HTTP com envio de credenciais e tratamento de 401` | M | |
| 4.4 | `[F4] Telas de cadastro e login` | M | |
| 4.5 | `[F4] Layout autenticado com indicação do tenant ativo` | M | |
| 4.6 | `[F4] Tela de listagem de agendamentos` | M | |
| 4.7 | `[F4] Formulário de novo agendamento com seleção de horários livres` | G | |
| 4.8 | `[F4] Ações de reagendar e cancelar` | M | |
| 4.9 | `[F4] Telas de cadastro de serviços e disponibilidades` | G | |
| 4.10 | `[F4] Indicador visual de cliente NÃO vinculado ao Telegram` | M | |
| 4.11 | `[F4] Estados de carregamento e exibição de erro da API` | M | |

**Critérios de aceite (4.1):** estrutura `api/`, `pages/`, `components/`, `types/`
definida; duas rotas renderizam e a navegação entre elas funciona; `npm run build`
passa sem erro de TypeScript e roda no CI. Reclassificada de P para M na v3.1 — o
setup em si é rápido, a estrutura de pastas que as demais issues assumem não é.

**Critérios de aceite (4.2):** com o Vite rodando, a SPA chama a API na mesma
origem e o cookie de sessão é enviado. Sem isso, `SameSite=Lax` bloqueia o cookie
em desenvolvimento e o time perde uma tarde achando que o login está quebrado.

> 4.10 não é detalhe cosmético. Sem esse indicador, o prestador assume que o
> lembrete foi enviado quando o cliente sequer é alcançável. Precisa distinguir
> "nunca vinculou" de "bloqueou o bot" — são estados diferentes na tabela.

## FASE 5 — Bot: vinculação

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 5.1 | `[F5] Criar bot no BotFather e documentar token no .env.example` | P | |
| 5.2 | `[F5] Camada de acesso à Telegram Bot API em Go` | M | |
| 5.3 | `[F5] Modo long polling para desenvolvimento local` | M | |
| 5.4 | `[F5] Endpoint de webhook para receber updates` | M | |
| 5.5 ⭑ | `[F5] Autenticar o webhook com secret_token` | P | |
| 5.6 | `[F5] Gerar vinculo_token e link t.me/Bot?start=<token> ao criar cliente` | M | |
| 5.7 | `[F5] Handler do comando /start: resolve token e grava telegram_chat_id` | G | |
| 5.8 | `[F5] Tratar /start com token inválido, expirado ou já usado` | M | |
| 5.9 | `[F5] Exibir o link de vinculação na tela do cliente (frontend)` | P | |

**Critérios de aceite (5.5):** `setWebhook` é chamado com `secret_token`; update
sem o header `X-Telegram-Bot-Api-Secret-Token` correspondente é rejeitado com 401.
Sem isso, qualquer pessoa forja um `/start` ou um cancelamento.

**Critérios de aceite (5.7):** abrir o deep link grava `telegram_chat_id` e
`vinculado_em` no cliente correto e no tenant correto; abrir duas vezes não
duplica nem quebra.

## FASE 6 — Worker de notificações

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 6.1 | `[F6] Definir interface Notificador (Enviar(destino, mensagem))` | P | |
| 6.2 | `[F6] Implementar TelegramNotificador sobre a interface` | M | |
| 6.3 | `[F6] Estrutura do worker: loop periódico e encerramento gracioso` | M | |
| 6.4 | `[F6] Consumir fila outbox com FOR UPDATE SKIP LOCKED` | G | |
| 6.5 | `[F6] Enfileirar lembretes com agendar_para = inicio - antecedencia` | M | |
| 6.6 | `[F6] Montar texto das três mensagens (confirmação, lembrete, cancelamento)` | M | |
| 6.7 | `[F6] Retry com backoff e marcação de status falhou` | M | |
| 6.8 | `[F6] Tratar HTTP 429 respeitando retry_after` | M | |
| 6.9 | `[F6] Tratar erro 403 (bot bloqueado) marcando cliente não notificável` | M | |
| 6.10 | `[F6] Tornar antecedência do lembrete configurável por tenant` | P | |
| 6.11 | `[F6] Teste: duas instâncias do worker não enviam a mesma notificação` | G | |

**Critérios de aceite (6.1):** a interface não menciona Telegram em nenhum ponto.
Trocar de canal deve ser adicionar uma implementação, não editar o worker.

**Critérios de aceite (6.11):** rodar dois workers em paralelo contra o mesmo banco
produz exatamente um envio por agendamento.

> 6.10 tem valor de desenvolvimento, não só de produto: com a antecedência
> configurável, dá para colocar 1 minuto em ambiente local. Sem isso, cada teste
> manual de lembrete custa uma tarde.

## FASE 7 — Interação pelo bot (opcional no MVP)

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 7.1 | `[F7] Adicionar botões inline de Confirmar e Cancelar no lembrete` | M | |
| 7.2 | `[F7] Handler de callback_query: atualiza status do agendamento` | G | |
| 7.3 | `[F7] Editar a mensagem após a resposta (feedback visual)` | P | |
| 7.4 | `[F7] Comando /meusagendamentos` | M | |
| 7.5 | `[F7] Log de mensagens enviadas visível no frontend` | M | |

> Fase inteira cortável se o prazo apertar. Corte esta antes de cortar qualquer
> coisa da F1 ou F2.

## FASE 8 — Entrega

| # | Título da issue | Tam. | Issue |
|---|---|---|---|
| 8.1 | `[F8] Dockerfile da API e do worker (multi-stage)` | M | |
| 8.2 | `[F8] docker-compose de produção (api, worker, postgres)` | M | |
| 8.3 | `[F8] Executar migrations no deploy` | P | |
| 8.4 | `[F8] Configurar HTTPS e webhook público do Telegram` | M | |
| 8.5 | `[F8] README com instruções de setup local` | M | |
| 8.6 | `[F8] Documentação da API (endpoints e exemplos)` | M | |
| 8.7 | `[F8] Roteiro de apresentação e demo` | M | |

---

## Caminho crítico

```
1.3 → 1.6 → 1.8 → 2.6 → 3.1 → 3.7 → 4.7
                    ↘
               5.7 → 6.4 → 6.5
```

Nada da F3 em diante funciona corretamente sem a **1.8** e a **2.6**. Se essas
duas atrasarem, todo o resto atrasa junto — e pior, o time pode construir por cima
de um isolamento quebrado sem perceber.

A **3.1** entrou no caminho crítico na v3.1: com sqlc, nenhum endpoint da F3 pode
ser escrito antes de a camada de repositório existir.

A F5 pode correr em paralelo com F3 e F4 assim que a F1 fechar. É o único
paralelismo real disponível: use-o para não deixar ninguém ocioso.

## Corte de escopo, em ordem

1. Fase 7 inteira
2. 4.9 (telas de configuração — pode ser feito via seed no banco)
3. 3.5 + 3.6 (disponibilidade fixa, sem cálculo de slots)
4. 3.9 (só cancelar, sem reagendar)
5. 7.5 / log de mensagens

**Nunca corte:** 1.6, 1.8, 1.11, 2.6, 2.10, 3.1, 3.10, 3.11, 5.5, 6.5, 6.11. São
os itens que separam um sistema funcional de um que vaza dados de um cliente para
outro, aceita comando forjado, avisa na hora errada ou manda o mesmo lembrete três
vezes.

A 6.10 saiu da lista de corte: ela é barata e serve para testar a F6 em minutos em
vez de horas.

## Definição de pronto

Um item só entra em "Feito" quando:

- [ ] O código está mergeado na `main`
- [ ] O PR foi aprovado por outro integrante
- [ ] O CI passou
- [ ] Os critérios de aceite da issue foram verificados
- [ ] Se alterou o banco, existe migration versionada (nunca alteração manual)
