using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Services;

public interface IMagaluFreightService
{
    Task<FreightCalculationResult> CalcularAsync(CalcularFreteRequest request, CancellationToken ct = default);
}

public sealed class MagaluFreightService : IMagaluFreightService
{
    public const decimal TaxaFixaObrigatoria = 5.00m;
    private const decimal LimiteTabela = 79.00m;
    private const decimal LimiteAcima199 = 199.00m;
    private const decimal DivisorCubagem = 6000m;

    private readonly AppDbContext _db;

    public MagaluFreightService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FreightCalculationResult> CalcularAsync(CalcularFreteRequest request, CancellationToken ct = default)
    {
        if (request.PrecoProduto <= 0m)
        {
            return FreightCalculationResult.Fail(400, "precoProduto deve ser maior que zero.");
        }

        if (request.PesoRealKg < 0m || request.ComprimentoCm < 0m || request.LarguraCm < 0m || request.AlturaCm < 0m)
        {
            return FreightCalculationResult.Fail(400, "Peso e medidas não podem ser negativos.");
        }

        var pesoReal = request.PesoRealKg;
        var comprimento = request.ComprimentoCm;
        var largura = request.LarguraCm;
        var altura = request.AlturaCm;

        var sku = request.Sku?.Trim();
        string? skuResposta = string.IsNullOrEmpty(sku) ? null : sku;
        string? nome = null;
        var origemMedidas = "REQUEST";

        // 1. Busca de medidas no cadastro (case-insensitive via collation NOCASE da coluna Sku)
        var faltaMedida = pesoReal <= 0m || comprimento <= 0m || largura <= 0m || altura <= 0m;
        if (!string.IsNullOrEmpty(sku) && faltaMedida)
        {
            var produto = await _db.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Sku == sku, ct);

            if (produto is null)
            {
                return FreightCalculationResult.Fail(404, $"SKU '{sku}' não encontrado e medidas não informadas.");
            }

            if (pesoReal <= 0m) pesoReal = produto.PesoRealKg;
            if (comprimento <= 0m) comprimento = produto.ComprimentoCm;
            if (largura <= 0m) largura = produto.LarguraCm;
            if (altura <= 0m) altura = produto.AlturaCm;

            skuResposta = produto.Sku;
            nome = produto.Nome;
            origemMedidas = "SKU_CADASTRO";
        }

        // 2. Peso cubado  3. Real vs Cubado
        var pesoCubado = Math.Round((comprimento * largura * altura) / DivisorCubagem, 3);
        var pesoConsiderado = Math.Max(pesoReal, pesoCubado);
        var maiorPesoTipo = pesoCubado > pesoReal ? "CUBADO" : "REAL";

        // 4. Tarifação
        var preco = request.PrecoProduto;
        decimal tarifaBase;
        string? faixaAplicada = null;
        string regra;

        if (preco < LimiteTabela)
        {
            var percentual = request.IsFulfillment ? 0.03m : 0.04m;
            tarifaBase = Math.Round(preco * percentual, 2);
            regra = request.IsFulfillment ? "PERCENTUAL_FULFILLMENT_3" : "PERCENTUAL_COLETA_ENTREGA_4";
        }
        else
        {
            if (pesoConsiderado <= 0m)
            {
                return FreightCalculationResult.Fail(
                    400,
                    "Para preço a partir de R$ 79,00 informe peso/medidas ou um SKU cadastrado.");
            }

            // SQLite não ordena/compara decimal no servidor: a tabela tem 27 linhas, então filtramos em memória.
            var tarifas = await _db.TarifasMagalu.AsNoTracking().ToListAsync(ct);
            if (tarifas.Count == 0)
            {
                return FreightCalculationResult.Fail(500, "Tabela de tarifas Magalu não carregada.");
            }

            var faixa = tarifas
                .OrderBy(t => t.PesoMaxKg)
                .FirstOrDefault(t => pesoConsiderado <= t.PesoMaxKg)
                ?? tarifas.OrderByDescending(t => t.PesoMaxKg).First();

            var acima199 = preco > LimiteAcima199;

            if (request.IsFulfillment)
            {
                tarifaBase = acima199 ? faixa.Acima199Fulfillment : faixa.De79a199Fulfillment;
            }
            else if (request.ReputacaoMaior97)
            {
                tarifaBase = acima199 ? faixa.Acima199ComDesconto97 : faixa.De79a199ComDesconto97;
            }
            else
            {
                tarifaBase = acima199 ? faixa.Acima199SemDesconto : faixa.De79a199SemDesconto;
            }

            var modalidade = request.IsFulfillment
                ? "FULFILLMENT"
                : request.ReputacaoMaior97 ? "REPUTACAO_MAIOR_97" : "SEM_DESCONTO";

            faixaAplicada = faixa.DescricaoFaixa;
            regra = $"TABELA_{modalidade}_{(acima199 ? "ACIMA_199" : "79_A_199")}";
        }

        // 5. Taxa fixa e resultado final (nunca soma o preço do produto)
        var valorFinal = Math.Round(tarifaBase + TaxaFixaObrigatoria, 2);

        var response = new CalcularFreteResponse(
            Sku: skuResposta,
            Nome: nome,
            OrigemMedidas: origemMedidas,
            PrecoProduto: Money(preco),
            IsFulfillment: request.IsFulfillment,
            ReputacaoMaior97: request.ReputacaoMaior97,
            PesoRealKg: Measure(pesoReal),
            ComprimentoCm: Measure(comprimento),
            LarguraCm: Measure(largura),
            AlturaCm: Measure(altura),
            PesoCubadoKg: Measure(pesoCubado),
            PesoConsideradoKg: Measure(pesoConsiderado),
            MaiorPesoTipo: maiorPesoTipo,
            FaixaPesoAplicada: faixaAplicada,
            RegraAplicada: regra,
            TarifaBase: Money(tarifaBase),
            TaxaFixaObrigatoria: Money(TaxaFixaObrigatoria),
            ValorFinalFrete: Money(valorFinal));

        return FreightCalculationResult.Ok(response);
    }

    // Garante no mínimo 2 casas (1.5 -> 1.50) para o JSON sair no formato monetário.
    private static decimal Money(decimal valor) => Math.Round(valor, 2) + 0.00m;

    // Garante no mínimo 1 casa (20 -> 20.0).
    private static decimal Measure(decimal valor) => Math.Round(valor, 3) + 0.0m;
}
