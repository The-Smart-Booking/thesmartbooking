# Requisitos — Smart Booking

Sistema de agendamentos multi-tenant com notificação via Telegram.
Documento de requisitos e decisões técnicas — **v0.6** · 24/09/2026

> Fonte da verdade: este arquivo. A pasta do Drive é histórico.

## Mudanças desde a v0.5

| # | O que mudou | Onde |
|---|---|---|
| 1 | Decisões em aberto vivem só em `docs/decisoes.md` | 12.1 |
| 2 | Decididos: UUID, prefixo `/api`, serviço na Fatia 1 e leitura da fila pelo worker | 12 |

## Mudanças desde a v0.4

| # | O que mudou | Onde |
|---|---|---|
| 1 | Fases horizontais (F0–F8) trocadas pelas fatias verticais do backlog v2.1 | 10 |
| 2 | Estimativa recalculada a partir do backlog (397h) | 13 |
| 3 | Prazos das decisões em aberto referenciam itens do backlog | 12.1 |

## Mudanças desde a v0.3

Revisão de arquitetura de 16/09/2026. Os três primeiros itens corrigem trechos
que estavam **errados** e que o guia de banco já trazia certos — a divergência
entre os dois documentos era o risco: quem copiasse daqui levava a versão bugada.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Policy de RLS agora tem `WITH CHECK`, não só `USING` | 6.2 |
| 2 | `current_setting` trocado pela função `app.current_tenant_id()` | 6.2 |
| 3 | `tstzrange(inicio, fim)` passou a `tstzrange(inicio, fim, '[)')` | 7 |
| 4 | Tabela `sessoes` adicionada ao modelo de dados | 7, 9 |
| 5 | Tabela `convites` adicionada (RF12 não tinha modelo) | 7 |
| 6 | Webhook do Telegram passa a exigir `secret_token` | 8.5, RNF10 |
| 7 | Exclusão lógica em `clientes` e `memberships` | 7 |
| 8 | Cookie de sessão e origem do frontend em desenvolvimento | 9 |
| 9 | Índices de chave estrangeira explicitados | 7 |
| 10 | Acesso a dados definido: pgx v5 + sqlc, sem ORM | 1, 6.2, 12 |

---

## 1. Visão geral

Plataforma web multi-tenant para gestão de agendamentos. Cada tenant
(empresa/prestador) tem seus próprios usuários, serviços, horários e clientes,
isolados dos demais. Notificações de lembrete enviadas via bot do Telegram.

**Stack:** API em Go, frontend em React, banco PostgreSQL (acesso via pgx v5 +
sqlc), worker de notificações em Go, Telegram Bot API.

## 2. Ordem de execução

Continua valendo: **o sistema de agendamento vem antes do bot**. O bot não tem o
que notificar sem o modelo de dados.

Com o Telegram, a burocracia da Meta desaparece: criar um bot no @BotFather leva
dois minutos e não exige CNPJ, verificação nem aprovação de template. Em
compensação, o multi-tenant adiciona complexidade estrutural que precisa estar
certa **desde a primeira migration**. Retrofit de `tenant_id` em schema pronto é
caro e propenso a erro.

## 3. Requisitos funcionais

| ID | Requisito | Prioridade |
|---|---|---|
| RF01 | Cadastro e login de usuário (e-mail + senha) | Alta |
| RF02 | Cadastro de tenant com um usuário owner inicial | Alta |
| RF03 | Papéis por tenant: owner, prestador, atendente | Alta |
| RF04 | Isolamento total de dados entre tenants | Alta |
| RF05 | CRUD de agendamentos (criar, listar, editar, cancelar) | Alta |
| RF06 | Definição de horários disponíveis por prestador | Alta |
| RF07 | Bloqueio de conflito de horário (double booking) | Alta |
| RF08 | Vinculação do cliente ao Telegram via deep link | Alta |
| RF09 | Envio automático de lembrete X horas antes | Alta |
| RF10 | Confirmação/cancelamento pelo cliente via botão inline | Média |
| RF11 | Log de mensagens enviadas com status | Média |
| RF12 | Convite de novos usuários para um tenant | Média |
| RF13 | Reenvio manual de notificação | Baixa |
| RF14 | Visualização em calendário | Baixa |

