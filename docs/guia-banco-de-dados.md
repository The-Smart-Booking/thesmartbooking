# Guia de Banco de Dados — Smart Booking

Fase 1 do backlog (issues 1.1 a 1.14). Complementa `docs/requisitos.md` (v0.4).
Versão **1.1** · 16/09/2026

## Mudanças desde a v1.0

| # | O que mudou | Onde |
|---|---|---|
| 1 | `UNIQUE (lower(email))` em `usuarios` | 003 |
| 2 | `removido_em` em `memberships` e `clientes` (exclusão lógica) | 003, 004 |
| 3 | `vinculo_expira_em` em `clientes`; token é credencial de uso único | 004 |
| 4 | Índice de FK em `notificacoes (agendamento_id, tenant_id)` | 008 |
| 5 | Migration 010 — `sessoes` | 010 |
| 6 | Migration 011 — `convites` (RF12) | 011 |
| 7 | Nota sobre `tenants` fora do RLS e segredo por tenant | 009 |
| 8 | Aviso sobre `NULLS NOT DISTINCT` em `telegram_chat_id` | 004 |
| 9 | Checklist ganhou exclusão de tenant, índices de FK e e-mail duplicado | final |

---

## Antes de escrever a primeira migration

**Versão mínima do PostgreSQL: 15.** A 13 já traz `gen_random_uuid()` nativo
(dispensa `pgcrypto`), mas a 15 traz melhorias em RLS que valem. No
`compose.yml`, fixe a versão: `postgres:16-alpine`. Nunca use `latest` — o banco
muda de versão sozinho e a equipe passa uma tarde caçando o motivo.

**Ferramenta de migrations:** goose. SQL puro, um único binário Go, sem DSL para
aprender. Alternativa razoável: `golang-migrate`. O que importa mais que a
escolha: **nenhuma alteração de schema fora de migration versionada, nunca**. Um
`ALTER TABLE` rodado à mão no banco de alguém é a origem clássica do "na minha
máquina funciona".

```bash
go install github.com/pressly/goose/v3/cmd/goose@latest
mkdir -p db/migrations
goose -dir db/migrations create criar_extensoes sql
```

**Regra para toda migration:** escreva o `Down` junto com o `Up`, e teste os dois.
Down que não funciona é Down que não existe, e vocês descobrem isso no pior
momento possível.

## Ordem das migrations

A ordem não é estética — é imposta pelas dependências de chave estrangeira:

```
001 extensões + função de contexto
002 tenants
003 usuarios + memberships
004 clientes
005 servicos
006 disponibilidades
007 agendamentos        ← depende de 003, 004, 005
008 notificacoes        ← depende de 007
009 RLS policies        ← depende de todas
010 sessoes             ← depende de 003 (independe do resto)
011 convites            ← depende de 002 e 003
```

---

## 001 — Extensões e função de contexto

```sql
-- +goose Up
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS app;

-- +goose StatementBegin
CREATE FUNCTION app.current_tenant_id() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;
-- +goose StatementEnd

-- +goose Down
DROP FUNCTION IF EXISTS app.current_tenant_id();
DROP SCHEMA IF EXISTS app;
DROP EXTENSION IF EXISTS btree_gist;
```

`btree_gist` é obrigatório para a constraint de sobreposição: um índice GiST não
sabe comparar `uuid` com `=` sem essa extensão, e a constraint mistura igualdade
de UUID com sobreposição de intervalo.

**Por que uma função em vez de `current_setting` direto na policy:** o segundo
argumento `true` faz a função retornar `NULL` quando a variável não foi definida,
em vez de lançar erro. Combinado com `tenant_id = NULL` (que avalia como
desconhecido, nunca verdadeiro), o resultado é **falha fechada**: esqueceu de
definir o tenant, não vem linha nenhuma.

`-- +goose StatementBegin/End` é necessário porque o goose divide o arquivo por
`;` e o corpo da função tem `;` dentro.

## 002 — tenants

```sql
-- +goose Up
CREATE TABLE tenants (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  nome               text NOT NULL,
  slug               text NOT NULL UNIQUE,
  fuso_horario       text NOT NULL DEFAULT 'America/Sao_Paulo',
  telegram_bot_token text,          -- NULL = usa o bot da plataforma
  criado_em          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT slug_formato CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$')
);

-- +goose Down
DROP TABLE tenants;
```

