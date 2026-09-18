# Backlog

## Organizado por fatias verticais — v2.0

Complementa `sistema-agendamentos-requisitos.md` (v0.3) e `guia-banco-de-dados.md`.

> **Mudança desde a v1.0:** o backlog era fatiado horizontalmente (banco → auth → API → frontend → bot). Agora é fatiado **verticalmente**: cada fatia atravessa todas as camadas e termina em algo demonstrável. As issues são quase as mesmas; mudou o agrupamento, a ordem e o critério de conclusão de cada bloco.

---

## Por que fatia vertical

No arranjo anterior, nada funcionava ponta a ponta até a semana ~15. Se aparecesse uma demonstração na semana 8, o time mostraria migrations e requisições no Postman. Além disso, todos os erros de integração apareceriam juntos, no fim, sem prazo para corrigir.

Fatiando vertical, existe sistema funcionando na semana ~7 e ele cresce em capacidade, não em camadas.

**O que isso custa** — e não é zero:

- **Algumas issues são tocadas duas vezes.** O endpoint de criar agendamento nasce na Fatia 1, ganha autenticação na Fatia 2, ganha cancelamento na Fatia 5 e cálculo de slots na Fatia 6. Isso é retrabalho real: ~2 a 4% de esforço a mais no total.
- **Exige disciplina de Git.** Fatia que vive três semanas em branch longa deixa de ser iteração e vira o mesmo waterfall com nome diferente. Toda fatia termina mergeada na `main`.
- **Tentação de declarar fatia pronta com stub.** A definição de pronto (fim do documento) importa mais aqui do que importava antes.

**O que não muda:** a Fatia 0 continua horizontal, e isso é deliberado. Schema, RLS e constraint de sobreposição precisam estar corretos antes de qualquer fatia vertical — retrofit de `tenant_id` e RLS em schema pronto é caro e é justamente a decisão que o grupo já tomou ao manter multi-tenant.

---

## Como usar

- Cada item vira **uma issue**.
- Campos do GitHub Projects: `Fatia` (0 a 7) e `Tamanho` (P ≈ 2h, M ≈ 5h, G ≈ 10h).
- Título no formato `[T3] Handler do /start: resolve token e grava chat_id`.
- **Crie as issues de uma fatia por vez.** Backlog completo no dia 1 vira paisagem.

---

## FATIA 0 — Fundação · ~56h · sem entrega demonstrável

Único bloco horizontal do projeto. Ao final, o banco está correto e o CI funciona; não há nada para mostrar a um usuário, e tudo bem.

| #    | Issue | Tam. |
|------|-------|------|
| 0.1  | `[T0] Criar repositório, README e estrutura de pastas` | P |
| 0.2  | `[T0] Branch protection na main (1 aprovação obrigatória)` | P |
| 0.3  | `[T0] docker-compose com postgres:16-alpine` | P |
| 0.4  | `[T0] .env.example e carregamento de config na aplicação` | P |
| 0.5  | `[T0] GitHub Actions: go build, go vet, go test` | M |
| 0.6  | `[T0] Padrão de commit e template de PR` | P |
| 0.7  | `[T0] Configurar goose + migration 001 (extensões e app.current_tenant_id)` | P |
| 0.8  | `[T0] Migration: tenants` | M |
| 0.9  | `[T0] Migration: usuarios e memberships` | M |
| 0.10 | `[T0] Migration: clientes` | M |
| 0.11 | `[T0] Migration: servicos e disponibilidades` | M |
| 0.12 | `[T0] Migration: agendamentos + constraint de sobreposição + trigger` | M |
| 0.13 | `[T0] Migration: notificacoes (outbox) com UNIQUE(agendamento_id, tipo, versao)` | M |
| 0.14 | `[T0] Migration: RLS com FORCE, USING e WITH CHECK em todas as tabelas` | G |
| 0.15 | `[T0] Script de init: app_user sem BYPASSRLS e grants` | P |
| 0.16 | `[T0] Seed com dois tenants fictícios e UUIDs fixos` | P |

**Critério de conclusão da fatia:** o checklist final do `guia-banco-de-dados.md` passa inteiro — incluindo o item que quase todo time esquece, que é confirmar que agendamentos consecutivos (9-10h e 10-11h) **funcionam**.