## 4. Requisitos não funcionais

- **RNF01** — Todos os horários em UTC (`timestamptz`); conversão só na
  apresentação. Cada tenant tem seu próprio fuso configurável.
- **RNF02** — Nenhuma query pode acessar dados sem escopo de tenant. Ver 6.2.
- **RNF03** — Envio de notificação idempotente: nunca duas vezes, mesmo com
  restart do worker ou execução concorrente.
- **RNF04** — Senhas com Argon2id ou bcrypt (custo ≥ 12).
- **RNF05** — `tenant_id` derivado exclusivamente da sessão, nunca aceito do
  corpo da requisição ou de query param.
- **RNF06** — Rate limit no login.
- **RNF07** — Segredos via variável de ambiente.
- **RNF08** — Falha do Telegram não pode derrubar o fluxo de agendamento.
- **RNF09** — Deploy via Docker Compose (api, worker, postgres).
- **RNF10** *(novo)* — Todo update recebido do Telegram é autenticado pelo
  `secret_token` do webhook. Endpoint sem essa verificação aceita `/start`
  forjado de qualquer origem.

## 5. Arquitetura

```
[React SPA] --HTTPS/JSON--> [API Go] ---> [PostgreSQL + RLS]
                                |            ^
                                v            |
                       [Worker Go (scheduler)]
                                |
                                v
                       [Telegram Bot API]
                                |
              [webhook de updates] --> [API Go]
```

**Worker separado da API:** a API responde HTTP e precisa ser rápida; o envio de
lembretes é um processo periódico. Juntar os dois funciona com uma instância e
quebra com duas (mensagem duplicada).

### 5.1 Padrão outbox para notificações

Três tipos de notificação, dois mecanismos:

| Tipo | Disparo | Momento do envio |
|---|---|---|
| `confirmacao` | Evento (criação do agendamento) | Imediato |
| `cancelamento` | Evento (cancelamento) | Imediato |
| `lembrete` | Agendado | X horas antes do início |

1. A API insere a linha em `notificacoes` **na mesma transação** que cria ou
   cancela o agendamento. Nunca existe agendamento sem notificação nem
   notificação sem agendamento.
2. `agendar_para` define quando: `NULL` para eventos imediatos,
   `inicio - antecedencia` para o lembrete.
3. O worker consome a fila sem saber quem produziu a linha:

```sql
SELECT * FROM notificacoes
WHERE status = 'pendente'
  AND (agendar_para IS NULL OR agendar_para <= now())
ORDER BY agendar_para NULLS FIRST
FOR UPDATE SKIP LOCKED
LIMIT 50;
```

**Idempotência:** `UNIQUE (agendamento_id, tipo, versao)` impede envio duplicado.
O `versao` existe porque um agendamento reagendado precisa de lembrete novo — a
linha antiga vira `descartado` e uma nova entra com `versao + 1`.

**Cancelamento cancela o lembrete.** Ao cancelar, a linha de lembrete pendente
vai para `descartado` na mesma transação. Sem isso, o cliente recebe o
cancelamento e, horas depois, o lembrete do mesmo compromisso.

## 6. Multi-tenancy

### 6.1 Modelo escolhido: shared schema com `tenant_id`

| Modelo | Isolamento | Custo operacional | Adequado aqui? |
|---|---|---|---|
| Banco por tenant | Máximo | Alto (N bancos, N migrations) | Não |
| Schema por tenant | Alto | Médio-alto (migrations em loop) | Não |
| Shared schema + `tenant_id` | Depende da disciplina de código | Baixo | **Sim** |

O preço do shared schema é que o isolamento vira responsabilidade do código — e é
exatamente aí que mora o pior bug possível deste projeto.

