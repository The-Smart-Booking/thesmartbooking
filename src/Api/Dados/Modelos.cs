namespace Dayspot.Api.Dados;

// Uma classe por tabela de src/Api/Migrations. O schema é o SQL das migrations:
// aqui só o mapeamento, com os nomes em snake_case pela convenção do AppDb.

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string Slug { get; set; } = "";
    public string FusoHorario { get; set; } = "";
    public string? TelegramBotToken { get; set; }
    public DateTime CriadoEm { get; set; }
    public Guid CriadoPor { get; set; }
    public string? ContatoPublico { get; set; }
}

public sealed class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public string Nome { get; set; } = "";
    public DateTime CriadoEm { get; set; }
}

public sealed class Membership
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid TenantId { get; set; }
    public string Papel { get; set; } = "";
    public DateTime? RemovidoEm { get; set; }
    public DateTime CriadoEm { get; set; }
}

// Sem vinculo_token, de propósito: é credencial do Telegram (guia de banco, §004)
// e nenhuma leitura pode trazê-lo. Entra no modelo com a rota de vínculo (Fatia 4).
public sealed class Cliente
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Nome { get; set; } = "";
    public string? Telefone { get; set; }
    public long? TelegramChatId { get; set; }
    public DateTime VinculoExpiraEm { get; set; }
    public DateTime? VinculadoEm { get; set; }
    public bool Notificavel { get; set; } = true;
    public DateTime? RemovidoEm { get; set; }
    public DateTime CriadoEm { get; set; }
}

public sealed class Servico
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Nome { get; set; } = "";
    public int DuracaoMinutos { get; set; }
    public int PrecoCentavos { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}

public sealed class Disponibilidade
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PrestadorId { get; set; }
    public short DiaSemana { get; set; } // 0 = domingo ... 6 = sábado
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }
}

public sealed class Agendamento
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ServicoId { get; set; }
    public Guid PrestadorId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public string Status { get; set; } = "";
    public int ValorCentavos { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

public sealed class Notificacao
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgendamentoId { get; set; }
    public string Tipo { get; set; } = "";
    public int Versao { get; set; }
    public string Status { get; set; } = "";
    public DateTime? AgendarPara { get; set; }
    public int Tentativas { get; set; }
    public DateTime? ProximaTentativa { get; set; }
    public DateTime? EnviadoEm { get; set; }
    public long? TelegramMessageId { get; set; }
    public string? Erro { get; set; }
    public DateTime CriadoEm { get; set; }
}

// Fora do RLS (guia §009): toda consulta em sessoes leva WHERE manual.
public sealed class Sessao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public string? UserAgent { get; set; }
}
