using EasyFrete.Api.Configuration;
using EasyFrete.Api.Data;
using EasyFrete.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyFrete.Api.Tests;

public sealed record CapturedResetEmail(string ToEmail, string ToName, string ResetUrl, int ExpiresInMinutes);

public sealed class FakePasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly List<CapturedResetEmail> _sent = [];

    public bool IsConfigured { get; set; } = true;
    public bool ThrowOnSend { get; set; }
    public IReadOnlyList<CapturedResetEmail> SentEmails => _sent;

    public void Clear() => _sent.Clear();

    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetUrl,
        int expiresInMinutes,
        CancellationToken ct)
    {
        if (ThrowOnSend)
        {
            throw new IOException("Falha simulada de SMTP.");
        }

        _sent.Add(new CapturedResetEmail(toEmail, toName, resetUrl, expiresInMinutes));
        return Task.CompletedTask;
    }
}

public sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public AdjustableTimeProvider(DateTimeOffset? start = null)
    {
        _utcNow = start ?? DateTimeOffset.UtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    public string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"easyfrete-tests-{Guid.NewGuid():N}.db");

    public FakePasswordResetEmailSender EmailSender { get; } = new();
    public AdjustableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 24, 20, 0, 0, TimeSpan.Zero));

    public TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ADMIN_EMAIL", "admin@easyfrete.test");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "AdminTeste123!");
        Environment.SetEnvironmentVariable("JWT_SECRET", "easyfrete-tests-jwt-secret-32-chars-minimum");
        Environment.SetEnvironmentVariable("JWT_ISSUER", "easyfrete-api");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "easyfrete-web");
        Environment.SetEnvironmentVariable("EXTERNAL_CALC_API_KEY", "external-test-key");
        Environment.SetEnvironmentVariable("SYNC_API_KEY", "sync-test-key");
        Environment.SetEnvironmentVariable("SQLITE_PATH", DatabasePath);
        Environment.SetEnvironmentVariable("FRONTEND_BASE_URL", "https://easyfrete.test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<ApiSettings>();
            services.RemoveAll<IPasswordResetEmailSender>();
            services.RemoveAll<TimeProvider>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={DatabasePath}"));
            services.AddSingleton(new ApiSettings
            {
                AdminEmail = "admin@easyfrete.test",
                AdminPassword = "AdminTeste123!",
                JwtSecret = "easyfrete-tests-jwt-secret-32-chars-minimum",
                JwtIssuer = "easyfrete-api",
                JwtAudience = "easyfrete-web",
                ExternalCalcApiKey = "external-test-key",
                SyncApiKey = "sync-test-key",
                SqlitePath = DatabasePath,
                FrontendBaseUrl = "https://easyfrete.test",
                PasswordResetTokenMinutes = 30,
                PasswordResetMaxAttempts = 2,
                PasswordResetLockoutHours = 24
            });
            services.AddSingleton<IPasswordResetEmailSender>(EmailSender);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public new void Dispose()
    {
        base.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { DatabasePath, $"{DatabasePath}-wal", $"{DatabasePath}-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