### 6.2 O risco número 1: vazamento entre tenants

Um único `SELECT` sem `WHERE tenant_id = $1` expõe a agenda de um cliente para
outro. Duas camadas de defesa, e as duas são obrigatórias:

**(a) Row Level Security no PostgreSQL** — a rede de segurança real, porque
funciona mesmo quando o código erra:

```sql
ALTER TABLE agendamentos ENABLE ROW LEVEL SECURITY;
ALTER TABLE agendamentos FORCE  ROW LEVEL SECURITY;

CREATE POLICY isolamento_tenant ON agendamentos
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());
```

Três detalhes que a v0.3 deixava passar:

- **`FORCE`**: sem ele, o dono da tabela ignora as policies. Se as migrations e a
  aplicação rodam com o mesmo usuário, o RLS vira enfeite.
- **`WITH CHECK`**: `USING` filtra o que é lido; `WITH CHECK` valida o que é
  escrito. Só com `USING`, um `INSERT` grava linha com `tenant_id` de outra
  empresa — que depois fica invisível para todo mundo, inclusive para quem
  deveria vê-la.
- **`app.current_tenant_id()`** em vez de `current_setting('app.tenant_id')::uuid`:
  a função usa `current_setting(..., true)` e retorna `NULL` quando a variável não
  foi definida, em vez de lançar erro. Com `tenant_id = NULL` avaliando como
  desconhecido, o resultado é **falhar fechado**.

A API define o contexto no início de cada transação:

```go
tx, err := pool.Begin(ctx)
defer tx.Rollback(ctx)

_, err = tx.Exec(ctx, "SELECT set_config('app.tenant_id', $1, true)", tenantID)
// o terceiro parâmetro `true` = local à transação
```

Nunca `SET` sem escopo local: com pool de conexões, a variável sobrevive ao fim da
requisição e a próxima pega a conexão reciclada com o tenant da anterior.

**(b) Camada de repositório** — nenhuma função de acesso a dados aceita query sem
receber `tenantID` como primeiro parâmetro. Torna o erro difícil de escrever, não
só difícil de passar. As queries são geradas pelo sqlc; o pacote gerado só é
usado por dentro de `internal/storage`, que abre a transação, chama `set_config`
e entrega `Queries.WithTx(tx)` (ver `docs/guia-banco-de-dados.md`).

**Exceções ao RLS, e por quê.** `usuarios` e `memberships` ficam de fora: são
consultadas *antes* de existir contexto de tenant (no login o sistema ainda não
sabe qual é o tenant — é `memberships` que responde isso). Consequência a
carregar: **essas são as duas únicas tabelas onde o `WHERE` manual é obrigatório**
e a rede de segurança não existe.

`tenants` também fica de fora pelo mesmo motivo (o login resolve o slug antes do
contexto). Por isso, **segredo por tenant não pode morar nessa tabela**: quando
`telegram_bot_token` passar a ser usado, ele sai para uma tabela própria com
policy, ou para o gerenciador de segredos.

### 6.3 Usuário pertence a um ou a vários tenants?

Modelar via `memberships` desde já, mesmo que na v1 cada usuário pertença a um
único tenant. O custo agora é uma tabela a mais; o custo depois é migrar dados de
produção.

## 7. Modelo de dados