`fuso_horario` como `text`, validado na aplicação contra `pg_timezone_names`.
Guardar offset numérico (`-3`) quebra no horário de verão de qualquer país que o
adote.

`slug` é o identificador legível usado na URL e no header de tenant. O `CHECK`
impede maiúscula, espaço ou caractere que quebre URL.

⚠️ **`telegram_bot_token` só pode ser preenchido depois que a 009 resolver o
acesso a esta tabela.** `tenants` fica fora do RLS (o login precisa dela antes de
existir contexto de tenant), então hoje qualquer sessão autenticada leria o token
de todas as empresas. Enquanto a coluna for sempre nula, não há risco; no dia em
que o bot por tenant entrar, o segredo sai para tabela própria com policy.

## 003 — usuarios e memberships

```sql
-- +goose Up
CREATE TABLE usuarios (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  email      text NOT NULL,
  senha_hash text NOT NULL,
  nome       text NOT NULL,
  criado_em  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX usuarios_email_unico ON usuarios (lower(email));

CREATE TABLE memberships (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  usuario_id  uuid NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  tenant_id   uuid NOT NULL REFERENCES tenants(id)  ON DELETE CASCADE,
  papel       text NOT NULL,
  removido_em timestamptz,
  criado_em   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT papel_valido CHECK (papel IN ('owner', 'prestador', 'atendente')),
  CONSTRAINT membership_unico UNIQUE (usuario_id, tenant_id)
);

CREATE INDEX idx_memberships_usuario ON memberships (usuario_id);
CREATE INDEX idx_memberships_tenant  ON memberships (tenant_id);

-- +goose Down
DROP TABLE memberships;
DROP TABLE usuarios;
```

**`usuarios` não tem `tenant_id`.** É deliberado: o mesmo e-mail pode pertencer a
mais de um tenant, e o vínculo mora em `memberships`. Colocar `tenant_id` aqui
obrigaria a mesma pessoa a ter contas e senhas separadas por empresa.

**Unicidade por `lower(email)`, não por `email`.** `UNIQUE` direto na coluna deixa
`Davi@x.com` e `davi@x.com` criarem duas contas — e o login de uma nunca encontra
a outra. O índice funcional resolve no banco; a aplicação ainda normaliza antes de
gravar.

**`UNIQUE (usuario_id, tenant_id)` faz dois trabalhos:** impede membership
duplicado e serve de alvo para a FK composta de `agendamentos` (migration 007). É
o que garante, **no banco**, que um prestador pertence ao tenant do agendamento.

**`removido_em` em vez de `DELETE`.** Como `agendamentos` referencia
`memberships` com `ON DELETE RESTRICT`, remover fisicamente um prestador que já
atendeu alguém é impossível. Sem exclusão lógica, não existe "tirar o funcionário
da equipe" — só erro de chave estrangeira. Queries de listagem filtram
`removido_em IS NULL`.

**`papel` como `text` + `CHECK`, não `ENUM`.** Tipo `ENUM` é difícil de alterar:
adicionar valor é fácil, remover ou renomear exige recriar o tipo e reescrever
todas as colunas que o usam. `CHECK` se altera com um `ALTER TABLE`.

## 004 — clientes

```sql
-- +goose Up
CREATE TABLE clientes (
  id                uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id         uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  nome              text NOT NULL,
  telefone          text,            -- contato manual apenas
  telegram_chat_id  bigint,
  vinculo_token     uuid DEFAULT gen_random_uuid(),
  vinculo_expira_em timestamptz NOT NULL DEFAULT now() + interval '30 days',
  vinculado_em      timestamptz,
  notificavel       boolean NOT NULL DEFAULT true,
  removido_em       timestamptz,
  criado_em         timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  CONSTRAINT clientes_id_tenant       UNIQUE (id, tenant_id),
  CONSTRAINT chat_id_unico_por_tenant UNIQUE (tenant_id, telegram_chat_id),
  CONSTRAINT vinculo_token_unico      UNIQUE (vinculo_token),
  CONSTRAINT vinculo_coerente CHECK (
       (telegram_chat_id IS NULL     AND vinculado_em IS NULL)
    OR (telegram_chat_id IS NOT NULL AND vinculado_em IS NOT NULL)
  )
);

CREATE INDEX idx_clientes_tenant ON clientes (tenant_id);

-- +goose Down
DROP TABLE clientes;
```

