# Kanban — Smart Booking

Estado de cada card do projeto, em texto. Serve para dois públicos: a equipe, que
quer ver a sprint inteira sem abrir o navegador, e o **Claude Code**, que precisa
saber em que card o time está antes de escrever qualquer linha.

**Atualizado em:** 17/09/2026
**Sprint atual:** Sprint 0 — Fundação
**Board:** GitHub Projects › Smart Booking DevOps

> A fonte da verdade do *status* é o GitHub Projects. Este arquivo é o espelho
> legível dele. Quem move um card no board atualiza este arquivo **no mesmo PR**
> que fecha a issue — se os dois discordarem, vale o board.

---

## Onde estamos agora

| | |
|---|---|
| Em andamento | #7 `GitHub Actions` (falta tornar o check obrigatório na proteção da `main`) |
| Em revisão | — |
| Próximos a puxar (Ready) | #25 |
| Fecha a Sprint 0 | #25 `Ambiente validado nas três máquinas` |
| Atenção | #26 (Postgres no CI) precisa entrar antes de #16 poder ser verificada |

### Para o Claude Code

1. **Não comece um card que não esteja em `Ready` ou `Em andamento`.** Card em
   `Backlog` ainda não tem corpo revisado — programar a partir dele é adivinhar.
2. O corpo da issue no GitHub é o contrato. Este arquivo tem só o título; os
   critérios de aceite estão lá.
3. Antes de escrever migration, leia `docs/guia-banco-de-dados.md`. Antes de
   mexer em requisito, `docs/requisitos.md`. O backlog completo está em
   `docs/backlog.md`.
4. Um PR por card, com `Closes #N` na descrição, e a caixa deste arquivo marcada
   no mesmo PR.
5. Definição de pronto (vale para marcar `[x]` aqui): mergeado na `main`, PR
   aprovado por outro integrante, CI verde, critérios de aceite verificados e —
   se mexeu no banco — migration versionada.

**Legenda de status:** `backlog` · `ready` · `wip` (em andamento) · `review` (em
revisão) · `feito`. A caixa `[x]` só é marcada quando o card chega em **feito**.

---

## Sprint 0 — Fundação

Meta: as três máquinas rodam o projeto, com CI verde e o primeiro PR mergeado.

- [x] #3 — [F0] Inicializar módulo Go e estrutura de pastas · `feito`
- [x] #4 — [F0] Branch protection na main (1 aprovação obrigatória) · `feito`
- [x] #5 — [F0] docker-compose com postgres:16-alpine · `feito`
- [x] #6 — [F0] .env.example e carregamento de config na API · `feito` · carregamento de config adiado para a F2 (junto do #19)
- [ ] #7 — [F0] GitHub Actions: go build, go vet, go test · `wip`
- [x] #8 — [F0] Padrão de commit, template de PR e template de issue · `feito`
- [ ] #9 — [F1] goose e migration 001 (extensões e app.current_tenant_id) · `backlog`
- [x] #23 — [F0] Trazer requisitos, backlog e guia de BD para docs/ · `feito`
- [x] #24 — [F0] Registrar as decisões em aberto com prazo em docs/decisoes.md · `feito`
- [ ] #25 — [F0] Ambiente validado nas três máquinas · `ready`

> #3: a estrutura agora é `cmd/api`. Os pacotes
> `internal/api` e `internal/storage` (decididos na revisão de arquitetura, no
> lugar de `internal/http` e `internal/db`) nascem nos cards que os usam.
>
> #4 está `feito`, mas houve push direto na `main` (`181b3d6`). Confira em
> Settings › Branches se "Do not allow bypassing the above settings" está ligado.
>
> Não há mais branch `dev`: branch sai da `main` e volta por PR. Padrões em
> `CONTRIBUTING.md`.

## Sprint 1 — Schema e isolamento

Meta: um `SELECT` sem `WHERE`, com o tenant definido, devolve só as linhas
daquele tenant — provado por teste no CI.

- [ ] #10 — [F1] Migration: tenants · `backlog`
- [ ] #11 — [F1] Migration: usuarios e memberships · `backlog`
- [ ] #12 — [F1] Migration: clientes · `backlog`
- [ ] #13 — [F1] Migration: servicos e disponibilidades · `backlog`
- [ ] #14 — [F1] Migration: agendamentos + constraint de sobreposição + trigger · `backlog`
- [ ] #15 — [F1] Migration: notificacoes (outbox) · `backlog`
- [ ] #16 — [F1] Migration: RLS com FORCE, USING e WITH CHECK · `backlog` · **caminho crítico**
- [ ] #17 — [F1] Script de init: app_user sem BYPASSRLS e grants · `backlog`
- [ ] #18 — [F1] Seed com dois tenants fictícios e UUIDs fixos · `backlog`
- [ ] #26 — [F1] Subir Postgres no CI para os testes de banco · `backlog`
- [ ] #27 — [F1] Migration: sessoes · `backlog`
- [ ] #28 — [F1] Teste: exclusão de tenant não quebra em chave estrangeira · `backlog`

**Ordem sugerida:** #9 → #10 → #11 → #12 → #13 → #14 → #15 → #17 → #16 → #18,
com #26 em paralelo assim que #9 fechar (sem ele, #16 não tem onde ser provada).
#27 e #28 podem entrar a qualquer momento depois de #11 e #15.

> #16 é a issue mais importante do backlog inteiro. **Não atribua a quem estiver
> aprendendo Postgres no projeto.**

## Sprint 2 — Primeira fatia

- [ ] #19 — [F2] Config com TENANT_FIXO e PRESTADOR_FIXO (temporário) · `backlog`

Os demais cards da F2 (signup, login, sessão, middleware de tenant, autorização
por papel, rate limit, teste de isolamento ponta a ponta) estão em
`docs/backlog.md` §FASE 2 e viram issues quando a Sprint 1 fechar.

## Backlog futuro (sem sprint)

- [ ] #29 — [F5] Autenticar o webhook do Telegram com secret_token · `backlog`

Fases 3 a 8 inteiras em `docs/backlog.md`. Nada delas vira card antes da F2
começar — card criado cedo demais é card que envelhece errado.

---

## Como atualizar este arquivo

Ao mover um card no board, mude o sufixo de status na linha correspondente. Ao
mergear, troque `- [ ]` por `- [x]`. Quando uma sprint fecha, mova a seção para
o final do arquivo sob `## Concluídas` em vez de apagar — o histórico de o que
foi feito em quanto tempo é o que permite estimar a sprint seguinte.

Cards novos entram com o número da issue, o título exato do GitHub e o status
`backlog`.