```
tenants(id, nome, slug UNIQUE, fuso_horario, telegram_bot_token NULL, criado_em)

usuarios(id, email, senha_hash, nome, criado_em,
         UNIQUE(lower(email)))          -- e-mail normalizado

memberships(id, usuario_id, tenant_id, papel, removido_em NULL,
            UNIQUE(usuario_id, tenant_id))

sessoes(id, usuario_id, tenant_id NULL, expira_em, criado_em, revogado_em NULL)

convites(id, tenant_id, email, papel, token UNIQUE, expira_em,
         aceito_em NULL, criado_por)

clientes(id, tenant_id, nome,
         telefone TEXT NULL,            -- contato manual apenas, NÃO é canal
         telegram_chat_id BIGINT NULL,
         notificavel BOOLEAN DEFAULT true,
         vinculo_token UUID, vinculo_expira_em, vinculado_em NULL,
         removido_em NULL,
         UNIQUE(id, tenant_id), UNIQUE(tenant_id, telegram_chat_id))

servicos(id, tenant_id, nome, duracao_minutos, ativo,
         UNIQUE(id, tenant_id))

disponibilidades(id, tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim)

agendamentos(id, tenant_id, cliente_id, servico_id, prestador_id,
             inicio TIMESTAMPTZ, fim TIMESTAMPTZ, status, observacoes,
             criado_em, atualizado_em,
             UNIQUE(id, tenant_id))

notificacoes(id, tenant_id, agendamento_id, tipo, versao, status,
             agendar_para NULL, tentativas, proxima_tentativa,
             enviado_em, telegram_message_id, erro,
             UNIQUE(agendamento_id, tipo, versao))
```

**Conflito de horário.** A regra não pode viver só na aplicação: duas requisições
simultâneas passam pelas duas validações e gravam as duas.

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE agendamentos ADD CONSTRAINT sem_sobreposicao
EXCLUDE USING gist (
  tenant_id    WITH =,
  prestador_id WITH =,
  tstzrange(inicio, fim, '[)') WITH &&
) WHERE (status <> 'cancelado');
```

O `'[)'` **não é opcional**: sem ele, um agendamento das 9h às 10h conflita com
outro das 10h às 11h e slots consecutivos ficam impossíveis. A v0.3 trazia a
versão sem o argumento — é o bug mais comum com `tstzrange`.

**Chaves estrangeiras compostas.** FK simples (`cliente_id → clientes.id`) não
impede associar um cliente do tenant A a um agendamento do tenant B. Para fechar
isso no banco: `UNIQUE (id, tenant_id)` na tabela pai e
`FOREIGN KEY (cliente_id, tenant_id) REFERENCES clientes (id, tenant_id)`.

**Índices de chave estrangeira.** O PostgreSQL cria índice para a PK e para
constraints `UNIQUE`, mas **não** para a coluna que referencia. Toda FK usada em
filtro precisa de índice explícito — em especial
`notificacoes (agendamento_id, tenant_id)`, percorrido a cada cancelamento.

**Exclusão lógica.** `servicos.ativo`, `clientes.removido_em` e
`memberships.removido_em`. Como `agendamentos` referencia os três com
`ON DELETE RESTRICT`, exclusão física fica bloqueada na prática assim que existir
histórico — e apagar registro apontado por agendamento destruiria esse histórico.

## 8. Telegram

### 8.1 O que melhora em relação ao WhatsApp

Sem custo por mensagem, sem verificação de Business Manager, sem CNPJ, sem número
dedicado, sem aprovação prévia de template, botões inline nativos, bot criado no
@BotFather em minutos.

### 8.2 O que não muda: o bot não pode iniciar conversa

É a restrição que define o design, equivalente à janela de 24h do WhatsApp — na
prática mais rígida, porque não existe saída via template pago. Sem `chat_id`
capturado, o cliente é inatingível.

**Fluxo de vinculação (deep link):**

1. Ao cadastrar o cliente, o sistema gera um `vinculo_token` (UUID) com validade.
2. Entrega-se ao cliente o link `https://t.me/SeuBot?start=<vinculo_token>`.
3. O cliente abre e o Telegram envia `/start <vinculo_token>`.
4. O webhook recebe o update, resolve o token, grava `telegram_chat_id` e marca
   `vinculado_em`.

O `vinculo_token` é **uma credencial**: quem o tiver vincula o próprio Telegram
àquele cliente. Por isso tem `vinculo_expira_em` e é consumido no primeiro uso
(`vinculado_em IS NOT NULL` ⇒ recusar novo `/start`).

