# Kanban — Smart Booking

Estado de cada card do projeto, em texto. Serve para dois públicos: a equipe, que
quer ver a sprint inteira sem abrir o navegador, e o **Claude Code**, que precisa
saber em que card o time está antes de escrever qualquer linha.

**Atualizado em:** 18/09/2026
**Sprint atual:** Sprint 1 — Fim da Fatia 0 + frontend base
**Board:** GitHub Projects › Smart Booking DevOps

> A fonte da verdade do *status* é o GitHub Projects. Este arquivo é o espelho
> legível dele. Quem move um card no board atualiza este arquivo **no mesmo PR**
> que fecha a issue — se os dois discordarem, vale o board.

> **Troca de numeração (18/09/2026):** o backlog passou de fases (`[F1]`) para
> fatias verticais (`[T1]`, `docs/backlog.md` v2.1). As issues antigas #3–#29
> foram apagadas; os cards novos começam em #48. Itens marcados *sem card* ainda
> precisam virar issue no GitHub.

---

## Onde estamos agora

| | |
|---|---|
| Em andamento | — |
| Em revisão | PR #32 (0.7 goose + 001) · PR #33 (0.8 tenants) · PR #61 (1.7 formato de erro) |
| Próximos a puxar | 0.9 usuarios e memberships · 0.17 Postgres no CI · #55 (frontend) |
| Fecha a Sprint 1 | 0.14 `RLS com FORCE, USING e WITH CHECK` + checklist da Fatia 0 no guia |
| Atenção | PRs #32 e #33 citam `Closes #9`/`#10`, que não existem mais — religar aos cards novos antes do merge |

### Para o Claude Code

1. **Não comece um card que não esteja em `Ready` ou `Em andamento`.** Card em
   `Backlog` ainda não tem corpo revisado — programar a partir dele é adivinhar.
   Item *sem card* também não se começa.
2. O corpo da issue no GitHub é o contrato. Este arquivo tem só o título; os
   critérios de aceite estão lá.
3. Antes de escrever migration ou acesso a dados, leia
   `docs/guia-banco-de-dados.md`. Antes de mexer em requisito,
   `docs/requisitos.md`. O backlog completo está em `docs/backlog.md`.
4. Um PR por card, com `Closes #N` na descrição, e a linha deste arquivo
   atualizada no mesmo PR.
5. Definição de pronto (vale para marcar `[x]` aqui): mergeado na `main`, PR
   aprovado por outro integrante, CI verde, critérios de aceite verificados e —
   se mexeu no banco — migration versionada.

**Legenda de status:** `backlog` · `ready` · `wip` (em andamento) · `review` (em
revisão) · `feito`. A caixa `[x]` só é marcada quando o card chega em **feito**.

---

## Sprint 1 — Fim da Fatia 0 + frontend base

Meta: um `SELECT` sem `WHERE`, com o tenant definido, devolve só as linhas
daquele tenant — provado por teste no CI. Em paralelo, o `web/` fica pronto para
receber as telas da Fatia 1.

**Fatia 0 — banco e isolamento**

- [ ] *sem card* — [T0] 0.7 Configurar goose + migration 001 (extensões e app.current_tenant_id) · `review` · PR #32
- [ ] *sem card* — [T0] 0.8 Migration: tenants · `review` · PR #33
- [ ] *sem card* — [T0] 0.9 Migration: usuarios e memberships · `backlog`
- [ ] *sem card* — [T0] 0.10 Migration: clientes · `backlog`
- [ ] *sem card* — [T0] 0.11 Migration: servicos e disponibilidades · `backlog`
- [ ] *sem card* — [T0] 0.12 Migration: agendamentos + constraint de sobreposição + trigger · `backlog`
- [ ] *sem card* — [T0] 0.13 Migration: notificacoes (outbox) com UNIQUE(agendamento_id, tipo, versao) · `backlog`
- [ ] *sem card* — [T0] 0.14 Migration: RLS com FORCE, USING e WITH CHECK em todas as tabelas · `backlog` · **caminho crítico**
- [ ] *sem card* — [T0] 0.15 Script de init: app_user sem BYPASSRLS e grants · `backlog`
- [ ] *sem card* — [T0] 0.16 Seed com dois tenants fictícios e UUIDs fixos · `backlog`
- [ ] *sem card* — [T0] 0.17 Subir Postgres no CI para os testes de banco · `backlog`
- [ ] *sem card* — [T0] 0.18 Teste: exclusão de tenant não quebra em chave estrangeira · `backlog`

**Fatia 1 — frontend base (não depende do banco)**

