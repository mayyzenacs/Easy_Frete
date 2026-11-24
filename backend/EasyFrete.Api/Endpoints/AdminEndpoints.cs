using System.Security.Claims;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFrete.Api.Endpoints;

public static class AdminEndpoints
{
    private const int MinMotivoLength = 5;
    private const int MaxMotivoLength = 500;

    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        // Usuários
        api.MapGet("/admin/usuarios", ListarUsuarios)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPatch("/admin/usuarios/{id:guid}", AtualizarUsuario)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapDelete("/admin/usuarios/{id:guid}", ExcluirUsuario)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        // Solicitações de exclusão (Admin solicita -> Master aprova ou recusa)
        api.MapGet("/admin/exclusoes", ListarSolicitacoesExclusao)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/admin/exclusoes", CriarSolicitacaoExclusao)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/admin/exclusoes/{id:guid}/aprovar", AprovarSolicitacaoExclusao)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/admin/exclusoes/{id:guid}/recusar", RecusarSolicitacaoExclusao)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        // Notificações
        api.MapGet("/admin/notificacoes", ListarNotificacoes)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPatch("/admin/notificacoes/{id:guid}/lida", MarcarNotificacaoComoLida)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);

        api.MapPost("/admin/notificacoes/marcar-todas-lidas", MarcarTodasNotificacoesComoLidas)
            .RequireAuthorization(p => p.RequireRole(UserRoles.Master, UserRoles.Admin))
            .RequireRateLimiting(RateLimitingConfiguration.ApiPolicy);
    }

    private static async Task<IResult> ListarUsuarios(
        string? status,
        string? role,
        string? q,
        AppDbContext db,
        CancellationToken ct)
    {
        var query = db.Usuarios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(u => u.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role.Trim());

        if (!string.IsNullOrWhiteSpace(q))
        {
            var termo = q.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Nome.ToLower().Contains(termo) ||
                u.Email.ToLower().Contains(termo));
        }

        var usuarios = await query
            .OrderBy(u => u.Status == UserStatuses.Pendente ? 0 : u.Role == UserRoles.Master ? 1 : u.Role == UserRoles.Admin ? 2 : 3)
            .ThenBy(u => u.Nome)
            .ToListAsync(ct);

        var idsComPedidoExclusao = await db.SolicitacoesExclusao
            .AsNoTracking()
            .Where(s => s.Status == DeletionRequestStatuses.Pendente && s.UsuarioAlvoId != null)
            .Select(s => s.UsuarioAlvoId!.Value)
            .ToHashSetAsync(ct);

        var resultado = usuarios
            .Select(u => ToAdminDto(u, idsComPedidoExclusao.Contains(u.Id)))
            .ToList();

        return Results.Ok(resultado);
    }

    private static async Task<IResult> AtualizarUsuario(
        Guid id,
        AdminUsuarioPatchRequest? request,
        HttpContext http,
        AppDbContext db,
        ApiSettings settings,
        TimeProvider clock,
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

        var (selfId, selfRole) = GetCallerIdentity(http);
        var isSelf = selfId == id;
        var callerIsMaster = selfRole == UserRoles.Master;
        var targetIsMaster = usuario.Role == UserRoles.Master;
        var targetIsAdmin = usuario.Role == UserRoles.Admin;

        // Regras de hierarquia:
        // 1) Conta Master: apenas a própria Master pode alterar o próprio Nome (e-mail, role e status sempre protegidos).
        if (targetIsMaster && !callerIsMaster)
        {
            return Results.Json(
                new ErrorResponse("Apenas a conta Master pode visualizar ou alterar seus próprios dados permitidos."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        // 2) Admin comum nunca altera outro Admin nem a própria conta via painel admin.
        if (!callerIsMaster && targetIsAdmin)
        {
            return Results.Json(
                new ErrorResponse("Administradores comuns podem gerenciar apenas contas de Usuário."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (request.Status is not (UserStatuses.Pendente or UserStatuses.Aprovado or UserStatuses.Rejeitado))
                return Results.BadRequest(new ErrorResponse("Status inválido."));
            if ((isSelf || targetIsMaster) && request.Status != UserStatuses.Aprovado)
                return Results.BadRequest(new ErrorResponse("Não é permitido alterar o Status da conta Master ou da própria conta para fora de Aprovado."));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (request.Role == UserRoles.Master)
            {
                if (!targetIsMaster)
                    return Results.BadRequest(new ErrorResponse("A Role Master é definida exclusivamente pela configuração do servidor."));
            }
            else if (request.Role is not (UserRoles.Admin or UserRoles.User))
            {
                return Results.BadRequest(new ErrorResponse("Role inválida."));
            }

            if (targetIsMaster && request.Role != UserRoles.Master)
                return Results.BadRequest(new ErrorResponse("Não é permitido alterar a Role da conta Master."));

            if (!callerIsMaster && request.Role != usuario.Role)
                return Results.Json(
                    new ErrorResponse("Apenas a conta Master pode alterar o perfil de acesso de um usuário."),
                    statusCode: StatusCodes.Status403Forbidden);

            if (isSelf && !targetIsMaster && request.Role != selfRole)
                return Results.BadRequest(new ErrorResponse("Não é permitido alterar sua própria Role."));
        }

        string? novoEmail = null;
        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                return Results.BadRequest(new ErrorResponse("E-mail não pode estar vazio."));
            if (email.Length > 180)
                return Results.BadRequest(new ErrorResponse("E-mail excede 180 caracteres."));
            if (!AuthEndpoints.IsValidEmail(email))
                return Results.BadRequest(new ErrorResponse("Informe um e-mail válido."));

            if (email != usuario.Email)
            {
                if (targetIsMaster || usuario.Email == settings.NormalizedAdminEmail)
                    return Results.BadRequest(new ErrorResponse(
                        "O e-mail da conta Master é definido na configuração do servidor."));
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

        var statusAnterior = usuario.Status;
        var roleAnterior = usuario.Role;
        var agora = clock.GetUtcNow().UtcDateTime;

        if (novoEmail is not null)
            usuario.Email = novoEmail;
        if (!string.IsNullOrWhiteSpace(request.Status))
            usuario.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Role))
            usuario.Role = request.Role;

        // Se a role mudou ou o status saiu de Aprovado, rotaciona SecurityStamp para revogar JWTs ativos imediatamente
        if (usuario.Role != roleAnterior ||
            (statusAnterior == UserStatuses.Aprovado && usuario.Status != UserStatuses.Aprovado))
        {
            usuario.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        // Quando um cadastro pendente é aprovado ou rejeitado, marca notificações de CadastroPendente como lidas
        if (statusAnterior == UserStatuses.Pendente && usuario.Status != UserStatuses.Pendente)
        {
            var notifsCadastro = await db.Notificacoes
                .Where(n => n.Tipo == NotificationTypes.CadastroPendente &&
                            n.ReferenciaId == usuario.Id &&
                            !n.Lida)
                .ToListAsync(ct);

            foreach (var notif in notifsCadastro)
            {
                notif.Lida = true;
                notif.LidaEm = agora;
            }
        }

        usuario.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);

        var temPedidoPendente = await db.SolicitacoesExclusao
            .AnyAsync(s => s.UsuarioAlvoId == usuario.Id && s.Status == DeletionRequestStatuses.Pendente, ct);

        return Results.Ok(ToAdminDto(usuario, temPedidoPendente));
    }

    private static async Task<IResult> ExcluirUsuario(
        Guid id,
        HttpContext http,
        AppDbContext db,
        ApiSettings settings,
        TimeProvider clock,
        CancellationToken ct)
    {
        var (selfId, _) = GetCallerIdentity(http);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
            return Results.NotFound(new ErrorResponse("Usuário não encontrado."));

        if (selfId == id ||
            usuario.Role == UserRoles.Master ||
            usuario.Email == settings.NormalizedAdminEmail)
        {
            return Results.BadRequest(new ErrorResponse("A conta Master não pode ser excluída."));
        }

        var master = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == selfId, ct);
        var masterNome = master?.Nome ?? "Master";
        var agora = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await ResolverPedidosExclusaoPendentesAoExcluirUsuarioAsync(
            db, usuario, selfId, masterNome, null, agora, ct);

        await MarcarNotificacoesCadastroComoLidasAsync(db, usuario.Id, agora, ct);

        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new MessageResponse($"Usuário '{usuario.Nome}' excluído permanentemente."));
    }

    private static async Task<IResult> ListarSolicitacoesExclusao(
        string? status,
        HttpContext http,
        AppDbContext db,
        CancellationToken ct)
    {
        var (selfId, selfRole) = GetCallerIdentity(http);
        var query = db.SolicitacoesExclusao.AsNoTracking();

        if (selfRole != UserRoles.Master)
        {
            query = query.Where(s => s.SolicitadoPorId == selfId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status.Trim());
        }

        var lista = await query
            .OrderBy(s => s.Status == DeletionRequestStatuses.Pendente ? 0 : 1)
            .ThenByDescending(s => s.CriadoEm)
            .Select(s => ToSolicitacaoDto(s))
            .ToListAsync(ct);

        return Results.Ok(lista);
    }

    private static async Task<IResult> CriarSolicitacaoExclusao(
        CriarSolicitacaoExclusaoRequest? request,
        HttpContext http,
        AppDbContext db,
        ApiSettings settings,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (request is null || request.UsuarioAlvoId == Guid.Empty)
            return Results.BadRequest(new ErrorResponse("Informe o usuário para solicitar exclusão."));

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < MinMotivoLength)
            return Results.BadRequest(new ErrorResponse($"Informe um motivo com pelo menos {MinMotivoLength} caracteres."));
        if (motivo.Length > MaxMotivoLength)
            return Results.BadRequest(new ErrorResponse($"O motivo excede o máximo de {MaxMotivoLength} caracteres."));

        var (selfId, _) = GetCallerIdentity(http);
        if (selfId == request.UsuarioAlvoId)
            return Results.BadRequest(new ErrorResponse("Não é permitido solicitar a exclusão da própria conta."));

        var alvo = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UsuarioAlvoId, ct);
        if (alvo is null)
            return Results.NotFound(new ErrorResponse("Usuário não encontrado."));

        if (alvo.Role == UserRoles.Master ||
            alvo.Email == settings.NormalizedAdminEmail ||
            alvo.Role == UserRoles.Admin)
        {
            return Results.Json(
                new ErrorResponse("Administradores comuns podem solicitar exclusão apenas de contas com perfil Usuário."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var jaPendente = await db.SolicitacoesExclusao.AnyAsync(
            s => s.UsuarioAlvoId == alvo.Id && s.Status == DeletionRequestStatuses.Pendente, ct);
        if (jaPendente)
        {
            return Results.Conflict(new ErrorResponse("Já existe uma solicitação de exclusão pendente para este usuário."));
        }

        var solicitante = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == selfId, ct);
        var solicitanteNome = solicitante?.Nome ?? "Administrador";
        var agora = clock.GetUtcNow().UtcDateTime;

        var solicitacao = new SolicitacaoExclusao
        {
            UsuarioAlvoId = alvo.Id,
            UsuarioAlvoNome = alvo.Nome,
            UsuarioAlvoEmail = alvo.Email,
            SolicitadoPorId = selfId,
            SolicitadoPorNome = solicitanteNome,
            Motivo = motivo,
            Status = DeletionRequestStatuses.Pendente,
            CriadoEm = agora
        };

        db.SolicitacoesExclusao.Add(solicitacao);

        var masterId = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Role == UserRoles.Master && u.Status == UserStatuses.Aprovado)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (masterId.HasValue)
        {
            db.Notificacoes.Add(new Notificacao
            {
                DestinatarioId = masterId.Value,
                Tipo = NotificationTypes.SolicitacaoExclusao,
                Titulo = "Solicitação de exclusão",
                Mensagem = $"{solicitanteNome} solicitou a exclusão de {alvo.Nome} ({alvo.Email}).",
                ReferenciaId = solicitacao.Id,
                Lida = false,
                CriadoEm = agora
            });
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/admin/exclusoes/{solicitacao.Id}", ToSolicitacaoDto(solicitacao));
    }

    private static async Task<IResult> AprovarSolicitacaoExclusao(
        Guid id,
        DecidirSolicitacaoExclusaoRequest? request,
        HttpContext http,
        AppDbContext db,
        ApiSettings settings,
        TimeProvider clock,
        CancellationToken ct)
    {
        var observacao = string.IsNullOrWhiteSpace(request?.Observacao) ? null : request.Observacao.Trim();
        if (observacao is not null && observacao.Length > MaxMotivoLength)
            return Results.BadRequest(new ErrorResponse($"A observação excede {MaxMotivoLength} caracteres."));

        var solicitacao = await db.SolicitacoesExclusao.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (solicitacao is null)
            return Results.NotFound(new ErrorResponse("Solicitação de exclusão não encontrada."));

        if (solicitacao.Status != DeletionRequestStatuses.Pendente)
            return Results.Conflict(new ErrorResponse("Esta solicitação já foi decidida."));

        var (selfId, _) = GetCallerIdentity(http);
        var master = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == selfId, ct);
        var masterNome = master?.Nome ?? "Master";
        var agora = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (solicitacao.UsuarioAlvoId.HasValue)
        {
            var alvo = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == solicitacao.UsuarioAlvoId.Value, ct);
            if (alvo is not null)
            {
                if (alvo.Role == UserRoles.Master || alvo.Email == settings.NormalizedAdminEmail || alvo.Id == selfId)
                {
                    return Results.BadRequest(new ErrorResponse("A conta Master não pode ser excluída."));
                }

                await MarcarNotificacoesCadastroComoLidasAsync(db, alvo.Id, agora, ct);
                db.Usuarios.Remove(alvo);
            }
        }

        solicitacao.UsuarioAlvoId = null;
        solicitacao.Status = DeletionRequestStatuses.Aprovado;
        solicitacao.DecididoPorId = selfId;
        solicitacao.DecididoPorNome = masterNome;
        solicitacao.ObservacaoDecisao = observacao;
        solicitacao.DecididoEm = agora;

        await MarcarNotificacoesSolicitacaoComoLidasAsync(db, solicitacao.Id, agora, ct);
        await NotificarAdminSobreDecisaoAsync(db, solicitacao, aprovado: true, agora, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(ToSolicitacaoDto(solicitacao));
    }

    private static async Task<IResult> RecusarSolicitacaoExclusao(
        Guid id,
        DecidirSolicitacaoExclusaoRequest? request,
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var observacao = string.IsNullOrWhiteSpace(request?.Observacao) ? null : request.Observacao.Trim();
        if (observacao is not null && observacao.Length > MaxMotivoLength)
            return Results.BadRequest(new ErrorResponse($"A observação excede {MaxMotivoLength} caracteres."));

        var solicitacao = await db.SolicitacoesExclusao.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (solicitacao is null)
            return Results.NotFound(new ErrorResponse("Solicitação de exclusão não encontrada."));

        if (solicitacao.Status != DeletionRequestStatuses.Pendente)
            return Results.Conflict(new ErrorResponse("Esta solicitação já foi decidida."));

        var (selfId, _) = GetCallerIdentity(http);
        var master = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == selfId, ct);
        var masterNome = master?.Nome ?? "Master";
        var agora = clock.GetUtcNow().UtcDateTime;

        solicitacao.Status = DeletionRequestStatuses.Recusado;
        solicitacao.DecididoPorId = selfId;
        solicitacao.DecididoPorNome = masterNome;
        solicitacao.ObservacaoDecisao = observacao;
        solicitacao.DecididoEm = agora;

        await MarcarNotificacoesSolicitacaoComoLidasAsync(db, solicitacao.Id, agora, ct);
        await NotificarAdminSobreDecisaoAsync(db, solicitacao, aprovado: false, agora, ct);

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToSolicitacaoDto(solicitacao));
    }

    private static async Task<IResult> ListarNotificacoes(
        HttpContext http,
        AppDbContext db,
        CancellationToken ct)
    {
        var (selfId, _) = GetCallerIdentity(http);

        var naoLidas = await db.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.DestinatarioId == selfId && !n.Lida, ct);

        var itens = await db.Notificacoes
            .AsNoTracking()
            .Where(n => n.DestinatarioId == selfId)
            .OrderByDescending(n => n.CriadoEm)
            .Take(50)
            .Select(n => new NotificacaoDto(
                n.Id,
                n.Tipo,
                n.Titulo,
                n.Mensagem,
                n.ReferenciaId,
                n.Lida,
                DateTime.SpecifyKind(n.CriadoEm, DateTimeKind.Utc),
                n.LidaEm.HasValue ? DateTime.SpecifyKind(n.LidaEm.Value, DateTimeKind.Utc) : null))
            .ToListAsync(ct);

        return Results.Ok(new NotificacoesResponse(naoLidas, itens));
    }

    private static async Task<IResult> MarcarNotificacaoComoLida(
        Guid id,
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var (selfId, _) = GetCallerIdentity(http);

        var notif = await db.Notificacoes
            .FirstOrDefaultAsync(n => n.Id == id && n.DestinatarioId == selfId, ct);
        if (notif is null)
            return Results.NotFound(new ErrorResponse("Notificação não encontrada."));

        if (!notif.Lida)
        {
            notif.Lida = true;
            notif.LidaEm = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new MessageResponse("Notificação marcada como lida."));
    }

    private static async Task<IResult> MarcarTodasNotificacoesComoLidas(
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var (selfId, _) = GetCallerIdentity(http);
        var agora = clock.GetUtcNow().UtcDateTime;

        var naoLidas = await db.Notificacoes
            .Where(n => n.DestinatarioId == selfId && !n.Lida)
            .ToListAsync(ct);

        foreach (var n in naoLidas)
        {
            n.Lida = true;
            n.LidaEm = agora;
        }

        if (naoLidas.Count > 0)
            await db.SaveChangesAsync(ct);

        return Results.Ok(new MessageResponse("Todas as notificações foram marcadas como lidas."));
    }

    private static async Task ResolverPedidosExclusaoPendentesAoExcluirUsuarioAsync(
        AppDbContext db,
        Usuario alvo,
        Guid masterId,
        string masterNome,
        string? observacao,
        DateTime agora,
        CancellationToken ct)
    {
        var pedidosPendentes = await db.SolicitacoesExclusao
            .Where(s => s.UsuarioAlvoId == alvo.Id && s.Status == DeletionRequestStatuses.Pendente)
            .ToListAsync(ct);

        foreach (var pedido in pedidosPendentes)
        {
            pedido.UsuarioAlvoId = null;
            pedido.Status = DeletionRequestStatuses.Aprovado;
            pedido.DecididoPorId = masterId;
            pedido.DecididoPorNome = masterNome;
            pedido.ObservacaoDecisao = observacao;
            pedido.DecididoEm = agora;

            await MarcarNotificacoesSolicitacaoComoLidasAsync(db, pedido.Id, agora, ct);
            await NotificarAdminSobreDecisaoAsync(db, pedido, aprovado: true, agora, ct);
        }

        // Desvincula pedidos históricos já decididos para manter o histórico intacto após a exclusão
        var pedidosHistoricos = await db.SolicitacoesExclusao
            .Where(s => s.UsuarioAlvoId == alvo.Id && s.Status != DeletionRequestStatuses.Pendente)
            .ToListAsync(ct);
        foreach (var hist in pedidosHistoricos)
        {
            hist.UsuarioAlvoId = null;
        }
    }

    private static async Task NotificarAdminSobreDecisaoAsync(
        AppDbContext db,
        SolicitacaoExclusao solicitacao,
        bool aprovado,
        DateTime agora,
        CancellationToken ct)
    {
        if (!solicitacao.SolicitadoPorId.HasValue)
            return;

        var adminExiste = await db.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == solicitacao.SolicitadoPorId.Value && u.Status == UserStatuses.Aprovado, ct);
        if (!adminExiste)
            return;

        var sufixoObs = string.IsNullOrWhiteSpace(solicitacao.ObservacaoDecisao)
            ? string.Empty
            : $" Observação: \"{solicitacao.ObservacaoDecisao}\"";

        db.Notificacoes.Add(new Notificacao
        {
            DestinatarioId = solicitacao.SolicitadoPorId.Value,
            Tipo = aprovado ? NotificationTypes.ExclusaoAprovada : NotificationTypes.ExclusaoRecusada,
            Titulo = aprovado ? "Exclusão aprovada pela Master" : "Exclusão recusada pela Master",
            Mensagem = aprovado
                ? $"A conta de {solicitacao.UsuarioAlvoNome} ({solicitacao.UsuarioAlvoEmail}) foi excluída conforme sua solicitação."
                : $"Sua solicitação para excluir {solicitacao.UsuarioAlvoNome} ({solicitacao.UsuarioAlvoEmail}) foi recusada.{sufixoObs}",
            ReferenciaId = solicitacao.Id,
            Lida = false,
            CriadoEm = agora
        });
    }

    private static async Task MarcarNotificacoesSolicitacaoComoLidasAsync(
        AppDbContext db,
        Guid solicitacaoId,
        DateTime agora,
        CancellationToken ct)
    {
        var notifs = await db.Notificacoes
            .Where(n => n.Tipo == NotificationTypes.SolicitacaoExclusao &&
                        n.ReferenciaId == solicitacaoId &&
                        !n.Lida)
            .ToListAsync(ct);

        foreach (var n in notifs)
        {
            n.Lida = true;
            n.LidaEm = agora;
        }
    }

    private static async Task MarcarNotificacoesCadastroComoLidasAsync(
        AppDbContext db,
        Guid usuarioId,
        DateTime agora,
        CancellationToken ct)
    {
        var notifs = await db.Notificacoes
            .Where(n => n.Tipo == NotificationTypes.CadastroPendente &&
                        n.ReferenciaId == usuarioId &&
                        !n.Lida)
            .ToListAsync(ct);

        foreach (var n in notifs)
        {
            n.Lida = true;
            n.LidaEm = agora;
        }
    }

    private static (Guid UserId, string Role) GetCallerIdentity(HttpContext http)
    {
        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(idClaim, out var userId);
        var role = http.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return (userId, role);
    }

    private static AdminUsuarioDto ToAdminDto(Usuario u, bool possuiPedidoExclusaoPendente) =>
        new(
            u.Id,
            u.Nome,
            u.Email,
            u.Role,
            u.Status,
            DateTime.SpecifyKind(u.CriadoEm, DateTimeKind.Utc),
            possuiPedidoExclusaoPendente);

    private static SolicitacaoExclusaoDto ToSolicitacaoDto(SolicitacaoExclusao s) =>
        new(
            s.Id,
            s.UsuarioAlvoId,
            s.UsuarioAlvoNome,
            s.UsuarioAlvoEmail,
            s.SolicitadoPorId,
            s.SolicitadoPorNome,
            s.Motivo,
            s.Status,
            DateTime.SpecifyKind(s.CriadoEm, DateTimeKind.Utc),
            s.DecididoPorId,
            s.DecididoPorNome,
            s.ObservacaoDecisao,
            s.DecididoEm.HasValue ? DateTime.SpecifyKind(s.DecididoEm.Value, DateTimeKind.Utc) : null);
}
