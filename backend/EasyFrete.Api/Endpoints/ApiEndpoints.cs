using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using EasyFrete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EasyFrete.Api.Endpoints;

public static class ApiEndpoints
{
    private const int MaxLote = 5000;
    private const int TamanhoChunk = 500;

    public static void MapApiEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<ApiSettings>();
        var api = app.MapGroup("/api/v1");

        api.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy", DateTime.UtcNow)))
            .AllowAnonymous();

        api.MapPost("/auth/register", (RegisterRequest? request, AppDbContext db, CancellationToken ct) =>
                Register(request, db, settings, ct))
            .AllowAnonymous();

        api.MapPost("/auth/login", (LoginRequest? request, AppDbContext db, CancellationToken ct) =>
                Login(request, db, settings, ct))
            .AllowAnonymous();

        api.MapGet("/produtos", ListarProdutos)
            .RequireAuthorization();

        api.MapGet("/produtos/{sku}", GetProduto)
            .RequireAuthorization();

        api.MapPost("/frete/magalu/calcular", Calcular)
            .AddEndpointFilter(JwtOrApiKeyFilter(settings.ExternalCalcApiKey));

        // Aceita tanto X-Api-Key (Google Sheets) quanto JWT Bearer (Importação pela UI)
        api.MapPost("/produtos/sync-lote", SyncLote)
            .AddEndpointFilter(JwtOrApiKeyFilter(settings.SyncApiKey));