- [ ] #54 — [T1] 1.7 Padronizar formato de resposta de erro da API · `review` · PR #61
- [ ] #55 — [T1] 1.8 Setup Vite + React + TypeScript + roteamento · `backlog`
- [ ] #56 — [T1] 1.9 Cliente HTTP com tratamento centralizado de erro · `backlog`

**Ordem sugerida:** #32 → #33 → 0.9 → 0.10 → 0.11 → 0.12 → 0.13 → 0.15 → 0.14 →
0.16, com 0.17 em paralelo assim que #32 entrar (sem ele, 0.14 não tem onde ser
provada). 0.18 entra depois da 0.12. #54 → #55 → #56 podem andar desde já.

> 0.14 é a issue mais importante do backlog inteiro. **Não atribua a quem estiver
> aprendendo Postgres no projeto.**

## Sprint 2 — Fatia 1: Agendar

Entrega: abrir a aplicação no navegador, criar um agendamento e vê-lo na lista.

- [ ] #48 — [T1] 1.1 Config com TENANT_FIXO e PRESTADOR_FIXO (temporário, remover na T2) · `backlog`
- [ ] #49 — [T1] 1.2 Camada de repositório: toda função recebe tenantID como 1º parâmetro · `backlog`
- [ ] #50 — [T1] 1.3 Abrir transação por requisição com set_config('app.tenant_id', ...) · `backlog` · **caminho crítico**
- [ ] #51 — [T1] 1.4 CRUD mínimo de clientes (criar e listar) · `backlog`
- [ ] #52 — [T1] 1.5 POST /agendamentos traduzindo SQLSTATE 23P01 para HTTP 409 · `backlog`
- [ ] #53 — [T1] 1.6 GET /agendamentos com filtro por período · `backlog`
- [ ] #57 — [T1] 1.10 Tela de listagem de agendamentos · `backlog`
- [ ] #58 — [T1] 1.11 Formulário de novo agendamento (data e hora digitadas, sem slots) · `backlog`
- [ ] #59 — [T1] 1.12 Estados de carregamento e exibição de erro · `backlog`

Depende da Fatia 0 fechada: #49 e #50 testam contra o seed, no CI.

## Backlog futuro (sem sprint)

Fatias 2 a 7 inteiras em `docs/backlog.md`. Nada delas vira card antes da Fatia 1
começar — card criado cedo demais é card que envelhece errado.

---

## Como atualizar este arquivo

Ao mover um card no board, mude o sufixo de status na linha correspondente. Ao
mergear, troque `- [ ]` por `- [x]`. Quando uma sprint fecha, mova a seção para
o final do arquivo sob `## Concluídas` em vez de apagar — o histórico de o que
foi feito em quanto tempo é o que permite estimar a sprint seguinte.

Cards novos entram com o número da issue, o título exato do GitHub e o status
`backlog`. Item *sem card* ganha o número quando a issue for criada.

---

## Concluídas

### Sprint 0 — Fundação · fechada em 17/09/2026

Meta: as três máquinas rodam o projeto, com CI verde e o primeiro PR mergeado.
Numeração antiga (fases); as issues foram apagadas na troca para fatias.

- [x] #3 — [F0] Inicializar módulo Go e estrutura de pastas · `feito` · hoje item 0.1
- [x] #4 — [F0] Branch protection na main (1 aprovação obrigatória) · `feito` · hoje 0.2
- [x] #5 — [F0] docker-compose com postgres:16-alpine · `feito` · hoje 0.3
- [x] #6 — [F0] .env.example e carregamento de config na API · `feito` · hoje 0.4; carregamento de config foi para a 1.1 (#48)
- [x] #7 — [F0] GitHub Actions: go build, go vet, go test · `feito` · hoje 0.5
- [x] #8 — [F0] Padrão de commit e template de PR · `feito` · hoje 0.6
- [x] #23 — [F0] Trazer requisitos, backlog e guia de BD para docs/ · `feito`
- [x] #24 — [F0] Registrar as decisões em aberto com prazo em docs/decisoes.md · `feito`
- [x] #25 — [F0] Ambiente validado nas três máquinas · `feito`

> #3: a estrutura é `cmd/api`. Os pacotes `internal/api` e `internal/storage`
> (decididos na revisão de arquitetura, no lugar de `internal/http` e
> `internal/db`) nascem nos cards que os usam.
>
> O fechamento da F0 foi para a `main` em push direto, sem PR, porque a proteção
> ainda não estava ativa. Daí em diante vale o `CONTRIBUTING.md`: branch a partir
> da `main` e PR com uma aprovação. A branch `dev` foi apagada.
