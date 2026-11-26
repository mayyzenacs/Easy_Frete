namespace EasyFrete.Api.DTOs;

public sealed record ConfiguracaoAmazonSyncDto
{
    public bool UsaPesoCubado { get; init; }
    public decimal LimitePrecoTabelaPeso { get; init; }
    public decimal LimitePesoTabelaKg { get; init; }
    public string? RegiaoReferenciaDba { get; init; }
    public decimal TaxaFixaObrigatoria { get; init; }
}

public sealed record TarifaAmazonSyncItemDto
{
    public int Id { get; init; }
    public string? Modalidade { get; init; }
    public string? TipoRegra { get; init; }
    public string? DescricaoFaixaPeso { get; init; }
    public decimal PrecoMin { get; init; }
    public decimal PrecoMax { get; init; }
    public decimal PesoMinKg { get; init; }
    public decimal PesoMaxKg { get; init; }
    public decimal Tarifa { get; init; }
    public string? FreteCliente { get; init; }
}

public sealed record SyncTarifasAmazonRequest
{
    public string? Origem { get; init; }
    public ConfiguracaoAmazonSyncDto? Configuracao { get; init; }
    public List<TarifaAmazonSyncItemDto>? Tarifas { get; init; }
}

public sealed record ConfiguracaoAmazonResponse(
    bool UsaPesoCubado,
    decimal LimitePrecoTabelaPeso,
    decimal LimitePesoTabelaKg,
    string RegiaoReferenciaDba,
    decimal TaxaFixaObrigatoria,
    DateTime AtualizadoEm);

public sealed record CalcularFreteAmazonRequest
{
    public string? Sku { get; init; }
    public decimal PrecoProduto { get; init; }
    public decimal PesoRealKg { get; init; }
    public string? Modalidade { get; init; }
}

public sealed record CalcularFreteAmazonResponse(
    string? Sku,
    string? Nome,
    string OrigemMedidas,
    decimal PrecoProduto,
    string Modalidade,
    string TipoRegra,
    string DescricaoFaixaPeso,
    string FreteCliente,
    decimal PesoRealKg,
    decimal ComprimentoCm,
    decimal LarguraCm,
    decimal AlturaCm,
    decimal TarifaBase,
    int KgExcedentes,
    decimal TarifaKgAdicional,
    decimal ValorKgExcedente,
    decimal TaxaFixaObrigatoria,
    decimal ValorFinalFrete);

public sealed record AmazonFreightCalculationResult(
    bool Success,
    int StatusCode,
    string? Error,
    CalcularFreteAmazonResponse? Data)
{
    public static AmazonFreightCalculationResult Ok(CalcularFreteAmazonResponse data) =>
        new(true, 200, null, data);

    public static AmazonFreightCalculationResult Fail(int statusCode, string error) =>
        new(false, statusCode, error, null);
}