`telefone` é `text` e nulo, **sem** validação E.164. Decisão registrada: contato
manual, não canal de notificação. Nenhuma query do worker pode referenciar essa
coluna. Se alguém validar formato de telefone aqui, entendeu errado o propósito
do campo.

`telegram_chat_id` é `bigint`, não `int`. IDs do Telegram já passam de 2³¹.

**`vinculo_token` é uma credencial.** Quem tiver o token vincula o próprio
Telegram àquele cliente. Por isso tem prazo (`vinculo_expira_em`) e é de uso
único: com `vinculado_em IS NOT NULL`, o handler do `/start` recusa. Regenerar o
token é uma operação explícita da aplicação, não um efeito colateral.

O token ser **único global** é o que permite o bot único: um `/start <token>`
resolve cliente e tenant de uma vez, sem o bot saber de qual empresa veio o link.

`vinculo_coerente` impede o estado inconsistente "tem chat_id mas nunca vinculou".
Custa uma linha e elimina uma classe de bug que só aparece em produção.

`notificavel` é desligado quando o Telegram retorna 403 (bot bloqueado). Separado
de `telegram_chat_id IS NULL` de propósito: são causas diferentes de "não dá para
avisar", e a UI precisa distinguir "nunca vinculou" de "bloqueou o bot".

`UNIQUE (id, tenant_id)` parece redundante com a PK. Não é: é o alvo obrigatório
da FK composta em `agendamentos`. Sem ela, o Postgres recusa a chave estrangeira.

⚠️ **Não troque `UNIQUE (tenant_id, telegram_chat_id)` por `NULLS NOT DISTINCT`.**
Hoje, vários clientes sem Telegram convivem porque `NULL` é distinto de `NULL`.
Com `NULLS NOT DISTINCT`, cada tenant passaria a poder ter **um único** cliente
não vinculado.

`removido_em`: mesma justificativa de `memberships`. O README promete que dá para
apagar um cliente; com `ON DELETE RESTRICT` vindo de `agendamentos`, a exclusão
lógica é a única forma de cumprir isso sem destruir histórico.

## 005 — servicos

```sql
-- +goose Up
CREATE TABLE servicos (
  id              uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  nome            text NOT NULL,
  duracao_minutos int  NOT NULL,
  ativo           boolean NOT NULL DEFAULT true,
  criado_em       timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  CONSTRAINT servicos_id_tenant UNIQUE (id, tenant_id),
  CONSTRAINT duracao_positiva CHECK (duracao_minutos > 0 AND duracao_minutos <= 1440)
);

CREATE INDEX idx_servicos_tenant ON servicos (tenant_id);

-- +goose Down
DROP TABLE servicos;
```

`ativo` em vez de `DELETE`. Apagar um serviço quebra o histórico de agendamentos
que apontam para ele. A listagem no frontend filtra `ativo = true`.

`agendamentos.fim` é gravado, não derivado de `duracao_minutos`: a duração do
serviço pode mudar, e o agendamento antigo tem que continuar contando a história
do horário que foi realmente reservado.

## 006 — disponibilidades

```sql
-- +goose Up
CREATE TABLE disponibilidades (
  id           uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  prestador_id uuid NOT NULL,
  dia_semana   smallint NOT NULL,     -- 0 = domingo ... 6 = sábado
  hora_inicio  time NOT NULL,
  hora_fim     time NOT NULL,
  PRIMARY KEY (id),
  FOREIGN KEY (prestador_id, tenant_id)
    REFERENCES memberships (usuario_id, tenant_id) ON DELETE CASCADE,
  CONSTRAINT dia_valido     CHECK (dia_semana BETWEEN 0 AND 6),
  CONSTRAINT horario_valido CHECK (hora_fim > hora_inicio)
);

CREATE INDEX idx_disponibilidades_prestador
  ON disponibilidades (tenant_id, prestador_id, dia_semana);

-- +goose Down
DROP TABLE disponibilidades;
```

