using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace EasyFrete.Api.Tests;

[TestFixture]
public sealed class ApiEndpointsIntegrationTests
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
    public async Task ProtectedEndpointsWithoutTokenOrApiKey_ReturnUnauthorized()
    {
        var productList = await _client.GetAsync("/api/v1/produtos");
        Assert.That(productList.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var productSync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            produtos = Array.Empty<object>()
        });
        Assert.That(productSync.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var admin = await _client.GetAsync("/api/v1/admin/usuarios");
        Assert.That(admin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProductSyncWithApiKeyPersistsAndGetBySkuWorks()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");
        var sync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            origem = "TESTE",
            produtos = new[]
            {
                new
                {
                    sku = "SKU-TESTE-1",
                    nome = "Produto Teste",
                    pesoBruto = 2.5m,
                    comprimentoCm = 30m,
                    larguraCm = 20m,
                    alturaCm = 10m,
                    linkDrive = "https://example.test/drive"
                }
            }
        });
        Assert.That(sync.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var adminToken = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var get = await _client.GetAsync("/api/v1/produtos/SKU-TESTE-1");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("sku").GetString(), Is.EqualTo("SKU-TESTE-1"));
        Assert.That(json.RootElement.GetProperty("linkDrive").GetString(),
            Is.EqualTo("https://example.test/drive"));
    }

    [Test]
    public async Task TariffSyncAndFreightCalculationReturn200AndInvalidRequestsReturnErrors()
    {
        var token = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tariffs = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "TESTE",
            tarifas = new[]
            {
                new
                {
                    id = 1,
                    descricaoFaixa = "Até 5kg",
                    pesoMinKg = 0m,
                    pesoMaxKg = 5m,
                    de79a199SemDesconto = 50m,
                    de79a199ComDesconto97 = 25m,
                    de79a199Fulfillment = 15m,
                    acima199SemDesconto = 60m,
                    acima199ComDesconto97 = 30m,
                    acima199Fulfillment = 18m
                }
            }
        });
        Assert.That(tariffs.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var calculate = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            sku = (string?)null,
            precoProduto = 149m,
            pesoRealKg = 2m,
            comprimentoCm = 30m,
            larguraCm = 20m,
            alturaCm = 10m,
            isFulfillment = false,
            reputacaoMaior97 = true
        });
        Assert.That(calculate.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var calcJson = JsonDocument.Parse(await calculate.Content.ReadAsStringAsync());
        Assert.That(calcJson.RootElement.GetProperty("valorFinalFrete").GetDecimal(), Is.GreaterThan(0m));

        var invalid = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            precoProduto = 0m
        });
        Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var missingSku = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            sku = "SKU-INEXISTENTE",
            precoProduto = 149m,
            pesoRealKg = 0m,
            comprimentoCm = 0m,
            larguraCm = 0m,
            alturaCm = 0m
        });
        Assert.That(missingSku.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
