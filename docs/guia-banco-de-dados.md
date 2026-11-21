# Guia de Banco de Dados — Dayspot

Fatia 0 do backlog (itens 0.7 a 0.18) e base de acesso a dados da Fatia 1 (1.2 e
1.3). Complementa `docs/requisitos.md`.
Versão **1.13** · 20/11/2026

## Mudanças desde a v1.12

Cadastro de empresa (#112). Uma migration nova, a primeira com id de data e hora.

- **`tenants.contato_publico`:** coluna `text`, nula, com `CHECK contato_publico_tamanho`
  (até 120 caracteres). É o contato exibido na página pública de agendamento, e o #170
  a reaproveita (§002).
- **Migration `20261120120000_AdicionarContatoPublico` (#112):** entra depois das dez
  primeiras e sem tomar a 011, reservada aos convites (§Ordem das migrations).

## Mudanças desde a v1.11

Decisões do piloto (#180, `requisitos.md` v0.15). Sem migration: o schema não muda.

- **Canal de avisos:** passa a ser o WhatsApp, sem integração existente. As colunas do
  Telegram ficam no schema, sem uso, até uma migration futura (§002, §004 e §008).
- **Convites:** a migration 011 está pendente e fica para depois do piloto (§011).
- **Rotas públicas:** a exceção da resolução por slug está registrada em §Acesso a dados.

## Mudanças desde a v1.10

Agendamento público (#162, `requisitos.md` v0.14). Sem migration: o schema não muda.

- **Rotas públicas:** o slug é resolvido em `tenants` (fora do RLS, com `WHERE` manual)
  antes da transação; dentro dela, o tenant entra por `set_config`, como nas demais rotas.
- **`clientes.telefone`:** nenhum aviso o usa hoje, e ele identifica o cliente no
  agendamento público, com advisory lock por tenant e telefone (§004).

## Mudanças desde a v1.9

Conferência do guia contra o código depois da migração para .NET (#144). Nenhuma
regra muda.

- **Seed:** o comando de execução (`psql "$MIGRATIONS_DATABASE_URL" -f db/seed.sql`)
  está em §Seed e teste de isolamento; `db/` guarda só o seed e o `db/init/app_user.sh`.
- **Rodar e testar localmente:** o fluxo completo (compose, migrations, seed,
  `dotnet test`) e os passos de banco do CI, na ordem do `ci.yml`.

## Mudanças desde a v1.8

Decisão do time de 15/11/2026 (`requisitos.md` v0.12): as migrations saem do goose
e passam para EF Core Migrations. O schema não muda: cada migration tem o mesmo SQL,
na mesma ordem.

- **Migrations:** uma classe por migration em `src/Api/Migrations/`, de
  `00000000000001_CriarExtensoes` a `00000000000010_CriarSessoes`, com o SQL em
  `migrationBuilder.Sql` no `Up` e no `Down` (§Antes de escrever a primeira
  migration). O `AppDbModelSnapshot` reflete o modelo do `AppDb`; o CI confere com
  `dotnet ef migrations has-pending-model-changes`.
- **Blocos de SQL deste guia:** `-- Up` e `-- Down` marcam o SQL de cada método.
  Sem `StatementBegin/End`: o EF manda o texto inteiro ao Postgres.
- **Conexão de dono:** `MIGRATIONS_DATABASE_URL` no lugar de `GOOSE_DBSTRING`, com
  o mesmo papel (migrations e seed). A API continua só com a `DATABASE_URL`.
- **Banco de dev migrado pelo goose** (tem `goose_db_version`, não tem
  `__EFMigrationsHistory`): recriar com `docker compose down -v`. Não há transição.
- As notas de versões anteriores citam o goose e os comandos dele: ficam como
  histórico.

## Mudanças desde a v1.7

Decisão do time de 15/11/2026 (`requisitos.md` v0.11): o backend passa de Go para
.NET e a decisão "pgx v5 + sqlc, sem ORM" é revertida. O schema não muda.

- **Acesso a dados:** EF Core com Npgsql no lugar de pgx + sqlc (§Acesso a dados).
  O EF **só mapeia** o schema; as migrations continuam no goose, em SQL puro. Não há
  EF Migrations, `sqlc.yaml` nem `db/queries/`.
- **Helper de transação:** `AppDb.IniciarComTenant` no lugar do `Store.ComTenant`;
  a regra é a mesma: `set_config('app.tenant_id', ..., true)` dentro da transação.
- **Testes de banco:** em `tests/Api.Tests/BancoTests.cs` (xUnit), contra o Postgres
  com as migrations do goose.
- As notas de versões anteriores abaixo citam arquivos Go (`internal/storage` etc.)
  que não existem mais: ficam como histórico.

## Mudanças desde a v1.6

- **Helper de transação (1.2):** o `fn` do `ComTenant` recebe `pgx.Tx`, não
  `*db.Queries`. Os repositórios de `internal/storage` são construídos sobre o
  `tx`; assim o middleware da 1.3 não importa `internal/storage/db`, e passar o
  pool no lugar da transação não compila.

## Mudanças desde a v1.5

Correções achadas ao escrever e testar as migrations 007, 008 e 009 (cards 0.12 a 0.14).
Nenhuma decisão muda: dois `Down` que não revertiam tudo e um `GRANT` que sumia.

- **007:** o `Down` só apagava a tabela e deixava `app.tocar_atualizado_em()` para trás.
  Consequência, reproduzida: o `Down` da 001 falha (`DROP SCHEMA app`, `2BP01`) e um
  `goose up` depois de desfazer a 007 falha (`function already exists`, `42723`). O `Down`
  agora apaga a função também.
- **009:** `DISABLE ROW LEVEL SECURITY` não desfaz o `FORCE` (são duas flags em
  `pg_class`). O `Down` agora faz `NO FORCE` antes do `DISABLE`, e o bloco traz as
  cinco tabelas por extenso em vez de "repetir o bloco acima".
- **009:** `GRANT USAGE ON SCHEMA app TO app_user` também na migration. O `Down` da
  001 apaga o schema `app` e o `Up` o recria sem o `GRANT` do script da 0.15: depois
  de um `goose down-to 0` (que a 003 manda rodar em banco de dev), o `app_user`
  perdia o acesso a `app.*` (`42501`). Achado pelo teste de isolamento.
- **Teste de isolamento:** está em `internal/storage/isolamento_test.go` e pula sem
  `GOOSE_DBSTRING` e `DATABASE_URL`; como rodar em §Seed e teste de isolamento.

## Mudanças desde a v1.4

Decisão de 03/10/2026 (`requisitos.md` v0.9): nomenclatura padronizada em português.

- Papel `owner` renomeado para `administrador`: o `CHECK` de papel em `memberships` e
  `convites` aceita `administrador` e `prestador` (os nomes das constraints não mudam).

## Mudanças desde a v1.3

Decisões de 02/10/2026 (`requisitos.md` v0.8). Nenhuma migration nova é escrita
agora: o guia descreve o que as 003, 005, 007, 009 e 011 vão conter.

- `CHECK` de papel só com `administrador` e `prestador`, em `memberships` e `convites`.
- `tenants.criado_por` entra por `ALTER TABLE` na 003: a 002 já foi mergeada.
- Dinheiro em centavos (§Dinheiro): `servicos.preco_centavos` sem `DEFAULT` e
  `agendamentos.valor_centavos`, os dois com `CHECK >= 0`.
- Índice `idx_agendamentos_prestador_inicio` para o escopo por prestador.
- `REVOKE DELETE ON agendamentos FROM app_user` na 009: agendamento nunca é apagado.
- `sessoes` e `convites` também ficam fora do RLS: são cinco tabelas com `WHERE` manual.
- Membership removido: o que exige ativo e o que mostra removidos; reconvite reativa.
- Seed na ordem `usuarios` → `tenants` → `memberships`.
- Escopo por prestador e operações por id em §Acesso a dados.
- Checklist da Fatia 0 com papel inválido, valor negativo e `DELETE` em `agendamentos`.

## Mudanças desde a v1.2

- Por que sem ORM, registrado em §Acesso a dados.
- UUID no Go é `google/uuid` (`overrides` no `sqlc.yaml`); `ComTenant` recebe `uuid.UUID`.
- Script do `app_user` roda no compose e no CI.
- goose e sqlc com versão fixa, igual nas três máquinas e no CI.
- Duas conexões no `.env`: `GOOSE_DBSTRING` (dono das tabelas, migrations e seed)
  e `DATABASE_URL` (`app_user`, a API).

## Mudanças desde a v1.1

- Referências a fases trocadas pelas fatias do backlog v2.1. Nenhuma mudança de
  schema nem de acesso a dados: continua pgx v5 + sqlc, sem ORM.
- O helper `ComTenant` é chamado pelo middleware da 1.3 (uma transação por
  requisição).
- Checklist dividido: Fatia 0 (schema e RLS) e Fatia 1 (sqlc e repositório).

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
| 10 | Acesso a dados com pgx v5 + sqlc e helper de transação com tenant | Acesso a dados |

---

## Antes de escrever a primeira migration

**Versão mínima do PostgreSQL: 15.** A 13 já traz `gen_random_uuid()` nativo
(dispensa `pgcrypto`), mas a 15 traz melhorias em RLS que valem. No
`compose.yml`, fixe a versão: `postgres:16-alpine`. Nunca use `latest` — o banco
muda de versão sozinho e a equipe passa uma tarde caçando o motivo.

**Ferramenta de migrations:** EF Core Migrations, com o SQL escrito à mão. O
`dotnet-ef` é ferramenta local, com versão fixa em `.config/dotnet-tools.json` (a
mesma do EF Core), e conecta como dono das tabelas pela `MIGRATIONS_DATABASE_URL`
(ambiente ou `.env`). O que importa mais que a
escolha: **nenhuma alteração de schema fora de migration versionada, nunca**. Um
`ALTER TABLE` rodado à mão no banco de alguém é a origem clássica do "na minha
máquina funciona".

```bash
dotnet tool restore                                        # dotnet-ef na versão fixa
dotnet ef migrations add CriarConvites --project src/Api   # classe nova + snapshot atualizado
dotnet ef database update --project src/Api                # aplica as pendentes
dotnet ef database update 0 --project src/Api              # desfaz todas
```

Cada migration é uma classe em `src/Api/Migrations/`, com o SQL em raw string:

```csharp
[DbContext(typeof(AppDb))]
[Migration("00000000000001_CriarExtensoes")]
public sealed class CriarExtensoes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;
            ...
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ...
            """);
    }
}
```

O `migrations add` gera operações a partir do modelo do `AppDb`, que não conhece
RLS, FK composta, `CHECK` nem constraint de exclusão: troque o corpo gerado do `Up`
e do `Down` pelo SQL e mantenha o snapshot como o EF o gerou (o CI roda
`has-pending-model-changes`). Migration nova ganha id com data e hora
(`20261120093000_Nome`) e entra depois das dez primeiras, numeradas de
`00000000000001` a `00000000000010`. Cada migration roda em transação; SQL que não
pode rodar em uma (`CREATE INDEX CONCURRENTLY`) vai em
`migrationBuilder.Sql(..., suppressTransaction: true)`.

Nos blocos de SQL abaixo, `-- Up` e `-- Down` marcam o que vai em cada método.

**Regra para toda migration:** escreva o `Down` junto com o `Up`, e teste os dois.
Down que não funciona é Down que não existe, e vocês descobrem isso no pior
momento possível.

## Ordem das migrations

A ordem não é estética — é imposta pelas dependências de chave estrangeira:

```
001 extensões + função de contexto
002 tenants
003 usuarios + memberships + tenants.criado_por (ALTER)
004 clientes
005 servicos
006 disponibilidades
007 agendamentos        ← depende de 003, 004, 005
008 notificacoes        ← depende de 007
009 RLS + REVOKE DELETE ← depende de todas
010 sessoes             ← depende de 003 (independe do resto)
011 convites            ← depende de 002 e 003 (pendente: fica para depois do piloto)

20261120120000 contato_publico ← depende de 002 (#112; id com data e hora, fora da sequência numerada)
```

As migrations numeradas aplicadas vão até a 010; depois delas vem a `20261120120000_AdicionarContatoPublico` (`src/Api/Migrations/`).

## Dinheiro: centavos em `integer`

Convenção para toda coluna de valor (hoje `servicos.preco_centavos` e
`agendamentos.valor_centavos`):

- `integer` em **centavos**, com sufixo `_centavos` no nome: R$ 45,90 é `4590`.
- `CHECK (... >= 0)` e sem `DEFAULT`: valor esquecido falha no `INSERT`, não vira zero.
- Nunca `money` (depende de `lc_monetary` e arredonda na conversão) nem `float`
  (`0,1 + 0,2` não dá `0,3`).
- Nem `numeric`: viraria `decimal` no C#, que centavos não pedem. Com `integer`, a
  coluna vira `int`; somas saem com `COALESCE(sum(...), 0)::bigint`, `long` no C#.
- Formatar em reais só na borda (frontend), como o fuso horário.

`integer` vai até 2³¹ − 1 centavos (~R$ 21 milhões) por linha: sobra para um
agendamento.

---

## 001 — Extensões e função de contexto

```sql
-- Up
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS app;

CREATE FUNCTION app.current_tenant_id() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;

-- Down
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

O corpo da função tem `;` dentro, e não precisa de marcação: o `migrationBuilder.Sql`
manda o texto inteiro, e o Npgsql respeita o `$$`.

## 002 — tenants

```sql
-- Up
CREATE TABLE tenants (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  nome               text NOT NULL,
  slug               text NOT NULL UNIQUE,
  fuso_horario       text NOT NULL DEFAULT 'America/Sao_Paulo',
  telegram_bot_token text,          -- legado do Telegram, sempre nulo
  criado_em          timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT slug_formato CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$'),
  CONSTRAINT fuso_horario_iana CHECK (fuso_horario = 'UTC' OR fuso_horario ~ '^[A-Z][A-Za-z_]+(/[A-Za-z0-9_+-]+)+$'),
  CONSTRAINT telegram_bot_token_nulo CHECK (telegram_bot_token IS NULL)
);

-- Down
DROP TABLE tenants;
```

`fuso_horario` como `text`, validado na aplicação contra `pg_timezone_names`.
Guardar offset numérico (`-3`) quebra no horário de verão de qualquer país que o
adote.

`slug` é o identificador legível usado na URL da página pública e das rotas
`/api/publico/{slug}`. O `CHECK` impede maiúscula, espaço ou caractere que quebre URL.

**`criado_por` não está aqui.** A 002 já foi mergeada, e `usuarios` só nasce na
003: a coluna entra por `ALTER TABLE` lá (ver 003).

**`contato_publico` entra depois, por `ALTER TABLE`** (migration `20261120120000_AdicionarContatoPublico`, #112): telefone,
WhatsApp ou e-mail da empresa, dado público exibido na página de agendamento. Até 120
caracteres (`CHECK contato_publico_tamanho`), nulo nas empresas antigas, fictício no seed,
obrigatório no cadastro (`POST /api/signup`). Sem contato, as rotas públicas respondem 404 (#170).

```sql
-- Up
ALTER TABLE tenants ADD COLUMN contato_publico text
  CONSTRAINT contato_publico_tamanho CHECK (char_length(contato_publico) <= 120);

-- Down
ALTER TABLE tenants DROP COLUMN contato_publico;
```

⚠️ **`telegram_bot_token` é legado e fica sempre nulo**, garantido pelo `CHECK
telegram_bot_token_nulo`. `tenants` fica fora do RLS (o login precisa dela antes de
existir contexto de tenant), então nenhum segredo por tenant pode morar nesta tabela.
A coluna sai numa migration futura.

## 003 — usuarios e memberships

```sql
-- Up
CREATE TABLE usuarios (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  email      text NOT NULL,
  senha_hash text NOT NULL,
  nome       text NOT NULL,
  criado_em  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX usuarios_email_unico ON usuarios (lower(email));

-- criador da empresa: a 002 já foi mergeada, então a coluna entra aqui
ALTER TABLE tenants
  ADD COLUMN criado_por uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT;

CREATE TABLE memberships (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  usuario_id  uuid NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  tenant_id   uuid NOT NULL REFERENCES tenants(id)  ON DELETE CASCADE,
  papel       text NOT NULL,
  removido_em timestamptz,
  criado_em   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT papel_valido CHECK (papel IN ('administrador', 'prestador')),
  CONSTRAINT membership_unico UNIQUE (usuario_id, tenant_id)
);

CREATE INDEX idx_memberships_usuario ON memberships (usuario_id);
CREATE INDEX idx_memberships_tenant  ON memberships (tenant_id);

-- Down
DROP TABLE memberships;
ALTER TABLE tenants DROP COLUMN criado_por;
DROP TABLE usuarios;
```

**`usuarios` não tem `tenant_id`.** É deliberado: o mesmo e-mail pode pertencer a
mais de um tenant, e o vínculo mora em `memberships`. Colocar `tenant_id` aqui
obrigaria a mesma pessoa a ter contas e senhas separadas por empresa.

**`tenants.criado_por` entra aqui, por `ALTER TABLE`.** A 002 já foi mergeada (e
migration que já rodou não se edita), e `usuarios` só existe a partir desta. O
`Down` tira a coluna antes de apagar `usuarios`. `ON DELETE RESTRICT`, não
`CASCADE`: com `CASCADE`, apagar a conta do criador apagaria a empresa. Aponta
para `usuarios`, não para `memberships`, para não criar ciclo entre `tenants` e
`memberships`. O signup (2.2) grava, na mesma transação, `usuarios` → `tenants`
(com `criado_por`) → `memberships` (administrador) — sem ciclo, porque `usuarios` não tem
`tenant_id`. Proteger o criador (ninguém o remove; só ele remove outro administrador) é
regra do endpoint de remoção (5.8), com teste, sem trigger. Sem índice: a coluna
não é filtro de consulta.

⚠️ **Banco de dev com linhas em `tenants`:** o `ADD COLUMN ... NOT NULL` falha. Ao
atualizar, rode `dotnet ef database update 0 --project src/Api` (ou `docker compose down -v`)
antes do `dotnet ef database update`.

**Dois papéis: `administrador` e `prestador`.** O administrador gerencia a empresa e também pode
atender: a FK de `agendamentos.prestador_id` aponta para `memberships`, não para o
papel, então um administrador é prestador de agendamento sem mudança no schema. O que cada
papel pode fazer é regra da API (`requisitos.md` §9).

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
da equipe" — só erro de chave estrangeira.

Quem exige membership ativo (`removido_em IS NULL`): a sessão, revalidada a cada
requisição (remover corta o acesso na hora); criar agendamento ou disponibilidade;
e o cálculo de slots (6.3), via `JOIN memberships ... removido_em IS NULL`. Quem
**não** filtra: o seletor de prestador do administrador e o financeiro por prestador, que
mostram os removidos marcados "(removido)" — o administrador ainda conclui os
agendamentos passados de quem saiu. As disponibilidades do removido ficam no banco
(o `CASCADE` da 006 só vale para `DELETE` físico), e a remoção cancela em lote os
agendamentos futuros dele (5.8).

**`papel` como `text` + `CHECK`, não `ENUM`.** Tipo `ENUM` é difícil de alterar:
adicionar valor é fácil, remover ou renomear exige recriar o tipo e reescrever
todas as colunas que o usam. `CHECK` se altera com um `ALTER TABLE`.

## 004 — clientes

```sql
-- Up
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

-- Down
DROP TABLE clientes;
```

`telefone` é `text` e nulo, **sem** validação E.164 no banco: guarda o contato como foi
digitado. Nenhum aviso usa essa coluna hoje; o uso como destino do WhatsApp está pendente
(`requisitos.md` §8.3). A normalização do telefone brasileiro no agendamento público é
da aplicação e está planejada no #169, sem reescrever cadastros antigos.

**Agendamento público (#162).** O telefone identifica o cliente:
`Consultas.ClienteDoTelefone` procura no tenant pelos dígitos,
com `removido_em IS NULL`, e reaproveita o mais antigo. Duas requisições simultâneas
com o mesmo telefone novo não criam dois clientes porque
`pg_advisory_xact_lock(hashtextextended(tenant || ':' || digitos, 0))` as serializa até
o fim da transação.

⚠️ **Não troque o advisory lock por `UNIQUE`.** `telefone` é texto livre e pode ter
duplicados gravados: o índice único falharia na migration ou obrigaria a alterar dados.

**Colunas do Telegram (legado).** O canal de avisos passou a ser o WhatsApp, e nenhuma
integração existe. `telegram_chat_id`, `vinculo_token`, `vinculo_expira_em`,
`vinculado_em`, `notificavel` e as constraints `chat_id_unico_por_tenant`,
`vinculo_token_unico` e `vinculo_coerente` continuam no schema, sem uso, até uma
migration futura. Nenhum código novo deve depender delas. `vinculo_token` segue fora do
modelo do EF: é uma credencial do desenho antigo e nenhuma leitura de `clientes` o traz.

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
-- Up
CREATE TABLE servicos (
  id              uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  nome            text NOT NULL,
  duracao_minutos int  NOT NULL,
  preco_centavos  integer NOT NULL,      -- sem DEFAULT: seed e API sempre informam
  ativo           boolean NOT NULL DEFAULT true,
  criado_em       timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id),
  CONSTRAINT servicos_id_tenant UNIQUE (id, tenant_id),
  CONSTRAINT duracao_positiva CHECK (duracao_minutos > 0 AND duracao_minutos <= 1440),
  CONSTRAINT preco_nao_negativo CHECK (preco_centavos >= 0)
);

CREATE INDEX idx_servicos_tenant ON servicos (tenant_id);

-- Down
DROP TABLE servicos;
```

`ativo` em vez de `DELETE`. Apagar um serviço quebra o histórico de agendamentos
que apontam para ele. A listagem no frontend filtra `ativo = true`.

`preco_centavos` segue §Dinheiro. **Sem `DEFAULT`** de propósito: com `DEFAULT 0`,
serviço cadastrado sem preço ficaria grátis em silêncio e o financeiro sairia
zerado sem ninguém notar. Só o administrador gerencia serviços e preços (6.1).

`agendamentos.fim` é gravado, não derivado de `duracao_minutos`: a duração do
serviço pode mudar, e o agendamento antigo tem que continuar contando a história
do horário que foi realmente reservado. Mesma justificativa para
`agendamentos.valor_centavos`: o preço do serviço muda, e o agendamento guarda o
valor da época (ver 007).

## 006 — disponibilidades

```sql
-- Up
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

-- Down
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
-- Up
CREATE TABLE agendamentos (
  id            uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id     uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  cliente_id    uuid NOT NULL,
  servico_id    uuid NOT NULL,
  prestador_id  uuid NOT NULL,
  inicio        timestamptz NOT NULL,
  fim           timestamptz NOT NULL,
  status        text NOT NULL DEFAULT 'confirmado',
  valor_centavos integer NOT NULL,      -- copiado de servicos.preco_centavos
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
  CONSTRAINT intervalo_valido  CHECK (fim > inicio),
  CONSTRAINT valor_nao_negativo CHECK (valor_centavos >= 0)
);

ALTER TABLE agendamentos ADD CONSTRAINT sem_sobreposicao
EXCLUDE USING gist (
  tenant_id    WITH =,
  prestador_id WITH =,
  tstzrange(inicio, fim, '[)') WITH &&
) WHERE (status <> 'cancelado');

CREATE INDEX idx_agendamentos_tenant_inicio    ON agendamentos (tenant_id, inicio);
CREATE INDEX idx_agendamentos_cliente          ON agendamentos (tenant_id, cliente_id);
CREATE INDEX idx_agendamentos_prestador_inicio ON agendamentos (tenant_id, prestador_id, inicio);

-- Down
DROP TABLE agendamentos;
DROP FUNCTION app.tocar_atualizado_em();   -- a função do trigger, abaixo
```

Esta é a tabela central e concentra os três mecanismos que sustentam o resto.

**As três FKs compostas.** Todas carregam `tenant_id`. Isso torna **impossível no
banco** vincular um cliente do tenant A a um agendamento do tenant B — não é
validação que a aplicação pode esquecer.

**`ON DELETE RESTRICT`, não `CASCADE`.** Apagar um cliente não deve apagar
silenciosamente o histórico dele. O banco recusa a operação destrutiva por padrão;
a aplicação decide o que fazer (e, com `removido_em`, a resposta é exclusão
lógica).

**A constraint de exclusão** é o item mais importante da Fatia 0 depois do RLS.
Duas requisições simultâneas passam pela validação da aplicação e gravam as duas —
só o banco resolve isso.

- `'[)'` deixa explícito que o fim é exclusivo. Sem isso, 9h–10h conflita com
  10h–11h e slots consecutivos ficam impossíveis. É o bug mais comum com
  `tstzrange`, e a v0.3 dos requisitos trazia exatamente essa versão.
- `WHERE (status <> 'cancelado')` permite reagendar para um horário antes ocupado
  por um agendamento cancelado.
- A constraint vale por **prestador**, não por cliente: o mesmo cliente pode, em
  tese, ter dois agendamentos simultâneos com prestadores diferentes. Se isso não
  for desejado, é uma segunda constraint — decidam explicitamente. O administrador que
  atende entra na mesma regra, sem mudança: é um `prestador_id` como outro qualquer.
- Violação chega na API como `PostgresException` com `SQLSTATE 23P01`
  (`exclusion_violation`) e precisa
  virar **HTTP 409**, não 500 — no `POST` (1.5) e no `PATCH` que remarca ou troca
  o serviço (5.1), que dispara a constraint de novo:

```csharp
catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ExclusionViolation)
{
    return Erros.Responder(StatusCodes.Status409Conflict, Erros.ConflitoHorario, ...);
}
```

- A constraint é checada depois de a linha entrar na tabela: duas escritas simultâneas
  no mesmo horário esperam uma pela outra e o Postgres aborta uma com `40P01`
  (`deadlock_detected`), que sairia como 500. Por isso `Consultas.CriarAgendamento` pega
  `pg_advisory_xact_lock(hashtextextended(tenant || ':prestador:' || prestador, 0))` antes
  do `INSERT`: a segunda espera o `COMMIT` da primeira e recebe `23P01`. Quem também pega
  o lock de telefone (§004) pega esse **antes** do de prestador. O `PATCH` que remarca ou
  troca o serviço (5.1) precisa do mesmo lock.

**Ciclo de vida, sem `DELETE`.** O status nasce `confirmado`. Concluir só com
`inicio <= now()`; cancelar a qualquer momento enquanto `confirmado`; `cancelado` e
`concluido` são finais; remarcar e trocar serviço só em `confirmado`. O agendamento
nunca é apagado: a 009 tira o `DELETE` do `app_user`. Como aplicar a regra sem
corrida está em §Acesso a dados (operações por id).

**`valor_centavos`: copiado na criação; recopiado só ao trocar o serviço (novo
`fim` + sobreposição); remarcar mantém.** O `INSERT` copia `servicos.preco_centavos`
na mesma instrução, nunca do payload. Remarcar grava
`fim = novo inicio + (fim − inicio)`, não a `duracao_minutos` atual do serviço.
Trocar o serviço só vale em `confirmado` e para serviço ativo:
`fim = inicio + duracao_minutos` do novo serviço, valor recopiado, e a constraint
de sobreposição roda de novo.

**`idx_agendamentos_prestador_inicio`.** Agenda com escopo por prestador,
calendário e financeiro filtram por `(tenant_id, prestador_id)` e período. O mesmo
índice cobre a FK do prestador.

`atualizado_em` precisa de trigger ou de disciplina no repositório. Trigger é mais
confiável — ninguém esquece:

```sql
CREATE FUNCTION app.tocar_atualizado_em() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.atualizado_em = now();
  RETURN NEW;
END;
$$;

CREATE TRIGGER trg_agendamentos_atualizado_em
BEFORE UPDATE ON agendamentos
FOR EACH ROW EXECUTE FUNCTION app.tocar_atualizado_em();
```

⚠️ **O `Down` apaga a função também (v1.6).** `DROP TABLE` leva o trigger, mas não a
função, que mora no schema `app`. Sobrando a função, o `Down` da 001 não consegue
apagar o schema (`2BP01`) e subir a 007 de novo falha com `42723` (função já existe).

## 008 — notificacoes (outbox)

```sql
-- Up
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

-- Down
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
de índice deixa lento o `CASCADE` quando a empresa é excluída (agendamento não se
apaga de outro jeito).

`ON DELETE CASCADE` aqui, ao contrário de `agendamentos`: notificação sem
agendamento é dado órfão puro.

`proxima_tentativa` guarda o backoff. Sem essa coluna, o worker reprocessa a mesma
notificação falha em todo ciclo e queima o limite de envio do canal.

`telegram_message_id` é legado do Telegram: fica no schema, sem uso, até uma migration
futura.

## 009 — RLS

```sql
-- Up
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
-- o mesmo bloco para servicos, disponibilidades, agendamentos e notificacoes
-- (na migration, as cinco por extenso)

-- agendamento nunca é apagado: só cancelado ou concluído
REVOKE DELETE ON agendamentos FROM app_user;

-- o Down da 001 apaga o schema app e leva o GRANT do script da 0.15 (v1.6)
GRANT USAGE ON SCHEMA app TO app_user;

-- Down
GRANT DELETE ON agendamentos TO app_user;
DROP POLICY isolamento_tenant ON clientes;          -- e nas outras quatro
ALTER TABLE clientes NO FORCE ROW LEVEL SECURITY;   -- e nas outras quatro
ALTER TABLE clientes DISABLE ROW LEVEL SECURITY;    -- e nas outras quatro
```

⚠️ **`DISABLE` não desfaz o `FORCE` (v1.6).** São duas flags (`relrowsecurity` e
`relforcerowsecurity` em `pg_class`). Sem o `NO FORCE`, o `Down` deixa a tabela
marcada como `FORCE` e o banco não volta ao estado da 008.

**`REVOKE DELETE` aqui, não na 007.** O `ALTER DEFAULT PRIVILEGES` do script da
0.15 (§Usuário de aplicação) concede `DELETE` no instante em que a tabela nasce,
então o `REVOKE` precisa vir depois do `CREATE`. Fica na 009, junto das outras
regras de acesso do `app_user`, porque é a 0.14 que já depende da 0.15 (o
`app_user` precisa existir); a 0.12 não depende. Com isso, `DELETE FROM
agendamentos` como `app_user` falha com `42501`. Duas consequências:

- O `app_user` precisa existir antes do `dotnet ef database update`. No compose já existe (o script
  roda no `initdb`); no CI (0.17), rode o script antes das migrations.
- O teste de exclusão de tenant (0.18) não é afetado: roda como dono das tabelas, e
  o `CASCADE` executa com o privilégio do dono.

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

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
await db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)", ct);
```

   `set_config(..., true)` é preferível a `SET LOCAL` porque aceita parâmetro —
   `SET LOCAL app.tenant_id = $1` não funciona, `SET` não parametriza. Contornar
   concatenando string é abrir SQL injection na própria camada de segurança.

3. **`WITH CHECK` ausente.** `USING` filtra o que é lido; `WITH CHECK` valida o
   que é escrito. Só com `USING`, um `INSERT` grava linha com `tenant_id` de outra
   empresa — que depois fica invisível para todo mundo, inclusive para quem
   deveria vê-la.

### Por que `usuarios`, `memberships`, `tenants`, `sessoes` e `convites` ficam de fora

Exceção deliberada, e precisa estar escrita para não parecer esquecimento: essas
tabelas são consultadas **antes** de existir contexto de tenant. No login o
sistema ainda não sabe qual é o tenant — é `memberships` que responde isso. A
sessão é lida pelo cookie justamente para descobrir o tenant, e o aceite de
convite acha o convite pelo token. Aplicar policy nelas cria dependência circular.
`convites` entra nessa lista quando a 011 for criada, depois do piloto.

Duas consequências a carregar:

- São as **únicas** tabelas onde o `WHERE` manual é obrigatório. A rede de
  segurança não cobre essas cinco. Em `convites`: listar e criar com
  `WHERE tenant_id = $1` manual; o lookup por token roda sem tenant, e o aceite
  exige sessão com o mesmo e-mail do convite.
- `tenants` legível por qualquer sessão significa que **segredo por tenant não
  pode morar lá** (ver nota na 002).

O RLS também não separa prestadores da mesma empresa: esse escopo é filtro na
query, sem policy extra (§Acesso a dados).

## 010 — sessoes

> Depende da decisão "cookie de sessão vs JWT" (requisitos, seção 9). Com a
> decisão atual — cookie `HttpOnly` —, esta migration entra na Fatia 2 (item 2.14), antes do login (2.3).

```sql
-- Up
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

-- Down
DROP TABLE sessoes;
```

O identificador da sessão vai no cookie; `tenant_id` fica **na sessão**, nunca no
corpo da requisição. `revogado_em` é o que faz logout significar alguma coisa —
exatamente o que um JWT sem estado não entrega.

## 011 — convites (RF12)

> **Pendente.** Esta migration ainda não existe em `src/Api/Migrations/`: convites ficam
> para depois do piloto. O SQL abaixo é o desenho previsto.

```sql
-- Up
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
  CONSTRAINT convite_papel_valido CHECK (papel IN ('administrador','prestador')),
  CONSTRAINT convite_token_unico  UNIQUE (token)
);

CREATE INDEX idx_convites_tenant ON convites (tenant_id);

-- Down
DROP TABLE convites;
```

Token único, com prazo e de uso único. RF12 é prioridade alta e fica para depois do
piloto (2.17 a 2.20); a remoção de membro, na 5.8. A
tabela está no desenho desde a v1.1 para ninguém inventar convite por e-mail sem
registro.

- **Papel do convite: `administrador` ou `prestador`.** Administrador convida administrador; o criador da
  empresa (`tenants.criado_por`) fica protegido pela regra do endpoint de remoção,
  não pelo banco.
- **Aceite reativa membership removido.** Quem já saiu tem membership com
  `removido_em` preenchido, e o `UNIQUE (usuario_id, tenant_id)` da 003 faz um
  `INSERT` simples falhar. O aceite é
  `INSERT ... ON CONFLICT (usuario_id, tenant_id) DO UPDATE SET removido_em = NULL, papel = EXCLUDED.papel`.
- **Respostas:** convite para e-mail que já tem membership ativo no tenant → 409
  `convite_invalido`; convite expirado ou já aceito → 409 `convite_invalido`; token
  inexistente → 404.
- **Fora do RLS** (ver 009): `WHERE tenant_id` manual para listar e criar; lookup
  por token sem tenant; aceite só com sessão cujo e-mail é o do convite.

## Usuário de aplicação (`app_user`)

```sql
CREATE ROLE app_user LOGIN PASSWORD '...';
GRANT CONNECT ON DATABASE dayspot TO app_user;
GRANT USAGE   ON SCHEMA public, app TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
```

`app_user` **não** é dono de nenhuma tabela, **não** tem `BYPASSRLS` e **não** é
superusuário. Migrations rodam com outro usuário. Sem essa separação, o RLS é
enfeite. No `.env`: a API conecta por `DATABASE_URL` (este usuário); migrations
(`dotnet ef`) e seed, por `MIGRATIONS_DATABASE_URL` (o dono).

O script da 0.15 precisa rodar em dois lugares: no `compose.yml`, via
`docker-entrypoint-initdb.d` (só executa com volume vazio — quem já tem banco
roda `docker compose down -v` uma vez), e no CI da 0.17, via `psql`, porque o
`services:` do GitHub Actions não monta arquivo do repositório. Escreva-o como
script que roda igual nos dois.

O `ALTER DEFAULT PRIVILEGES` evita ter que lembrar de dar `GRANT` toda vez que uma
tabela nova for criada. A exceção é `DELETE` em `agendamentos`, que a 009 revoga.

## Seed e teste de isolamento

O seed cria **dois tenants** desde o primeiro dia, com dados reconhecíveis. Um
tenant só não testa isolamento — é como testar autenticação com um único usuário.

```sql
-- ordem: usuarios → tenants (criado_por é NOT NULL) → memberships
INSERT INTO tenants (id, nome, slug, criado_por) VALUES
  ('11111111-1111-1111-1111-111111111111', 'Barbearia Alfa', 'alfa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
  ('22222222-2222-2222-2222-222222222222', 'Clinica Beta',   'beta', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
```

UUIDs fixos e legíveis no seed facilitam depuração. Em produção nunca, no seed
sempre.

O seed mora em `db/seed.sql` e roda como dono das tabelas, num banco já migrado.
Pode rodar de novo: `ON CONFLICT (id) DO NOTHING` pula o que já existe.

```bash
psql "$MIGRATIONS_DATABASE_URL" -f db/seed.sql
```

Os usuários `aaaa…` e `bbbb…` entram antes, e cada um ganha depois a membership
`administrador` do tenant que criou. No Alfa, o administrador criador também atende (tem
agendamentos como `prestador_id`) e há mais um usuário com papel `prestador`, para
demonstrar o escopo por prestador. Todo serviço tem `preco_centavos`, e todo
agendamento, `valor_centavos` igual ao preço do seu serviço.

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

O teste automatizado é o `IsolamentoTests` de `tests/Api.Tests/BancoTests.cs`
(0.14). Ele grava os
próprios dados como dono (`MIGRATIONS_DATABASE_URL`), com UUIDs fora da faixa do seed, lê como
`app_user` (`DATABASE_URL`) e apaga tudo no fim. Confere também que `DATABASE_URL` não
é superusuário, não tem `BYPASSRLS` e não é dona de tabela (armadilha 1), e que
`DELETE FROM agendamentos` falha com `42501`, e repete a leitura pelo EF Core, que
é por onde a API passa. Sem as duas variáveis, o teste pula (no CI, falha):

```bash
set -a; . ./.env; set +a     # MIGRATIONS_DATABASE_URL e DATABASE_URL
dotnet ef database update --project src/Api
dotnet test --filter-class "*IsolamentoTests"
```

Os testes leem as variáveis só do ambiente, não do `.env`: daí o `set -a`. O
`dotnet ef` lê as duas formas.

### Rodar e testar localmente

Com o `.env` copiado do `.env.example`, do zero:

```bash
docker compose up -d                            # Postgres em localhost:5467; o initdb cria o app_user
dotnet tool restore                             # dotnet-ef na versão fixa
dotnet ef database update --project src/Api     # migrations, como dono
set -a; . ./.env; set +a
psql "$MIGRATIONS_DATABASE_URL" -f db/seed.sql  # os testes de banco leem o seed
dotnet test                                     # todos; os de banco estão em BancoTests.cs e TransacaoTests.cs
```

Os testes de banco gravam e apagam os próprios dados (UUIDs fora da faixa do seed)
ou desfazem por `ROLLBACK`; o seed pode ficar.

### No CI

O job `build-and-test` do `.github/workflows/ci.yml` sobe o Postgres em
`services: postgres` (item 0.17, `postgres:16-alpine`) e roda, nesta ordem:

1. `dotnet ef migrations has-pending-model-changes`: modelo do `AppDb` diferente do
   snapshot = faltou gerar a migration.
2. `sh db/init/app_user.sh`: o `app_user` precisa existir antes da 009.
3. `dotnet ef database update`, depois `database update 0` e `database update` de
   novo: o `Down` de toda migration é testado em todo PR.
4. `psql "$MIGRATIONS_DATABASE_URL" -f db/seed.sql`.
5. `dotnet test`. Com `CI` definida, teste de banco sem `DATABASE_URL` ou
   `MIGRATIONS_DATABASE_URL` falha em vez de pular.

Teste de isolamento que só existe como comando manual deixa de ser executado na
terceira semana.

## Acesso a dados: EF Core + Npgsql

**ORM:** EF Core, com o provider `Npgsql.EntityFrameworkCore.PostgreSQL`. Decisão do
time de 15/11/2026, junto com a troca do backend de Go para .NET; substitui a
"pgx v5 + sqlc, sem ORM" de 25/09/2026 (`requisitos.md`, decisão 10).

**O modelo do EF só mapeia. O schema é o SQL das migrations.** O que protege os
dados deste projeto mora no Postgres: RLS com `FORCE`, `set_config` por transação,
constraint de exclusão, FKs compostas e `FOR UPDATE SKIP LOCKED` na fila. Nada disso
é descrito no modelo do EF, e nada disso pode ser gerado por ele. Por isso:

- **Migration com SQL escrito à mão.** O `dotnet ef migrations add` cria a classe e
  atualiza o snapshot; o corpo gerado dá lugar ao SQL em `migrationBuilder.Sql`
  (§Antes de escrever a primeira migration). No mesmo PR, o ajuste da classe em
  `Modelos.cs`. Nada de `Database.Migrate()` na API nem `EnsureCreated()`: a API
  conecta como `app_user`, e migration roda pelo `dotnet ef`, como dono.
- **Nomes em snake_case** pela convenção `UseSnakeCaseNamingConvention()`
  (`EFCore.NamingConventions`): `TenantId` ↔ `tenant_id`, `DbSet Clientes` ↔
  `clientes`. Sem `[Column]` espalhado.
- **Uma classe por tabela** em `src/Api/Dados/Modelos.cs`, sem navegações: as FKs
  compostas ficam no banco e os joins são explícitos na consulta.
- **`DEFAULT` do banco** que a API precisa ler de volta (id, `criado_em`) é declarado
  com `HasDefaultValueSql` em `AppDb.OnModelCreating`: o `INSERT` não envia a coluna.
- **`vinculo_token` não está no modelo**: é credencial do desenho antigo (§004), e assim
  nenhuma leitura de `clientes` o traz.
- **Leitura sem rastreamento** (`QueryTrackingBehavior.NoTracking`), configurada em
  `AppDb.Opcoes`. Para alterar, prefira `ExecuteUpdateAsync` com o `WHERE` explícito
  a carregar, mudar e salvar.

O teste `ModeloCasaComOSchema` consulta cada tabela pelo EF contra o banco migrado:
coluna com nome ou tipo errado aparece no CI, não em produção.

**Tipos:** `uuid` ↔ `Guid` (nulo: `Guid?`); `timestamptz` ↔ `DateTime` com
`Kind = Utc` (o Npgsql recusa gravar outro `Kind`); `integer` ↔ `int`; `bigint` ↔
`long`; `time` ↔ `TimeOnly`.

As consultas ficam em `src/Api/Dados/Consultas.cs`, como métodos de extensão do
`AppDb`. Toda uma recebe `tenantId` logo depois do contexto. A única exceção é
`BuscarEmpresaPorSlug`, que descobre o tenant das rotas públicas (§O helper de transação
com tenant):

```csharp
public static Task<List<Cliente>> ListarClientes(this AppDb db, Guid tenantId, CancellationToken ct = default) =>
    db.Clientes.Where(c => c.TenantId == tenantId && c.RemovidoEm == null)
        .OrderBy(c => c.Nome)
        .ToListAsync(ct);
```

O `Where(c => c.TenantId == tenantId)` continua lá mesmo com RLS: é a camada (b) de
defesa. O RLS pega o erro que escapar dela, não substitui. Conferir isso na revisão
é olhar um arquivo só.

**SQL à mão quando o LINQ não expressa.** O `INSERT ... SELECT` do agendamento (uma
instrução só, 1.5) e o que vier com `FOR UPDATE` usam `FromSql` com interpolação,
que vira parâmetro, nunca concatenação:

```csharp
db.Agendamentos.FromSql($"""
    INSERT INTO agendamentos (...)
    SELECT s.tenant_id, {n.ClienteId}, ...
    WHERE s.tenant_id = {tenantId} AND s.id = {n.ServicoId} AND s.ativo
    RETURNING *
    """)
```

`FromSqlRaw` e `ExecuteSqlRaw` com texto montado por concatenação são proibidos: é
SQL injection.

### O helper de transação com tenant

Uma consulta pelo `AppDb` fora de transação roda **sem** `set_config`, portanto sem
tenant, portanto zero linhas (RLS falha fechado) ou, pior, bug silencioso nas
tabelas fora do RLS. Por isso toda consulta roda dentro de uma transação aberta por
este helper, com uma exceção: `BuscarEmpresaPorSlug` roda **antes** da transação nas
rotas públicas, porque é ela que descobre o tenant. Lê só `tenants`, que fica fora do
RLS, filtra pelo slug da rota e devolve só `id`, `nome`, `fuso_horario` e `contato_publico`
(empresa sem contato volta nula, como slug inexistente, #170).

```csharp
public async Task<IDbContextTransaction> IniciarComTenant(Guid tenantId, CancellationToken ct = default)
{
    var tx = await Database.BeginTransactionAsync(ct);
    try
    {
        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)", ct);
        return tx;
    }
    catch
    {
        await tx.DisposeAsync();
        throw;
    }
}
```

Quem chama na API é o middleware da 1.3 (`UseTransacaoNasRotas`, em
`src/Api/Transacao.cs`): uma transação por requisição. Nas rotas públicas
`/api/publico/{slug}`, o tenant vem do slug da rota, resolvido pelo `UsePublico`
(`src/Api/Publico.cs`), nunca do corpo nem da query string; nas demais, de `TENANT_FIXO`
(e da sessão a partir da Fatia 2). Os handlers recebem o `AppDb` da
requisição, que já está nessa transação. Resposta 4xx/5xx ou exceção = `ROLLBACK`;
o resto, `COMMIT`, e a resposta só sai depois dele.

Uso direto, fora de uma requisição HTTP (testes, scripts):

```csharp
await using var tx = await db.IniciarComTenant(tenantId, ct);
var clientes = await db.ListarClientes(tenantId, ct);
await tx.CommitAsync(ct); // sem o Commit, o Dispose desfaz
```

Regras:

- **Nenhum handler abre transação nem chama `set_config`.** Quem faz é o middleware.
- **Nunca `SET app.tenant_id`**, nem `set_config(..., false)`: valem para a conexão,
  que volta para o pool. O teste `SemSetTenantForaDoSetConfigLocal` varre o código.
- **`COMMIT` em transação abortada vira `ROLLBACK` sem erro** no Postgres, e o
  Npgsql não avisa. O middleware roda um `SELECT 1` antes do `COMMIT` para a falha
  aparecer como 500, e não como um 201 de algo que não foi gravado.
- **Erros do Postgres** chegam como `PostgresException` (ver `23P01` na 007), dentro
  de `DbUpdateException` quando vêm do `SaveChangesAsync`. "Não encontrado" é o
  `null` do `FirstOrDefaultAsync`.

### Escopo por prestador (2.7) e operações por id

O RLS separa empresas; dentro da empresa, quem separa prestadores é a query, sem
policy extra.

**Listagens, financeiro e `.ics`: filtro na query.**

```csharp
where a.TenantId == tenantId && a.Inicio >= de && a.Inicio < ate
    && (prestadorId == null || a.PrestadorId == prestadorId)
orderby a.Inicio
```

`prestadorId` é `Guid?` — e um `null` por bug devolve a empresa inteira, sem o RLS
como rede (é o mesmo tenant). Por isso o handler nunca monta esse filtro: recebe um
`Escopo` pronto do middleware da 2.7.

```csharp
// Escopo é construído só pelo middleware da 2.7, a partir da sessão.
// PrestadorId — prestador: sempre o da sessão; administrador: o do seletor ou null.
public sealed record Escopo(Guid TenantId, Guid? PrestadorId);
```

- Papel `prestador`: `PrestadorId` é sempre o usuário da sessão; `prestador_id` de
  outro na query string → 403 `sem_permissao`.
- Papel `administrador`: `PrestadorId` vem do seletor (query string) ou fica `null` (a
  empresa toda).
- Vale para agendamentos, slots (6.3), log de notificações (6.8), reenvio (RF13),
  financeiro e `.ics`.

**Período no fuso do tenant.** A semana do calendário e o mês do financeiro contam
pelo `inicio`, no fuso do tenant (RNF01), não em UTC. Os limites saem do C#
(`TimeZoneInfo.FindSystemTimeZoneById(tenant.FusoHorario)`) e entram como
`timestamptz` (`de` e `ate` na consulta acima). O financeiro soma `valor_centavos` dos `concluido`
(§Dinheiro).

**Operações por id (GET, PATCH, cancelar, concluir): trava, compara, atualiza.**
Com o filtro na query, agendamento de outro prestador viraria "não encontrado" e
404 — mas a regra é 403. Por isso a operação busca no tenant, sem filtro de
prestador, e decide no código (o `FOR UPDATE` vai por `FromSql`):

```sql
SELECT id, prestador_id, servico_id, status, inicio, fim
FROM agendamentos
WHERE tenant_id = $1 AND id = $2
FOR UPDATE;
```

1. Sem linha → 404 `nao_encontrado` (outro tenant cai aqui, pelo RLS).
2. `prestador_id` diferente do usuário da sessão e papel diferente de administrador → 403
   `sem_permissao`.
3. Status que não permite a transição → 409 `status_invalido`.
4. Só então o `UPDATE`, na mesma transação.

Concluir usa o relógio do banco, dentro do lock:
`UPDATE agendamentos SET status = 'concluido' WHERE tenant_id = $1 AND id = $2 AND status = 'confirmado' AND inicio <= now()`;
zero linhas atualizadas → 409 `status_invalido` (o horário ainda não começou).

## Checklist de conclusão da Fatia 0

- [ ] Todas as migrations rodam do zero em banco vazio (`dotnet ef database update`)
- [ ] Todas as migrations revertem (`dotnet ef database update 0`) e rodam de novo
- [ ] Inserir dois agendamentos sobrepostos retorna erro `23P01`
- [ ] Inserir agendamentos consecutivos (9–10h e 10–11h) **funciona**
- [ ] Agendamento cancelado libera o horário
- [ ] FK composta recusa cliente de outro tenant
- [ ] Teste de isolamento passa conectado como `app_user`, no CI (0.17)
- [ ] `app_user` não é dono de tabela nem tem `BYPASSRLS`
- [ ] Seed cria dois tenants com dados fictícios
- [ ] `DELETE FROM tenants` roda sem erro de chave estrangeira *(novo)*
- [ ] Cadastrar `Davi@x.com` e `davi@x.com` falha na segunda vez *(novo)*
- [ ] Toda FK usada em filtro tem índice explícito *(novo)*
- [ ] `INSERT` em `memberships` (e em `convites`, quando a 011 existir) com papel que não
      seja `administrador` nem `prestador` falha *(v1.4)*
- [ ] Preço ou valor negativo falha (`servicos.preco_centavos`,
      `agendamentos.valor_centavos`); serviço sem preço falha *(v1.4)*
- [ ] `DELETE FROM agendamentos` como `app_user` falha (`42501`) *(v1.4)*

O quarto item é o mais esquecido: quase toda equipe escreve a constraint de
sobreposição e só testa o caso que deve falhar. Se `'[)'` virar `'[]'` por
descuido, o sistema fica impossível de usar e ninguém descobre até a demonstração.

O décimo é novo e vale explicar: `tenants` cascateia para `clientes`, mas
`agendamentos` referencia `clientes` com `RESTRICT`. Dependendo da ordem em que o
Postgres processa, a exclusão do tenant pode falhar. Descubram isso num teste, não
na véspera da entrega.

## Checklist da Fatia 1 (1.2 e 1.3)

- [ ] O modelo do EF casa com o schema das migrations (`ModeloCasaComOSchema` no CI)
- [ ] Toda consulta de `Consultas.cs` recebe `tenantId` e filtra por ele, menos
      `BuscarEmpresaPorSlug`, que descobre o tenant antes da transação
- [ ] Duas requisições seguidas na mesma conexão do pool, com tenants diferentes,
      não se enxergam
- [ ] Nenhum `SET app.tenant_id` no repositório fora de `set_config(..., true)`