> **0.14 é a issue mais importante do projeto.** Não atribua a quem está aprendendo Postgres agora.

---

## FATIA 1 — Agendar · ~46h · 🎯 primeira demonstração

**Entrega:** abrir a aplicação no navegador, criar um agendamento e vê-lo na lista. Sem login: tenant e prestador vêm de constante no config.

| #    | Issue | Tam. |
|------|-------|------|
| 1.1  | `[T1] Config com TENANT_FIXO e PRESTADOR_FIXO (temporário, remover na T2)` | P |
| 1.2  | `[T1] Camada de repositório: toda função recebe tenantID como 1º parâmetro` | M |
| 1.3  | `[T1] Abrir transação por requisição com set_config('app.tenant_id', ...)` | M |
| 1.4  | `[T1] CRUD mínimo de clientes (criar e listar)` | M |
| 1.5  | `[T1] POST /agendamentos traduzindo SQLSTATE 23P01 para HTTP 409` | M |
| 1.6  | `[T1] GET /agendamentos com filtro por período` | M |
| 1.7  | `[T1] Padronizar formato de resposta de erro da API` | P |
| 1.8  | `[T1] Setup Vite + React + TypeScript + roteamento` | P |
| 1.9  | `[T1] Cliente HTTP com tratamento centralizado de erro` | M |
| 1.10 | `[T1] Tela de listagem de agendamentos` | M |
| 1.11 | `[T1] Formulário de novo agendamento (data e hora digitadas, sem slots)` | M |
| 1.12 | `[T1] Estados de carregamento e exibição de erro` | M |

**Por que sem login já na primeira fatia:** autenticação é ~55h. Esperá-la para ver a primeira tela empurraria a demonstração inicial para a semana ~14. O RLS já está ativo por baixo desde a Fatia 0 — o `tenant_id` só vem de constante em vez de vir da sessão, então não se cria dívida de isolamento, apenas uma linha a remover na Fatia 2.

> **Cuidado:** 1.3 precisa estar certo desde já. Se alguém usar `SET` em vez de `SET LOCAL` / `set_config(..., true)`, o bug fica dormindo até existir concorrência real e vira vazamento entre tenants.

---

## FATIA 2 — Entrar · ~55h · 🎯 multi-tenant real

**Entrega:** dois usuários de tenants diferentes fazem login e cada um vê apenas a própria agenda.

| #    | Issue | Tam. |
|------|-------|------|
| 2.1  | `[T2] Hash de senha com Argon2id` | P |
| 2.2  | `[T2] POST /signup: cria usuário + tenant + membership owner na mesma transação` | M |
| 2.3  | `[T2] POST /login com emissão de sessão` | M |
| 2.4  | `[T2] POST /logout e invalidação` | P |
| 2.5  | `[T2] Middleware de autenticação` | M |
| 2.6  | `[T2] Middleware de tenant: valida membership e injeta app.tenant_id` | G |
| 2.7  | `[T2] Middleware de autorização por papel` | M |
| 2.8  | `[T2] Rate limit no endpoint de login` | P |
| 2.9  | `[T2] GET /me com os tenants do usuário` | P |
| 2.10 | `[T2] Telas de cadastro e login` | M |
| 2.11 | `[T2] Layout autenticado com indicação do tenant ativo` | M |
| 2.12 | `[T2] Remover TENANT_FIXO e PRESTADOR_FIXO do config` | P |
| 2.13 | `[T2] Teste automatizado de isolamento entre tenants em todos os endpoints` | M |

**Decisão travada por esta fatia:** sessão com cookie `HttpOnly` ou JWT. Precisa estar resolvida antes da 2.3.

**Critério de conclusão:** 2.13 passa no CI. Sem esse teste verde, o isolamento é suposição, não garantia — e as fatias seguintes vão construir por cima dele.

---

## FATIA 3 — Avisar · ~66h · 🎯 canal de notificação validado

**Entrega:** criar um agendamento e o cliente receber a confirmação no Telegram. Envio imediato, sem agendador — o que valida o canal inteiro sem depender do worker temporizado.

