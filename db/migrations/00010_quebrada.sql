-- +goose Up
CREATE TABELA quebrada (id uuid);

-- +goose Down
DROP TABLE IF EXISTS quebrada;
