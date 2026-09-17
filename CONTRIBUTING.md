# Como contribuir — Smart Booking

Regras de trabalho do time. Valem para os três integrantes e para o Claude Code.

---

## Fluxo de um card, do início ao fim

1. Pegue um card em **Ready** no GitHub Projects, atribua a você e mova para **In progress**.
2. Crie a branch a partir da `main` atualizada:

   ```bash
   git switch main
   git pull
   git switch -c feat/f1-migration-clientes
   ```

3. Faça commits pequenos, no padrão abaixo.
4. Suba a branch e abra o PR **para a `main`**:

   ```bash
   git push -u origin feat/f1-migration-clientes
   ```

5. Mova o card para **In review** e peça revisão a outro integrante.
6. Com aprovação e CI verde, faça **squash merge** e apague a branch.
7. Mova o card para **Done** (o `Closes #N` fecha a issue sozinho).

> Não existe branch `dev`. Todo trabalho sai da `main` e volta para a `main` por PR.
> Ninguém faz push direto na `main`.

---

## Commits

```
<tipo> - <descrição breve>
<tipo>(<escopo>) - <descrição breve>
```

- Descrição em minúsculas, no imperativo ou curta e direta, sem ponto final.
- Escopo é opcional: use quando ajuda a localizar (`web`, `api`, `db`, `ci`).
- Um commit = uma mudança. "ajustes" não é descrição.

| Tipo | Quando usar |
|---|---|
| `feat` | funcionalidade nova |
| `fix` | correção de bug |
| `docs` | só documentação |
| `test` | só testes |
| `refactor` | muda código sem mudar comportamento |
| `build` | dependências, Docker, versão de Go/Node |
| `ci` | GitHub Actions |
| `chore` | configuração e tarefas que não entram nas outras |

Exemplos:

```
feat - migration de clientes
fix - .env.example sem espaço antes do valor
docs - padrão de commit e branch
build(web) - setup inicial react com vite
ci - roda go test em todo PR
```

---

## Branches

```
<tipo>/f<fase>-<descricao-com-hifens>
```

- `<tipo>`: o mesmo da tabela de commits.
- `<fase>`: número da fase do card (`[F0]` → `f0`, `[F1]` → `f1`).
- Descrição curta, minúsculas, **sem espaços e sem acento** (o Git não aceita espaço).

Exemplos:

```
chore/f0-config-api
docs/f0-padrao-contribuicao
feat/f1-migration-clientes
fix/f2-sessao-expirada
```

---

## Pull requests

- **Um PR por card.**
- Título: o mesmo da issue — `[F1] Migration: clientes`.
- Descrição com `Closes #N` (o template já traz o campo).
- Precisa de **1 aprovação de outro integrante** e **CI verde** para mergear.
- Merge por **squash**; a mensagem final segue o padrão de commit.
- Apague a branch depois do merge.

Quem revisa confere: critérios de aceite da issue, testes, e se o kanban foi atualizado.

---

## Kanban — obrigatório

O board do GitHub Projects (**Smart Booking DevOps**) é a fonte da verdade do status.
`docs/kanban.md` é o espelho em texto dele.

| Momento | No board | No `docs/kanban.md` |
|---|---|---|
| Começou o card | mover para **In progress** | sufixo `wip` |
| Abriu o PR | mover para **In review** | sufixo `review` (no próprio PR) |
| Mergeou | mover para **Done** | `- [x]` e sufixo `feito` (no próprio PR) |

**PR que não atualiza o card não é aprovado.** Se o board e o arquivo discordarem, vale o board.

---

## Definição de pronto

Está em [`docs/backlog.md`](docs/backlog.md#definição-de-pronto). Resumo: mergeado na
`main`, PR aprovado por outro integrante, CI verde, critérios de aceite verificados e,
se mexeu no banco, migration versionada.
