namespace EasyFrete.Api.Models;

public class Usuario
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string Status { get; set; } = "Pendente";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
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

    // Produtos de R$ 79,00 a R$ 199,00
    public decimal De79a199SemDesconto { get; set; }     // reputação < 97% (sem desconto)
    public decimal De79a199ComDesconto97 { get; set; }   // reputação > 97% (50% já embutido)
    public decimal De79a199Fulfillment { get; set; }     // Fulfillment (>70% já embutido)

    // Produtos acima de R$ 199,00
    public decimal Acima199SemDesconto { get; set; }     // reputação < 97% (sem desconto)
    public decimal Acima199ComDesconto97 { get; set; }   // reputação > 97% (50% já embutido)
    public decimal Acima199Fulfillment { get; set; }     // Fulfillment (>70% já embutido)
}