        api.MapPost("/tarifas/magalu/sync", SyncTarifasMagalu)
            .AddEndpointFilter(JwtOrApiKeyFilter(settings.SyncApiKey));
    }

    // ---------- Autorização ----------

    private static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> JwtOrApiKeyFilter(string expectedKey) =>
        async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            if (http.User.Identity?.IsAuthenticated == true || ApiKeyAuth.IsValid(http, expectedKey))
            {
                return await next(ctx);
            }

            return ApiKeyAuth.Unauthorized("Não autorizado. Envie um JWT Bearer válido ou o header X-Api-Key.");
        };

    // ---------- Handlers de Autenticação (Guid + BCrypt) ----------

    private static async Task<IResult> Register(
        RegisterRequest? request,
        AppDbContext db,
        ApiSettings settings,
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
        var senha = request.Password;

        if (senha.Length < 6)
        {
            return Results.BadRequest(new ErrorResponse("A senha deve ter pelo menos 6 caracteres."));
        }

        var jaExiste = await db.Usuarios.AnyAsync(u => u.Email == email, ct);
        if (jaExiste)
        {
            return Results.Conflict(new ErrorResponse("Este e-mail já está cadastrado."));
        }

        var novoUsuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Email = email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 12),
            CriadoEm = DateTime.UtcNow
        };

        db.Usuarios.Add(novoUsuario);
        await db.SaveChangesAsync(ct);

        return Results.Ok(GerarTokenResponse(novoUsuario, settings));
    }

    private static async Task<IResult> Login(
        LoginRequest? request,
        AppDbContext db,
        ApiSettings settings,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.BadRequest(new ErrorResponse("Informe e-mail e senha."));
        }

        var emailInput = request.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == emailInput, ct);

        if (usuario is not null)
        {
            var senhaValida = BCrypt.Net.BCrypt.Verify(request.Password, usuario.SenhaHash);
            if (!senhaValida)
            {
                return Results.Json(new ErrorResponse("Credenciais inválidas."), statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(GerarTokenResponse(usuario, settings));
        }

        // Fallback para o Admin definido no .env (cria automaticamente no banco com Guid e BCrypt no 1º login)
        var emailOk = ApiKeyAuth.SecureEquals(emailInput, settings.AdminEmail.Trim().ToLowerInvariant());
        var senhaOk = ApiKeyAuth.SecureEquals(request.Password, settings.AdminPassword);

        if (!(emailOk & senhaOk))
        {
            return Results.Json(new ErrorResponse("Credenciais inválidas."), statusCode: StatusCodes.Status401Unauthorized);
        }

        var nomeExtraido = ExtrairNomeDoEmail(settings.AdminEmail);
        var adminUser = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nomeExtraido,
            Email = settings.AdminEmail.Trim().ToLowerInvariant(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(settings.AdminPassword, workFactor: 12),
            CriadoEm = DateTime.UtcNow
        };

        db.Usuarios.Add(adminUser);
        await db.SaveChangesAsync(ct);

        return Results.Ok(GerarTokenResponse(adminUser, settings));
    }

    private static LoginResponse GerarTokenResponse(Usuario usuario, ApiSettings settings)
    {
        var agora = DateTime.UtcNow;
        var expira = agora.AddHours(12);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", usuario.Id.ToString()),
                new Claim("name", usuario.Nome),
                new Claim("email", usuario.Email),
                new Claim("role", "User"),
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
        return new LoginResponse(usuario.Id, usuario.Nome, usuario.Email, token, expira);
    }

    private static string ExtrairNomeDoEmail(string email)
    {
        var prefixo = email.Split('@')[0].Trim();
        if (string.IsNullOrEmpty(prefixo)) return "Admin";
        return char.ToUpperInvariant(prefixo[0]) + prefixo[1..].ToLowerInvariant();
    }

    // ---------- Handlers de Produtos e Cálculo ----------

    private static async Task<IResult> ListarProdutos(AppDbContext db, CancellationToken ct)
    {
        var lista = await db.Produtos
            .AsNoTracking()
            .OrderByDescending(p => p.AtualizadoEm)
            .Take(100)
            .Select(p => new ProdutoResponse(p.Sku, p.Nome, p.PesoRealKg, p.ComprimentoCm, p.LarguraCm, p.AlturaCm, p.AtualizadoEm, p.LinkDrive))
            .ToListAsync(ct);

        return Results.Ok(lista);
    }

    private static async Task<IResult> GetProduto(string sku, AppDbContext db, CancellationToken ct)
    {
        var chave = sku.Trim();
        var produto = await db.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Sku == chave, ct);

        if (produto is null)
        {
            return Results.NotFound(new ErrorResponse($"Produto '{chave}' não encontrado."));
        }

        return Results.Ok(new ProdutoResponse(
            produto.Sku,
            produto.Nome,
            produto.PesoRealKg,
            produto.ComprimentoCm,
            produto.LarguraCm,
            produto.AlturaCm,
            produto.AtualizadoEm,
            produto.LinkDrive));
    }

    private static async Task<IResult> Calcular(
        CalcularFreteRequest request,
        IMagaluFreightService service,
        CancellationToken ct)
    {
        var resultado = await service.CalcularAsync(request, ct);

        if (resultado.Success)
        {
            return Results.Ok(resultado.Data);
        }

        return Results.Json(new ErrorResponse(resultado.Error ?? "Erro ao calcular frete."), statusCode: resultado.StatusCode);
    }

    private static async Task<IResult> SyncLote(SyncLoteRequest? request, AppDbContext db, CancellationToken ct)
    {
        var produtos = request?.Produtos;
        if (request is null || produtos is null || produtos.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("Envie { origem, produtos: [...] } com ao menos um produto."));
        }

        if (produtos.Count > MaxLote)
        {
            return Results.BadRequest(new ErrorResponse($"Lote excede o máximo de {MaxLote} produtos."));
        }

        var origem = string.IsNullOrWhiteSpace(request.Origem) ? "DESCONHECIDA" : request.Origem.Trim();
        var erros = new List<string>();
        var validos = new Dictionary<string, SyncProdutoItem>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < produtos.Count; i++)
        {
            var item = produtos[i];
            if (item is null)
            {
                erros.Add($"Item {i}: objeto nulo.");
                continue;
            }

            var sku = item.Sku?.Trim();
            if (string.IsNullOrEmpty(sku))
            {
                erros.Add($"Item {i}: sku vazio.");
                continue;
            }

            if (sku.Length > 64)
            {
                erros.Add($"Item {i}: sku '{sku[..20]}...' excede 64 caracteres.");
                continue;
            }

            if (item.PesoBruto < 0m || item.AlturaCm < 0m || item.LarguraCm < 0m || item.ComprimentoCm < 0m)
            {
                erros.Add($"Item {i}: sku '{sku}' com peso/medida negativa.");
                continue;
            }

            if (validos.ContainsKey(sku))
            {
                erros.Add($"Item {i}: sku '{sku}' duplicado no lote; prevaleceu o último.");
            }

            validos[sku] = item;
        }

        var agora = DateTime.UtcNow;
        var inseridos = 0;
        var atualizados = 0;

        var chaves = validos.Keys.ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        for (var offset = 0; offset < chaves.Count; offset += TamanhoChunk)
        {
            var fatia = chaves.Skip(offset).Take(TamanhoChunk).ToList();
            var existentes = await db.Produtos
                .Where(p => fatia.Contains(p.Sku))
                .ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var sku in fatia)
            {
                var item = validos[sku];
                var nome = string.IsNullOrWhiteSpace(item.Nome) ? sku : item.Nome.Trim();
                if (nome.Length > 255) nome = nome[..255];

                if (existentes.TryGetValue(sku, out var existente))
                {
                    existente.Nome = nome;
                    existente.PesoRealKg = Math.Round(item.PesoBruto, 3);
                    existente.AlturaCm = Math.Round(item.AlturaCm, 2);
                    existente.LarguraCm = Math.Round(item.LarguraCm, 2);
                    existente.ComprimentoCm = Math.Round(item.ComprimentoCm, 2);
                    existente.LinkDrive = string.IsNullOrWhiteSpace(item.LinkDrive) ? null : item.LinkDrive.Trim();
                    existente.AtualizadoEm = agora;
                    atualizados++;
                }
                else
                {
                    db.Produtos.Add(new Produto
                    {
                        Sku = sku,
                        Nome = nome,
                        PesoRealKg = Math.Round(item.PesoBruto, 3),
                        AlturaCm = Math.Round(item.AlturaCm, 2),
                        LarguraCm = Math.Round(item.LarguraCm, 2),
                        ComprimentoCm = Math.Round(item.ComprimentoCm, 2),
                        LinkDrive = string.IsNullOrWhiteSpace(item.LinkDrive) ? null : item.LinkDrive.Trim(),
                        AtualizadoEm = agora
                    });
                    inseridos++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);

        var ignorados = produtos.Count - (inseridos + atualizados);
        return Results.Ok(new SyncLoteResponse(
            origem,
            produtos.Count,
            inseridos,
            atualizados,
            ignorados < 0 ? 0 : ignorados,
            erros,
            agora));
    }

    private static async Task<IResult> SyncTarifasMagalu(
        SyncTarifasMagaluRequest? request,
        AppDbContext db,
        CancellationToken ct)
    {
        var tarifas = request?.Tarifas;
        if (request is null || tarifas is null || tarifas.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse(
                "Envie { origem, tarifas: [...] } com ao menos uma tarifa."));
        }

        var idsRecebidos = new HashSet<int>();
        foreach (var tarifa in tarifas)
        {
            var valoresMonetarios = new[]
            {
                tarifa.De79a199SemDesconto,
                tarifa.De79a199ComDesconto97,
                tarifa.De79a199Fulfillment,
                tarifa.Acima199SemDesconto,
                tarifa.Acima199ComDesconto97,
                tarifa.Acima199Fulfillment
            };

            if (tarifa.Id <= 0)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id inválido: {tarifa.Id}."));
            }

            if (!idsRecebidos.Add(tarifa.Id))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id duplicado no lote: {tarifa.Id}."));
            }

            if (string.IsNullOrWhiteSpace(tarifa.DescricaoFaixa))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui DescricaoFaixa vazia."));
            }

            if (tarifa.PesoMaxKg <= tarifa.PesoMinKg)
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui PesoMaxKg inválido."));
            }

            if (valoresMonetarios.Any(valor => valor < 0m))
            {
                return Results.BadRequest(new ErrorResponse(
                    $"Tarifa com Id {tarifa.Id} possui valor monetário inválido."));
            }
        }

        var origem = string.IsNullOrWhiteSpace(request.Origem)
            ? "DESCONHECIDA"
            : request.Origem.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existentes = await db.TarifasMagalu.ToListAsync(ct);
        var existentesPorId = existentes.ToDictionary(tarifa => tarifa.Id);
        var inseridos = 0;
        var atualizados = 0;

        foreach (var item in tarifas)
        {
            if (existentesPorId.TryGetValue(item.Id, out var existente))
            {
                existente.DescricaoFaixa = item.DescricaoFaixa.Trim();
                existente.PesoMinKg = item.PesoMinKg;
                existente.PesoMaxKg = item.PesoMaxKg;
                existente.De79a199SemDesconto = item.De79a199SemDesconto;
                existente.De79a199ComDesconto97 = item.De79a199ComDesconto97;
                existente.De79a199Fulfillment = item.De79a199Fulfillment;
                existente.Acima199SemDesconto = item.Acima199SemDesconto;
                existente.Acima199ComDesconto97 = item.Acima199ComDesconto97;
                existente.Acima199Fulfillment = item.Acima199Fulfillment;
                atualizados++;
            }
            else
            {
                db.TarifasMagalu.Add(new TarifaMagalu
                {
                    Id = item.Id,
                    DescricaoFaixa = item.DescricaoFaixa.Trim(),
                    PesoMinKg = item.PesoMinKg,
                    PesoMaxKg = item.PesoMaxKg,
                    De79a199SemDesconto = item.De79a199SemDesconto,
                    De79a199ComDesconto97 = item.De79a199ComDesconto97,
                    De79a199Fulfillment = item.De79a199Fulfillment,
                    Acima199SemDesconto = item.Acima199SemDesconto,
                    Acima199ComDesconto97 = item.Acima199ComDesconto97,
                    Acima199Fulfillment = item.Acima199Fulfillment
                });
                inseridos++;
            }
        }

        var removidos = existentes.Count(tarifa => !idsRecebidos.Contains(tarifa.Id));
        db.TarifasMagalu.RemoveRange(
            existentes.Where(tarifa => !idsRecebidos.Contains(tarifa.Id)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            origem,
            totalRecebido = tarifas.Count,
            inseridos,
            atualizados,
            removidos
        });
    }
}

internal static class ApiKeyAuth
{
    public const string HeaderName = "X-Api-Key";

    public static bool IsValid(HttpContext http, string expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey)) return false;
        if (!http.Request.Headers.TryGetValue(HeaderName, out var valores)) return false;
        var enviada = valores.FirstOrDefault();
        return !string.IsNullOrEmpty(enviada) && SecureEquals(enviada.Trim(), expectedKey);
    }

    public static bool SecureEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    public static IResult Unauthorized(string mensagem) =>
        Results.Json(new ErrorResponse(mensagem), statusCode: StatusCodes.Status401Unauthorized);
}