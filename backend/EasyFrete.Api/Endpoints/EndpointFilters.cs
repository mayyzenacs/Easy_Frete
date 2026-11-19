using System.Security.Cryptography;
using System.Text;
using EasyFrete.Api.DTOs;

namespace EasyFrete.Api.Endpoints;

internal static class EndpointFilters
{
    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> JwtOrApiKeyFilter(string expectedKey) =>
        async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            if (http.User.Identity?.IsAuthenticated == true || ApiKeyAuth.IsValid(http, expectedKey))
            {
                return await next(ctx);
            }

            return ApiKeyAuth.Unauthorized("Não autorizado. Envie um JWT Bearer válido ou o header X-Api-Key.");
        };
}

internal static class ApiKeyAuth
{
    public const string HeaderName = "X-Api-Key";

    public static bool IsValid(HttpContext http, string expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey)) return false;
        if (!http.Request.Headers.TryGetValue(HeaderName, out var valores)) return false;
        var enviada = valores.FirstOrDefault();
        return !string.IsNullOrEmpty(enviada) && SecureEquals(enviada.Trim(), expectedKey);
    }

    public static bool SecureEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    public static IResult Unauthorized(string mensagem) =>
        Results.Json(new ErrorResponse(mensagem), statusCode: StatusCodes.Status401Unauthorized);
}
