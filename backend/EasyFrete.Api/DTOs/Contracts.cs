namespace EasyFrete.Api.DTOs;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(string? Nome, string? Email, string? Password);

public sealed record LoginResponse(Guid Id, string Nome, string Email, string Token, DateTime ExpiresAt);

public sealed record HealthResponse(string Status, DateTime Timestamp);

public sealed record ErrorResponse(string Error);

public sealed record ProdutoResponse(
    string Sku,
    string Nome,
    decimal PesoRealKg,
    decimal ComprimentoCm,
    decimal LarguraCm,
    decimal AlturaCm,
    DateTime AtualizadoEm,
    string? LinkDrive);

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

public sealed record SyncProdutoItem
{
    public string? Sku { get; init; }
    public string? Nome { get; init; }
    public decimal PesoBruto { get; init; }
    public decimal AlturaCm { get; init; }
    public decimal LarguraCm { get; init; }
    public decimal ComprimentoCm { get; init; }
    public string? LinkDrive { get; init; }
}

public sealed record SyncLoteRequest
{
    public string? Origem { get; init; }
    public List<SyncProdutoItem?>? Produtos { get; init; }
}

public sealed record SyncLoteResponse(
    string Origem,
    int Recebidos,
    int Inseridos,
    int Atualizados,
    int Ignorados,
    IReadOnlyList<string> Erros,
    DateTime ProcessadoEm);

public sealed record TarifaMagaluSyncItemDto
{
    public int Id { get; init; }
    public string DescricaoFaixa { get; init; } = string.Empty;
    public decimal PesoMinKg { get; init; }
    public decimal PesoMaxKg { get; init; }
    public decimal De79a199SemDesconto { get; init; }
    public decimal De79a199ComDesconto97 { get; init; }
    public decimal De79a199Fulfillment { get; init; }
    public decimal Acima199SemDesconto { get; init; }
    public decimal Acima199ComDesconto97 { get; init; }
    public decimal Acima199Fulfillment { get; init; }
}

public sealed record SyncTarifasMagaluRequest
{
    public string? Origem { get; init; }
    public List<TarifaMagaluSyncItemDto>? Tarifas { get; init; }
}

