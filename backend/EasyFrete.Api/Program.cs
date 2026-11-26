using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DotNetEnv;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Endpoints;
using EasyFrete.Api.Models;
using EasyFrete.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

if (!string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        "Testing",
        StringComparison.OrdinalIgnoreCase))
{
    try
    {
        Env.TraversePath().Load();
    }
    catch (FileNotFoundException)
    {
    }
}

var builder = WebApplication.CreateBuilder(args);

var settings = ApiSettings.FromEnvironment(builder.Configuration);

var dbPath = Path.GetFullPath(settings.SqlitePath);
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<IMagaluFreightService, MagaluFreightService>();
builder.Services.AddScoped<IAmazonFreightService, AmazonFreightService>();
builder.Services.AddScoped<IPasswordResetEmailSender, MailKitPasswordResetEmailSender>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            RoleClaimType = ClaimTypes.Role,
            ValidateIssuer = true,
            ValidIssuer = settings.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = settings.JwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Extrai JWT do cookie HttpOnly quando o header Authorization não estiver presente
        // e revoga imediatamente via SecurityStamp quando a conta é alterada/excluída/redefinida
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!context.Request.Headers.ContainsKey("Authorization") &&
                    context.Request.Cookies.TryGetValue(AuthEndpoints.AuthCookieName, out var cookieToken) &&
                    !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var roleClaim = context.Principal?.FindFirstValue(ClaimTypes.Role);
                var stampClaim = context.Principal?.FindFirstValue("sst");

                if (!Guid.TryParse(idClaim, out var userId) ||
                    string.IsNullOrWhiteSpace(roleClaim) ||
                    string.IsNullOrWhiteSpace(stampClaim))
                {
                    context.Fail("Sessão inválida.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var usuario = await db.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Role, u.Status, u.SecurityStamp })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (usuario is null ||
                    usuario.Status != UserStatuses.Aprovado ||
                    !string.Equals(usuario.Role, roleClaim, StringComparison.Ordinal) ||
                    !string.Equals(usuario.SecurityStamp, stampClaim, StringComparison.Ordinal))
                {
                    context.Fail("Sessão revogada. Faça login novamente.");
                }
            }
        };
    });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.18.0.0/16"));
    options.ForwardLimit = 1;
});

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddEasyFreteRateLimiting(builder.Environment);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Database.GetMigrations().Any())
    {
        throw new InvalidOperationException(
            "Nenhuma migration encontrada. Rode 'dotnet ef migrations add InitialCreate -o Data/Migrations' " +
            "em backend/EasyFrete.Api e faça commit da pasta Data/Migrations.");
    }

    db.Database.Migrate();

    var masterEmail = settings.NormalizedAdminEmail;
    var passwordHasher = new PasswordHasher<Usuario>();

    // Garante que apenas a conta configurada em ADMIN_EMAIL possua a role Master
    var outrosMasters = await db.Usuarios
        .Where(u => u.Role == UserRoles.Master && u.Email != masterEmail)
        .ToListAsync();
    foreach (var antigo in outrosMasters)
    {
        antigo.Role = UserRoles.Admin;
        antigo.SecurityStamp = Guid.NewGuid().ToString("N");
        antigo.AtualizadoEm = DateTime.UtcNow;
    }

    var master = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == masterEmail);
    if (master is null)
    {
        master = new Usuario
        {
            Nome = "Administrador",
            Email = masterEmail,
            Role = UserRoles.Master,
            Status = UserStatuses.Aprovado,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CriadoEm = DateTime.UtcNow
        };
        master.PasswordHash = passwordHasher.HashPassword(master, settings.AdminPassword);
        db.Usuarios.Add(master);
    }
    else
    {
        var verify = passwordHasher.VerifyHashedPassword(
            master, master.PasswordHash, settings.AdminPassword);
        var passwordChanged = verify != PasswordVerificationResult.Success &&
                              verify != PasswordVerificationResult.SuccessRehashNeeded;

        if (passwordChanged)
        {
            master.PasswordHash = passwordHasher.HashPassword(master, settings.AdminPassword);
            master.SecurityStamp = Guid.NewGuid().ToString("N");
            master.AtualizadoEm = DateTime.UtcNow;
        }

        if (master.Role != UserRoles.Master || master.Status != UserStatuses.Aprovado)
        {
            master.Role = UserRoles.Master;
            master.Status = UserStatuses.Aprovado;
            master.SecurityStamp = Guid.NewGuid().ToString("N");
            master.AtualizadoEm = DateTime.UtcNow;
        }

        if (string.IsNullOrWhiteSpace(master.SecurityStamp) || master.SecurityStamp == "legacy-stamp")
        {
            master.SecurityStamp = Guid.NewGuid().ToString("N");
        }
    }

    // Garante SecurityStamp único para contas existentes migradas
    var usuariosSemStamp = await db.Usuarios
        .Where(u => u.SecurityStamp == "" || u.SecurityStamp == "legacy-stamp")
        .ToListAsync();
    foreach (var u in usuariosSemStamp)
    {
        u.SecurityStamp = Guid.NewGuid().ToString("N");
    }

    await db.SaveChangesAsync();

    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

app.UseForwardedHeaders();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorResponse("Erro interno do servidor."));
}));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapApiEndpoints();

app.Run();

public partial class Program;
