namespace EasyFrete.Api.DTOs;

public sealed record CalcularFreteRequest
{
    public string? Sku { get; init; }
    public decimal PrecoProduto { get; init; }
    public bool IsFulfillment { get; init; }
    public bool ReputacaoMaior97 { get; init; }
    public decimal PesoRealKg { get; init; }
    public decimal ComprimentoCm { get; init; }
    public decimal LarguraCm { get; init; }
    public decimal AlturaCm { get; init; }
}

public sealed record CalcularFreteResponse(
    string? Sku,
    string? Nome,
    string OrigemMedidas,
    decimal PrecoProduto,
    bool IsFulfillment,
    bool ReputacaoMaior97,
    decimal PesoRealKg,
    decimal ComprimentoCm,
    decimal LarguraCm,
    decimal AlturaCm,
    decimal PesoCubadoKg,
    decimal PesoConsideradoKg,
    string MaiorPesoTipo,
    string? FaixaPesoAplicada,
    string RegraAplicada,
    decimal TarifaBase,
    decimal TaxaFixaObrigatoria,
    decimal ValorFinalFrete);

public sealed record FreightCalculationResult(
    bool Success,
    int StatusCode,
    string? Error,
    CalcularFreteResponse? Data)
{
    public static FreightCalculationResult Ok(CalcularFreteResponse data) =>
        new(true, 200, null, data);

    public static FreightCalculationResult Fail(int statusCode, string error) =>
        new(false, statusCode, error, null);
}
