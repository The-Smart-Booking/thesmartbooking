# Smart Booking — regras para o Claude Code

1. **Card antes de código.** Só trabalhe em card que esteja em **Ready** ou
   **In progress** no board (GitHub Projects › Smart Booking DevOps). Item do
   backlog sem card não se começa. Lista dos cards em `docs/cards.md`; status,
   só no board.
2. **O corpo da issue é o contrato**: critérios de aceite e dependências estão lá.
   Se o card depende de decisão aberta em `docs/decisoes.md`, pare e pergunte.
3. **Banco:** leia `docs/guia-banco-de-dados.md` antes de migration ou acesso a
   dados. Sem ORM: SQL em `db/queries/`, Go gerado pelo sqlc. Tenant só por
   `set_config('app.tenant_id', $1, true)` dentro de transação, nunca `SET`.
4. **API:** erros no formato de `docs/erros-api.md`, pelos helpers de
   `internal/api/erros.go`.
5. **Git:** `CONTRIBUTING.md`. Branch a partir da `main`, um PR por card com
   `Closes #N`, nada de push direto na `main`.

Plano em `docs/backlog.md`; requisitos e decisões tomadas em `docs/requisitos.md`.
