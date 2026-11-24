using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using EasyFrete.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EasyFrete.Api.Endpoints;

public static class AuthEndpoints
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    private const string GenericForgotPasswordMessage =
        "Se o e-mail estiver cadastrado e com acesso aprovado, enviaremos um link para redefinir sua senha.";

    public static void MapAuthEndpoints(this RouteGroupBuilder api, ApiSettings settings)
    {
        api.MapPost("/auth/register", Register)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);

        api.MapPost("/auth/login", (LoginRequest? request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
                Login(request, db, settings, clock, ct))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);

        api.MapPost("/auth/forgot-password", ForgotPassword)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.PasswordResetPolicy);

        api.MapGet("/auth/reset-password/validate", ValidateResetToken)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);

        api.MapPost("/auth/reset-password", ResetPassword)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);
    }

    private static async Task<IResult> Register(
        RegisterRequest? request,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Nome) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ErrorResponse("Preencha nome, e-mail e senha."));
        }

        var nome = request.Nome.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (nome.Length > 120)
            return Results.BadRequest(new ErrorResponse("Nome excede 120 caracteres."));
        if (email.Length > 180)
            return Results.BadRequest(new ErrorResponse("E-mail excede 180 caracteres."));
        if (!IsValidEmail(email))
            return Results.BadRequest(new ErrorResponse("Informe um e-mail válido."));
        if (request.Password.Length < MinPasswordLength)
            return Results.BadRequest(new ErrorResponse($"A senha deve ter pelo menos {MinPasswordLength} caracteres."));
        if (request.Password.Length > MaxPasswordLength)
            return Results.BadRequest(new ErrorResponse($"A senha excede o máximo de {MaxPasswordLength} caracteres."));
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new ErrorResponse("Este e-mail já está cadastrado."));

        var agora = clock.GetUtcNow().UtcDateTime;
        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            Role = UserRoles.User,
            Status = UserStatuses.Pendente,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CriadoEm = agora
        };
        var passwordHasher = new PasswordHasher<Usuario>();
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, request.Password);

        db.Usuarios.Add(usuario);

        // Notifica Master e Admins aprovados sobre o novo cadastro pendente (sem envio de e-mail de confirmação)
        var destinatarios = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Status == UserStatuses.Aprovado &&
                        (u.Role == UserRoles.Master || u.Role == UserRoles.Admin))
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var destinatarioId in destinatarios)
        {
            db.Notificacoes.Add(new Notificacao
            {
                DestinatarioId = destinatarioId,
                Tipo = NotificationTypes.CadastroPendente,
                Titulo = "Novo cadastro pendente",
                Mensagem = $"{usuario.Nome} ({usuario.Email}) solicitou acesso e aguarda aprovação.",
                ReferenciaId = usuario.Id,
                Lida = false,
                CriadoEm = agora
            });
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/admin/usuarios/{usuario.Id}",
            new UsuarioResumoDto(usuario.Id, usuario.Nome, usuario.Email, usuario.Role, usuario.Status));
    }

    private static async Task<IResult> Login(
        LoginRequest? request,
        AppDbContext db,
        ApiSettings settings,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Json(new ErrorResponse("Credenciais inválidas."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (usuario is null)
        {
            return Results.Json(new ErrorResponse("Credenciais inválidas."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var passwordHasher = new PasswordHasher<Usuario>();
        var verificacao = passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (verificacao == PasswordVerificationResult.Failed)
        {
            return Results.Json(new ErrorResponse("Credenciais inválidas."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (usuario.Status == UserStatuses.Pendente)
            return Results.Json(new ErrorResponse("Sua conta está pendente e aguarda aprovação do administrador."),
                statusCode: StatusCodes.Status403Forbidden);
        if (usuario.Status == UserStatuses.Rejeitado)
            return Results.Json(new ErrorResponse("Seu acesso foi rejeitado e não está liberado."),
                statusCode: StatusCodes.Status403Forbidden);

        var agora = clock.GetUtcNow().UtcDateTime;
        var expira = agora.AddHours(12);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim("email", usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Role),
                new Claim("sst", usuario.SecurityStamp),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            Issuer = settings.JwtIssuer,
            Audience = settings.JwtAudience,
            NotBefore = agora,
            IssuedAt = agora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)),
                SecurityAlgorithms.HmacSha256)
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return Results.Ok(new LoginResponse(token,
            new UsuarioResumoDto(usuario.Id, usuario.Nome, usuario.Email, usuario.Role, usuario.Status)));
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest? request,
        AppDbContext db,
        ApiSettings settings,
        IPasswordResetEmailSender emailSender,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new ErrorResponse("Informe o e-mail cadastrado."));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (email.Length > 180 || !IsValidEmail(email))
        {
            return Results.BadRequest(new ErrorResponse("Informe um e-mail válido."));
        }

        if (!emailSender.IsConfigured)
        {
            return Results.Json(
                new ErrorResponse("Serviço de envio de e-mail não configurado no servidor."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var agora = clock.GetUtcNow().UtcDateTime;
        var emailHash = ComputeSha256Hex(email);
        var lockoutWindow = TimeSpan.FromHours(settings.PasswordResetLockoutHours);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var controle = await db.ControlesEnvioRedefinicao
            .FirstOrDefaultAsync(c => c.EmailHash == emailHash, ct);

        if (controle is not null && controle.BloqueadoAteEm.HasValue && controle.BloqueadoAteEm.Value > agora)
        {
            var bloqueadoAteUtc = DateTime.SpecifyKind(controle.BloqueadoAteEm.Value, DateTimeKind.Utc);
            return Results.Json(
                new ForgotPasswordBlockedResponse(
                    $"Limite de {settings.PasswordResetMaxAttempts} envios atingido. Por segurança, novos envios para este e-mail estão bloqueados por {settings.PasswordResetLockoutHours} horas.",
                    bloqueadoAteUtc),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (controle is null)
        {
            controle = new ControleEnvioRedefinicao
            {
                EmailHash = emailHash,
                TentativasNaJanela = 1,
                PrimeiraTentativaEm = agora,
                UltimaTentativaEm = agora
            };
            db.ControlesEnvioRedefinicao.Add(controle);
        }
        else
        {
            var janelaExpirada =
                (controle.BloqueadoAteEm.HasValue && controle.BloqueadoAteEm.Value <= agora) ||
                (agora - controle.PrimeiraTentativaEm) >= lockoutWindow;

            if (janelaExpirada)
            {
                controle.TentativasNaJanela = 1;
                controle.PrimeiraTentativaEm = agora;
                controle.BloqueadoAteEm = null;
            }
            else
            {
                controle.TentativasNaJanela++;
            }

            controle.UltimaTentativaEm = agora;
        }

        if (controle.TentativasNaJanela >= settings.PasswordResetMaxAttempts)
        {
            controle.BloqueadoAteEm = agora.Add(lockoutWindow);
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Apenas contas aprovadas e não-Master recebem link (Master é controlada exclusivamente via .env)
        // A resposta continua idêntica para evitar enumeração de contas.
        if (usuario is not null &&
            usuario.Status == UserStatuses.Aprovado &&
            usuario.Role != UserRoles.Master)
        {
            var tokensAbertos = await db.TokensRedefinicaoSenha
                .Where(t => t.UsuarioId == usuario.Id && t.UsadoEm == null && t.RevogadoEm == null)
                .ToListAsync(ct);

            foreach (var antigo in tokensAbertos)
            {
                antigo.RevogadoEm = agora;
            }

            var rawToken = GenerateSecureToken();
            var tokenHash = ComputeSha256Hex(rawToken);
            var expiraEm = agora.AddMinutes(settings.PasswordResetTokenMinutes);

            db.TokensRedefinicaoSenha.Add(new TokenRedefinicaoSenha
            {
                UsuarioId = usuario.Id,
                TokenHash = tokenHash,
                CriadoEm = agora,
                ExpiraEm = expiraEm
            });

            var resetUrl = $"{settings.FrontendBaseUrl}/?reset={Uri.EscapeDataString(rawToken)}";

            try
            {
                await emailSender.SendPasswordResetEmailAsync(
                    usuario.Email,
                    usuario.Nome,
                    resetUrl,
                    settings.PasswordResetTokenMinutes,
                    ct);
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger("PasswordReset");
                logger.LogError(ex, "Falha ao enviar e-mail de redefinição de senha via SMTP.");
                await tx.RollbackAsync(ct);
                return Results.Json(
                    new ErrorResponse("Não foi possível enviar o e-mail de redefinição no momento. Tente novamente mais tarde."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new MessageResponse(GenericForgotPasswordMessage));
    }

    private static async Task<IResult> ValidateResetToken(
        string? token,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest(new ErrorResponse("Link de redefinição inválido ou expirado."));
        }

        var agora = clock.GetUtcNow().UtcDateTime;
        var tokenHash = ComputeSha256Hex(token.Trim());

        var registro = await db.TokensRedefinicaoSenha
            .AsNoTracking()
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (registro is null ||
            registro.UsadoEm is not null ||
            registro.RevogadoEm is not null ||
            registro.ExpiraEm <= agora ||
            registro.Usuario is null ||
            registro.Usuario.Status != UserStatuses.Aprovado ||
            registro.Usuario.Role == UserRoles.Master)
        {
            return Results.BadRequest(new ErrorResponse("Link de redefinição inválido ou expirado."));
        }

        return Results.Ok(new ResetTokenValidationResponse(
            true,
            DateTime.SpecifyKind(registro.ExpiraEm, DateTimeKind.Utc)));
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest? request,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new ErrorResponse("Informe o token e a nova senha."));
        }

        if (request.NewPassword.Length < MinPasswordLength)
            return Results.BadRequest(new ErrorResponse($"A senha deve ter pelo menos {MinPasswordLength} caracteres."));
        if (request.NewPassword.Length > MaxPasswordLength)
            return Results.BadRequest(new ErrorResponse($"A senha excede o máximo de {MaxPasswordLength} caracteres."));

        var agora = clock.GetUtcNow().UtcDateTime;
        var tokenHash = ComputeSha256Hex(request.Token.Trim());

        var registro = await db.TokensRedefinicaoSenha
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (registro is null ||
            registro.UsadoEm is not null ||
            registro.RevogadoEm is not null ||
            registro.ExpiraEm <= agora ||
            registro.Usuario is null ||
            registro.Usuario.Status != UserStatuses.Aprovado ||
            registro.Usuario.Role == UserRoles.Master)
        {
            return Results.BadRequest(new ErrorResponse("Link de redefinição inválido ou expirado."));
        }

        var passwordHasher = new PasswordHasher<Usuario>();
        registro.Usuario.PasswordHash = passwordHasher.HashPassword(registro.Usuario, request.NewPassword);
        registro.Usuario.SecurityStamp = Guid.NewGuid().ToString("N");
        registro.Usuario.AtualizadoEm = agora;
        registro.UsadoEm = agora;

        var outrosTokens = await db.TokensRedefinicaoSenha
            .Where(t => t.UsuarioId == registro.UsuarioId &&
                        t.Id != registro.Id &&
                        t.UsadoEm == null &&
                        t.RevogadoEm == null)
            .ToListAsync(ct);

        foreach (var outro in outrosTokens)
        {
            outro.RevogadoEm = agora;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new MessageResponse("Senha redefinida com sucesso. Você já pode entrar com a nova senha."));
    }

    internal static bool IsValidEmail(string email)
    {
        try
        {
            return MailAddress.TryCreate(email, out var addr) &&
                   string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    internal static string ComputeSha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
