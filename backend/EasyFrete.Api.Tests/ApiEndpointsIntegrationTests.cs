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
    public async Task HealthEndpoints_ReturnHealthyWithoutAuth()
    {
        var nativeHealth = await _client.GetAsync("/health");
        Assert.That(nativeHealth.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var nativeBody = await nativeHealth.Content.ReadAsStringAsync();
        Assert.That(nativeBody, Is.EqualTo("Healthy"));

        var apiHealth = await _client.GetAsync("/api/v1/health");
        Assert.That(apiHealth.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await apiHealth.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task ProtectedEndpointsWithoutTokenOrApiKey_ReturnUnauthorized()
    {
        var productList = await _client.GetAsync("/api/v1/produtos");
        Assert.That(productList.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var productGet = await _client.GetAsync("/api/v1/produtos/SKU-QUALQUER");
        Assert.That(productGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var productSync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            produtos = Array.Empty<object>()
        });
        Assert.That(productSync.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var tariffSync = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            tarifas = Array.Empty<object>()
        });
        Assert.That(tariffSync.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var freightCalc = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            precoProduto = 100m
        });
        Assert.That(freightCalc.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var admin = await _client.GetAsync("/api/v1/admin/usuarios");
        Assert.That(admin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var adminPatch = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/usuarios/{Guid.NewGuid()}",
            new { nome = "Sem Token" });
        Assert.That(adminPatch.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task InvalidOrWrongScopeApiKey_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", "chave-invalida");

        var productSync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            produtos = Array.Empty<object>()
        });
        Assert.That(productSync.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var tariffSync = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            tarifas = Array.Empty<object>()
        });
        Assert.That(tariffSync.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Chave de sync não pode autorizar cálculo externo
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");
        var freightCalc = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            precoProduto = 149m,
            pesoRealKg = 1m,
            comprimentoCm = 20m,
            larguraCm = 15m,
            alturaCm = 10m
        });
        Assert.That(freightCalc.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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

    [Test]
    public async Task ProductEndpoints_ListGetNotFoundAndSyncUpsertValidation()
    {
        var token = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var emptyBatch = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            origem = "TESTE",
            produtos = Array.Empty<object>()
        });
        Assert.That(emptyBatch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Sync via JWT (inserção inicial)
        var firstSync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            origem = "JWT-SYNC",
            produtos = new[]
            {
                new
                {
                    sku = "SKU-UPSERT-1",
                    nome = "Nome Original",
                    pesoBruto = 1.5m,
                    comprimentoCm = 20m,
                    larguraCm = 15m,
                    alturaCm = 10m,
                    linkDrive = (string?)null
                }
            }
        });
        Assert.That(firstSync.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var firstJson = JsonDocument.Parse(await firstSync.Content.ReadAsStringAsync()))
        {
            Assert.That(firstJson.RootElement.GetProperty("inseridos").GetInt32(), Is.EqualTo(1));
            Assert.That(firstJson.RootElement.GetProperty("atualizados").GetInt32(), Is.EqualTo(0));
        }

        // Upsert + item inválido no lote
        var secondSync = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            origem = "JWT-SYNC",
            produtos = new[]
            {
                new
                {
                    sku = "SKU-UPSERT-1",
                    nome = "Nome Atualizado",
                    pesoBruto = 3.0m,
                    comprimentoCm = 25m,
                    larguraCm = 20m,
                    alturaCm = 15m,
                    linkDrive = (string?)"https://example.test/atualizado"
                },
                new
                {
                    sku = "SKU-INVALIDO",
                    nome = "Peso Negativo",
                    pesoBruto = -1.0m,
                    comprimentoCm = 10m,
                    larguraCm = 10m,
                    alturaCm = 10m,
                    linkDrive = (string?)null
                }
            }
        });
        Assert.That(secondSync.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var secondJson = JsonDocument.Parse(await secondSync.Content.ReadAsStringAsync()))
        {
            Assert.That(secondJson.RootElement.GetProperty("atualizados").GetInt32(), Is.EqualTo(1));
            Assert.That(secondJson.RootElement.GetProperty("ignorados").GetInt32(), Is.EqualTo(1));
            Assert.That(secondJson.RootElement.GetProperty("erros").GetArrayLength(), Is.EqualTo(1));
        }

        // GET /api/v1/produtos autenticado lista produtos persistidos
        var list = await _client.GetAsync("/api/v1/produtos");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            Assert.That(listJson.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(listJson.RootElement[0].GetProperty("nome").GetString(), Is.EqualTo("Nome Atualizado"));
        }

        // GET /api/v1/produtos/{sku} inexistente retorna 404
        var notFound = await _client.GetAsync("/api/v1/produtos/SKU-NAO-EXISTE");
        Assert.That(notFound.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TariffSync_WithApiKeySupportsInsertUpdateRemoveAndRejectsInvalidPayloads()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");

        var emptyTariffs = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
            tarifas = Array.Empty<object>()
        });
        Assert.That(emptyTariffs.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var invalidRange = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
            tarifas = new[]
            {
                new
                {
                    id = 1,
                    descricaoFaixa = "Faixa Inválida",
                    pesoMinKg = 5m,
                    pesoMaxKg = 2m,
                    de79a199SemDesconto = 50m,
                    de79a199ComDesconto97 = 25m,
                    de79a199Fulfillment = 15m,
                    acima199SemDesconto = 60m,
                    acima199ComDesconto97 = 30m,
                    acima199Fulfillment = 18m
                }
            }
        });
        Assert.That(invalidRange.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var zeroTariff = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
            tarifas = new[]
            {
                new
                {
                    id = 3,
                    descricaoFaixa = "De 1kg a 2kg",
                    pesoMinKg = 1m,
                    pesoMaxKg = 2m,
                    de79a199SemDesconto = 42.90m,
                    de79a199ComDesconto97 = 21.45m,
                    de79a199Fulfillment = 10.73m,
                    acima199SemDesconto = 46.90m,
                    acima199ComDesconto97 = 23.45m,
                    acima199Fulfillment = 0m
                }
            }
        });
        Assert.That(zeroTariff.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var firstSync = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
            tarifas = new[]
            {
                new
                {
                    id = 1,
                    descricaoFaixa = "Até 2kg",
                    pesoMinKg = 0m,
                    pesoMaxKg = 2m,
                    de79a199SemDesconto = 40m,
                    de79a199ComDesconto97 = 20m,
                    de79a199Fulfillment = 12m,
                    acima199SemDesconto = 50m,
                    acima199ComDesconto97 = 25m,
                    acima199Fulfillment = 15m
                },
                new
                {
                    id = 2,
                    descricaoFaixa = "De 2kg a 5kg",
                    pesoMinKg = 2m,
                    pesoMaxKg = 5m,
                    de79a199SemDesconto = 55m,
                    de79a199ComDesconto97 = 27.5m,
                    de79a199Fulfillment = 16m,
                    acima199SemDesconto = 65m,
                    acima199ComDesconto97 = 32.5m,
                    acima199Fulfillment = 20m
                }
            }
        });
        Assert.That(firstSync.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var firstJson = JsonDocument.Parse(await firstSync.Content.ReadAsStringAsync()))
        {
            Assert.That(firstJson.RootElement.GetProperty("inseridos").GetInt32(), Is.EqualTo(2));
            Assert.That(firstJson.RootElement.GetProperty("atualizados").GetInt32(), Is.EqualTo(0));
            Assert.That(firstJson.RootElement.GetProperty("removidos").GetInt32(), Is.EqualTo(0));
        }

        // Segundo sync: atualiza id=1 e remove id=2
        var secondSync = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
            tarifas = new[]
            {
                new
                {
                    id = 1,
                    descricaoFaixa = "Até 5kg",
                    pesoMinKg = 0m,
                    pesoMaxKg = 5m,
                    de79a199SemDesconto = 45m,
                    de79a199ComDesconto97 = 22.5m,
                    de79a199Fulfillment = 14m,
                    acima199SemDesconto = 55m,
                    acima199ComDesconto97 = 27.5m,
                    acima199Fulfillment = 17m
                }
            }
        });
        Assert.That(secondSync.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var secondJson = JsonDocument.Parse(await secondSync.Content.ReadAsStringAsync()))
        {
            Assert.That(secondJson.RootElement.GetProperty("inseridos").GetInt32(), Is.EqualTo(0));
            Assert.That(secondJson.RootElement.GetProperty("atualizados").GetInt32(), Is.EqualTo(1));
            Assert.That(secondJson.RootElement.GetProperty("removidos").GetInt32(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task FreightCalculation_WithExternalApiKeyAndPersistedSku_Returns200()
    {
        // Popula produto e tarifa usando SYNC_API_KEY
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");
        var syncProd = await _client.PostAsJsonAsync("/api/v1/produtos/sync-lote", new
        {
            origem = "GAS",
            produtos = new[]
            {
                new
                {
                    sku = "SKU-FRETE-EXT",
                    nome = "Produto Frete Externo",
                    pesoBruto = 1.8m,
                    comprimentoCm = 25m,
                    larguraCm = 20m,
                    alturaCm = 12m,
                    linkDrive = (string?)null
                }
            }
        });
        Assert.That(syncProd.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var syncTariff = await _client.PostAsJsonAsync("/api/v1/tarifas/magalu/sync", new
        {
            origem = "GAS",
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
        Assert.That(syncTariff.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Calcula frete via EXTERNAL_CALC_API_KEY usando apenas o SKU salvo no banco
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", "external-test-key");

        var calc = await _client.PostAsJsonAsync("/api/v1/frete/magalu/calcular", new
        {
            sku = "SKU-FRETE-EXT",
            precoProduto = 220m,
            pesoRealKg = 0m,
            comprimentoCm = 0m,
            larguraCm = 0m,
            alturaCm = 0m,
            isFulfillment = true,
            reputacaoMaior97 = true
        });
        Assert.That(calc.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var calcJson = JsonDocument.Parse(await calc.Content.ReadAsStringAsync());
        Assert.That(calcJson.RootElement.GetProperty("origemMedidas").GetString(), Is.EqualTo("SKU_CADASTRO"));
        Assert.That(calcJson.RootElement.GetProperty("faixaPrecoAplicada").GetString(), Is.EqualTo("Acima de R$ 199,00"));
        Assert.That(calcJson.RootElement.GetProperty("modalidadeAplicada").GetString(), Is.EqualTo("Fulfillment"));
        Assert.That(calcJson.RootElement.GetProperty("tarifaBase").GetDecimal(), Is.EqualTo(18m));
        Assert.That(calcJson.RootElement.GetProperty("valorFinalFrete").GetDecimal(), Is.EqualTo(23m));
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