**`time` sem fuso, `dia_semana` como número.** A disponibilidade é uma regra
semanal recorrente no fuso do tenant ("segunda das 9h às 18h"), não um instante no
tempo. Guardar como `timestamptz` seria erro conceitual. A conversão para instante
acontece no cálculo de slots, usando `tenants.fuso_horario`.

**Primeira FK composta do schema.** `(prestador_id, tenant_id)` →
`memberships (usuario_id, tenant_id)` garante que só é possível cadastrar
disponibilidade para alguém que realmente pertence àquele tenant. FK simples para
`usuarios(id)` deixaria passar prestador de outra empresa.

## 007 — agendamentos

```sql
-- +goose Up
CREATE TABLE agendamentos (
  id            uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id     uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  cliente_id    uuid NOT NULL,
  servico_id    uuid NOT NULL,
  prestador_id  uuid NOT NULL,
  inicio        timestamptz NOT NULL,
  fim           timestamptz NOT NULL,
  status        text NOT NULL DEFAULT 'confirmado',
  observacoes   text,
  criado_em     timestamptz NOT NULL DEFAULT now(),
  atualizado_em timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  CONSTRAINT agendamentos_id_tenant UNIQUE (id, tenant_id),
  FOREIGN KEY (cliente_id, tenant_id)
    REFERENCES clientes (id, tenant_id) ON DELETE RESTRICT,
  FOREIGN KEY (servico_id, tenant_id)
    REFERENCES servicos (id, tenant_id) ON DELETE RESTRICT,
  FOREIGN KEY (prestador_id, tenant_id)
    REFERENCES memberships (usuario_id, tenant_id) ON DELETE RESTRICT,
  CONSTRAINT status_valido     CHECK (status IN ('confirmado','cancelado','concluido')),
  CONSTRAINT intervalo_valido  CHECK (fim > inicio)
);

ALTER TABLE agendamentos ADD CONSTRAINT sem_sobreposicao
EXCLUDE USING gist (
  tenant_id    WITH =,
  prestador_id WITH =,
  tstzrange(inicio, fim, '[)') WITH &&
) WHERE (status <> 'cancelado');

CREATE INDEX idx_agendamentos_tenant_inicio ON agendamentos (tenant_id, inicio);
CREATE INDEX idx_agendamentos_cliente       ON agendamentos (tenant_id, cliente_id);

-- +goose Down
DROP TABLE agendamentos;
```

Esta é a tabela central e concentra os três mecanismos que sustentam o resto.

**As três FKs compostas.** Todas carregam `tenant_id`. Isso torna **impossível no
banco** vincular um cliente do tenant A a um agendamento do tenant B — não é
validação que a aplicação pode esquecer.

**`ON DELETE RESTRICT`, não `CASCADE`.** Apagar um cliente não deve apagar
silenciosamente o histórico dele. O banco recusa a operação destrutiva por padrão;
a aplicação decide o que fazer (e, com `removido_em`, a resposta é exclusão
lógica).

**A constraint de exclusão** é o item mais importante da Fase 1 depois do RLS.
Duas requisições simultâneas passam pela validação da aplicação e gravam as duas —
só o banco resolve isso.

- `'[)'` deixa explícito que o fim é exclusivo. Sem isso, 9h–10h conflita com
  10h–11h e slots consecutivos ficam impossíveis. É o bug mais comum com
  `tstzrange`, e a v0.3 dos requisitos trazia exatamente essa versão.
- `WHERE (status <> 'cancelado')` permite reagendar para um horário antes ocupado
  por um agendamento cancelado.
- A constraint vale por **prestador**, não por cliente: o mesmo cliente pode, em
  tese, ter dois agendamentos simultâneos com prestadores diferentes. Se isso não
  for desejado, é uma segunda constraint — decidam explicitamente.
- Violação chega no Go como `SQLSTATE 23P01` (`exclusion_violation`) e precisa
  virar **HTTP 409**, não 500:

```go
var pgErr *pgconn.PgError
if errors.As(err, &pgErr) && pgErr.Code == "23P01" {
    return ErrHorarioIndisponivel
}
```

