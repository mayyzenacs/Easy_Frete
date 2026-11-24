using System.Globalization;
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
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

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

        var config = await _db.ConfiguracoesMagalu
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, ct);

        if (config is null)
        {
            return FreightCalculationResult.Fail(500, "Configuração de tarifas Magalu não carregada.");
        }

        var pesoReal = request.PesoRealKg;
        var comprimento = request.ComprimentoCm;
        var largura = request.LarguraCm;
        var altura = request.AlturaCm;

        var sku = request.Sku?.Trim();
        string? skuResposta = string.IsNullOrEmpty(sku) ? null : sku;
        string? nome = null;
        var origemMedidas = "REQUEST";
        var preco = request.PrecoProduto;

        // 1. Busca de medidas no cadastro (case-insensitive via collation NOCASE da coluna Sku)
        var faltaMedida = pesoReal <= 0m || comprimento <= 0m || largura <= 0m || altura <= 0m;
        if (!string.IsNullOrEmpty(sku) && faltaMedida)
        {
            var produto = await _db.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Sku == sku, ct);

            if (produto is null)
            {
                if (preco >= config.LimiteTabela)
                {
                    return FreightCalculationResult.Fail(404, $"SKU '{sku}' não encontrado e medidas não informadas.");
                }
            }
            else
            {
                if (pesoReal <= 0m) pesoReal = produto.PesoRealKg;
                if (comprimento <= 0m) comprimento = produto.ComprimentoCm;
                if (largura <= 0m) largura = produto.LarguraCm;
                if (altura <= 0m) altura = produto.AlturaCm;

                skuResposta = produto.Sku;
                nome = produto.Nome;
                origemMedidas = "SKU_CADASTRO";
            }
        }

        // 2. Peso cubado  3. Real vs Cubado
        var pesoCubado = Math.Round((comprimento * largura * altura) / config.DivisorCubagem, 3);
        var pesoConsiderado = Math.Max(pesoReal, pesoCubado);
        var maiorPesoTipo = pesoCubado > pesoReal ? "CUBADO" : "REAL";

        // 4. Tarifação
        decimal tarifaBase;
        string faixaPrecoAplicada;
        string? faixaAplicada = null;
        string modalidadeAplicada;
        string regra;

        if (preco < config.LimiteTabela)
        {
            var percentualConfig = request.IsFulfillment
                ? config.PercentualAbaixoLimiteFulfillment
                : config.PercentualAbaixoLimiteColeta;
            tarifaBase = Math.Round(preco * (percentualConfig / 100m), 2);
            faixaPrecoAplicada = $"Abaixo de {FormatBrl(config.LimiteTabela)}";
            modalidadeAplicada = request.IsFulfillment
                ? $"Fulfillment ({FormatPercent(percentualConfig)}%)"
                : $"Coleta e Entrega ({FormatPercent(percentualConfig)}%)";
            regra = request.IsFulfillment ? "PERCENTUAL_FULFILLMENT" : "PERCENTUAL_COLETA_ENTREGA";
        }
        else
        {
            if (pesoConsiderado <= 0m)
            {
                return FreightCalculationResult.Fail(
                    400,
                    $"Para preço a partir de {FormatBrl(config.LimiteTabela)} informe peso/medidas ou um SKU cadastrado.");
            }

            // SQLite não ordena/compara decimal no servidor: filtramos a tabela em memória.
            var tarifas = await _db.TarifasMagalu.AsNoTracking().ToListAsync(ct);
            if (tarifas.Count == 0)
            {
                return FreightCalculationResult.Fail(500, "Tabela de tarifas Magalu não carregada.");
            }

            var faixa = tarifas
                .OrderBy(t => t.PesoMaxKg)
                .FirstOrDefault(t => pesoConsiderado <= t.PesoMaxKg)
                ?? tarifas.OrderByDescending(t => t.PesoMaxKg).First();

            var acimaLimiteSuperior = preco > config.LimiteAcima199;

            if (request.IsFulfillment)
            {
                tarifaBase = acimaLimiteSuperior ? faixa.Acima199Fulfillment : faixa.De79a199Fulfillment;
            }
            else if (request.ReputacaoMaior97)
            {
                tarifaBase = acimaLimiteSuperior ? faixa.Acima199ComDesconto97 : faixa.De79a199ComDesconto97;
            }
            else
            {
                tarifaBase = acimaLimiteSuperior ? faixa.Acima199SemDesconto : faixa.De79a199SemDesconto;
            }

            var modalidade = request.IsFulfillment
                ? "FULFILLMENT"
                : request.ReputacaoMaior97 ? "REPUTACAO_COM_DESCONTO" : "SEM_DESCONTO";

            faixaPrecoAplicada = acimaLimiteSuperior
                ? $"Acima de {FormatBrl(config.LimiteAcima199)}"
                : $"{FormatBrl(config.LimiteTabela)} – {FormatBrl(config.LimiteAcima199)}";
            faixaAplicada = faixa.DescricaoFaixa;
            modalidadeAplicada = request.IsFulfillment
                ? "Fulfillment"
                : request.ReputacaoMaior97
                    ? $"Reputação > {FormatPercent(config.PercentualReputacaoMinima)}%"
                    : "Sem desconto";
            regra = $"TABELA_{modalidade}_{(acimaLimiteSuperior ? "FAIXA_SUPERIOR" : "FAIXA_INTERMEDIARIA")}";
        }

        // 5. Taxa fixa e resultado final (nunca soma o preço do produto)
        var valorFinal = Math.Round(tarifaBase + config.TaxaFixaObrigatoria, 2);

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
            FaixaPrecoAplicada: faixaPrecoAplicada,
            FaixaPesoAplicada: faixaAplicada,
            ModalidadeAplicada: modalidadeAplicada,
            RegraAplicada: regra,
            TarifaBase: Money(tarifaBase),
            TaxaFixaObrigatoria: Money(config.TaxaFixaObrigatoria),
            ValorFinalFrete: Money(valorFinal));

        return FreightCalculationResult.Ok(response);
    }

    // Garante no mínimo 2 casas (1.5 -> 1.50) para o JSON sair no formato monetário.
    private static decimal Money(decimal valor) => Math.Round(valor, 2) + 0.00m;

    // Garante no mínimo 1 casa (20 -> 20.0).
    private static decimal Measure(decimal valor) => Math.Round(valor, 3) + 0.0m;

    private static string FormatBrl(decimal valor) => string.Create(PtBr, $"R$ {valor:N2}");

    private static string FormatPercent(decimal valor) =>
        valor % 1m == 0m
            ? ((int)valor).ToString(PtBr)
            : valor.ToString("0.##", PtBr);
}
