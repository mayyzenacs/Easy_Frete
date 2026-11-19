using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EasyFrete.Api.Data;
using EasyFrete.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EasyFrete.Api.Tests;

[TestFixture]
public sealed class AuthAndAdminSecurityTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Register_AlwaysCreatesUserPendingIgnoringRole()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nome = "Novo Usuario",
            email = "Novo@EasyFrete.Test",
            password = "SenhaSegura123!",
            role = "Admin",
            status = "Aprovado"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usuario = db.Usuarios.Single(u => u.Email == "novo@easyfrete.test");
        Assert.That(usuario.Role, Is.EqualTo("User"));
        Assert.That(usuario.Status, Is.EqualTo("Pendente"));
        Assert.That(usuario.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [TestCase("Pendente")]
    [TestCase("Rejeitado")]
    public async Task PendingOrRejectedLogin_ReturnsForbidden(string status)
    {
        var email = $"blocked-{status.ToLower()}@easyfrete.test";
        var create = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nome = "Usuario Bloqueado",
            email,
            password = "SenhaSegura123!"
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usuario = db.Usuarios.Single(u => u.Email == email);
        usuario.Status = status;
        await db.SaveChangesAsync();

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "SenhaSegura123!"
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CommonUserCannotListOrPatchAdminEndpoints()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nome = "Usuario Comum",
            email = "comum@easyfrete.test",
            password = "SenhaSegura123!"
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuario = db.Usuarios.Single(u => u.Email == "comum@easyfrete.test");
            usuario.Status = "Aprovado";
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "comum@easyfrete.test",
            password = "SenhaSegura123!"
        });
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("token").GetString()!;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var list = await _client.GetAsync("/api/v1/admin/usuarios");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var patchScope = _factory.Services.CreateScope();
        var patchDb = patchScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alvoId = patchDb.Usuarios.Single(u => u.Email == "comum@easyfrete.test").Id;
        var patch = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{alvoId}",
            new { nome = "Tentativa" });
        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task AdminCanListRenamePromoteAndApproveUser()
    {
        var adminToken = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nome = "Nome Inicial",
            email = "candidato@easyfrete.test",
            password = "SenhaSegura123!"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var candidato = db.Usuarios.Single(u => u.Email == "candidato@easyfrete.test");

        var list = await _client.GetAsync("/api/v1/admin/usuarios?status=Pendente");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        foreach (var item in listJson.RootElement.EnumerateArray())
        {
            Assert.That(Guid.TryParse(item.GetProperty("id").GetString(), out var listedId), Is.True);
            Assert.That(listedId, Is.Not.EqualTo(Guid.Empty));
        }

        var rename = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{candidato.Id}",
            new { nome = "Nome Atualizado" });
        Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var promote = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{candidato.Id}",
            new { role = "Admin", status = "Aprovado" });
        Assert.That(promote.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "candidato@easyfrete.test",
            password = "SenhaSegura123!"
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.That(loginJson.RootElement.GetProperty("token").GetString(), Is.Not.Null.And.Not.Empty);
        var returnedId = loginJson.RootElement
            .GetProperty("usuario")
            .GetProperty("id")
            .GetString();
        Assert.That(Guid.TryParse(returnedId, out var loginId), Is.True);
        Assert.That(loginId, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task AdminCannotDemoteOrUnapproveSelf()
    {
        var adminToken = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Usuarios.Single(u => u.Email == "admin@easyfrete.test");

        var demote = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{admin.Id}",
            new { role = "User" });
        Assert.That(demote.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var unapprove = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{admin.Id}",
            new { status = "Pendente" });
        Assert.That(unapprove.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AdminPatchWithEmptyJsonReturnsBadRequest()
    {
        var adminToken = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Usuarios.Single(u => u.Email == "admin@easyfrete.test");

        var patch = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{admin.Id}",
            new { });

        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private async Task<string> LoginAdminAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@easyfrete.test",
            password = "AdminTeste123!"
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }
}
