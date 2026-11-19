using System.Security.Claims;
using System.Threading.RateLimiting;
using EasyFrete.Api.DTOs;

namespace EasyFrete.Api.Configuration;

public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "AuthPolicy";
    public const string ApiPolicy = "ApiPolicy";
    public const string SyncPolicy = "SyncPolicy";

    public static IServiceCollection AddEasyFreteRateLimiting(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var isTesting = environment.IsEnvironment("Testing");
        var authLimit = isTesting ? 10_000 : 10;
        var apiLimit = isTesting ? 10_000 : 60;
        var syncLimit = isTesting ? 10_000 : 120;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Muitas requisições. Tente novamente em instantes."),
                    cancellationToken);
            };

            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(ApiPolicy, httpContext =>
            {
                var partitionKey =
                    httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = apiLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(SyncPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = syncLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
