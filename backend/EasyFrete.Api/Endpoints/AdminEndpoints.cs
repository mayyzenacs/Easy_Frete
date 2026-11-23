using System.Net.Mail;
using System.Security.Claims;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/admin/usuarios", ListarUsuarios)
            .RequireAuthorization(p => p.RequireRole("Admin"))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPatch("/admin/usuarios/{id:guid}", AtualizarUsuario)
            .RequireAuthorization(p => p.RequireRole("Admin"))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);
    }

    private static async Task<IResult> ListarUsuarios(
        string? status,
        AppDbContext db,
        CancellationToken ct)
    {
        var query = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(u => u.Status == status);

        var usuarios = await query
            .OrderBy(u => u.Status == "Pendente" ? 0 : u.Status == "Aprovado" ? 1 : 2)
            .ThenBy(u => u.Nome)
            .Select(u => new AdminUsuarioDto(
                u.Id,
                u.Nome,
                u.Email,
                u.Role,
                u.Status,
                DateTime.SpecifyKind(u.CriadoEm, DateTimeKind.Utc)))
            .ToListAsync(ct);

        return Results.Ok(usuarios);
    }

    private static async Task<IResult> AtualizarUsuario(
        Guid id,
        AdminUsuarioPatchRequest? request,
        HttpContext http,
        AppDbContext db,
        ApiSettings settings,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new ErrorResponse("Informe pelo menos um campo para atualizar."));
        if (request.Nome is null &&
            request.Email is null &&
            string.IsNullOrWhiteSpace(request.Status) &&
            string.IsNullOrWhiteSpace(request.Role))
            return Results.BadRequest(new ErrorResponse("Informe pelo menos um campo para atualizar."));

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
            return Results.NotFound(new ErrorResponse("Usuário não encontrado."));

        var selfClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSelf = Guid.TryParse(selfClaim, out var selfId) && selfId == id;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (request.Status is not ("Pendente" or "Aprovado" or "Rejeitado"))
                return Results.BadRequest(new ErrorResponse("Status inválido."));
            if (isSelf && request.Status != "Aprovado")
                return Results.BadRequest(new ErrorResponse("Não é permitido alterar seu próprio Status para fora de Aprovado."));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (request.Role is not ("Admin" or "User"))
                return Results.BadRequest(new ErrorResponse("Role inválida."));
            if (isSelf && request.Role != "Admin")
                return Results.BadRequest(new ErrorResponse("Não é permitido alterar sua própria Role para fora de Admin."));
        }

        string? novoEmail = null;
        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                return Results.BadRequest(new ErrorResponse("E-mail não pode estar vazio."));
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

            if (email != usuario.Email)
            {
                if (usuario.Email == settings.AdminEmail.Trim().ToLowerInvariant())
                    return Results.BadRequest(new ErrorResponse(
                        "O e-mail do administrador principal é definido na configuração do servidor."));
                if (await db.Usuarios.AnyAsync(u => u.Id != id && u.Email == email, ct))
                    return Results.Conflict(new ErrorResponse("Este e-mail já está cadastrado."));
                novoEmail = email;
            }
        }

        if (request.Nome is not null)
        {
            var nome = request.Nome.Trim();
            if (string.IsNullOrEmpty(nome))
                return Results.BadRequest(new ErrorResponse("Nome não pode estar vazio."));
            if (nome.Length > 120)
                return Results.BadRequest(new ErrorResponse("Nome excede 120 caracteres."));
            usuario.Nome = nome;
        }

        if (novoEmail is not null)
            usuario.Email = novoEmail;
        if (!string.IsNullOrWhiteSpace(request.Status))
            usuario.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Role))
            usuario.Role = request.Role;

        usuario.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToAdminDto(usuario));
    }

    private static AdminUsuarioDto ToAdminDto(Usuario u) =>
        new(u.Id, u.Nome, u.Email, u.Role, u.Status, DateTime.SpecifyKind(u.CriadoEm, DateTimeKind.Utc));
}