**Consequência no produto:** existe o estado "cliente cadastrado porém não
notificável". A UI precisa mostrar isso explicitamente, senão o prestador acha que
o lembrete foi enviado e não foi.

### 8.3 Um bot para todos os tenants, ou um por tenant?

| | Bot único | Bot por tenant |
|---|---|---|
| Setup | Um token no `.env` | Cada tenant cria no BotFather |
| Marca | Genérica | Cada empresa com seu bot |
| Webhook | Uma URL | Uma URL por bot |
| Rate limit | 30 msg/s compartilhado | 30 msg/s por tenant |
| Complexidade | Baixa | Média-alta |

**Decisão: bot único no MVP**, com a coluna `tenants.telegram_bot_token` já
prevista (nula = bot da plataforma). Com bot único, o `vinculo_token` precisa
resolver cliente **e** tenant — o que ele já faz, sendo UUID único global.

### 8.4 Limites de envio

Cerca de 1 mensagem por segundo no mesmo chat e ~30 por segundo no total. Acima
disso a API retorna HTTP 429 com `retry_after`. O worker trata 429 respeitando
`retry_after` e usa token bucket simples — nunca dispara em loop apertado.

### 8.5 Webhook vs long polling

- **Long polling em desenvolvimento**: não exige URL pública, funciona atrás de NAT.
- **Webhook em produção**: exige HTTPS com certificado válido e endpoint público.

Abstrair atrás de uma interface em Go permite trocar por configuração.

**Autenticação do webhook (RNF10).** O endpoint é público: sem verificação,
qualquer um faz POST forjando `/start <token>` ou um `callback_query` de
cancelamento. No `setWebhook`, passe `secret_token`; a cada update, compare com o
header `X-Telegram-Bot-Api-Secret-Token` e rejeite com 401 quando não bater.

## 9. Autenticação e autorização

- **Autenticação** — quem é o usuário. E-mail + senha.
- **Autorização** — a qual tenant ele pertence e o que pode fazer lá dentro.

Regras não negociáveis:

1. O `tenant_id` sai da sessão, **nunca** do payload da requisição.
2. Middleware obrigatório que extrai o tenant, valida o membership e injeta no
   contexto antes de qualquer handler.
3. Verificação de papel por rota (owner convida usuários; atendente, não).

**Decisão: sessão de servidor com cookie `HttpOnly` + `Secure` + `SameSite=Lax`**,
mais simples e mais segura que JWT em `localStorage` para uma SPA. Isso exige a
tabela `sessoes` — não é só código. Um JWT sem estado parece mais barato até o
primeiro logout que precisa invalidar de verdade.

O `tenant_id` fica **na sessão**, não no token, e é revalidado contra
`memberships` a cada requisição. Custa uma query e elimina a classe de bug do
"token continua válido para o tenant antigo".

**Cookie e origem em desenvolvimento.** Com o Vite em `:5173` e a API em `:8080`,
o cookie `SameSite=Lax` não é enviado nas chamadas da SPA. Use o proxy do Vite
(`server.proxy`) para a API responder na mesma origem — a alternativa
(`SameSite=None` + CORS com credenciais) obriga a implementar CSRF já no MVP.

## 10. Fatias

O backlog é fatiado verticalmente: cada fatia atravessa banco, API e frontend e
termina em algo demonstrável. Detalhe, issues e critérios em `docs/backlog.md`.

| Fatia | Entrega | Depende de |
|---|---|---|
| T0 Fundação | Repositório, CI, schema com `tenant_id`, RLS, constraint de sobreposição, seed | — |
| T1 Agendar | Criar e listar agendamentos no navegador, tenant fixo e sem login | T0 |
| T2 Entrar | Signup, login, sessão, middleware de tenant e papéis | T1 |
| T3 Avisar | Vinculação por deep link e confirmação imediata no Telegram | T2 |
| T4 Lembrar | Worker com outbox, idempotência, backoff e tratamento de 429/403 | T3 |
| T5 Cancelar | Cancelar e reagendar, inclusive por botão inline | T4 |
| T6 Configurar | Serviços, disponibilidades, slots e fuso por tenant | T1 |
| T7 Entregar | Deploy e documentação | T0–T6 |

