using System.Globalization;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Services;

public interface IAmazonFreightService
{
    Task<AmazonFreightCalculationResult> CalcularAsync(
        CalcularFreteAmazonRequest request,
        CancellationToken ct = default);
}

public sealed class AmazonFreightService : IAmazonFreightService
{
    private static readonly NumberFormatInfo PtBrNumberFormat = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3]
    };

    private readonly AppDbContext _db;

    public AmazonFreightService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AmazonFreightCalculationResult> CalcularAsync(
        CalcularFreteAmazonRequest request,
        CancellationToken ct = default)
    {
        if (request.PrecoProduto <= 0m)
        {
            return AmazonFreightCalculationResult.Fail(400, "precoProduto deve ser maior que zero.");
        }

        if (request.PesoRealKg < 0m)
        {
            return AmazonFreightCalculationResult.Fail(400, "pesoRealKg não pode ser negativo.");
        }

        var modalidade = request.Modalidade?.Trim().ToUpperInvariant();
        if (modalidade is not (AmazonModalidades.Dba or AmazonModalidades.Fba))
        {
            return AmazonFreightCalculationResult.Fail(400, "modalidade deve ser DBA ou FBA.");
        }

        var config = await _db.ConfiguracoesAmazon
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, ct);

        if (config is null)
        {
            return AmazonFreightCalculationResult.Fail(500, "Configuração de tarifas Amazon não carregada.");
        }

        var preco = request.PrecoProduto;
        var pesoReal = request.PesoRealKg;
        var comprimento = 0m;
        var largura = 0m;
        var altura = 0m;

        var sku = request.Sku?.Trim();
        string? skuResposta = string.IsNullOrEmpty(sku) ? null : sku;
        string? nome = null;
        var origemMedidas = "REQUEST";

        if (!string.IsNullOrEmpty(sku))
        {
            var produto = await _db.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Sku == sku, ct);

            if (produto is null)
            {
                if (pesoReal <= 0m && preco >= config.LimitePrecoTabelaPeso)
                {
                    return AmazonFreightCalculationResult.Fail(
                        404,
                        $"SKU '{sku}' não encontrado e peso não informado.");
                }
            }
            else
            {
                if (pesoReal <= 0m)
                {
                    pesoReal = produto.PesoRealKg;
                    origemMedidas = "SKU_CADASTRO";
                }

                // Dimensões retornadas exclusivamente para conferência visual (Amazon usa somente peso real).
                comprimento = produto.ComprimentoCm;
                largura = produto.LarguraCm;
                altura = produto.AlturaCm;
                skuResposta = produto.Sku;
                nome = produto.Nome;
            }
        }

        // SQLite não compara/ordena decimal no servidor: filtramos modalidade no banco e faixas em memória.
        var tarifasModalidade = await _db.TarifasAmazon
            .AsNoTracking()
            .Where(t => t.Modalidade == modalidade)
            .ToListAsync(ct);

        if (tarifasModalidade.Count == 0)
        {
            return AmazonFreightCalculationResult.Fail(
                500,
                $"Tabela de tarifas Amazon ({modalidade}) não carregada.");
        }

        string tipoRegra;
        string descricaoFaixaPeso;
        string freteCliente;
        decimal tarifaBase;
        var kgExcedentes = 0;
        var tarifaKgAdicional = 0m;
        var valorKgExcedente = 0m;

        if (preco < config.LimitePrecoTabelaPeso)
        {
            // Caso 1: Preço abaixo do limite da tabela de peso -> regra FIXO (independe do peso)
            var faixaFixa = tarifasModalidade
                .Where(t => t.TipoRegra == AmazonTiposRegra.Fixo &&
                            preco >= t.PrecoMin &&
                            preco <= t.PrecoMax)
                .OrderBy(t => t.PrecoMax)
                .FirstOrDefault();

            if (faixaFixa is null)
            {
                return AmazonFreightCalculationResult.Fail(
                    422,
                    $"Nenhuma tarifa Amazon ({modalidade}) encontrada para o preço informado.");
            }

            tipoRegra = AmazonTiposRegra.Fixo;
            descricaoFaixaPeso = faixaFixa.DescricaoFaixaPeso;
            freteCliente = faixaFixa.FreteCliente;
            tarifaBase = faixaFixa.Tarifa;
        }
        else
        {
            if (pesoReal <= 0m)
            {
                return AmazonFreightCalculationResult.Fail(
                    400,
                    $"Para preço a partir de {FormatBrl(config.LimitePrecoTabelaPeso)} informe o peso real ou um SKU cadastrado.");
            }

            if (pesoReal <= config.LimitePesoTabelaKg)
            {
                // Caso 2: Preço >= limite e Peso <= limite da tabela -> FAIXA_PESO
                // Ordenação crescente por PesoMaxKg garante seleção determinística em fronteiras exatas (ex.: 0.10 kg, 0.25 kg).
                var faixaPeso = tarifasModalidade
                    .Where(t => t.TipoRegra == AmazonTiposRegra.FaixaPeso &&
                                preco >= t.PrecoMin &&
                                preco <= t.PrecoMax &&
                                pesoReal <= t.PesoMaxKg)
                    .OrderBy(t => t.PesoMaxKg)
                    .FirstOrDefault();

                if (faixaPeso is null)
                {
                    return AmazonFreightCalculationResult.Fail(
                        422,
                        $"Nenhuma faixa de peso Amazon ({modalidade}) encontrada para o preço e peso informados.");
                }

                tipoRegra = AmazonTiposRegra.FaixaPeso;
                descricaoFaixaPeso = faixaPeso.DescricaoFaixaPeso;
                freteCliente = faixaPeso.FreteCliente;
                tarifaBase = faixaPeso.Tarifa;
            }
            else
            {
                // Caso 3: Preço >= limite e Peso > limite da tabela -> última FAIXA_PESO + KG_ADICIONAL
                var faixaBase = tarifasModalidade
                    .Where(t => t.TipoRegra == AmazonTiposRegra.FaixaPeso &&
                                preco >= t.PrecoMin &&
                                preco <= t.PrecoMax)
                    .OrderByDescending(t => t.PesoMaxKg)
                    .FirstOrDefault();

                var regraExcedente = tarifasModalidade
                    .Where(t => t.TipoRegra == AmazonTiposRegra.KgAdicional &&
                                preco >= t.PrecoMin &&
                                preco <= t.PrecoMax)
                    .OrderBy(t => t.PrecoMax)
                    .FirstOrDefault();

                if (faixaBase is null || regraExcedente is null)
                {
                    return AmazonFreightCalculationResult.Fail(
                        422,
                        $"Regra de quilo adicional Amazon ({modalidade}) não encontrada para a faixa de preço informada.");
                }

                kgExcedentes = (int)Math.Ceiling(pesoReal - config.LimitePesoTabelaKg);
                tarifaBase = faixaBase.Tarifa;
                tarifaKgAdicional = regraExcedente.Tarifa;
                valorKgExcedente = Math.Round(kgExcedentes * tarifaKgAdicional, 2);
                tipoRegra = AmazonTiposRegra.KgAdicional;
                descricaoFaixaPeso = faixaBase.DescricaoFaixaPeso;
                freteCliente = string.IsNullOrWhiteSpace(regraExcedente.FreteCliente)
                    ? faixaBase.FreteCliente
                    : regraExcedente.FreteCliente;
            }
        }

        var valorFinal = Math.Round(tarifaBase + valorKgExcedente + config.TaxaFixaObrigatoria, 2);

        var response = new CalcularFreteAmazonResponse(
            Sku: skuResposta,
            Nome: nome,
            OrigemMedidas: origemMedidas,
            PrecoProduto: Money(preco),
            Modalidade: modalidade,
            TipoRegra: tipoRegra,
            DescricaoFaixaPeso: descricaoFaixaPeso,
            FreteCliente: freteCliente,
            PesoRealKg: Measure(pesoReal),
            ComprimentoCm: Measure(comprimento),
            LarguraCm: Measure(largura),
            AlturaCm: Measure(altura),
            TarifaBase: Money(tarifaBase),
            KgExcedentes: kgExcedentes,
            TarifaKgAdicional: Money(tarifaKgAdicional),
            ValorKgExcedente: Money(valorKgExcedente),
            TaxaFixaObrigatoria: Money(config.TaxaFixaObrigatoria),
            ValorFinalFrete: Money(valorFinal));

        return AmazonFreightCalculationResult.Ok(response);
    }

    private static decimal Money(decimal valor) => Math.Round(valor, 2) + 0.00m;

    private static decimal Measure(decimal valor) => Math.Round(valor, 3) + 0.0m;

    private static string FormatBrl(decimal valor) => string.Create(PtBrNumberFormat, $"R$ {valor:N2}");
}