| #    | Issue | Tam. |
|------|-------|------|
| 3.1  | `[T3] Criar bot no BotFather e documentar token no .env.example` | P |
| 3.2  | `[T3] Camada de acesso à Telegram Bot API em Go` | M |
| 3.3  | `[T3] Modo long polling para desenvolvimento local` | M |
| 3.4  | `[T3] Endpoint de webhook para receber updates` | M |
| 3.5  | `[T3] Gerar vinculo_token e link t.me/Bot?start=<token> ao criar cliente` | M |
| 3.6  | `[T3] Handler do /start: resolve token, grava telegram_chat_id e vinculado_em` | G |
| 3.7  | `[T3] Tratar /start com token inválido, expirado ou já usado` | M |
| 3.8  | `[T3] Exibir link de vinculação na tela do cliente` | P |
| 3.9  | `[T3] Indicador visual de cliente não vinculado / bot bloqueado` | M |
| 3.10 | `[T3] Definir interface Notificador (sem mencionar Telegram)` | P |
| 3.11 | `[T3] Implementar TelegramNotificador sobre a interface` | M |
| 3.12 | `[T3] Estrutura do worker: loop periódico e encerramento gracioso` | M |
| 3.13 | `[T3] Enfileirar notificação de confirmação na transação de criação` | M |
| 3.14 | `[T3] Montar texto das mensagens` | M |

**A fatia mais pesada do projeto.** Se o prazo apertar, ela é candidata a quebrar em duas: vinculação (3.1–3.9) e envio (3.10–3.14). A primeira metade sozinha já é demonstrável — o cliente abre o link e o sistema mostra "vinculado".

> **3.9 não é cosmético.** Sem ele, o prestador acha que o lembrete foi enviado quando o cliente sequer é alcançável. Precisa distinguir "nunca vinculou" de "bloqueou o bot" — são estados diferentes na tabela.

---

## FATIA 4 — Lembrar · ~42h · 🎯 o requisito central

**Entrega:** lembrete chega sozinho X horas antes do agendamento, sem ninguém apertar nada.

| #   | Issue | Tam. |
|-----|-------|------|
| 4.1 | `[T4] Enfileirar lembrete com agendar_para = inicio - antecedencia` | M |
| 4.2 | `[T4] Consumir fila outbox com FOR UPDATE SKIP LOCKED` | G |
| 4.3 | `[T4] Retry com backoff usando proxima_tentativa e tentativas` | M |
| 4.4 | `[T4] Tratar HTTP 429 respeitando retry_after` | M |
| 4.5 | `[T4] Tratar 403 (bot bloqueado) marcando notificavel = false` | M |
| 4.6 | `[T4] Antecedência do lembrete configurável por tenant` | P |
| 4.7 | `[T4] Teste: duas instâncias do worker não enviam a mesma notificação` | G |

**Critério de conclusão:** 4.7 verde. Rodar dois workers em paralelo contra o mesmo banco produz exatamente um envio por agendamento.

**Truque para testar sem esperar horas:** deixe a antecedência configurável (4.6) e coloque em 1 minuto no ambiente de desenvolvimento. Sem isso, cada teste manual custa uma tarde.

---

## FATIA 5 — Cancelar · ~37h · 🎯 ciclo de vida completo

**Entrega:** cancelar e reagendar pelo sistema e pelo botão da mensagem, com as notificações certas em cada caso.

| #   | Issue | Tam. |
|-----|-------|------|
| 5.1 | `[T5] PATCH e DELETE /agendamentos (reagendar e cancelar)` | M |
| 5.2 | `[T5] Enfileirar cancelamento e descartar lembrete pendente na mesma transação` | M |
| 5.3 | `[T5] Reagendamento: descartar lembrete antigo e enfileirar versao + 1` | M |
| 5.4 | `[T5] Ações de reagendar e cancelar no frontend` | M |
| 5.5 | `[T5] Botões inline de Confirmar e Cancelar na mensagem` | M |
| 5.6 | `[T5] Handler de callback_query atualizando o status` | G |
| 5.7 | `[T5] Editar a mensagem após a resposta do cliente` | P |

> **5.2 é a issue que evita o bug mais constrangedor possível:** o cliente recebe "seu agendamento foi cancelado" e, horas depois, "lembrete do seu agendamento".

---

## FATIA 6 — Configurar · ~50h · 🎯 autonomia do prestador

**Entrega:** o prestador cadastra os próprios serviços e horários; o formulário passa a oferecer slots calculados em vez de campo de hora livre.

