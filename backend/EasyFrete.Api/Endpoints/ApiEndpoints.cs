using EasyFrete.Api.Configuration;
using EasyFrete.Api.DTOs;

namespace EasyFrete.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<ApiSettings>();
        var api = app.MapGroup("/api/v1");

        api.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy", DateTime.UtcNow)))
            .AllowAnonymous();

        api.MapAuthEndpoints(settings);
        api.MapAdminEndpoints();
        api.MapProdutosEndpoints(settings);
        api.MapFreteEndpoints(settings);
        api.MapTarifasEndpoints(settings);

        // Alias explícito para /api/auth/logout mantendo também /api/v1/auth/logout
        app.MapPost("/api/auth/logout", AuthEndpoints.Logout)
            .AllowAnonymous();
    }
}
