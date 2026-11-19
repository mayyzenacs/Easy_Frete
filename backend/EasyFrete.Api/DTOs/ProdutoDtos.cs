namespace EasyFrete.Api.DTOs;

public sealed record ProdutoResponse(
    string Sku,
    string Nome,
    decimal PesoRealKg,
    decimal ComprimentoCm,
    decimal LarguraCm,
    decimal AlturaCm,
    DateTime AtualizadoEm,
    string? LinkDrive);

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