**Teste que fecha a Fatia 2:** criar dois tenants, popular ambos, autenticar como
usuário do tenant A e verificar que nenhum endpoint retorna dado do tenant B (item
2.13). Sem esse teste, o isolamento é suposição, não garantia — e ele exige
**Postgres no CI** (item 0.17), não só `go test`.

## 11. Riscos

| Risco | Impacto | Mitigação |
|---|---|---|
| Query sem escopo de tenant | Crítico | RLS + repositório + teste automatizado |
| Cliente nunca abre o deep link | Não recebe lembrete | Estado explícito na UI |
| Webhook sem autenticação | Update forjado | `secret_token` (RNF10) |
| Baixa adoção do Telegram | Produto inútil na prática | Decidir cedo |
| Escopo maior que a equipe | Não entrega | Cortar RF10, RF12, RF14 |
| Equipe sem experiência em Go | Atraso geral | Responsáveis e estudo na semana 1 |
| Notificação duplicada | Perda de confiança | `UNIQUE` + `SKIP LOCKED` + backoff |

## 12. Decisões tomadas

| Tema | Decisão |
|---|---|
| Multi-tenancy | Shared schema com `tenant_id` + RLS |
| Telefone do cliente | Contato manual; nunca referenciado pelo worker |
| Dados pessoais | Seed fictício; finalidade e retenção no README |
| Criação de tenant | Signup self-service cria tenant + membership owner |
| Bot | Único no MVP, coluna por tenant já prevista |
| Sessão | Cookie `HttpOnly` + tabela `sessoes` |
| Papéis | `text` + `CHECK`, não `ENUM` |
| Acesso a dados | pgx v5 (`pgxpool`) + sqlc; sem ORM (motivo no guia de banco, §Acesso a dados) |
| UUID no Go | `google/uuid` via `overrides` do sqlc |
| Rotas da API | Tudo sob `/api`; proxy do Vite sem reescrita |
| Serviço na Fatia 1 | `SERVICO_FIXO` no config até a 6.6 |
| Worker × RLS | Laço por tenant com `ComTenant`, como `app_user` |

### 12.1 Ainda em aberto

Lista única, com prazo e responsável, em `docs/decisoes.md`. Não se repete aqui
para as duas não divergirem.

## 13. Estimativa de prazo

Base: 3 pessoas × ~5h/semana = 15h/semana nominais. Esforço por fatia no
`docs/backlog.md` (§Cronograma).

| Fatia | Esforço |
|---|---|
| T0 Fundação | 63h |
| T1 Agendar | 46h |
| T2 Entrar | 59h |
| T3 Avisar | 68h |
| T4 Lembrar | 42h |
| T5 Cancelar | 37h |
| T6 Configurar | 50h |
| T7 Entregar | 32h |
| **Total** | **~397h** |

Escopo completo: ~27 semanas nominais, ~38 com 70% de eficiência real. Com os
cortes do backlog (Fatia 6 exceto 6.4, botões inline, log e reagendamento), ~330h:
~22 semanas nominais, ~32 realistas.

**Revisar o prazo comunicado.** A v0.4 dizia "5 a 6 meses" com base em 357h. Com
o backlog atual, mesmo o escopo cortado passa de 7 meses no ritmo realista — ou o
time sobe as horas semanais, ou corta mais, ou comunica outro prazo.

Dois fatores que puxam para cima e não aparecem na conta: a curva de aprendizado
de Go (três pessoas ao mesmo tempo) e o fato de as Fatias 0 e 1 quase não
paralelizarem. Nas primeiras ~8 semanas o time roda com uma ou duas pessoas
produtivas no backend; o frontend base (1.7–1.9) é o que ocupa a terceira. A
partir da Fatia 3 abrem três frentes.
