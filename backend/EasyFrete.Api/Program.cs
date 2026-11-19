using System.Text;
using System.Text.Json;
using System.Security.Claims;
using DotNetEnv;
using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Endpoints;
using EasyFrete.Api.Models;
using EasyFrete.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var settings = ApiSettings.FromEnvironment();

var dbPath = Path.GetFullPath(settings.SqlitePath);
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<IMagaluFreightService, MagaluFreightService>();

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
    });

builder.Services.AddAuthorization();
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

    var adminEmail = settings.AdminEmail.Trim().ToLowerInvariant();
    var admin = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == adminEmail);
    var passwordHasher = new PasswordHasher<Usuario>();

    if (admin is null)
    {
        admin = new Usuario
        {
            Nome = "Administrador",
            Email = adminEmail,
            Role = "Admin",
            Status = "Aprovado",
            CriadoEm = DateTime.UtcNow
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, settings.AdminPassword);
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();
    }
    else
    {
        var verify = passwordHasher.VerifyHashedPassword(
            admin, admin.PasswordHash, settings.AdminPassword);
        var changed = verify != PasswordVerificationResult.Success &&
                      verify != PasswordVerificationResult.SuccessRehashNeeded;

        if (changed)
        {
            admin.PasswordHash =
                passwordHasher.HashPassword(admin, settings.AdminPassword);
        }

        if (admin.Role != "Admin" || admin.Status != "Aprovado")
        {
            admin.Role = "Admin";
            admin.Status = "Aprovado";
            admin.AtualizadoEm = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorResponse("Erro interno do servidor."));
}));

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapApiEndpoints();

app.Run();

public partial class Program;
