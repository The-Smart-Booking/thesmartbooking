# Requisitos — Dayspot

Sistema de agendamentos multi-tenant, com página pública para o cliente final e avisos
planejados pelo WhatsApp.
Documento de requisitos e decisões técnicas — **v0.15** · 18/11/2026

> Fonte da verdade: este arquivo. A pasta do Drive é histórico.

## Mudanças desde a v0.14

Decisões para a primeira publicação, um piloto com uma empresa (#180), 18/11/2026.

| # | O que mudou | Onde |
|---|---|---|
| 1 | RF19: o cliente final agenda sozinho pela página pública | 1, 3, 5 |
| 2 | O canal de avisos passa de Telegram para WhatsApp, sem integração existente; fornecedor, custo e prazo ficam pendentes | 1, 2, 3, 4, 5, 6.2, 7, 8, 10, 11, 12 |
| 3 | RNF09: hospedagem no Render e no Neon | 4, 12 |
| 4 | Piloto: serviços por SQL, `POST` do painel com valores fixos, contato na página e convites depois | 7, 12 |
| 5 | Dados pessoais: empresa controladora, retenção de 12 meses e aviso do formulário | 12 |
| 6 | Papel de `clientes.telefone` sem contradição entre o modelo e as decisões | 7, 12 |
| 7 | Dependências das Fatias 5 e 6 corrigidas; a estimativa deixa de citar Go | 10, 13 |

## Mudanças desde a v0.13

Decisão de produto (#162), 18/11/2026: o cliente final agenda sozinho, sem conta, pela
página pública da empresa.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Rotas públicas `/api/publico/{slug}`: o tenant sai do slug da rota, exceção à regra da sessão | RNF05, 9, 12 |
| 2 | O telefone passa a identificar o cliente no agendamento público; continua fora das notificações | 12 |
| 3 | Janela do agendamento público, profissionais e limite de requisições por IP | 12 |

## Mudanças desde a v0.12

Migração do frontend (#146, PR #147), 16/11/2026.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Frontend passa de React + Vite para Angular; proxy de dev do `ng serve` em `:4200` | 1, 5, 9, 12 |

## Mudanças desde a v0.11

Decisão do time, 15/11/2026: as migrations também passam para o .NET.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Migrations em EF Core Migrations (`src/Api/Migrations/`), com o SQL escrito à mão em `migrationBuilder.Sql`. **Reverte o item 3 da v0.11** (goose dono do schema). O schema não muda | 1, 12 |

## Mudanças desde a v0.10

Decisão do time, 15/11/2026: o backend passa de Go para .NET.

| # | O que mudou | Onde |
|---|---|---|
| 1 | API (e worker, quando nascer) em C# com ASP.NET Core (.NET 10), no lugar de Go | 1, 5, 11, 12 |
| 2 | Acesso a dados por EF Core + Npgsql. **Reverte a decisão 10 da v0.4** ("pgx v5 + sqlc, sem ORM") | 1, 6.2, 12 |
| 3 | O goose continua dono do schema: o EF Core só mapeia, sem EF Migrations (**revertida na v0.12**: EF Core Migrations) | 6.2, 12 |

## Mudanças desde a v0.9

Revisão da Fatia 2 (backlog v2.3), 23/10/2026.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Decidido como o usuário com várias empresas escolhe a ativa no login; o slug serve à escolha, não ao formulário | 6.2, 12 |
| 2 | Decidido o cadastro do convidado sem conta (2.22) | 12 |
| 3 | Estimativa recalculada (~481h) com os itens 2.21 e 2.22 | 13 |
| 4 | Decididos os códigos de erro da Fatia 2, o rate limit do login, a entrega do link do convite e as rotas do convite | 12 |

## Mudanças desde a v0.8

Decisão de 03/10/2026: nomenclatura padronizada em português.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Papel `owner` renomeado para `administrador`; nomenclatura padronizada em português | 3, 7, 9, 12, 14 |

## Mudanças desde a v0.7

Decisões do grupo de 02/10/2026: papéis, agenda e financeiro.

| # | O que mudou | Onde |
|---|---|---|
| 1 | Papéis por tenant: só administrador e prestador; o administrador também pode atender | 3, 7, 9, 14 |
| 2 | Agendamento nunca é excluído: reagendar, cancelar e concluir, com regra de transição | 3, 5.1, 7 |
| 3 | Escopo por prestador dentro da empresa (RF15, RNF11) | 3, 4, 6.2, 9, 11 |
| 4 | Convite e remoção de membros; criador da empresa (`tenants.criado_por`) protegido | 3, 5.1, 7, 9 |
| 5 | Preço por serviço e valor no agendamento, em centavos (RF18) | 3, 7 |
| 6 | Calendário semanal, resumo financeiro mensal e exportação `.ics` (RF14, RF16, RF17) | 3, 10 |
| 7 | `sessoes` e `convites` também ficam fora do RLS | 6.2 |
| 8 | Botão Confirmar do Telegram não muda status | 8.6 |
| 9 | Fatia nova T7 Acompanhar; Entregar passa a T8; estimativa recalculada (~474h) | 10, 11, 13 |
| 10 | Decisões de 02/10/2026 registradas; seção nova com o que fica fora do escopo | 12, 14 |

## Mudanças desde a v0.6

| # | O que mudou | Onde |
|---|---|---|
| 1 | Lista de decisões em aberto (§12.1, `docs/decisoes.md`) descontinuada | 12 |

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
| 10 | Acesso a dados definido: pgx v5 + sqlc, sem ORM (**revertida na v0.11**: EF Core + Npgsql) | 1, 6.2, 12 |

---

## 1. Visão geral

Plataforma web multi-tenant para gestão de agendamentos. Cada tenant (empresa)
tem seus próprios usuários (administradores e prestadores), serviços, horários e clientes,
isolados dos demais. O cliente final agenda sozinho, sem conta, pela página pública
da empresa em `/agendar/:slug` (RF19). Os avisos ao cliente serão enviados pelo
WhatsApp; essa integração ainda não existe (§8).

A primeira publicação é um **piloto com uma empresa** (§12).

**Stack:** API em C# (ASP.NET Core, .NET 10), frontend em Angular, banco PostgreSQL
(acesso via EF Core + Npgsql; EF Core Migrations em SQL). Planejados: worker de
notificações em C# e integração com o WhatsApp.

## 2. Ordem de execução

Continua valendo: **o sistema de agendamento vem antes dos avisos**. O canal não tem
o que notificar sem o modelo de dados.

O multi-tenant adiciona complexidade estrutural que precisa estar certa **desde a
primeira migration**. Retrofit de `tenant_id` em schema pronto é caro e propenso a
erro.

## 3. Requisitos funcionais

| ID | Requisito | Prioridade |
|---|---|---|
| RF01 | Cadastro e login de usuário (e-mail + senha) | Alta |
| RF02 | Cadastro de tenant com um usuário administrador inicial, o criador (`tenants.criado_por`) | Alta |
| RF03 | Papéis por tenant: administrador e prestador; o administrador também pode atender | Alta |
| RF04 | Isolamento total de dados entre tenants | Alta |
| RF05 | Agendamentos: criar, listar, reagendar, cancelar e concluir; nunca excluídos. O prestador só opera os próprios (RF15) | Alta |
| RF06 | Definição de horários disponíveis por prestador | Alta |
| RF07 | Bloqueio de conflito de horário (double booking) | Alta |
| RF08 | Vinculação do cliente ao canal de avisos, o WhatsApp (fluxo pendente, §8.3) | Alta |
| RF09 | Envio automático de lembrete X horas antes | Alta |
| RF10 | Confirmação/cancelamento pelo cliente na mensagem do WhatsApp (forma pendente, §8.3) | Média |
| RF11 | Log de mensagens enviadas com status | Média |
| RF12 | Convite e remoção de membros: o administrador convida administradores e prestadores e remove prestadores; só o criador remove administrador; o criador não é removível | Alta |
| RF13 | Reenvio manual de notificação | Baixa |
| RF14 | Calendário semanal: eixo de horas, navegação entre semanas, cor por status e seletor de prestador para o administrador | Média |
| RF15 | Escopo por prestador: o prestador vê e opera só os próprios agendamentos (agenda, slots, log, reenvio, financeiro, `.ics`); o administrador vê todos, com seletor. Garantia em RNF11 | Alta |
| RF16 | Resumo financeiro mensal dos agendamentos concluídos: o próprio, para o prestador; total e por prestador, para o administrador | Média |
| RF17 | Exportar a agenda do período para o calendário do celular (download de `.ics`) | Baixa |
| RF18 | Preço por serviço, em centavos; o valor do agendamento é copiado na criação; recopiado só ao trocar o serviço (novo `fim` + sobreposição); remarcar mantém | Alta |
| RF19 | Agendamento público: o cliente final, sem conta, escolhe serviço, profissional e horário em `/agendar/:slug` e informa nome e telefone | Alta |

**Agendamento público (RF19).** Já existe na API (#162): rotas `/api/publico/{slug}`,
de segunda a sábado, com início a partir de 08:00 e fim até 19:00, no fuso da empresa;
todos os memberships ativos aparecem como profissionais; limite por IP de 60 leituras e
10 envios por minuto. Planejado: a tela `/agendar/:slug` (#163, PR #167); até 60 dias de
antecedência, grade de 30 minutos, até 3 agendamentos futuros por telefone e telefone
brasileiro normalizado, sem SMS (#169); contato da empresa na página (#170).

**Ciclo de vida do agendamento (RF05).** Nasce `confirmado`. Concluir só com
`inicio <= now()`; cancelar a qualquer momento enquanto `confirmado`; `cancelado` e
`concluido` são finais; remarcar e trocar serviço só em `confirmado`. Transição
fora disso responde 409 `status_invalido`.

**Clientes são da empresa**, não do prestador: todos os membros do tenant veem e
agendam os mesmos clientes. Só o administrador gerencia serviços e preços.

## 4. Requisitos não funcionais

- **RNF01** — Todos os horários em UTC (`timestamptz`); conversão só na
  apresentação. Cada tenant tem seu próprio fuso configurável.
- **RNF02** — Nenhuma query pode acessar dados sem escopo de tenant. Ver 6.2.
- **RNF03** — Envio de notificação idempotente: nunca duas vezes, mesmo com
  restart do worker ou execução concorrente.
- **RNF04** — Senhas com Argon2id ou bcrypt (custo ≥ 12).
- **RNF05** — `tenant_id` derivado exclusivamente da sessão, nunca aceito do
  corpo da requisição ou de query param. **Exceção:** nas rotas públicas
  `/api/publico/{slug}`, sem sessão, o tenant sai do slug da rota, e continua nunca
  vindo do corpo nem da query string (#162).
- **RNF06** — Rate limit no login.
- **RNF07** — Segredos via variável de ambiente.
- **RNF08** — Falha do canal de avisos não pode derrubar o fluxo de agendamento.
- **RNF09** — Hospedagem no Render, com frontend e API no mesmo endereço, e PostgreSQL
  gerenciado no Neon. Deploy da `main` depois do CI; migrations aplicadas fora do
  deploy. O `Dockerfile` da raiz gera só a imagem da API, e a configuração real do
  Render fica documentada no roteiro do #173. O Docker Compose serve só ao ambiente
  local.
- **RNF10** — Todo evento recebido do canal de avisos é autenticado antes de ser
  processado. O mecanismo depende da integração do WhatsApp (§8.3).
- **RNF11** *(v0.8)* — Escopo por prestador (RF15) garantido no backend: o filtro
  sai da sessão, pelo `Escopo` que o middleware monta (2.7), nunca do payload;
  prestador que pede `prestador_id` de outro recebe 403. Coberto por teste (2.16).
  Esconder na tela não é autorização.

## 5. Arquitetura

```
[Cliente final: /agendar/:slug]--+
                                 +--> [Angular SPA] -HTTPS/JSON-> [API .NET] --> [PostgreSQL + RLS]
[Equipe: painel]-----------------+                                                      ^
                                                                                        |
                                               (planejado) [Worker .NET (scheduler)] ---+
                                                                     |
                                                                     v
                                               (planejado) [WhatsApp, fornecedor pendente]
```

**Página pública.** `/agendar/:slug` abre sem login e chama as rotas
`/api/publico/{slug}`, que ficam fora da sessão: o tenant sai do slug da rota (RNF05,
§9). As rotas estão na `main` (#162); a tela entra pelo #163 (PR #167).

**Worker separado da API (planejado):** a API responde HTTP e precisa ser rápida; o
envio de lembretes é um processo periódico. Juntar os dois funciona com uma instância
e quebra com duas (mensagem duplicada).

### 5.1 Padrão outbox para notificações

Planejado: a tabela `notificacoes` existe, mas nenhuma rota grava nela ainda.

Três tipos de notificação, dois mecanismos:

| Tipo | Disparo | Momento do envio |
|---|---|---|
| `confirmacao` | Evento (criação do agendamento) | Imediato |
| `cancelamento` | Evento (cancelamento, inclusive em lote ao remover um membro) | Imediato |
| `lembrete` | Agendado | X horas antes do início |

Concluir não gera notificação.

1. A API insere a linha em `notificacoes` **na mesma transação** que cria ou
   cancela o agendamento, ou que remove um membro (cancelamento em lote dos
   agendamentos futuros dele, item 5.8). Nunca existe agendamento sem
   notificação nem notificação sem agendamento.
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

**Cancelamento cancela o lembrete.** Ao cancelar, as linhas pendentes de lembrete
e de confirmação vão para `descartado` na mesma transação. Sem isso, o cliente
recebe o cancelamento e, horas depois, o lembrete do mesmo compromisso.

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

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
await db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)", ct);
// o terceiro parâmetro `true` = local à transação
```

Nunca `SET` sem escopo local: com pool de conexões, a variável sobrevive ao fim da
requisição e a próxima pega a conexão reciclada com o tenant da anterior.

**(b) Camada de acesso a dados** — nenhuma consulta roda sem receber `tenantId`
logo depois do contexto e filtrar por ele. Torna o erro difícil de escrever, não
só difícil de passar. As consultas ficam em `src/Api/Dados/Consultas.cs`, sobre o
`AppDb` do EF Core, e rodam dentro da transação que o `AppDb.IniciarComTenant`
abre com `set_config` (ver `docs/guia-banco-de-dados.md`).

**Exceções ao RLS, e por quê.** `usuarios` e `memberships` ficam de fora: são
consultadas *antes* de existir contexto de tenant (no login o sistema ainda não
sabe qual é o tenant — é `memberships` que responde isso). `sessoes` e `convites`
pelo mesmo motivo: é a sessão que diz o tenant, e o aceite acha o convite pelo
token, sem tenant. Consequência a carregar: **nessas quatro tabelas e em `tenants`
(abaixo), o `WHERE` manual é obrigatório** e a rede de segurança não existe.

`tenants` também fica de fora pelo mesmo motivo (o login lista as empresas do
usuário e a escolha resolve o slug antes do contexto). Por isso, **segredo por
tenant não pode morar nessa tabela**: a coluna legada `telegram_bot_token` fica sempre
nula, garantida pelo `CHECK telegram_bot_token_nulo`.

**O RLS isola empresas, não prestadores.** Dentro da empresa, o escopo por
prestador (RF15) é filtro na query, sem policy extra: o repositório recebe o
`Escopo` montado pelo middleware a partir da sessão (RNF11; guia de banco,
§Acesso a dados).

### 6.3 Usuário pertence a um ou a vários tenants?

Modelar via `memberships` desde já, mesmo que na v1 cada usuário pertença a um
único tenant. O custo agora é uma tabela a mais; o custo depois é migrar dados de
produção.

## 7. Modelo de dados

```
tenants(id, nome, slug UNIQUE, fuso_horario, telegram_bot_token NULL,
        criado_por,                    -- usuarios: o criador da empresa
        criado_em)

usuarios(id, email, senha_hash, nome, criado_em,
         UNIQUE(lower(email)))          -- e-mail normalizado

memberships(id, usuario_id, tenant_id, papel IN ('administrador','prestador'),
            removido_em NULL, UNIQUE(usuario_id, tenant_id))

sessoes(id, usuario_id, tenant_id NULL, expira_em, criado_em, revogado_em NULL)

convites(id, tenant_id, email, papel IN ('administrador','prestador'), token UNIQUE,
         expira_em, aceito_em NULL, criado_por)

clientes(id, tenant_id, nome,
         telefone TEXT NULL,            -- contato; identifica o cliente no agendamento público
         telegram_chat_id BIGINT NULL,  -- legado, sem uso
         notificavel BOOLEAN DEFAULT true,
         vinculo_token UUID, vinculo_expira_em, vinculado_em NULL,
         removido_em NULL,
         UNIQUE(id, tenant_id), UNIQUE(tenant_id, telegram_chat_id))

servicos(id, tenant_id, nome, duracao_minutos, preco_centavos, ativo,
         UNIQUE(id, tenant_id))

disponibilidades(id, tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim)

agendamentos(id, tenant_id, cliente_id, servico_id, prestador_id,
             inicio TIMESTAMPTZ, fim TIMESTAMPTZ, status,
             valor_centavos,             -- preço do serviço, copiado
             observacoes, criado_em, atualizado_em,
             UNIQUE(id, tenant_id))

notificacoes(id, tenant_id, agendamento_id, tipo, versao, status,
             agendar_para NULL, tentativas, proxima_tentativa,
             enviado_em, telegram_message_id, erro,
             UNIQUE(agendamento_id, tipo, versao))
```

**O que está no schema hoje.** As migrations vão até a 010. `convites` ainda não
existe: a migration 011 fica para depois do piloto. As colunas do Telegram
(`tenants.telegram_bot_token`, `clientes.telegram_chat_id`, `clientes.vinculo_token`,
`clientes.vinculo_expira_em`, `clientes.vinculado_em` e
`notificacoes.telegram_message_id`) continuam no schema, sem uso, até uma migration
futura.

**Telefone do cliente.** Serve de contato e, no agendamento público, identifica o
cliente dentro da empresa pelos dígitos (#162). Nenhum aviso usa o telefone hoje; o uso
como destino do WhatsApp depende das decisões pendentes de §8.3.

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
**Agendamento nunca é apagado:** termina `cancelado` ou `concluido`, e o `app_user`
nem tem `DELETE` nessa tabela (migration 009).

**Preço e valor (RF18).** Dinheiro em centavos inteiros (`integer`, sufixo
`_centavos`), nunca `money` nem `float`. `servicos.preco_centavos` não tem
`DEFAULT`: serviço sem preço ficaria grátis em silêncio. `agendamentos.valor_centavos`
é copiado na criação; recopiado só ao trocar o serviço (novo `fim` + sobreposição);
remarcar mantém. Mesma lógica do `fim` gravado: o preço do serviço muda, e o
agendamento guarda o valor da época.

**Criador da empresa.** `tenants.criado_por` aponta para `usuarios` com
`ON DELETE RESTRICT` e é gravado pelo signup. Ninguém remove o criador, e só ele
remove outro administrador (regra do endpoint de remoção, item 5.8).

## 8. Avisos pelo WhatsApp

### 8.1 Decisão e estado atual

O canal de avisos ao cliente é o **WhatsApp**; o Telegram sai (decidido em 18/11/2026).
**Nenhuma integração existe:** o sistema não envia mensagem a ninguém e o worker não
existe. O desenho anterior, com bot, deep link e botões inline, deixou de valer.

### 8.2 O que vale para qualquer canal

- Outbox, idempotência e descarte de lembrete no cancelamento (§5.1, RNF03).
- Falha do canal não derruba o agendamento (RNF08), e evento recebido é autenticado
  (RNF10).
- O agendamento já nasce `confirmado`: confirmar pela mensagem não muda status;
  cancelar segue o mesmo caminho do cancelamento pelo sistema (item 5.2). Decidido em
  02/10/2026.
- Cliente que não pode receber aviso aparece como tal na tela, para o prestador não
  achar que o lembrete foi enviado.

### 8.3 Decisões pendentes

| Tema | Situação |
|---|---|
| Fornecedor da integração | Pendente |
| Custo | Pendente |
| Prazo | Pendente |
| Telefone como destino e consentimento do cliente (RF08) | Pendente; o aviso atual do formulário não trata de mensagens |
| Confirmar e cancelar pela mensagem (RF10) | Pendente; depende do fornecedor |
| Remoção das colunas do Telegram | Migration futura (§7) |

## 9. Autenticação e autorização

- **Autenticação** — quem é o usuário. E-mail + senha.
- **Autorização** — a qual tenant ele pertence e o que pode fazer lá dentro.

Regras não negociáveis:

1. O `tenant_id` sai da sessão, **nunca** do payload da requisição. Exceção: as rotas
   públicas `/api/publico/{slug}` não têm sessão, ficam fora dos middlewares de
   autenticação e de tenant e tiram o tenant do slug da rota (#162).
2. Middleware obrigatório que extrai o tenant, valida o membership e injeta no
   contexto antes de qualquer handler.
3. Verificação de papel por rota: só o administrador convida e remove membros, gerencia
   serviços e preços e vê o financeiro da empresa. Papel prestador: toda consulta
   de agendamento filtra pelo prestador da sessão; administrador: seletor opcional (RF15,
   RNF11). Para o prestador, agendamento de outro prestador do mesmo tenant
   responde 403; agendamento de outro tenant é 404 para qualquer papel.

**Decisão: sessão de servidor com cookie `HttpOnly` + `Secure` + `SameSite=Lax`**,
mais simples e mais segura que JWT em `localStorage` para uma SPA. Isso exige a
tabela `sessoes` — não é só código. Um JWT sem estado parece mais barato até o
primeiro logout que precisa invalidar de verdade.

O `tenant_id` fica **na sessão**, não no token, e é revalidado contra
`memberships` (com `removido_em IS NULL`) a cada requisição. Custa uma query e
elimina a classe de bug do "token continua válido para o tenant antigo" — e a
remoção de um membro corta o acesso na hora.

**Cookie e origem em desenvolvimento.** Com o `ng serve` em `:4200` e a API em `:8080`,
o cookie `SameSite=Lax` não é enviado nas chamadas da SPA. Use o proxy do `ng serve`
(`web/proxy.conf.json`) para a API responder na mesma origem — a alternativa
(`SameSite=None` + CORS com credenciais) obriga a implementar CSRF já no MVP.

## 10. Fatias

O backlog é fatiado verticalmente: cada fatia atravessa banco, API e frontend e
termina em algo demonstrável. Detalhe, issues e critérios em `docs/backlog.md`.

| Fatia | Entrega | Depende de |
|---|---|---|
| T0 Fundação | Repositório, CI, schema com `tenant_id`, RLS, constraint de sobreposição, seed | — |
| T1 Agendar | Criar e listar agendamentos no navegador, tenant fixo e sem login | T0 |
| T2 Entrar | Signup, login, sessão, middleware de tenant, papéis com escopo por prestador e convites | T1 |
| T3 Avisar | Vinculação ao canal e confirmação imediata pelo WhatsApp (integração pendente, §8.3) | T2 |
| T4 Lembrar | Worker com outbox, idempotência, backoff e tratamento dos limites do canal | T3 |
| T5 Cancelar | Cancelar, reagendar e concluir no painel e cancelar pela mensagem; remoção de membro | T2 no painel; T3 e T4 só para os avisos e o descarte do lembrete |
| T6 Configurar | Serviços com preço, disponibilidades, slots e fuso por tenant | T1, 2.7 |
| T7 Acompanhar | Calendário semanal, resumo financeiro mensal e exportação `.ics` | T5, T6 (6.1) |
| T8 Entregar | Deploy e documentação | T0–T7 |

A primeira publicação (Fatia P, piloto com uma empresa) tem cards próprios no board.

**Teste que fecha a Fatia 2:** criar dois tenants, popular ambos, autenticar como
usuário do tenant A e verificar que nenhum endpoint retorna dado do tenant B (item
2.13); e, dentro do tenant A, que um prestador não vê agendamento de outro (item
2.16). Sem esses testes, o isolamento é suposição, não garantia — e eles exigem
**Postgres no CI** (item 0.17), não só `dotnet test` sem banco.

## 11. Riscos

| Risco | Impacto | Mitigação |
|---|---|---|
| Query sem escopo de tenant | Crítico | RLS + repositório + teste automatizado |
| Vazamento entre prestadores da mesma empresa | Alto | Filtro no repositório (`Escopo`) + teste 2.16 |
| Cliente sem canal de aviso | Não recebe lembrete | Estado explícito na UI |
| Evento do canal sem autenticação | Evento forjado | Autenticar cada evento (RNF10) |
| Fornecedor, custo e prazo do WhatsApp indefinidos | Avisos sem data para entrar | Decidir antes da Fatia 3 (§8.3) |
| Abuso da página pública, sem SMS | Agenda ocupada por pedidos falsos | Limite por IP (na `main`); limites por telefone e antecedência (#169) |
| Escopo maior que a equipe | Não entrega | Cortar RF10 e RF17; RF14 só em último caso (ordem no backlog) |
| Troca de Go para .NET no meio da Fatia 2 | Retrabalho | Porte com paridade de comportamento e os mesmos testes, antes de código novo |
| Notificação duplicada | Perda de confiança | `UNIQUE` + `SKIP LOCKED` + backoff |

## 12. Decisões tomadas

| Tema | Decisão |
|---|---|
| Multi-tenancy | Shared schema com `tenant_id` + RLS |
| Telefone do cliente | Contato e, no agendamento público, identificação do cliente dentro da empresa pelos dígitos (#162). Nenhum aviso usa o telefone hoje; o uso como destino do WhatsApp está pendente (§8.3) |
| Dados pessoais | Seed só com dados fictícios; regras do piloto nas decisões de 18/11/2026 abaixo |
| Criação de tenant | Signup self-service cria tenant + membership administrador e grava `tenants.criado_por` |
| Canal de avisos | WhatsApp (§8), no lugar do bot do Telegram |
| Sessão | Cookie `HttpOnly` + tabela `sessoes` |
| Papéis | `text` + `CHECK`, não `ENUM` |
| Backend | C# com ASP.NET Core (.NET 10), minimal APIs; um projeto de API e um de testes (xUnit). Decidido em 15/11/2026, no lugar de Go (#144) |
| Frontend | Angular (Angular CLI, componentes standalone, signals, `@angular/router`), em `web/`. Decidido em 16/11/2026, no lugar de React + Vite (#146, #147) |
| Acesso a dados | EF Core + Npgsql, só mapeando o schema (guia de banco, §Acesso a dados). Reverte "pgx v5 + sqlc, sem ORM" em 15/11/2026 |
| Migrations | EF Core Migrations em `src/Api/Migrations/`, SQL escrito à mão em `migrationBuilder.Sql` (`Up` e `Down`); `dotnet-ef` como ferramenta local de versão fixa. Rodam como dono das tabelas (`MIGRATIONS_DATABASE_URL`). Decidido em 15/11/2026 |
| UUID no C# | `Guid` (nulo: `Guid?`) |
| Rotas da API | Tudo sob `/api`; proxy do `ng serve` sem reescrita |
| Serviço na Fatia 1 | `SERVICO_FIXO` no config até a 6.6 |
| Sessão na Fatia 1 | `TENANT_FIXO` e `PRESTADOR_FIXO` no config até a 2.12 |
| Configuração | Só `src/Api/Config.cs` lê o ambiente; `.env` lido pelo próprio `Config`, ambiente real vence o arquivo |
| Worker × RLS | Laço por tenant com `IniciarComTenant`, como `app_user` |

Decididos em 02/10/2026:

| Tema | Decisão |
|---|---|
| Papéis por tenant | `administrador` e `prestador`; o administrador também pode atender (ser o prestador de um agendamento) |
| Escopo do prestador | Filtro na query pelo `Escopo` do middleware (2.7), sem policy extra no RLS |
| Agendamento de outro prestador | 403 `sem_permissao` no mesmo tenant; outro tenant segue 404 |
| Exclusão de agendamento | Nunca: termina cancelado ou concluído; `app_user` sem `DELETE` na tabela |
| Concluir e cancelar | Concluir só com `inicio <= now()`; cancelar enquanto `confirmado`; cancelado e concluído são finais |
| Dinheiro | Centavos em `integer` (sufixo `_centavos`), nunca `money` nem `float` |
| Valor do agendamento | Copiado na criação; recopiado só ao trocar o serviço (novo `fim` + sobreposição); remarcar mantém |
| Serviços e preços | Só o administrador gerencia |
| Convites | Administrador convida administrador ou prestador; o criador não é removível e só ele remove outro administrador |
| Remoção de membro | Cancela em lote os agendamentos futuros (`inicio > now()`) e avisa os clientes, na mesma transação |
| Exportação | Download de `.ics` (fotografia do período), sem feed |
| Disponibilidade de outros | O administrador edita a de qualquer prestador; o prestador, só a própria |
| Botão Confirmar | Só registra a presença e edita a mensagem; não muda status |
| Corte de escopo | RF12, RF14, 5.3, 6.1 e 6.5a protegidos; na Fatia 7, cortar `.ics` → financeiro por prestador → calendário (último recurso; fica a lista da 1.10) |

Decidido em 03/10/2026:

| Tema | Decisão |
|---|---|
| Nomenclatura | Padronizada em português; papel `owner` renomeado para `administrador` |

Decidido em 23/10/2026 (recomendações de `docs/especificacao-fluxo.md` §8.1, §8.2 e §8.9):

| Tema | Decisão |
|---|---|
| Empresa no login | Login só com e-mail e senha, sem slug no formulário. Com uma membership ativa, a sessão nasce na empresa; com várias, nasce com `tenant_id` nulo e a tela de escolha grava a empresa pelo slug (`POST /api/sessao/empresa`), após validar o membership ativo. Até escolher, a sessão só acessa `/me`, logout e a escolha (2.3, 2.6, 2.10) |
| Convidado sem conta | "Criar conta" pelo convite grava só `usuarios` e a membership do convite, com o e-mail do convite; o `/signup` continua criando empresa nova (2.22) |
| E-mail ou slug já usado | 409 `email_em_uso` (signup e criar conta pelo convite) e 409 `slug_em_uso` (2.2, 2.22) |
| Credencial inválida | 401 `credencial_invalida`, a mesma resposta para e-mail inexistente e senha errada (2.3) |
| Rate limit do login | 429 `muitas_tentativas` com `Retry-After`; 5 tentativas falhas por e-mail normalizado e 20 por IP em 15 minutos; login certo zera o contador do e-mail; contador em memória na API (2.8) |
| Link do convite | O administrador copia o link `/convite/{token}` na tela de prestadores; sem envio de e-mail no MVP. `POST` e `GET /api/convites` devolvem o token (2.18, 2.20) |
| Rotas do convite | `GET /api/convites/{token}` sem sessão (empresa, papel e e-mail); `POST /api/convites/{token}/aceite` com sessão, fora do middleware de tenant, e a sessão passa para a empresa do convite; `POST /api/convites/{token}/conta` sem sessão. Telas `/convite/:token` e `/convite/:token/conta`. Sessão com outro e-mail → 403 `sem_permissao` (2.19, 2.22, 2.6) |

Decidido em 18/11/2026 (#162):

| Tema | Decisão |
|---|---|
| Agendamento público | O cliente final agenda sem conta em `/api/publico/{slug}`. Cliente e agendamento na mesma transação; telefone já cadastrado reaproveita o cliente mais antigo, sem alterar o nome gravado e sem revelar dados dele |
| Janela do agendamento público | Segunda a sábado, início a partir de 08:00 e fim até 19:00, no fuso da empresa (`tenants.fuso_horario`). Regra fixa até existir o cadastro de disponibilidade. O `POST /api/agendamentos` do painel não muda |
| Profissionais na página pública | Todos os memberships ativos da empresa, sem marca de "atende" |
| Limite das rotas públicas | Por IP: 60 leituras e 10 envios por minuto. Acima disso, 429 `muitas_tentativas` com `Retry-After`. O IP vem do `CF-Connecting-IP`, que a Cloudflare do Render sobrescreve; o `X-Forwarded-For` não é usado, porque o cliente controla o começo dele. IPv6 conta por bloco /64. Contador em memória na API |
| Dados da empresa na página pública | Hoje a API devolve só nome e fuso. Revisto abaixo: o contato entra pelo #170 |

Decidido em 18/11/2026, para o piloto (#180):

| Tema | Decisão |
|---|---|
| Primeira publicação | Piloto com uma empresa |
| Hospedagem | Render e Neon, com frontend e API no mesmo endereço, deploy da `main` depois do CI e migrations aplicadas fora do deploy (RNF09, #173) |
| Serviços do piloto | Cadastrados por SQL no provisionamento (#174) |
| `POST /api/agendamentos` do painel | Grava o serviço e o prestador fixos da empresa piloto (`SERVICO_FIXO` e `PRESTADOR_FIXO`) e copia o preço desse serviço. O corpo traz só `cliente_id`, `inicio` e `fim` |
| Regras planejadas do agendamento público | Até 60 dias de antecedência, grade de 30 minutos, até 3 agendamentos futuros por telefone e telefone brasileiro normalizado, sem SMS (#169) |
| Contato na página pública | A página mostra um contato da empresa (#170); o endereço fica fora |
| Convites | Ficam para depois do piloto |
| Dados pessoais | A empresa é a controladora; retenção de 12 meses após o último atendimento; validação jurídica pendente (#175) |
| Aviso de privacidade | Texto atual do formulário público (#163, PR #167): "Seu nome e telefone ficam guardados com esta empresa para registrar o agendamento e identificar você nos próximos." |
| Aprovação de PR | PR para a `main` exige 1 aprovação |

Decidido em 20/11/2026 (#170):

| Tema | Decisão |
|---|---|
| Contato na página pública | `tenants.contato_publico` (até 120 caracteres), devolvido como `contato` em `GET /api/publico/{slug}` e exibido como texto simples, sem link, no topo da página e na confirmação, com a orientação de procurar a empresa para cancelar. O valor é lido sem espaços nas pontas |
| Empresa sem contato | Contato vazio ou só com espaços conta como ausente: todas as rotas `/api/publico/{slug}` respondem 404 `nao_encontrado` e a página mostra empresa não encontrada. O contato da empresa piloto é gravado no provisionamento (#174); a edição pelo painel fica fora do piloto (só por SQL) |

## 13. Estimativa de prazo

Base: 3 pessoas × ~5h/semana = 15h/semana nominais. Esforço por fatia no
`docs/backlog.md` (§Cronograma).

| Fatia | Esforço |
|---|---|
| T0 Fundação | 63h |
| T1 Agendar | 46h |
| T2 Entrar | 88h |
| T3 Avisar | 71h |
| T4 Lembrar | 42h |
| T5 Cancelar | 52h |
| T6 Configurar | 50h |
| T7 Acompanhar | 37h |
| T8 Entregar | 32h |
| **Total** | **~481h** |

Escopo completo: ~33 semanas nominais, ~46 com 70% de eficiência real. Com os
cortes do backlog (Fatia 6 exceto 6.1, 6.4 e 6.5a, botões na mensagem, `.ics` e
financeiro por prestador), ~417h: ~28 semanas nominais, ~40 realistas. Cortar
também o calendário semanal, o último recurso, leva a ~402h.

**Revisar o prazo comunicado.** A v0.4 dizia "5 a 6 meses" com base em 357h. Com
o backlog atual, mesmo o escopo cortado passa de 9 meses no ritmo realista — ou o
time sobe as horas semanais, ou corta mais, ou comunica outro prazo.

Dois fatores que puxam para cima e não aparecem na conta: a curva de aprendizado
de .NET (três pessoas ao mesmo tempo) e o fato de as Fatias 0 e 1 quase não
paralelizarem. Nas primeiras ~8 semanas o time roda com uma ou duas pessoas
produtivas no backend; o frontend base (1.7–1.9) é o que ocupa a terceira. A
partir da Fatia 3 abrem três frentes.

## 14. Fora do escopo

O que o MVP não faz, de propósito. Decidido em 02/10/2026.

| Tema | Por quê |
|---|---|
| Transferir a empresa, ou recuperá-la quando o criador sai ou perde a conta | O criador (`tenants.criado_por`) não é removível; trocar de dono pede fluxo próprio |
| Feed (assinatura) de calendário `.ics` | Só download (RF17): o feed precisa de URL com token e expõe a agenda fora da sessão |
| Papel `atendente` | Removido na v0.8: quem agenda é o administrador, o próprio prestador ou, desde a v0.14, o cliente final pela página pública |
