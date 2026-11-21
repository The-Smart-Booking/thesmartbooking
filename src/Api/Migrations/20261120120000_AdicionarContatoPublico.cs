using Dayspot.Api.Dados;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dayspot.Api.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261120120000_AdicionarContatoPublico")]
public sealed class AdicionarContatoPublico : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Dado público (telefone, WhatsApp ou e-mail da empresa): tenants fica fora do RLS
            -- e a página pública (#170) lê esta coluna. Nulo nas empresas antigas e no seed;
            -- o cadastro (#112) a torna obrigatória.
            ALTER TABLE tenants ADD COLUMN contato_publico text
              CONSTRAINT contato_publico_tamanho CHECK (char_length(contato_publico) <= 120);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenants DROP COLUMN contato_publico;
            """);
    }
}
