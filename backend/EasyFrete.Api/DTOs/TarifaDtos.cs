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

public sealed record SyncTarifasMagaluRequest
{
    public string? Origem { get; init; }
    public List<TarifaMagaluSyncItemDto>? Tarifas { get; init; }
}
