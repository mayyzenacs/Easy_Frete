namespace EasyFrete.Api.Configuration;
using EasyFrete.Api.DTOs;

public sealed class ApiSettings
{
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string JwtSecret { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = "easyfrete-api";
    public string JwtAudience { get; init; } = "easyfrete-web";
    public string ExternalCalcApiKey { get; init; } = string.Empty;
    public string SyncApiKey { get; init; } = string.Empty;
    public string SqlitePath { get; init; } = "data/easyfrete.db";

public static ApiSettings FromEnvironment()
    {
        var problemas = new List<string>();

        string Obrigatoria(string nome)
        {
            var valor = Environment.GetEnvironmentVariable(nome);
            if (string.IsNullOrWhiteSpace(valor))
            {
                problemas.Add($"{nome} não definida");
                return string.Empty;
            }
            return valor.Trim();
        }

        string Opcional(string nome, string padrao)
        {
            var valor = Environment.GetEnvironmentVariable(nome);
            return string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();
        }

        var settings = new ApiSettings
        {
            AdminEmail = Obrigatoria("ADMIN_EMAIL"),
            AdminPassword = Obrigatoria("ADMIN_PASSWORD"),
            JwtSecret = Obrigatoria("JWT_SECRET"),
            ExternalCalcApiKey = Obrigatoria("EXTERNAL_CALC_API_KEY"),
            SyncApiKey = Obrigatoria("SYNC_API_KEY"),
            JwtIssuer = Opcional("JWT_ISSUER", "easyfrete-api"),
            JwtAudience = Opcional("JWT_AUDIENCE", "easyfrete-web"),
            SqlitePath = Opcional("SQLITE_PATH", "data/easyfrete.db")
        };

        if (settings.JwtSecret.Length > 0 && settings.JwtSecret.Length < 32)
        {
            problemas.Add("JWT_SECRET precisa ter no mínimo 32 caracteres");
        }

        if (problemas.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuração inválida (.env / variáveis de ambiente): " + string.Join("; ", problemas) + ".");
        }

        return settings;
    }}