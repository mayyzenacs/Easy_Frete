using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Endpoints;

public static class TarifasEndpoints
{
    public static void MapTarifasEndpoints(this RouteGroupBuilder api, ApiSettings settings)
    {
        api.MapGet("/tarifas/magalu/config", GetConfiguracaoMagalu)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/tarifas/magalu/sync", SyncTarifasMagalu)
            .AddEndpointFilter(EndpointFilters.JwtOrApiKeyFilter(settings.SyncApiKey))
            .RequireRateLimiting(RateLimitingConfiguration.SyncPolicy);

        api.MapGet("/tarifas/amazon/config", GetConfiguracaoAmazon)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/tarifas/amazon/sync", SyncTarifasAmazon)
            .AddEndpointFilter(EndpointFilters.JwtOrApiKeyFilter(settings.SyncApiKey))
            .RequireRateLimiting(RateLimitingConfiguration.SyncPolicy);
    }

    private static async Task<IResult> GetConfiguracaoMagalu(
        AppDbContext db,
        CancellationToken ct)
    {
        var config = await db.ConfiguracoesMagalu
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, ct);

        if (config is null)
        {
            return Results.NotFound(new ErrorResponse(
                "Configuração de tarifas Magalu não carregada. Sincronize a planilha."));
        }

        return Results.Ok(new ConfiguracaoMagaluResponse(
            TaxaFixaObrigatoria: config.TaxaFixaObrigatoria,
            LimiteTabela: config.LimiteTabela,
            LimiteAcima199: config.LimiteAcima199,
            PercentualAbaixoLimiteFulfillment: config.PercentualAbaixoLimiteFulfillment,
            PercentualAbaixoLimiteColeta: config.PercentualAbaixoLimiteColeta,
            DivisorCubagem: config.DivisorCubagem,
            PercentualReputacaoMinima: config.PercentualReputacaoMinima,
            AtualizadoEm: config.AtualizadoEm));
    }

    private static async Task<IResult> SyncTarifasMagalu(
        SyncTarifasMagaluRequest? request,
        AppDbContext db,
        CancellationToken ct)
    {
        var tarifas = request?.Tarifas;
        var configuracao = request?.Configuracao;
        if (request is null || tarifas is null || tarifas.Count == 0 || configuracao is null)
        {
            return Results.BadRequest(new ErrorResponse(
                "Envie { origem, configuracao: {...}, tarifas: [...] } com configuração e ao menos uma tarifa."));
        }

        if (configuracao.TaxaFixaObrigatoria < 0m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: taxaFixaObrigatoria não pode ser negativa."));
        }

        if (configuracao.LimiteTabela <= 0m || configuracao.LimiteAcima199 <= configuracao.LimiteTabela)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: limiteTabela deve ser maior que zero e limiteAcima199 maior que limiteTabela."));
        }

        if (configuracao.PercentualAbaixoLimiteFulfillment <= 0m || configuracao.PercentualAbaixoLimiteFulfillment > 100m ||
            configuracao.PercentualAbaixoLimiteColeta <= 0m || configuracao.PercentualAbaixoLimiteColeta > 100m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: percentuais abaixo do limite devem estar entre 0 e 100."));
        }

        if (configuracao.DivisorCubagem <= 0m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: divisorCubagem deve ser maior que zero."));
        }

        if (configuracao.PercentualReputacaoMinima <= 0m || configuracao.PercentualReputacaoMinima > 100m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: percentualReputacaoMinima deve estar entre 0 e 100."));
        }

        var idsRecebidos = new HashSet<int>();
        foreach (var tarifa in tarifas)
        {
            var valoresMonetarios = new[]
            {
                tarifa.De79a199SemDesconto,
                tarifa.De79a199ComDesconto97,
                tarifa.De79a199Fulfillment,
                tarifa.Acima199SemDesconto,
                tarifa.Acima199ComDesconto97,
                tarifa.Acima199Fulfillment
            };

            if (tarifa.Id <= 0)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id inválido: {tarifa.Id}."));
            }

            if (!idsRecebidos.Add(tarifa.Id))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id duplicado no lote: {tarifa.Id}."));
            }

            if (string.IsNullOrWhiteSpace(tarifa.DescricaoFaixa))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui DescricaoFaixa vazia."));
            }

            if (tarifa.PesoMaxKg <= tarifa.PesoMinKg)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui PesoMaxKg inválido."));
            }

            if (valoresMonetarios.Any(valor => valor <= 0m))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui valor monetário inválido (deve ser maior que zero)."));
            }
        }

        var origem = string.IsNullOrWhiteSpace(request.Origem)
            ? "DESCONHECIDA"
            : request.Origem.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var agora = DateTime.UtcNow;
        var configExistente = await db.ConfiguracoesMagalu.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (configExistente is not null)
        {
            configExistente.TaxaFixaObrigatoria = configuracao.TaxaFixaObrigatoria;
            configExistente.LimiteTabela = configuracao.LimiteTabela;
            configExistente.LimiteAcima199 = configuracao.LimiteAcima199;
            configExistente.PercentualAbaixoLimiteFulfillment = configuracao.PercentualAbaixoLimiteFulfillment;
            configExistente.PercentualAbaixoLimiteColeta = configuracao.PercentualAbaixoLimiteColeta;
            configExistente.DivisorCubagem = configuracao.DivisorCubagem;
            configExistente.PercentualReputacaoMinima = configuracao.PercentualReputacaoMinima;
            configExistente.AtualizadoEm = agora;
        }
        else
        {
            db.ConfiguracoesMagalu.Add(new ConfiguracaoMagalu
            {
                Id = 1,
                TaxaFixaObrigatoria = configuracao.TaxaFixaObrigatoria,
                LimiteTabela = configuracao.LimiteTabela,
                LimiteAcima199 = configuracao.LimiteAcima199,
                PercentualAbaixoLimiteFulfillment = configuracao.PercentualAbaixoLimiteFulfillment,
                PercentualAbaixoLimiteColeta = configuracao.PercentualAbaixoLimiteColeta,
                DivisorCubagem = configuracao.DivisorCubagem,
                PercentualReputacaoMinima = configuracao.PercentualReputacaoMinima,
                AtualizadoEm = agora
            });
        }

        var existentes = await db.TarifasMagalu.ToListAsync(ct);
        var existentesPorId = existentes.ToDictionary(tarifa => tarifa.Id);
        var inseridos = 0;
        var atualizados = 0;

        foreach (var item in tarifas)
        {
            if (existentesPorId.TryGetValue(item.Id, out var existente))
            {
                existente.DescricaoFaixa = item.DescricaoFaixa.Trim();
                existente.PesoMinKg = item.PesoMinKg;
                existente.PesoMaxKg = item.PesoMaxKg;
                existente.De79a199SemDesconto = item.De79a199SemDesconto;
                existente.De79a199ComDesconto97 = item.De79a199ComDesconto97;
                existente.De79a199Fulfillment = item.De79a199Fulfillment;
                existente.Acima199SemDesconto = item.Acima199SemDesconto;
                existente.Acima199ComDesconto97 = item.Acima199ComDesconto97;
                existente.Acima199Fulfillment = item.Acima199Fulfillment;
                atualizados++;
            }
            else
            {
                db.TarifasMagalu.Add(new TarifaMagalu
                {
                    Id = item.Id,
                    DescricaoFaixa = item.DescricaoFaixa.Trim(),
                    PesoMinKg = item.PesoMinKg,
                    PesoMaxKg = item.PesoMaxKg,
                    De79a199SemDesconto = item.De79a199SemDesconto,
                    De79a199ComDesconto97 = item.De79a199ComDesconto97,
                    De79a199Fulfillment = item.De79a199Fulfillment,
                    Acima199SemDesconto = item.Acima199SemDesconto,
                    Acima199ComDesconto97 = item.Acima199ComDesconto97,
                    Acima199Fulfillment = item.Acima199Fulfillment
                });
                inseridos++;
            }
        }

        var removidos = existentes.Count(tarifa => !idsRecebidos.Contains(tarifa.Id));
        db.TarifasMagalu.RemoveRange(
            existentes.Where(tarifa => !idsRecebidos.Contains(tarifa.Id)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            origem,
            totalRecebido = tarifas.Count,
            inseridos,
            atualizados,
            removidos
        });
    }

    private static async Task<IResult> GetConfiguracaoAmazon(
        AppDbContext db,
        CancellationToken ct)
    {
        var config = await db.ConfiguracoesAmazon
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, ct);

        if (config is null)
        {
            return Results.NotFound(new ErrorResponse(
                "Configuração de tarifas Amazon não carregada. Sincronize a planilha."));
        }

        return Results.Ok(new ConfiguracaoAmazonResponse(
            UsaPesoCubado: config.UsaPesoCubado,
            LimitePrecoTabelaPeso: config.LimitePrecoTabelaPeso,
            LimitePesoTabelaKg: config.LimitePesoTabelaKg,
            RegiaoReferenciaDba: config.RegiaoReferenciaDba,
            TaxaFixaObrigatoria: config.TaxaFixaObrigatoria,
            AtualizadoEm: config.AtualizadoEm));
    }

    private static async Task<IResult> SyncTarifasAmazon(
        SyncTarifasAmazonRequest? request,
        AppDbContext db,
        CancellationToken ct)
    {
        var tarifas = request?.Tarifas;
        var configuracao = request?.Configuracao;
        if (request is null || tarifas is null || tarifas.Count == 0 || configuracao is null)
        {
            return Results.BadRequest(new ErrorResponse(
                "Envie { origem, configuracao: {...}, tarifas: [...] } com configuração e ao menos uma tarifa."));
        }

        if (configuracao.LimitePrecoTabelaPeso <= 0m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: limitePrecoTabelaPeso deve ser maior que zero."));
        }

        if (configuracao.LimitePesoTabelaKg <= 0m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: limitePesoTabelaKg deve ser maior que zero."));
        }

        if (configuracao.TaxaFixaObrigatoria < 0m)
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: taxaFixaObrigatoria não pode ser negativa."));
        }

        var regiaoReferenciaDba = configuracao.RegiaoReferenciaDba?.Trim();
        if (string.IsNullOrWhiteSpace(regiaoReferenciaDba))
        {
            return Results.BadRequest(new ErrorResponse(
                "Configuração inválida: regiaoReferenciaDba é obrigatória."));
        }

        var idsRecebidos = new HashSet<int>();
        var itensNormalizados = new List<TarifaAmazon>(tarifas.Count);

        foreach (var tarifa in tarifas)
        {
            if (tarifa.Id <= 0)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id inválido: {tarifa.Id}."));
            }

            if (!idsRecebidos.Add(tarifa.Id))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id duplicado no lote: {tarifa.Id}."));
            }

            var modalidade = tarifa.Modalidade?.Trim().ToUpperInvariant();
            if (modalidade is not (AmazonModalidades.Dba or AmazonModalidades.Fba))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui modalidade inválida (esperado DBA ou FBA)."));
            }

            var tipoRegra = tarifa.TipoRegra?.Trim().ToUpperInvariant();
            if (tipoRegra is not (AmazonTiposRegra.Fixo or AmazonTiposRegra.FaixaPeso or AmazonTiposRegra.KgAdicional))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui tipoRegra inválido (esperado FIXO, FAIXA_PESO ou KG_ADICIONAL)."));
            }

            var freteCliente = tarifa.FreteCliente?.Trim().ToUpperInvariant();
            if (freteCliente is not (AmazonFreteClientes.Gratis or AmazonFreteClientes.PagoPeloCliente))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui freteCliente inválido (esperado GRATIS ou PAGO_PELO_CLIENTE)."));
            }

            var descricaoFaixa = tarifa.DescricaoFaixaPeso?.Trim();
            if (string.IsNullOrWhiteSpace(descricaoFaixa))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui descricaoFaixaPeso vazia."));
            }

            if (tarifa.PrecoMin < 0m || tarifa.PrecoMax < tarifa.PrecoMin)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui faixa de preço inválida."));
            }

            if (tarifa.PesoMinKg < 0m ||
                tarifa.PesoMaxKg < tarifa.PesoMinKg ||
                (tipoRegra == AmazonTiposRegra.FaixaPeso && tarifa.PesoMaxKg <= tarifa.PesoMinKg))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui faixa de peso inválida."));
            }

            if (tarifa.Tarifa < 0m)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa Amazon com Id {tarifa.Id} possui valor monetário negativo."));
            }

            itensNormalizados.Add(new TarifaAmazon
            {
                Id = tarifa.Id,
                Modalidade = modalidade,
                TipoRegra = tipoRegra,
                DescricaoFaixaPeso = descricaoFaixa,
                PrecoMin = Math.Round(tarifa.PrecoMin, 2),
                PrecoMax = Math.Round(tarifa.PrecoMax, 2),
                PesoMinKg = Math.Round(tarifa.PesoMinKg, 3),
                PesoMaxKg = Math.Round(tarifa.PesoMaxKg, 3),
                Tarifa = Math.Round(tarifa.Tarifa, 2),
                FreteCliente = freteCliente
            });
        }

        var origem = string.IsNullOrWhiteSpace(request.Origem)
            ? "DESCONHECIDA"
            : request.Origem.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var agora = DateTime.UtcNow;
        var configExistente = await db.ConfiguracoesAmazon.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (configExistente is not null)
        {
            configExistente.UsaPesoCubado = configuracao.UsaPesoCubado;
            configExistente.LimitePrecoTabelaPeso = Math.Round(configuracao.LimitePrecoTabelaPeso, 2);
            configExistente.LimitePesoTabelaKg = Math.Round(configuracao.LimitePesoTabelaKg, 3);
            configExistente.RegiaoReferenciaDba = regiaoReferenciaDba;
            configExistente.TaxaFixaObrigatoria = Math.Round(configuracao.TaxaFixaObrigatoria, 2);
            configExistente.AtualizadoEm = agora;
        }
        else
        {
            db.ConfiguracoesAmazon.Add(new ConfiguracaoAmazon
            {
                Id = 1,
                UsaPesoCubado = configuracao.UsaPesoCubado,
                LimitePrecoTabelaPeso = Math.Round(configuracao.LimitePrecoTabelaPeso, 2),
                LimitePesoTabelaKg = Math.Round(configuracao.LimitePesoTabelaKg, 3),
                RegiaoReferenciaDba = regiaoReferenciaDba,
                TaxaFixaObrigatoria = Math.Round(configuracao.TaxaFixaObrigatoria, 2),
                AtualizadoEm = agora
            });
        }

        var existentes = await db.TarifasAmazon.ToListAsync(ct);
        var existentesPorId = existentes.ToDictionary(tarifa => tarifa.Id);
        var inseridos = 0;
        var atualizados = 0;

        foreach (var item in itensNormalizados)
        {
            if (existentesPorId.TryGetValue(item.Id, out var existente))
            {
                existente.Modalidade = item.Modalidade;
                existente.TipoRegra = item.TipoRegra;
                existente.DescricaoFaixaPeso = item.DescricaoFaixaPeso;
                existente.PrecoMin = item.PrecoMin;
                existente.PrecoMax = item.PrecoMax;
                existente.PesoMinKg = item.PesoMinKg;
                existente.PesoMaxKg = item.PesoMaxKg;
                existente.Tarifa = item.Tarifa;
                existente.FreteCliente = item.FreteCliente;
                atualizados++;
            }
            else
            {
                db.TarifasAmazon.Add(item);
                inseridos++;
            }
        }

        var removidos = existentes.Count(tarifa => !idsRecebidos.Contains(tarifa.Id));
        db.TarifasAmazon.RemoveRange(
            existentes.Where(tarifa => !idsRecebidos.Contains(tarifa.Id)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            origem,
            totalRecebido = tarifas.Count,
            inseridos,
            atualizados,
            removidos
        });
    }
}
