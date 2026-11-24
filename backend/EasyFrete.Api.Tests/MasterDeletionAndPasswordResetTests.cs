using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EasyFrete.Api.Data;
using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EasyFrete.Api.Tests;

[TestFixture]
public sealed class MasterDeletionAndPasswordResetTests
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
    public async Task MasterAccount_IsProtectedAgainstSelfDeleteRoleEmailAndAdminTampering()
    {
        var masterToken = await LoginAsync("admin@easyfrete.test", "AdminTeste123!");
        Guid masterId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var master = db.Usuarios.Single(u => u.Email == "admin@easyfrete.test");
            Assert.That(master.Role, Is.EqualTo(UserRoles.Master));
            Assert.That(master.Status, Is.EqualTo(UserStatuses.Aprovado));
            masterId = master.Id;
        }

        // Master pode editar apenas o próprio nome
        UseBearer(masterToken);
        var renameSelf = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{masterId}",
            new { nome = "Mayra Master" });
        Assert.That(renameSelf.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Master nunca pode se excluir, alterar o próprio e-mail, role ou status
        var selfDelete = await _client.DeleteAsync($"/api/v1/admin/usuarios/{masterId}");
        Assert.That(selfDelete.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var selfEmail = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{masterId}",
            new { email = "novo@easyfrete.test" });
        Assert.That(selfEmail.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var selfRole = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{masterId}",
            new { role = UserRoles.Admin });
        Assert.That(selfRole.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Cria um Admin comum
        var adminId = await RegisterAndApproveAsync("Admin Comum", "admin.comum@easyfrete.test", "SenhaForte123!", UserRoles.Admin, masterToken);

        // Ninguém pode promover outro usuário para Master
        UseBearer(masterToken);
        var promoteToMaster = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{adminId}",
            new { role = UserRoles.Master });
        Assert.That(promoteToMaster.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Admin comum NÃO pode alterar nada na Master, nem excluir, nem pedir exclusão da Master
        var commonAdminToken = await LoginAsync("admin.comum@easyfrete.test", "SenhaForte123!");
        UseBearer(commonAdminToken);

        var tamperName = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{masterId}",
            new { nome = "Hackeado" });
        Assert.That(tamperName.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var deleteMaster = await _client.DeleteAsync($"/api/v1/admin/usuarios/{masterId}");
        Assert.That(deleteMaster.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var requestDeleteMaster = await _client.PostAsJsonAsync("/api/v1/admin/exclusoes", new
        {
            usuarioAlvoId = masterId,
            motivo = "Tentativa indevida de excluir a Master"
        });
        Assert.That(requestDeleteMaster.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CommonAdmin_ManagesOnlyUsers_AndCannotTouchOtherAdminsOrDirectDelete()
    {
        var masterToken = await LoginAsync("admin@easyfrete.test", "AdminTeste123!");
        var admin1Id = await RegisterAndApproveAsync("Admin Um", "admin1@easyfrete.test", "SenhaForte123!", UserRoles.Admin, masterToken);
        var admin2Id = await RegisterAndApproveAsync("Admin Dois", "admin2@easyfrete.test", "SenhaForte123!", UserRoles.Admin, masterToken);
        var userId = await RegisterAndApproveAsync("Usuario Um", "user1@easyfrete.test", "SenhaForte123!", UserRoles.User, masterToken);

        var admin1Token = await LoginAsync("admin1@easyfrete.test", "SenhaForte123!");
        UseBearer(admin1Token);

        // Admin pode editar nome/email/status de User
        var editUser = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{userId}",
            new { nome = "Usuario Editado", email = "user1.editado@easyfrete.test" });
        Assert.That(editUser.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Admin NÃO pode promover User a Admin
        var promoteUser = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{userId}",
            new { role = UserRoles.Admin });
        Assert.That(promoteUser.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Admin NÃO pode editar outro Admin nem a si mesmo via painel
        var editOtherAdmin = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{admin2Id}",
            new { nome = "Outro Nome" });
        Assert.That(editOtherAdmin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var editSelfAdmin = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{admin1Id}",
            new { nome = "Auto Edit" });
        Assert.That(editSelfAdmin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Admin NÃO pode excluir diretamente nem pedir exclusão de outro Admin
        var directDelete = await _client.DeleteAsync($"/api/v1/admin/usuarios/{userId}");
        Assert.That(directDelete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var reqDeleteOtherAdmin = await _client.PostAsJsonAsync("/api/v1/admin/exclusoes", new
        {
            usuarioAlvoId = admin2Id,
            motivo = "Tentativa de pedir exclusão de outro admin"
        });
        Assert.That(reqDeleteOtherAdmin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeletionRequestFlow_NotifiesMasterAndAdminOnApprovalAndRejection_AndRevokesDeletedJwt()
    {
        var masterToken = await LoginAsync("admin@easyfrete.test", "AdminTeste123!");
        await RegisterAndApproveAsync("Admin Solicitante", "solicitante@easyfrete.test", "SenhaForte123!", UserRoles.Admin, masterToken);

        // Novo usuário se cadastra -> gera notificação de cadastro pendente e NÃO envia e-mail
        _client.DefaultRequestHeaders.Authorization = null;
        var reg1 = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nome = "Alvo Aprovacao",
            email = "alvo1@easyfrete.test",
            password = "SenhaForte123!"
        });
        Assert.That(reg1.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(_factory.EmailSender.SentEmails, Is.Empty);

        // Master vê notificação de novo cadastro pendente
        UseBearer(masterToken);
        var masterNotifsRes = await _client.GetAsync("/api/v1/admin/notificacoes");
        Assert.That(masterNotifsRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var notifJson = JsonDocument.Parse(await masterNotifsRes.Content.ReadAsStringAsync()))
        {
            Assert.That(notifJson.RootElement.GetProperty("naoLidas").GetInt32(), Is.GreaterThanOrEqualTo(1));
            Assert.That(
                notifJson.RootElement.GetProperty("itens").EnumerateArray()
                    .Any(n => n.GetProperty("tipo").GetString() == NotificationTypes.CadastroPendente),
                Is.True);
        }

        // Aprova alvo1 e alvo2
        Guid alvo1Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            alvo1Id = db.Usuarios.Single(u => u.Email == "alvo1@easyfrete.test").Id;
        }
        await _client.PatchAsJsonAsync($"/api/v1/admin/usuarios/{alvo1Id}", new { status = UserStatuses.Aprovado });
        var alvo2Id = await RegisterAndApproveAsync("Alvo Recusa", "alvo2@easyfrete.test", "SenhaForte123!", UserRoles.User, masterToken);

        // Alvo1 faz login (obtém JWT válido antes da exclusão)
        var alvo1Token = await LoginAsync("alvo1@easyfrete.test", "SenhaForte123!");

        // Admin solicita exclusão de alvo1 e alvo2
        var adminToken = await LoginAsync("solicitante@easyfrete.test", "SenhaForte123!");
        UseBearer(adminToken);

        var req1Res = await _client.PostAsJsonAsync("/api/v1/admin/exclusoes", new
        {
            usuarioAlvoId = alvo1Id,
            motivo = "Colaborador desligado da operação."
        });
        Assert.That(req1Res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Guid req1Id;
        using (var json = JsonDocument.Parse(await req1Res.Content.ReadAsStringAsync()))
        {
            req1Id = json.RootElement.GetProperty("id").GetGuid();
        }

        // Pedido duplicado para o mesmo usuário retorna 409 Conflict
        var dupReq = await _client.PostAsJsonAsync("/api/v1/admin/exclusoes", new
        {
            usuarioAlvoId = alvo1Id,
            motivo = "Outro motivo duplicado."
        });
        Assert.That(dupReq.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var req2Res = await _client.PostAsJsonAsync("/api/v1/admin/exclusoes", new
        {
            usuarioAlvoId = alvo2Id,
            motivo = "Revisar necessidade de acesso."
        });
        Assert.That(req2Res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Guid req2Id;
        using (var json = JsonDocument.Parse(await req2Res.Content.ReadAsStringAsync()))
        {
            req2Id = json.RootElement.GetProperty("id").GetGuid();
        }

        // Master recebe notificações de solicitação de exclusão e aprova req1, recusa req2
        UseBearer(masterToken);
        var masterNotifs2 = await _client.GetAsync("/api/v1/admin/notificacoes");
        using (var json = JsonDocument.Parse(await masterNotifs2.Content.ReadAsStringAsync()))
        {
            Assert.That(
                json.RootElement.GetProperty("itens").EnumerateArray()
                    .Count(n => n.GetProperty("tipo").GetString() == NotificationTypes.SolicitacaoExclusao && !n.GetProperty("lida").GetBoolean()),
                Is.EqualTo(2));
        }

        var approveRes = await _client.PostAsJsonAsync($"/api/v1/admin/exclusoes/{req1Id}/aprovar", new { });
        Assert.That(approveRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var rejectRes = await _client.PostAsJsonAsync($"/api/v1/admin/exclusoes/{req2Id}/recusar", new
        {
            observacao = "Ainda utiliza o sistema para conferência mensal."
        });
        Assert.That(rejectRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Alvo1 foi excluído e seu JWT antigo é revogado IMEDIATAMENTE (401)
        UseBearer(alvo1Token);
        var revokedCall = await _client.GetAsync("/api/v1/produtos");
        Assert.That(revokedCall.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Admin solicitante recebe as notificações de Aprovada e Recusada + vê histórico completo
        UseBearer(adminToken);
        var adminHistory = await _client.GetAsync("/api/v1/admin/exclusoes");
        Assert.That(adminHistory.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var histJson = JsonDocument.Parse(await adminHistory.Content.ReadAsStringAsync()))
        {
            var items = histJson.RootElement.EnumerateArray().ToList();
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items.Any(i => i.GetProperty("status").GetString() == DeletionRequestStatuses.Aprovado), Is.True);
            Assert.That(items.Any(i =>
                i.GetProperty("status").GetString() == DeletionRequestStatuses.Recusado &&
                i.GetProperty("observacaoDecisao").GetString() == "Ainda utiliza o sistema para conferência mensal."), Is.True);
        }

        var adminNotifs = await _client.GetAsync("/api/v1/admin/notificacoes");
        using (var notifJson = JsonDocument.Parse(await adminNotifs.Content.ReadAsStringAsync()))
        {
            var tipos = notifJson.RootElement.GetProperty("itens").EnumerateArray()
                .Select(n => n.GetProperty("tipo").GetString())
                .ToList();
            Assert.That(tipos, Does.Contain(NotificationTypes.ExclusaoAprovada));
            Assert.That(tipos, Does.Contain(NotificationTypes.ExclusaoRecusada));
        }

        // Marcar todas como lidas zera contador
        var markAll = await _client.PostAsync("/api/v1/admin/notificacoes/marcar-todas-lidas", null);
        Assert.That(markAll.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var adminNotifsAfter = await _client.GetAsync("/api/v1/admin/notificacoes");
        using (var afterJson = JsonDocument.Parse(await adminNotifsAfter.Content.ReadAsStringAsync()))
        {
            Assert.That(afterJson.RootElement.GetProperty("naoLidas").GetInt32(), Is.EqualTo(0));
        }
    }

    [Test]
    public async Task PasswordReset_EnforcesTwoSendsAnd24hLockout_HashesTokens_AndRevokesOldSessions()
    {
        var masterToken = await LoginAsync("admin@easyfrete.test", "AdminTeste123!");
        await RegisterAndApproveAsync("Usuario Reset", "reset@easyfrete.test", "SenhaAntiga123!", UserRoles.User, masterToken);

        // Faz login antes do reset para validar que a sessão antiga será revogada após redefinir
        var oldJwt = await LoginAsync("reset@easyfrete.test", "SenhaAntiga123!");
        _client.DefaultRequestHeaders.Authorization = null;

        // 1º envio -> OK + e-mail enviado
        var first = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "reset@easyfrete.test" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.EmailSender.SentEmails.Count, Is.EqualTo(1));
        var token1 = ExtractTokenFromUrl(_factory.EmailSender.SentEmails[0].ResetUrl);

        // Token em texto plano NUNCA existe salvo no banco (somente SHA-256 hash)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.TokensRedefinicaoSenha.SingleAsync();
            Assert.That(saved.TokenHash, Is.Not.EqualTo(token1));
            Assert.That(saved.TokenHash.Length, Is.EqualTo(64));
        }

        // 2º envio -> OK + 2º e-mail enviado (e 1º token revogado automaticamente)
        var second = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "reset@easyfrete.test" });
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.EmailSender.SentEmails.Count, Is.EqualTo(2));
        var token2 = ExtractTokenFromUrl(_factory.EmailSender.SentEmails[1].ResetUrl);

        var validateOld = await _client.GetAsync($"/api/v1/auth/reset-password/validate?token={Uri.EscapeDataString(token1)}");
        Assert.That(validateOld.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var validateCurrent = await _client.GetAsync($"/api/v1/auth/reset-password/validate?token={Uri.EscapeDataString(token2)}");
        Assert.That(validateCurrent.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3º envio dentro de 24h -> BLOQUEADO (429 TooManyRequests), nenhum e-mail extra enviado
        var third = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "reset@easyfrete.test" });
        Assert.That(third.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(_factory.EmailSender.SentEmails.Count, Is.EqualTo(2));

        // Redefine a senha com token2
        var resetRes = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = token2,
            newPassword = "NovaSenhaSegura456!"
        });
        Assert.That(resetRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Reuso do mesmo token falha (uso único)
        var reuseRes = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = token2,
            newPassword = "OutraSenhaSegura789!"
        });
        Assert.That(reuseRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // JWT antigo (antes do reset) foi revogado imediatamente via SecurityStamp
        UseBearer(oldJwt);
        var callWithOldJwt = await _client.GetAsync("/api/v1/produtos");
        Assert.That(callWithOldJwt.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        _client.DefaultRequestHeaders.Authorization = null;

        // Senha antiga não entra mais; nova senha entra normalmente
        var oldLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "reset@easyfrete.test",
            password = "SenhaAntiga123!"
        });
        Assert.That(oldLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var newToken = await LoginAsync("reset@easyfrete.test", "NovaSenhaSegura456!");
        Assert.That(newToken, Is.Not.Empty);

        // Após 24h + 1 min, o bloqueio de envios expira e libera novamente
        _factory.Clock.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromMinutes(1)));
        var after24h = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "reset@easyfrete.test" });
        Assert.That(after24h.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.EmailSender.SentEmails.Count, Is.EqualTo(3));

        // Token expira após 30 minutos
        var token3 = ExtractTokenFromUrl(_factory.EmailSender.SentEmails[2].ResetUrl);
        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        var expiredValidate = await _client.GetAsync($"/api/v1/auth/reset-password/validate?token={Uri.EscapeDataString(token3)}");
        Assert.That(expiredValidate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PasswordReset_PreventsAccountEnumeration_ProtectsMaster_AndHandlesUnconfiguredSmtp()
    {
        // E-mail inexistente retorna 200 genérico, não envia e-mail e ainda sofre bloqueio 2x/24h
        var u1 = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "fantasma@easyfrete.test" });
        var u2 = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "fantasma@easyfrete.test" });
        var u3 = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "fantasma@easyfrete.test" });
        Assert.That(u1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(u2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(u3.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(_factory.EmailSender.SentEmails, Is.Empty);

        // Conta Master não gera e-mail de reset (gerenciada via .env), mantendo resposta genérica
        var masterReset = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "admin@easyfrete.test" });
        Assert.That(masterReset.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.EmailSender.SentEmails, Is.Empty);

        // Se SMTP não estiver configurado, retorna 503 sem consumir tentativa
        _factory.EmailSender.IsConfigured = false;
        var noSmtp = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "outro@easyfrete.test" });
        Assert.That(noSmtp.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    private void UseBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<Guid> RegisterAndApproveAsync(
        string nome,
        string email,
        string password,
        string role,
        string masterToken)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register", new { nome, email, password });
        Assert.That(reg.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Guid id;
        using (var json = JsonDocument.Parse(await reg.Content.ReadAsStringAsync()))
        {
            id = json.RootElement.GetProperty("id").GetGuid();
        }

        UseBearer(masterToken);
        var patch = await _client.PatchAsJsonAsync($"/api/v1/admin/usuarios/{id}", new
        {
            status = UserStatuses.Aprovado,
            role
        });
        Assert.That(patch.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return id;
    }

    private static string ExtractTokenFromUrl(string url)
    {
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "reset")
                return Uri.UnescapeDataString(kv[1]);
        }
        throw new InvalidOperationException($"Token 'reset' não encontrado na URL: {url}");
    }
}
