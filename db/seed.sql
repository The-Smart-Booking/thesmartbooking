-- Seed de desenvolvimento (0.16): dois tenants fictícios, Barbearia Alfa e Clinica Beta.
-- Dois de propósito: com um só não dá para testar isolamento.
--
-- Roda como dono das tabelas (GOOSE_DBSTRING, superusuário, ignora o RLS), num
-- banco recém-migrado: psql "$GOOSE_DBSTRING" -f db/seed.sql (README §Seed).
-- Pode rodar de novo: ON CONFLICT (id) DO NOTHING pula o que já existe. O alvo é
-- só o id, de propósito: e-mail repetido ou horário sobreposto continuam falhando.
--
-- Dados fictícios: nenhum telefone nem chat_id (cliente sem Telegram vinculado).
--
-- UUIDs fixos e legíveis: <tenant>-<nº da migration da tabela>-0000-0000-<sequência>.
--   tenants      11111111-… (Alfa)   22222222-… (Beta)
--   usuarios     aaaaaaaa-… admin Alfa · bbbbbbbb-… admin Beta · cccccccc-… prestador Alfa
--   PRESTADOR_FIXO (1.1) = cccccccc-cccc-cccc-cccc-cccccccccccc
--   SERVICO_FIXO   (1.1) = 11111111-0005-0000-0000-000000000001 (Corte, Alfa)
-- O teste de isolamento (0.14) usa f1111111-…, f2222222-…, fa…, fb…: não colide.

\set ON_ERROR_STOP on

BEGIN;

-- Ordem: usuarios → tenants (criado_por é NOT NULL) → memberships.
-- senha_hash: formato Argon2id com sal e hash falsos (base64 de "seed-falso" e
-- "seed-hash-nao-funcional"). Não confere com senha nenhuma: login só depois da 2.1.
INSERT INTO usuarios (id, email, senha_hash, nome) VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'admin@alfa.example',     '$argon2id$v=19$m=65536,t=3,p=4$c2VlZC1mYWxzbw$c2VlZC1oYXNoLW5hby1mdW5jaW9uYWw', 'Alfa Administrador'),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'prestador@alfa.example', '$argon2id$v=19$m=65536,t=3,p=4$c2VlZC1mYWxzbw$c2VlZC1oYXNoLW5hby1mdW5jaW9uYWw', 'Alfa Prestador'),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'admin@beta.example',     '$argon2id$v=19$m=65536,t=3,p=4$c2VlZC1mYWxzbw$c2VlZC1oYXNoLW5hby1mdW5jaW9uYWw', 'Beta Administradora')
ON CONFLICT (id) DO NOTHING;

