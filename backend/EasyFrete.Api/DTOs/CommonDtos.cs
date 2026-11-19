namespace EasyFrete.Api.DTOs;

public sealed record HealthResponse(string Status, DateTime Timestamp);

public sealed record ErrorResponse(string Error);
