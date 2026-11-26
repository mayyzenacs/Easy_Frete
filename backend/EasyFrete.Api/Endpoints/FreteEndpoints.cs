using EasyFrete.Api.Configuration;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Services;

namespace EasyFrete.Api.Endpoints;

public static class FreteEndpoints
{
    public static void MapFreteEndpoints(this RouteGroupBuilder api, ApiSettings settings)
    {
        api.MapPost("/frete/magalu/calcular", Calcular)
            .AddEndpointFilter(EndpointFilters.JwtOrApiKeyFilter(settings.ExternalCalcApiKey))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/frete/amazon/calcular", CalcularAmazon)
            .AddEndpointFilter(EndpointFilters.JwtOrApiKeyFilter(settings.ExternalCalcApiKey))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);
    }

    private static async Task<IResult> Calcular(
        CalcularFreteRequest request,
        IMagaluFreightService service,
        CancellationToken ct)
    {
        var resultado = await service.CalcularAsync(request, ct);

        if (resultado.Success)
        {
            return Results.Ok(resultado.Data);
        }

        return Results.Json(new ErrorResponse(resultado.Error ?? "Erro ao calcular frete."), statusCode: resultado.StatusCode);
    }

    private static async Task<IResult> CalcularAmazon(
        CalcularFreteAmazonRequest request,
        IAmazonFreightService service,
        CancellationToken ct)
    {
        var resultado = await service.CalcularAsync(request, ct);

        if (resultado.Success)
        {
            return Results.Ok(resultado.Data);
        }

        return Results.Json(new ErrorResponse(resultado.Error ?? "Erro ao calcular frete."), statusCode: resultado.StatusCode);
    }
}