| #   | Issue | Tam. |
|-----|-------|------|
| 6.1 | `[T6] CRUD de servicos` | M |
| 6.2 | `[T6] CRUD de disponibilidades` | M |
| 6.3 | `[T6] GET /horarios-livres com cálculo de slots` | G |
| 6.4 | `[T6] Fuso horário por tenant e conversão na borda` | M |
| 6.5 | `[T6] Telas de cadastro de serviços e disponibilidades` | G |
| 6.6 | `[T6] Substituir campo de hora livre por seleção de slots` | M |
| 6.7 | `[T6] Comando /meusagendamentos no bot` | M |
| 6.8 | `[T6] Log de mensagens enviadas visível no frontend` | M |

**Fatia mais cortável do projeto.** Até aqui o sistema já agenda, autentica, isola tenants, avisa, lembra e cancela. Serviços e disponibilidades podem viver no seed até o fim do semestre sem prejudicar a demonstração.

> **6.4 é a exceção — não corte.** Fuso errado significa lembrete na hora errada, e isso invalida a Fatia 4 inteira.

---

## FATIA 7 — Entregar · ~32h

| #   | Issue | Tam. |
|-----|-------|------|
| 7.1 | `[T7] Dockerfile multi-stage da API e do worker` | M |
| 7.2 | `[T7] docker-compose de produção` | M |
| 7.3 | `[T7] Executar migrations no deploy` | P |
| 7.4 | `[T7] HTTPS e webhook público do Telegram` | M |
| 7.5 | `[T7] README com instruções de setup local` | M |
| 7.6 | `[T7] Documentação da API` | M |
| 7.7 | `[T7] Roteiro de apresentação e demonstração` | M |

---

## Cronograma

A 15h/semana nominais, com ~70% de eficiência real (~10,5h/semana):

| Fatia        | Esforço  | Nominal     | Realista    |
|--------------|----------|-------------|-------------|
| 0 Fundação   | 56h      | sem. 1–4    | sem. 1–5    |
| 1 Agendar    | 46h      | sem. 5–7    | sem. 6–10   |
| 2 Entrar     | 55h      | sem. 8–11   | sem. 11–15  |
| 3 Avisar     | 66h      | sem. 12–16  | sem. 16–22  |
| 4 Lembrar    | 42h      | sem. 17–19  | sem. 23–26  |
| 5 Cancelar   | 37h      | sem. 20–22  | sem. 27–30  |
| 6 Configurar | 50h      | sem. 23–25  | sem. 31–34  |
| 7 Entregar   | 32h      | sem. 26–27  | sem. 35–37  |
| **Total**    | **384h** | **~26 sem.** | **~37 sem.** |

O total subiu de 380h para 384h: é o custo do retrabalho de fatiar vertical. Em troca, a primeira demonstração sai na semana ~7 em vez da semana ~15.

**Paralelismo:** as Fatias 0 e 1 quase não paralelizam — trabalho serial com três pessoas em cima. A partir da Fatia 3 abrem duas ou três frentes (bot / worker / frontend).

---

## Corte de escopo, em ordem

1. Fatia 6 inteira (deixar serviços e disponibilidades no seed) — exceto 6.4
2. 5.5, 5.6, 5.7 (botões inline; cancelar só pelo sistema)
3. 6.8 (log de mensagens)
4. 5.3 (sem reagendar; só cancelar e criar novo)

**Nunca corte:** 0.12, 0.14, 1.3, 2.6, 2.13, 4.2, 4.7 e 6.4. São as issues que separam um sistema funcional de um que vaza dados entre clientes, manda o mesmo lembrete três vezes ou avisa na hora errada.

---

## Definição de pronto

**Issue:**

- [ ] Mergeada na `main`
- [ ] PR aprovado por outro integrante
- [ ] CI verde
- [ ] Critérios de aceite verificados
- [ ] Alteração de banco só via migration versionada

**Fatia:**

- [ ] Todas as issues fechadas
- [ ] O cenário de entrega funciona ponta a ponta em ambiente limpo (`docker compose up` a partir do zero)
- [ ] Alguém que não escreveu o código conseguiu executar o cenário

O último item é o que impede fatia declarada pronta com stub. Se só quem escreveu consegue rodar, a fatia não acabou.