`atualizado_em` precisa de trigger ou de disciplina no repositório. Trigger é mais
confiável — ninguém esquece:

```sql
-- +goose StatementBegin
CREATE FUNCTION app.tocar_atualizado_em() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.atualizado_em = now();
  RETURN NEW;
END;
$$;
-- +goose StatementEnd

CREATE TRIGGER trg_agendamentos_atualizado_em
BEFORE UPDATE ON agendamentos
FOR EACH ROW EXECUTE FUNCTION app.tocar_atualizado_em();
```

## 008 — notificacoes (outbox)

```sql
-- +goose Up
CREATE TABLE notificacoes (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  agendamento_id      uuid NOT NULL,
  tipo                text NOT NULL,
  versao              int  NOT NULL DEFAULT 1,
  status              text NOT NULL DEFAULT 'pendente',
  agendar_para        timestamptz,        -- NULL = enviar imediatamente
  tentativas          int  NOT NULL DEFAULT 0,
  proxima_tentativa   timestamptz,
  enviado_em          timestamptz,
  telegram_message_id bigint,
  erro                text,
  criado_em           timestamptz NOT NULL DEFAULT now(),
  FOREIGN KEY (agendamento_id, tenant_id)
    REFERENCES agendamentos (id, tenant_id) ON DELETE CASCADE,
  CONSTRAINT tipo_valido   CHECK (tipo   IN ('confirmacao','lembrete','cancelamento')),
  CONSTRAINT status_valido CHECK (status IN ('pendente','enviado','falhou','descartado')),
  CONSTRAINT notificacao_unica UNIQUE (agendamento_id, tipo, versao)
);

CREATE INDEX idx_notificacoes_fila
  ON notificacoes (agendar_para NULLS FIRST)
  WHERE status = 'pendente';

CREATE INDEX idx_notificacoes_agendamento
  ON notificacoes (agendamento_id, tenant_id);

-- +goose Down
DROP TABLE notificacoes;
```

`UNIQUE (agendamento_id, tipo, versao)` é a garantia de idempotência. O `versao`
existe porque um agendamento reagendado precisa de lembrete novo, e a constraint
sem ele bloquearia o segundo insert.

**Índice parcial na fila.** `WHERE status = 'pendente'` mantém o índice pequeno
para sempre: notificações já enviadas saem dele. Sem o `WHERE`, o índice cresce
junto com todo o histórico e a consulta do worker degrada com o tempo.

**Índice de FK (novo na v1.1).** O Postgres cria índice para PK e `UNIQUE`, mas
**não** para a coluna que referencia. O cancelamento roda
`UPDATE notificacoes SET status='descartado' WHERE agendamento_id = $1 AND status='pendente'`,
que sem `idx_notificacoes_agendamento` vira varredura sequencial — e a mesma falta
de índice deixa lento o `CASCADE` quando um agendamento é apagado.

`ON DELETE CASCADE` aqui, ao contrário de `agendamentos`: notificação sem
agendamento é dado órfão puro.

`proxima_tentativa` guarda o backoff. Sem essa coluna, o worker reprocessa a mesma
notificação falha em todo ciclo e queima o rate limit do Telegram.

## 009 — RLS

```sql
-- +goose Up
ALTER TABLE clientes         ENABLE ROW LEVEL SECURITY;
ALTER TABLE servicos         ENABLE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades ENABLE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     ENABLE ROW LEVEL SECURITY;
ALTER TABLE notificacoes     ENABLE ROW LEVEL SECURITY;

ALTER TABLE clientes         FORCE ROW LEVEL SECURITY;
ALTER TABLE servicos         FORCE ROW LEVEL SECURITY;
ALTER TABLE disponibilidades FORCE ROW LEVEL SECURITY;
ALTER TABLE agendamentos     FORCE ROW LEVEL SECURITY;
ALTER TABLE notificacoes     FORCE ROW LEVEL SECURITY;

CREATE POLICY isolamento_tenant ON clientes
  FOR ALL
  USING      (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());
-- repetir o bloco acima para servicos, disponibilidades,
-- agendamentos e notificacoes

-- +goose Down
DROP POLICY isolamento_tenant ON clientes;
-- ... demais tabelas
ALTER TABLE clientes DISABLE ROW LEVEL SECURITY;
-- ... demais tabelas
```

