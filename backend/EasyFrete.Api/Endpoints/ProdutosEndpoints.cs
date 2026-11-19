using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Endpoints;

public static class ProdutosEndpoints
{
    private const int MaxLote = 5000;
    private const int TamanhoChunk = 500;

    public static void MapProdutosEndpoints(this RouteGroupBuilder api, ApiSettings settings)
    {
        api.MapGet("/produtos", ListarProdutos)
            .RequireAuthorization();

        api.MapGet("/produtos/{sku}", GetProduto)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/produtos/sync-lote", SyncLote)
            .AddEndpointFilter(EndpointFilters.JwtOrApiKeyFilter(settings.SyncApiKey))
            .RequireRateLimiting(RateLimitingConfiguration.SyncPolicy);
    }

    private static async Task<IResult> ListarProdutos(AppDbContext db, CancellationToken ct)
    {
        var lista = await db.Produtos
            .AsNoTracking()
            .OrderByDescending(p => p.AtualizadoEm)
            .Take(100)
            .Select(p => new ProdutoResponse(p.Sku, p.Nome, p.PesoRealKg, p.ComprimentoCm, p.LarguraCm, p.AlturaCm, p.AtualizadoEm, p.LinkDrive))
            .ToListAsync(ct);

        return Results.Ok(lista);
    }

    private static async Task<IResult> GetProduto(string sku, AppDbContext db, CancellationToken ct)
    {
        var chave = sku.Trim();
        var produto = await db.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Sku == chave, ct);

        if (produto is null)
        {
            return Results.NotFound(new ErrorResponse($"Produto '{chave}' não encontrado."));
        }

        return Results.Ok(new ProdutoResponse(
            produto.Sku,
            produto.Nome,
            produto.PesoRealKg,
            produto.ComprimentoCm,
            produto.LarguraCm,
            produto.AlturaCm,
            produto.AtualizadoEm,
            produto.LinkDrive));
    }

    private static async Task<IResult> SyncLote(SyncLoteRequest? request, AppDbContext db, CancellationToken ct)
    {
        var produtos = request?.Produtos;
        if (request is null || produtos is null || produtos.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("Envie { origem, produtos: [...] } com ao menos um produto."));
        }

        if (produtos.Count > MaxLote)
        {
            return Results.BadRequest(new ErrorResponse($"Lote excede o máximo de {MaxLote} produtos."));
        }

        var origem = string.IsNullOrWhiteSpace(request.Origem) ? "DESCONHECIDA" : request.Origem.Trim();
        var erros = new List<string>();
        var validos = new Dictionary<string, SyncProdutoItem>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < produtos.Count; i++)
        {
            var item = produtos[i];
            if (item is null)
            {
                erros.Add($"Item {i}: objeto nulo.");
                continue;
            }

            var sku = item.Sku?.Trim();
            if (string.IsNullOrEmpty(sku))
            {
                erros.Add($"Item {i}: sku vazio.");
                continue;
            }

            if (sku.Length > 64)
            {
                erros.Add($"Item {i}: sku '{sku[..20]}...' excede 64 caracteres.");
                continue;
            }

            if (item.PesoBruto < 0m || item.AlturaCm < 0m || item.LarguraCm < 0m || item.ComprimentoCm < 0m)
            {
                erros.Add($"Item {i}: sku '{sku}' com peso/medida negativa.");
                continue;
            }

            if (validos.ContainsKey(sku))
            {
                erros.Add($"Item {i}: sku '{sku}' duplicado no lote; prevaleceu o último.");
            }

            validos[sku] = item;
        }

        var agora = DateTime.UtcNow;
        var inseridos = 0;
        var atualizados = 0;

        var chaves = validos.Keys.ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        for (var offset = 0; offset < chaves.Count; offset += TamanhoChunk)
        {
            var fatia = chaves.Skip(offset).Take(TamanhoChunk).ToList();
            var existentes = await db.Produtos
                .Where(p => fatia.Contains(p.Sku))
                .ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var sku in fatia)
            {
                var item = validos[sku];
                var nome = string.IsNullOrWhiteSpace(item.Nome) ? sku : item.Nome.Trim();
                if (nome.Length > 255) nome = nome[..255];

                if (existentes.TryGetValue(sku, out var existente))
                {
                    existente.Nome = nome;
                    existente.PesoRealKg = Math.Round(item.PesoBruto, 3);
                    existente.AlturaCm = Math.Round(item.AlturaCm, 2);
                    existente.LarguraCm = Math.Round(item.LarguraCm, 2);
                    existente.ComprimentoCm = Math.Round(item.ComprimentoCm, 2);
                    existente.LinkDrive = string.IsNullOrWhiteSpace(item.LinkDrive) ? null : item.LinkDrive.Trim();
                    existente.AtualizadoEm = agora;
                    atualizados++;
                }
                else
                {
                    db.Produtos.Add(new Produto
                    {
                        Sku = sku,
                        Nome = nome,
                        PesoRealKg = Math.Round(item.PesoBruto, 3),
                        AlturaCm = Math.Round(item.AlturaCm, 2),
                        LarguraCm = Math.Round(item.LarguraCm, 2),
                        ComprimentoCm = Math.Round(item.ComprimentoCm, 2),
                        LinkDrive = string.IsNullOrWhiteSpace(item.LinkDrive) ? null : item.LinkDrive.Trim(),
                        AtualizadoEm = agora
                    });
                    inseridos++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);

        var ignorados = produtos.Count - (inseridos + atualizados);
        return Results.Ok(new SyncLoteResponse(
            origem,
            produtos.Count,
            inseridos,
            atualizados,
            ignorados < 0 ? 0 : ignorados,
            erros,
            agora));
    }
}
