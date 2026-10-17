# Design — Smart Booking

Referência visual do `web/`. A fonte é o canvas **Smart Booking — design system**
(<https://claude.ai/artifact/5wVbMpoFcFtUr87MYhSVQc>), com os boards Sistema (claro e
premium dark), Marca e as telas de referência. Mudou o canvas, muda este arquivo no
mesmo PR que levar a mudança para o código.

Direção **Grade**: calma e precisa, como uma grade de horários. Cada região tem uma
superfície, e a cor aparece só em ação, seleção e exceção.

## Regras

1. **Ponto é horário livre. Pílula é horário reservado** e cresce com a duração.
2. **O normal não leva selo.** Só a exceção ganha cor (confirmado, ativo e vinculado
   não têm selo).
3. **Um nível de cartão.** Dentro dele, separe com espaço e preenchimento. Borda só em
   campo.
4. **Azul em ação e seleção; verde na marca, no sucesso e no hover.** O resto é marinho
   e cinza.
5. A **nav** (menu lateral) é a única superfície de cor sólida.
6. **Top-bar** em `superficie` com linha fina embaixo: branca no claro e um tom acima
   do fundo no escuro, para se destacar dele nos dois temas (o canvas usava a cor da
   nav no escuro; mudou em 17/10/2026). Página atual à esquerda, tema, busca e conta à
   direita.
7. O conteúdo centraliza em até **1040 px**.
8. Escuro sem brilho, sombra ou degradê: as superfícies sobem de tom.
9. **Mobile first.** O CSS parte do celular (390 px) e cresce com `@media (min-width: …)`.
   Toda tela é desenhada e testada primeiro no celular.

## Estrutura

| | Celular (até 1023 px) | Desktop (1024 px ou mais) |
|---|---|---|
| Menu | Gaveta de 304 px (`<dialog>` modal) aberta pelo botão de menu; logo e "Fechar" no topo | Menu lateral fixo de 232 px em `nav`; logo (símbolo 28, nome 18) no topo |
| Top-bar | 56 px: menu, título (16/500), tema, busca e conta | 64 px: título (15/500) à esquerda; tema, busca e conta à direita |
| Ação principal | Botão flutuante de 56 px, raio 16, no canto inferior direito | Botão primário no cabeçalho da página |
| Conteúdo | padding 16 × 20 | padding 24 × 40, centralizado em até 1040 px |

- O **título da página** fica na top-bar, como trilha ("Agenda › Novo agendamento"),
  e não se repete dentro da página.
- O menu mostra só as telas que existem: cada item entra com o card da sua tela.
- Empresa ativa, busca e conta entram com os seus cards (sessão na Fatia 2).
- Menu recolhido (72 px, só ícones): quando o calendário da 7.1 precisar de espaço.
- **Tema:** botão redondo de 36 px (`afundado`) no canto direito da top-bar, com o
  ícone do tema em uso (sol ou lua); o clique alterna entre claro e escuro. Sem
  escolha, segue o sistema (`prefers-color-scheme`). A escolha fica no `localStorage`
  e é aplicada em `<html data-tema>` antes do React montar, para não piscar.

## Tokens de cor

Mesmo nome nos dois temas. No código viram variáveis CSS (`--fundo`, `--marca`...)
com `light-dark(claro, escuro)`: o `color-scheme` do `<html>` escolhe o lado (sistema,
ou o tema escolhido no botão da top-bar).

| Token | Claro | Escuro | Uso |
|---|---|---|---|
| `fundo` | `#F4F5F2` | `#060C17` | fundo do app |
| `superficie` | `#FFFFFF` | `#0B1322` | cartão e folha |
| `afundado` | `#ECEEEA` | `#121C2E` | trilho e botão secundário |
| `linha` | `#E1E4DF` | `#1A2538` | divisória |
| `linha-forte` | `#CDD2CB` | `#26334A` | borda de campo e ponto livre |
| `texto` | `#0B1F3A` | `#E8ECF2` | texto principal (marinho do logo) |
| `texto-2` | `#5B6878` | `#8A96A8` | secundário, rótulo de apoio |
| `texto-3` | `#97A1AD` | `#5A677A` | placeholder, desabilitado |
| `nav` | `#0B1F3A` | `#03070F` | menu lateral, logo branco |
| `marca` | `#0F6E56` | `#0F6E56` | pílula do logo e sucesso (ícone no escuro: `#34A07E`) |
| `sucesso-suave` | `#E3F1EC` | `#0E2E26` | fundo do aviso de sucesso |
| `destaque` | `#1D4F91` | `#5B8BD6` | ação, seleção, "agora" |
| `destaque-texto` | `#1D4F91` | `#8FB1E6` | link (hover em verde: `#0F6E56` / `#34A07E`) |
| `agendado` | `#E8EEF7` | `#1A2B48` | bloco de agendamento (hora em `destaque`; no escuro `#A8C1E6`) |
| `sobre-cor` | `#FFFFFF` | `#060C17` | texto sobre `destaque` ou `marca` |
| `atencao` | `#935A00` | `#E3B062` | ponto e texto de atenção |
| `atencao-suave` | `#FCF1DC` | `#2B2214` | fundo do selo e aviso de atenção |
| `perigo` | `#B3261E` | `#F0908A` | texto e ação destrutiva, erro |
| `perigo-suave` | `#FBEAE8` | `#2E1A1D` | fundo do selo e aviso de erro |

Foco: anel de 3 px em `destaque` com transparência (`rgba(29,79,145,.22)` no claro,
`rgba(91,139,214,.32)` no escuro) e borda do campo em `destaque`.