### Três armadilhas que fazem o RLS parecer ligado sem estar

1. **O dono da tabela ignora RLS por padrão.** É a pegadinha número um. Se as
   migrations rodam como `postgres` e a aplicação conecta como `postgres`, todas
   as policies acima são decorativas — e o teste de isolamento passa por acidente
   enquanto o vazamento continua aberto. Duas defesas, use as duas: `FORCE ROW
   LEVEL SECURITY` e um usuário de aplicação separado que **não** é dono das
   tabelas.

2. **`SET` em vez de `SET LOCAL`.** Com pool de conexões, `SET` persiste na
   conexão depois que a requisição termina. A próxima pega a conexão reciclada com
   o tenant da anterior — e passa a ler dados da outra empresa. `set_config(...,
   true)` morre com a transação. **Toda** requisição precisa abrir transação:

```go
tx, err := pool.Begin(ctx)
defer tx.Rollback(ctx)

_, err = tx.Exec(ctx, "SELECT set_config('app.tenant_id', $1, true)", tenantID)
```

   `set_config(..., true)` é preferível a `SET LOCAL` porque aceita parâmetro —
   `SET LOCAL app.tenant_id = $1` não funciona, `SET` não parametriza. Contornar
   concatenando string é abrir SQL injection na própria camada de segurança.

3. **`WITH CHECK` ausente.** `USING` filtra o que é lido; `WITH CHECK` valida o
   que é escrito. Só com `USING`, um `INSERT` grava linha com `tenant_id` de outra
   empresa — que depois fica invisível para todo mundo, inclusive para quem
   deveria vê-la.

### Por que `usuarios`, `memberships` e `tenants` ficam de fora

Exceção deliberada, e precisa estar escrita para não parecer esquecimento: essas
tabelas são consultadas **antes** de existir contexto de tenant. No login o
sistema ainda não sabe qual é o tenant — é `memberships` que responde isso.
Aplicar policy nelas cria dependência circular.

Duas consequências a carregar:

- São as **únicas** tabelas onde o `WHERE` manual é obrigatório. A rede de
  segurança não cobre essas três.
- `tenants` legível por qualquer sessão significa que **segredo por tenant não
  pode morar lá** (ver nota na 002).

## 010 — sessoes

> Depende da decisão "cookie de sessão vs JWT" (requisitos, seção 9). Com a
> decisão atual — cookie `HttpOnly` —, esta migration é obrigatória antes da F2.

```sql
-- +goose Up
CREATE TABLE sessoes (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  usuario_id  uuid NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  tenant_id   uuid REFERENCES tenants(id) ON DELETE CASCADE,
  criado_em   timestamptz NOT NULL DEFAULT now(),
  expira_em   timestamptz NOT NULL,
  revogado_em timestamptz,
  user_agent  text,
  CONSTRAINT expira_depois_de_criar CHECK (expira_em > criado_em)
);

CREATE INDEX idx_sessoes_usuario ON sessoes (usuario_id);
CREATE INDEX idx_sessoes_validas ON sessoes (expira_em) WHERE revogado_em IS NULL;

-- +goose Down
DROP TABLE sessoes;
```

O identificador da sessão vai no cookie; `tenant_id` fica **na sessão**, nunca no
corpo da requisição. `revogado_em` é o que faz logout significar alguma coisa —
exatamente o que um JWT sem estado não entrega.

## 011 — convites (RF12)

```sql
-- +goose Up
CREATE TABLE convites (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id  uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  email      text NOT NULL,
  papel      text NOT NULL,
  token      uuid NOT NULL DEFAULT gen_random_uuid(),
  expira_em  timestamptz NOT NULL DEFAULT now() + interval '7 days',
  aceito_em  timestamptz,
  criado_por uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT,
  criado_em  timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT convite_papel_valido CHECK (papel IN ('owner','prestador','atendente')),
  CONSTRAINT convite_token_unico  UNIQUE (token)
);

CREATE INDEX idx_convites_tenant ON convites (tenant_id);

-- +goose Down
DROP TABLE convites;
```

