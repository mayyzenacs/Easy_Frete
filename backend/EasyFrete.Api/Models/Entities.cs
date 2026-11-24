namespace EasyFrete.Api.Models;

public static class UserRoles
{
    public const string Master = "Master";
    public const string Admin = "Admin";
    public const string User = "User";
}

public static class UserStatuses
{
    public const string Pendente = "Pendente";
    public const string Aprovado = "Aprovado";
    public const string Rejeitado = "Rejeitado";
}

public static class DeletionRequestStatuses
{
    public const string Pendente = "Pendente";
    public const string Aprovado = "Aprovado";
    public const string Recusado = "Recusado";
}

public static class NotificationTypes
{
    public const string CadastroPendente = "CadastroPendente";
    public const string SolicitacaoExclusao = "SolicitacaoExclusao";
    public const string ExclusaoAprovada = "ExclusaoAprovada";
    public const string ExclusaoRecusada = "ExclusaoRecusada";
}

public class Usuario
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
    public string Status { get; set; } = UserStatuses.Pendente;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}

public class SolicitacaoExclusao
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? UsuarioAlvoId { get; set; }
    public string UsuarioAlvoNome { get; set; } = string.Empty;
    public string UsuarioAlvoEmail { get; set; } = string.Empty;
    public Guid? SolicitadoPorId { get; set; }
    public string SolicitadoPorNome { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string Status { get; set; } = DeletionRequestStatuses.Pendente;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public Guid? DecididoPorId { get; set; }
    public string? DecididoPorNome { get; set; }
    public string? ObservacaoDecisao { get; set; }
    public DateTime? DecididoEm { get; set; }
}

public class Notificacao
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DestinatarioId { get; set; }
    public Usuario? Destinatario { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public Guid? ReferenciaId { get; set; }
    public bool Lida { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? LidaEm { get; set; }
}

public class TokenRedefinicaoSenha
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadoEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
}

public class ControleEnvioRedefinicao
{
    public string EmailHash { get; set; } = string.Empty;
    public int TentativasNaJanela { get; set; }
    public DateTime PrimeiraTentativaEm { get; set; }
    public DateTime UltimaTentativaEm { get; set; }
    public DateTime? BloqueadoAteEm { get; set; }
}

public class Produto
{
    public string Sku { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public decimal PesoRealKg { get; set; }
    public decimal ComprimentoCm { get; set; }
    public decimal LarguraCm { get; set; }
    public decimal AlturaCm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public string? LinkDrive { get; set; }
}

public class TarifaMagalu
{
    public int Id { get; set; }
    public string DescricaoFaixa { get; set; } = string.Empty;
    public decimal PesoMinKg { get; set; }
    public decimal PesoMaxKg { get; set; }

    // Faixa intermediária de preço
    public decimal De79a199SemDesconto { get; set; }
    public decimal De79a199ComDesconto97 { get; set; }
    public decimal De79a199Fulfillment { get; set; }

    // Faixa superior de preço
    public decimal Acima199SemDesconto { get; set; }
    public decimal Acima199ComDesconto97 { get; set; }
    public decimal Acima199Fulfillment { get; set; }
}

public class ConfiguracaoMagalu
{
    public int Id { get; set; }
    public decimal TaxaFixaObrigatoria { get; set; }
    public decimal LimiteTabela { get; set; }
    public decimal LimiteAcima199 { get; set; }
    public decimal PercentualAbaixoLimiteFulfillment { get; set; }
    public decimal PercentualAbaixoLimiteColeta { get; set; }
    public decimal DivisorCubagem { get; set; }
    public decimal PercentualReputacaoMinima { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
