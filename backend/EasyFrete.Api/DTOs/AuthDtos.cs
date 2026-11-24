namespace EasyFrete.Api.DTOs;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(string? Nome, string? Email, string? Password);

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ResetPasswordRequest(string? Token, string? NewPassword);

public sealed record MessageResponse(string Message);

public sealed record ForgotPasswordBlockedResponse(string Error, DateTime BloqueadoAte);

public sealed record ResetTokenValidationResponse(bool Valid, DateTime ExpiraEm);

public sealed record UsuarioResumoDto(Guid Id, string Nome, string Email, string Role, string Status);

public sealed record LoginResponse(string Token, UsuarioResumoDto Usuario);

public sealed record AdminUsuarioPatchRequest(string? Nome, string? Status, string? Role, string? Email = null);

public sealed record AdminUsuarioDto(
    Guid Id,
    string Nome,
    string Email,
    string Role,
    string Status,
    DateTime CriadoEm,
    bool PossuiPedidoExclusaoPendente = false);

public sealed record CriarSolicitacaoExclusaoRequest(Guid UsuarioAlvoId, string? Motivo);

public sealed record DecidirSolicitacaoExclusaoRequest(string? Observacao = null);

public sealed record SolicitacaoExclusaoDto(
    Guid Id,
    Guid? UsuarioAlvoId,
    string UsuarioAlvoNome,
    string UsuarioAlvoEmail,
    Guid? SolicitadoPorId,
    string SolicitadoPorNome,
    string Motivo,
    string Status,
    DateTime CriadoEm,
    Guid? DecididoPorId,
    string? DecididoPorNome,
    string? ObservacaoDecisao,
    DateTime? DecididoEm);

public sealed record NotificacaoDto(
    Guid Id,
    string Tipo,
    string Titulo,
    string Mensagem,
    Guid? ReferenciaId,
    bool Lida,
    DateTime CriadoEm,
    DateTime? LidaEm);

public sealed record NotificacoesResponse(int NaoLidas, IReadOnlyList<NotificacaoDto> Itens);