Mesmo padrão do `vinculo_token`: token único, com prazo e de uso único. RF12 é
prioridade média — a migration pode esperar a F2, mas a tabela precisa existir no
desenho desde já para ninguém inventar convite por e-mail sem registro.

## Usuário de aplicação (`app_user`)

```sql
CREATE ROLE app_user LOGIN PASSWORD '...';
GRANT CONNECT ON DATABASE smartbooking TO app_user;
GRANT USAGE   ON SCHEMA public, app TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
```

`app_user` **não** é dono de nenhuma tabela, **não** tem `BYPASSRLS` e **não** é
superusuário. Migrations rodam com outro usuário. Sem essa separação, o RLS é
enfeite.

O `ALTER DEFAULT PRIVILEGES` evita ter que lembrar de dar `GRANT` toda vez que uma
tabela nova for criada.

## Seed e teste de isolamento

O seed cria **dois tenants** desde o primeiro dia, com dados reconhecíveis. Um
tenant só não testa isolamento — é como testar autenticação com um único usuário.

```sql
INSERT INTO tenants (id, nome, slug) VALUES
  ('11111111-1111-1111-1111-111111111111', 'Barbearia Alfa', 'alfa'),
  ('22222222-2222-2222-2222-222222222222', 'Clinica Beta',   'beta');
```

UUIDs fixos e legíveis no seed facilitam depuração. Em produção nunca, no seed
sempre.

**Dados fictícios por padrão.** Nada de telefone ou `chat_id` real no `seed.sql`,
mesmo sendo projeto de estudo. O arquivo vai para o repositório e fica lá para
sempre.

### O teste que prova que o RLS funciona

```sql
BEGIN;
SELECT set_config('app.tenant_id', '11111111-1111-1111-1111-111111111111', true);

-- deve retornar SOMENTE clientes da Barbearia Alfa
SELECT count(*) FROM clientes;

-- deve retornar 0, mesmo com WHERE explícito no outro tenant
SELECT count(*) FROM clientes
WHERE tenant_id = '22222222-2222-2222-2222-222222222222';

-- deve FALHAR (violação de WITH CHECK)
INSERT INTO clientes (tenant_id, nome)
VALUES ('22222222-2222-2222-2222-222222222222', 'invasor');
ROLLBACK;
```

Rode conectado como `app_user`. Se o segundo `SELECT` retornar linha ou o `INSERT`
passar, o RLS não está ativo — provavelmente por uma das três armadilhas.

Transforme isso em teste Go e coloque no CI. **Isso exige Postgres no CI**
(`services: postgres` no workflow): o job atual roda só `go build/vet/test` e não
tem banco. Teste de isolamento que só existe como comando manual deixa de ser
executado na terceira semana.

## Checklist de conclusão da Fase 1

- [ ] Todas as migrations rodam do zero em banco vazio (`goose up`)
- [ ] Todas as migrations revertem (`goose down-to 0`) e rodam de novo
- [ ] Inserir dois agendamentos sobrepostos retorna erro `23P01`
- [ ] Inserir agendamentos consecutivos (9–10h e 10–11h) **funciona**
- [ ] Agendamento cancelado libera o horário
- [ ] FK composta recusa cliente de outro tenant
- [ ] Teste de isolamento passa conectado como `app_user`
- [ ] `app_user` não é dono de tabela nem tem `BYPASSRLS`
- [ ] Seed cria dois tenants com dados fictícios
- [ ] `DELETE FROM tenants` roda sem erro de chave estrangeira *(novo)*
- [ ] Cadastrar `Davi@x.com` e `davi@x.com` falha na segunda vez *(novo)*
- [ ] Toda FK usada em filtro tem índice explícito *(novo)*

O quarto item é o mais esquecido: quase toda equipe escreve a constraint de
sobreposição e só testa o caso que deve falhar. Se `'[)'` virar `'[]'` por
descuido, o sistema fica impossível de usar e ninguém descobre até a demonstração.

O décimo é novo e vale explicar: `tenants` cascateia para `clientes`, mas
`agendamentos` referencia `clientes` com `RESTRICT`. Dependendo da ordem em que o
Postgres processa, a exclusão do tenant pode falhar. Descubram isso num teste, não
na véspera da entrega.
