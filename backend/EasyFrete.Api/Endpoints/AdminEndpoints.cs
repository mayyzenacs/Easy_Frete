using System.Security.Claims;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
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
            .Select(u => new UsuarioResumoDto(u.Id, u.Nome, u.Email, u.Role, u.Status))
            .ToListAsync(ct);

        return Results.Ok(usuarios);
    }

    private static async Task<IResult> AtualizarUsuario(
        Guid id,
        AdminUsuarioPatchRequest? request,
        HttpContext http,
        AppDbContext db,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new ErrorResponse("Informe pelo menos um campo para atualizar."));
        if (request.Nome is null &&
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

        if (request.Nome is not null)
        {
            var nome = request.Nome.Trim();
            if (string.IsNullOrEmpty(nome))
                return Results.BadRequest(new ErrorResponse("Nome não pode estar vazio."));
            if (nome.Length > 120)
                return Results.BadRequest(new ErrorResponse("Nome excede 120 caracteres."));
            usuario.Nome = nome;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
            usuario.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Role))
            usuario.Role = request.Role;

        usuario.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new UsuarioResumoDto(
            usuario.Id, usuario.Nome, usuario.Email, usuario.Role, usuario.Status));
    }
}