## Tipografia

**Sora** só no logo, título de tela sem top-bar (entrar) e número de destaque.
**Inter** no resto.

| Papel | Fonte | Tamanho / altura |
|---|---|---|
| Título de página (top-bar) | Inter 500 | 15 / 24 no desktop, 16 / 24 no celular |
| Título de tela sem top-bar | Sora 600 | 24 / 32, espaçamento −2% |
| Número de destaque (dinheiro) | Sora | 28 |
| Título de seção | Inter 600 | 15 / 22 |
| Texto e controles | Inter 400 (500 em controle) | 14 / 20 |
| Rótulo e descrição | Inter 500 | 13 / 18 |
| Metadado e selo | Inter 500 | 12 |

Hora e dinheiro com algarismos tabulares (`font-variant-numeric: tabular-nums`). No
escuro, o peso 300 do logo só a partir de 18 px.

## Forma

- Raio **8** em controle, **12** em cartão, **16** em folha (painel lateral, modal);
  **pílula** (`999px`) em horário e selo. Bloco de agendamento: 10.
- Controles com **36 px** de altura; segmento (Dia/Semana) 30 px dentro de um trilho
  `afundado` com 2 px de folga.

## Componentes

| Componente | Como é |
|---|---|
| Botão primário | fundo `destaque`, texto `sobre-cor`, 500, padding 0 14, ícone opcional à esquerda |
| Botão secundário | fundo `afundado`, texto `texto`, sem borda |
| Botão terciário | transparente, texto `texto-2`; destrutivo em `perigo` |
| Botão desabilitado | fundo `afundado`, texto `texto-3` |
| Campo (`input`, `select`) | `superficie`, borda 1 px `linha-forte`, padding 0 12; rótulo acima, 13/500, 6 px de distância |
| Horário livre | pílula de 32 px em `afundado`; selecionado em `destaque` com `sobre-cor` |
| Agendamento | bloco `agendado`, hora e serviço 12/600 em `destaque`, nome 12 |
| Selo | pílula de 22 px, 12/500, fundo `*-suave` e texto da cor do estado; neutro em `afundado`/`texto-2` |
| Aviso | parágrafo com ícone, padding 10 14, raio 10, fundo `*-suave`, texto da cor (`marca`, `atencao`, `perigo`) |
| Interruptor | 36 × 20, ligado em `destaque`, desligado em `linha-forte` |

### Status do agendamento

| Status | Visual |
|---|---|
| Confirmado | bloco `agendado`, sem selo |
| A concluir | bloco `agendado` + ponto `atencao` no canto; selo "A concluir" |
| Concluído | bloco `afundado`, texto `texto-2`, ícone de check |
| Cancelado | `superficie` com borda tracejada `linha-forte`, texto riscado; **oculto por padrão** ("Mostrar cancelados") |

## Marca

Símbolo: grade 3 × 3 de pontos (quadro de 120, raio 11 nos eixos 22, 60 e 98) com uma
pílula de 60 × 22 no lugar do ponto central e do da direita: uma semana vista de cima,
com um horário reservado. Nome em Sora minúsculo, colado, `smart` 300 + `booking` 700,
espaçamento −3%.

- Pílula sempre em `marca`, em qualquer fundo. Pontos em marinho sobre fundo claro e
  brancos sobre fundo escuro, inclusive no menu lateral e na gaveta (`nav`).
- Favicon (aba do navegador): o ícone de app, isto é, fundo marinho com canto de 23%
  e o símbolo a 58% do quadro, com pontos brancos. Lê em aba clara e escura.
- Tamanhos: 16 (aba), 24 (barra do celular), 28 (menu lateral, nome a 18, 8 px entre os
  dois), 48 (entrar, versão empilhada com nome a 24).
- Não pode: girar a grade, mudar a pílula de linha, pílula em âmbar ou vermelho (são
  cores de estado), degradê, sombra, contorno ou nome esticado.

## Telas de referência

Boards do canvas e o card que realiza cada um.

| Board | Card |
|---|---|
| Agenda da semana, detalhe aberto (claro, escuro, menu recolhido) | 7.1, 7.2, 5.4 |
| Agenda do dia no celular, menu aberto no celular | 7.1 |
| Mensagens do bot no Telegram | Fatia 3 |
| Financeiro do mês (administrador) | 7.5 |
| Prestadores, confirmar remoção | 2.20, 5.8 |
| Serviços e preços | 6.5a |
| Clientes e Telegram | 3.8, 3.9 |
| Disponibilidade da semana | 6.5b |
| Entrar | 2.10 |

**Fatia 1:** a lista de `/agendamentos` (1.10) é provisória, e o calendário da 7.1
a substitui. O formulário de `/agendamentos/novo` (1.11) não tem board: usa os
componentes acima (campos, botão primário, aviso de erro para o 409). A estrutura
(menu, top-bar, gaveta, tema) já vale desde a 1.10, com o menu só com a Agenda.

## Acessibilidade

- Texto com contraste AA (4,5:1). Os pares de texto e estado acima passam, conferidos
  em 17/10/2026. A exceção é `texto-3` (2,6:1 no claro, 3,2:1 no escuro), que fica
  só em placeholder e desabilitado e nunca em texto que precisa ser lido. Cor nova
  precisa ser conferida.
- Estado nunca só por cor: selo tem texto, cancelado é riscado e concluído tem ícone.
- Foco sempre visível (o anel acima); grupos de seleção com `role`/`aria-pressed` ou
  `aria-checked`.
- Ícone sem texto leva `aria-label`.
