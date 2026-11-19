using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EasyFrete.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api, ApiSettings settings)
    {
        api.MapPost("/auth/register", Register)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);
        api.MapPost("/auth/login", (LoginRequest? request, AppDbContext db, CancellationToken ct) =>
                Login(request, db, settings, ct))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);
    }

    private static async Task<IResult> Register(
        RegisterRequest? request,
        AppDbContext db,
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
        try
        {
            if (!MailAddress.TryCreate(email, out _))
                return Results.BadRequest(new ErrorResponse("Informe um e-mail válido."));
        }
        catch
        {
            return Results.BadRequest(new ErrorResponse("Informe um e-mail válido."));
        }
        if (request.Password.Length < 8)
            return Results.BadRequest(new ErrorResponse("A senha deve ter pelo menos 8 caracteres."));
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new ErrorResponse("Este e-mail já está cadastrado."));

        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            Role = "User",
            Status = "Pendente",
            CriadoEm = DateTime.UtcNow
        };
        var passwordHasher = new PasswordHasher<Usuario>();
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, request.Password);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/admin/usuarios/{usuario.Id}",
            new UsuarioResumoDto(usuario.Id, usuario.Nome, usuario.Email, usuario.Role, usuario.Status));
    }

    private static async Task<IResult> Login(
        LoginRequest? request,
        AppDbContext db,
        ApiSettings settings,
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

        if (usuario.Status == "Pendente")
            return Results.Json(new ErrorResponse("Sua conta está pendente e aguarda aprovação do administrador."),
                statusCode: StatusCodes.Status403Forbidden);
        if (usuario.Status == "Rejeitado")
            return Results.Json(new ErrorResponse("Seu acesso foi rejeitado e não está liberado."),
                statusCode: StatusCodes.Status403Forbidden);

        var agora = DateTime.UtcNow;
        var expira = agora.AddHours(12);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim("email", usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Role),
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
}
