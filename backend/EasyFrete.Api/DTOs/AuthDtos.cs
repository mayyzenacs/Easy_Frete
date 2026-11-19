namespace EasyFrete.Api.DTOs;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(string? Nome, string? Email, string? Password);

public sealed record UsuarioResumoDto(Guid Id, string Nome, string Email, string Role, string Status);

public sealed record LoginResponse(string Token, UsuarioResumoDto Usuario);

public sealed record AdminUsuarioPatchRequest(string? Nome, string? Status, string? Role);
