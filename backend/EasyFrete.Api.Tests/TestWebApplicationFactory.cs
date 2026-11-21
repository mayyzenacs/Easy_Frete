using EasyFrete.Api.Data;
using EasyFrete.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyFrete.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    public string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"easyfrete-tests-{Guid.NewGuid():N}.db");

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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<ApiSettings>();

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
                SqlitePath = DatabasePath
            });
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
