using Microsoft.Extensions.Configuration;

namespace EasyFrete.Api.Configuration;

public sealed class ApiSettings
{
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string JwtSecret { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = "easyfrete-api";
    public string JwtAudience { get; init; } = "easyfrete-web";
    public int JwtExpirationMinutes { get; init; } = 60;
    public string ExternalCalcApiKey { get; init; } = string.Empty;
    public string SyncApiKey { get; init; } = string.Empty;
    public string SqlitePath { get; init; } = "data/easyfrete.db";

    // Configurações de e-mail (exclusivo para redefinição de senha) e política de reset
    public string FrontendBaseUrl { get; init; } = string.Empty;
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string SmtpFromEmail { get; init; } = string.Empty;
    public string SmtpFromName { get; init; } = "Easy Frete";
    public bool? SmtpUseSsl { get; init; }
    public int PasswordResetTokenMinutes { get; init; } = 30;
    public int PasswordResetMaxAttempts { get; init; } = 2;
    public int PasswordResetLockoutHours { get; init; } = 24;

    public bool IsSmtpConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(SmtpFromEmail) &&
        !string.IsNullOrWhiteSpace(FrontendBaseUrl);

    public string NormalizedAdminEmail => AdminEmail.Trim().ToLowerInvariant();

    public static ApiSettings FromEnvironment(IConfiguration? configuration = null)
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

        int OpcionalInt(string nome, int padrao, int min, int max)
        {
            var valor = Environment.GetEnvironmentVariable(nome);
            if (string.IsNullOrWhiteSpace(valor))
                return padrao;
            if (!int.TryParse(valor.Trim(), out var parsed) || parsed < min || parsed > max)
            {
                problemas.Add($"{nome} deve ser um inteiro entre {min} e {max}");
                return padrao;
            }
            return parsed;
        }

        bool? OpcionalBool(string nome)
        {
            var valor = Environment.GetEnvironmentVariable(nome);
            if (string.IsNullOrWhiteSpace(valor))
                return null;
            if (!bool.TryParse(valor.Trim(), out var parsed))
            {
                problemas.Add($"{nome} deve ser 'true' ou 'false'");
                return null;
            }
            return parsed;
        }

        var frontendBaseUrl = Opcional("FRONTEND_BASE_URL", string.Empty).TrimEnd('/');
        if (!string.IsNullOrEmpty(frontendBaseUrl) &&
            (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            problemas.Add("FRONTEND_BASE_URL deve ser uma URL HTTP/HTTPS válida");
        }

        var jwtExpirationMinutes = 60;
        var jwtExpRaw = configuration?["Jwt:ExpirationMinutes"]
            ?? Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES");
        if (!string.IsNullOrWhiteSpace(jwtExpRaw))
        {
            if (!int.TryParse(jwtExpRaw.Trim(), out var parsedExp) || parsedExp < 1 || parsedExp > 60)
            {
                problemas.Add("Jwt:ExpirationMinutes deve ser um inteiro entre 1 e 60");
            }
            else
            {
                jwtExpirationMinutes = parsedExp;
            }
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
            JwtExpirationMinutes = jwtExpirationMinutes,
            SqlitePath = Opcional("SQLITE_PATH", "data/easyfrete.db"),
            FrontendBaseUrl = frontendBaseUrl,
            SmtpHost = Opcional("SMTP_HOST", string.Empty),
            SmtpPort = OpcionalInt("SMTP_PORT", 587, 1, 65535),
            SmtpUser = Opcional("SMTP_USER", string.Empty),
            SmtpPassword = Opcional("SMTP_PASSWORD", string.Empty),
            SmtpFromEmail = Opcional("SMTP_FROM_EMAIL", string.Empty),
            SmtpFromName = Opcional("SMTP_FROM_NAME", "Easy Frete"),
            SmtpUseSsl = OpcionalBool("SMTP_USE_SSL"),
            PasswordResetTokenMinutes = OpcionalInt("PASSWORD_RESET_TOKEN_MINUTES", 30, 5, 120),
            PasswordResetMaxAttempts = OpcionalInt("PASSWORD_RESET_MAX_ATTEMPTS", 2, 1, 10),
            PasswordResetLockoutHours = OpcionalInt("PASSWORD_RESET_LOCKOUT_HOURS", 24, 1, 168)
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
    }
}
