namespace EasyFrete.Api.DTOs;

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

public sealed record ConfiguracaoMagaluSyncDto
{
    public decimal TaxaFixaObrigatoria { get; init; }
    public decimal LimiteTabela { get; init; }
    public decimal LimiteAcima199 { get; init; }
    public decimal PercentualAbaixoLimiteFulfillment { get; init; }
    public decimal PercentualAbaixoLimiteColeta { get; init; }
    public decimal DivisorCubagem { get; init; }
    public decimal PercentualReputacaoMinima { get; init; }
}

public sealed record ConfiguracaoMagaluResponse(
    decimal TaxaFixaObrigatoria,
    decimal LimiteTabela,
    decimal LimiteAcima199,
    decimal PercentualAbaixoLimiteFulfillment,
    decimal PercentualAbaixoLimiteColeta,
    decimal DivisorCubagem,
    decimal PercentualReputacaoMinima,
    DateTime AtualizadoEm);

public sealed record SyncTarifasMagaluRequest
{
    public string? Origem { get; init; }
    public ConfiguracaoMagaluSyncDto? Configuracao { get; init; }
    public List<TarifaMagaluSyncItemDto>? Tarifas { get; init; }
}
