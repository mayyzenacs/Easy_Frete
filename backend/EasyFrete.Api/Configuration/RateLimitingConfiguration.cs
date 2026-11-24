using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using EasyFrete.Api.DTOs;

namespace EasyFrete.Api.Configuration;

public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "AuthPolicy";
    public const string PasswordResetPolicy = "PasswordResetPolicy";
    public const string ApiPolicy = "ApiPolicy";
    public const string SyncPolicy = "SyncPolicy";

    public static IServiceCollection AddEasyFreteRateLimiting(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var isTesting = environment.IsEnvironment("Testing");
        var authLimit = isTesting ? 10_000 : 10;
        var passwordResetIpLimit = isTesting ? 10_000 : 8;
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
                    GetClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            // Camada extra por IP para o endpoint de esqueci minha senha (complementa o bloqueio persistente 2x/24h por e-mail)
            options.AddPolicy(PasswordResetPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = passwordResetIpLimit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(ApiPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = apiLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(SyncPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetSyncPartitionKey(httpContext),
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

    private static string GetClientIp(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return "unknown";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        return ip.ToString();
    }

    private static string GetSyncPartitionKey(HttpContext httpContext)
    {
        var apiKey = httpContext.Request.Headers["X-Api-Key"].ToString().Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return $"ip:{GetClientIp(httpContext)}";
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        var shortHash = Convert.ToHexString(hashBytes)[..16];
        return $"key:{shortHash}";
    }
}