INSERT INTO tenants (id, nome, slug, criado_por) VALUES
  ('11111111-1111-1111-1111-111111111111', 'Barbearia Alfa', 'alfa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
  ('22222222-2222-2222-2222-222222222222', 'Clinica Beta',   'beta', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb')
ON CONFLICT (id) DO NOTHING;

-- Todo criador é administrador do tenant que criou. No Alfa, o administrador
-- também atende, e o cccc… é só prestador (escopo por prestador, 2.7).
INSERT INTO memberships (id, usuario_id, tenant_id, papel) VALUES
  ('11111111-0003-0000-0000-000000000001', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'administrador'),
  ('11111111-0003-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', '11111111-1111-1111-1111-111111111111', 'prestador'),
  ('22222222-0003-0000-0000-000000000001', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222', 'administrador')
ON CONFLICT (id) DO NOTHING;

INSERT INTO clientes (id, tenant_id, nome) VALUES
  ('11111111-0004-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Andre Cliente Alfa'),
  ('11111111-0004-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'Alice Cliente Alfa'),
  ('11111111-0004-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', 'Artur Cliente Alfa'),
  ('22222222-0004-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'Bianca Cliente Beta'),
  ('22222222-0004-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 'Bruno Cliente Beta')
ON CONFLICT (id) DO NOTHING;

INSERT INTO servicos (id, tenant_id, nome, duracao_minutos, preco_centavos) VALUES
  ('11111111-0005-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Corte',          30,  4500),
  ('11111111-0005-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'Barba',          20,  3000),
  ('11111111-0005-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', 'Corte e barba',  50,  7000),
  ('22222222-0005-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'Consulta',       60, 25000),
  ('22222222-0005-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 'Retorno',        30, 12000)
ON CONFLICT (id) DO NOTHING;

-- Alfa: terça a sábado (2–6); Beta: segunda a sexta (1–5). 0 = domingo.
INSERT INTO disponibilidades (id, tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim) VALUES
  ('11111111-0006-0000-0000-000000000a02', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 2, '09:00', '18:00'),
  ('11111111-0006-0000-0000-000000000a03', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 3, '09:00', '18:00'),
  ('11111111-0006-0000-0000-000000000a04', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 4, '09:00', '18:00'),
  ('11111111-0006-0000-0000-000000000a05', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 5, '09:00', '18:00'),
  ('11111111-0006-0000-0000-000000000a06', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 6, '09:00', '14:00'),
  ('11111111-0006-0000-0000-000000000c02', '11111111-1111-1111-1111-111111111111', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 2, '10:00', '19:00'),
  ('11111111-0006-0000-0000-000000000c03', '11111111-1111-1111-1111-111111111111', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 3, '10:00', '19:00'),
  ('11111111-0006-0000-0000-000000000c04', '11111111-1111-1111-1111-111111111111', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 4, '10:00', '19:00'),
  ('11111111-0006-0000-0000-000000000c05', '11111111-1111-1111-1111-111111111111', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 5, '10:00', '19:00'),
  ('11111111-0006-0000-0000-000000000c06', '11111111-1111-1111-1111-111111111111', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 6, '09:00', '14:00'),
  ('22222222-0006-0000-0000-000000000b01', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 1, '08:00', '17:00'),
  ('22222222-0006-0000-0000-000000000b02', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 2, '08:00', '17:00'),
  ('22222222-0006-0000-0000-000000000b03', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 3, '08:00', '17:00'),
  ('22222222-0006-0000-0000-000000000b04', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 4, '08:00', '17:00'),
  ('22222222-0006-0000-0000-000000000b05', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 5, '08:00', '17:00')
ON CONFLICT (id) DO NOTHING;

-- valor_centavos e fim saem do serviço no próprio INSERT: valor igual ao preço
-- por construção. Data = próximo (futuro) ou último (passado) dia da semana dow,
-- contado a partir de hoje em São Paulo (1 a 7 dias), + hora local do tenant.
-- Assim sempre há agendamentos passados e futuros, e todos caem dentro da
-- disponibilidade do prestador, seja qual for o dia em que o seed roda.
INSERT INTO agendamentos (id, tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, status, valor_centavos)
SELECT a.id::uuid, a.tenant_id::uuid, a.cliente_id::uuid, s.id, a.prestador_id::uuid,
       x.inicio, x.inicio + s.duracao_minutos * interval '1 minute',
       a.status, s.preco_centavos
FROM (VALUES
  -- Alfa: o administrador criador também atende (sáb 10h passado, qua 9h)
  ('11111111-0007-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '11111111-0004-0000-0000-000000000001', '11111111-0005-0000-0000-000000000001', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 6, 'passado', '10:00', 'concluido'),
  ('11111111-0007-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', '11111111-0004-0000-0000-000000000002', '11111111-0005-0000-0000-000000000003', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 3, 'futuro',  '09:00', 'confirmado'),
  -- Alfa: prestador; mesmo dia, logo depois do administrador (outro prestador)
  ('11111111-0007-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', '11111111-0004-0000-0000-000000000003', '11111111-0005-0000-0000-000000000002', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 3, 'futuro',  '10:00', 'confirmado'),
  -- Alfa: cancelado libera o horário, que o seguinte ocupa (qui 14h)
  ('11111111-0007-0000-0000-000000000004', '11111111-1111-1111-1111-111111111111', '11111111-0004-0000-0000-000000000001', '11111111-0005-0000-0000-000000000001', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 4, 'futuro',  '14:00', 'cancelado'),
  ('11111111-0007-0000-0000-000000000005', '11111111-1111-1111-1111-111111111111', '11111111-0004-0000-0000-000000000002', '11111111-0005-0000-0000-000000000001', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 4, 'futuro',  '14:00', 'confirmado'),
  -- Beta: sex 15h passado; '[)' deixa a consulta das 9h e o retorno das 10h (ter) encostarem
  ('22222222-0007-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', '22222222-0004-0000-0000-000000000001', '22222222-0005-0000-0000-000000000001', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 5, 'passado', '15:00', 'concluido'),
  ('22222222-0007-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', '22222222-0004-0000-0000-000000000001', '22222222-0005-0000-0000-000000000001', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 2, 'futuro',  '09:00', 'confirmado'),
  ('22222222-0007-0000-0000-000000000003', '22222222-2222-2222-2222-222222222222', '22222222-0004-0000-0000-000000000002', '22222222-0005-0000-0000-000000000002', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 2, 'futuro',  '10:00', 'confirmado')
) AS a (id, tenant_id, cliente_id, servico_id, prestador_id, dow, quando, hora, status)
CROSS JOIN (SELECT (now() AT TIME ZONE 'America/Sao_Paulo')::date AS base) b
CROSS JOIN LATERAL (SELECT (CASE a.quando
    WHEN 'futuro'  THEN b.base + ((a.dow - extract(dow FROM b.base)::int + 6) % 7) + 1
    WHEN 'passado' THEN b.base - ((extract(dow FROM b.base)::int - a.dow + 6) % 7) - 1
  END + a.hora::time) AT TIME ZONE 'America/Sao_Paulo' AS inicio) x
-- LEFT: serviço errado vira NULL e falha no NOT NULL, em vez de sumir a linha.
LEFT JOIN servicos s ON s.id = a.servico_id::uuid AND s.tenant_id = a.tenant_id::uuid
ON CONFLICT (id) DO NOTHING;

COMMIT;
