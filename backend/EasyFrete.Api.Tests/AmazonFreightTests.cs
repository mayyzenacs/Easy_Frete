using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EasyFrete.Api.Data;
using EasyFrete.Api.DTOs;
using EasyFrete.Api.Models;
using EasyFrete.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyFrete.Api.Tests;

[TestFixture]
public sealed class AmazonFreightServiceUnitTests
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private AmazonFreightService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.MigrateAsync();

        SeedDefaultAmazonData(_db);
        await _db.SaveChangesAsync();

        _service = new AmazonFreightService(_db);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task Preco1899Vs1900_Dba_AlternaPagoPeloClienteParaGratis()
    {
        var r1899 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 18.99m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });

        Assert.That(r1899.Success, Is.True);
        Assert.That(r1899.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(r1899.Data.FreteCliente, Is.EqualTo(AmazonFreteClientes.PagoPeloCliente));
        Assert.That(r1899.Data.TarifaBase, Is.EqualTo(4.50m));
        Assert.That(r1899.Data.TaxaFixaObrigatoria, Is.EqualTo(0.00m));
        Assert.That(r1899.Data.ValorFinalFrete, Is.EqualTo(4.50m));

        var r1900 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 19.00m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });

        Assert.That(r1900.Success, Is.True);
        Assert.That(r1900.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(r1900.Data.FreteCliente, Is.EqualTo(AmazonFreteClientes.Gratis));
        Assert.That(r1900.Data.TarifaBase, Is.EqualTo(8.50m));
        Assert.That(r1900.Data.ValorFinalFrete, Is.EqualTo(8.50m));
    }

    [Test]
    public async Task Preco2999Vs3000_DbaEFba_AplicaFaixaFixaCorreta()
    {
        var dba2999 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 29.99m,
            PesoRealKg = 0.5m,
            Modalidade = AmazonModalidades.Dba
        });
        var dba3000 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 30.00m,
            PesoRealKg = 0.5m,
            Modalidade = AmazonModalidades.Dba
        });

        Assert.That(dba2999.Success, Is.True);
        Assert.That(dba2999.Data!.ValorFinalFrete, Is.EqualTo(8.50m));
        Assert.That(dba3000.Success, Is.True);
        Assert.That(dba3000.Data!.ValorFinalFrete, Is.EqualTo(11.95m));

        var fba2999 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 29.99m,
            PesoRealKg = 0.5m,
            Modalidade = AmazonModalidades.Fba
        });
        var fba3000 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 30.00m,
            PesoRealKg = 0.5m,
            Modalidade = AmazonModalidades.Fba
        });

        Assert.That(fba2999.Success, Is.True);
        Assert.That(fba2999.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(fba2999.Data.ValorFinalFrete, Is.EqualTo(6.50m));

        Assert.That(fba3000.Success, Is.True);
        Assert.That(fba3000.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(fba3000.Data.ValorFinalFrete, Is.EqualTo(9.75m));
    }

    [Test]
    public async Task Preco7899Vs7900_TransicaoDeFixoParaFaixaPeso()
    {
        var dba7899 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 78.99m,
            PesoRealKg = 0.25m,
            Modalidade = AmazonModalidades.Dba
        });
        var dba7900 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 79.00m,
            PesoRealKg = 0.25m,
            Modalidade = AmazonModalidades.Dba
        });

        Assert.That(dba7899.Success, Is.True);
        Assert.That(dba7899.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(dba7899.Data.ValorFinalFrete, Is.EqualTo(11.95m));

        Assert.That(dba7900.Success, Is.True);
        Assert.That(dba7900.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.FaixaPeso));
        Assert.That(dba7900.Data.DescricaoFaixaPeso, Is.EqualTo("0-250g"));
        Assert.That(dba7900.Data.ValorFinalFrete, Is.EqualTo(19.45m));

        var fba7899 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 78.99m,
            PesoRealKg = 0.10m,
            Modalidade = AmazonModalidades.Fba
        });
        var fba7900 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 79.00m,
            PesoRealKg = 0.10m,
            Modalidade = AmazonModalidades.Fba
        });

        Assert.That(fba7899.Success, Is.True);
        Assert.That(fba7899.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(fba7899.Data.ValorFinalFrete, Is.EqualTo(9.75m));

        Assert.That(fba7900.Success, Is.True);
        Assert.That(fba7900.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.FaixaPeso));
        Assert.That(fba7900.Data.DescricaoFaixaPeso, Is.EqualTo("0 a 100g"));
        Assert.That(fba7900.Data.ValorFinalFrete, Is.EqualTo(14.20m));
    }

    [Test]
    public async Task PesoExatoNaFronteira_Fba010KgEDba025Kg_NaoPulaParaFaixaSeguinte()
    {
        var fba010 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 120.00m,
            PesoRealKg = 0.10m,
            Modalidade = AmazonModalidades.Fba
        });
        Assert.That(fba010.Success, Is.True);
        Assert.That(fba010.Data!.DescricaoFaixaPeso, Is.EqualTo("0 a 100g"));
        Assert.That(fba010.Data.ValorFinalFrete, Is.EqualTo(14.20m));

        var fba0101 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 120.00m,
            PesoRealKg = 0.101m,
            Modalidade = AmazonModalidades.Fba
        });
        Assert.That(fba0101.Success, Is.True);
        Assert.That(fba0101.Data!.DescricaoFaixaPeso, Is.EqualTo("100g a 250g"));
        Assert.That(fba0101.Data.ValorFinalFrete, Is.EqualTo(15.90m));

        var dba025 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 120.00m,
            PesoRealKg = 0.25m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(dba025.Success, Is.True);
        Assert.That(dba025.Data!.DescricaoFaixaPeso, Is.EqualTo("0-250g"));
        Assert.That(dba025.Data.ValorFinalFrete, Is.EqualTo(19.45m));

        var dba0251 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 120.00m,
            PesoRealKg = 0.251m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(dba0251.Success, Is.True);
        Assert.That(dba0251.Data!.DescricaoFaixaPeso, Is.EqualTo("250g-500g"));
        Assert.That(dba0251.Data.ValorFinalFrete, Is.EqualTo(21.95m));
    }

    [Test]
    public async Task Peso1000Vs1020Vs1110_AplicaKgAdicionalComCeiling()
    {
        var peso10 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 150.00m,
            PesoRealKg = 10.00m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(peso10.Success, Is.True);
        Assert.That(peso10.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.FaixaPeso));
        Assert.That(peso10.Data.DescricaoFaixaPeso, Is.EqualTo("9-10kg"));
        Assert.That(peso10.Data.KgExcedentes, Is.EqualTo(0));
        Assert.That(peso10.Data.ValorFinalFrete, Is.EqualTo(58.95m));

        var peso1020 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 150.00m,
            PesoRealKg = 10.20m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(peso1020.Success, Is.True);
        Assert.That(peso1020.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.KgAdicional));
        Assert.That(peso1020.Data.DescricaoFaixaPeso, Is.EqualTo("9-10kg"));
        Assert.That(peso1020.Data.TarifaBase, Is.EqualTo(58.95m));
        Assert.That(peso1020.Data.KgExcedentes, Is.EqualTo(1));
        Assert.That(peso1020.Data.TarifaKgAdicional, Is.EqualTo(4.50m));
        Assert.That(peso1020.Data.ValorKgExcedente, Is.EqualTo(4.50m));
        Assert.That(peso1020.Data.ValorFinalFrete, Is.EqualTo(63.45m));

        var peso1110 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 150.00m,
            PesoRealKg = 11.10m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(peso1110.Success, Is.True);
        Assert.That(peso1110.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.KgAdicional));
        Assert.That(peso1110.Data.KgExcedentes, Is.EqualTo(2));
        Assert.That(peso1110.Data.ValorKgExcedente, Is.EqualTo(9.00m));
        Assert.That(peso1110.Data.ValorFinalFrete, Is.EqualTo(67.95m));
    }

    [Test]
    public async Task PrecoAcimaOuIgual200_DbaZona45EFba_FaixaPesoEKgAdicional()
    {
        var dba200 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 200.00m,
            PesoRealKg = 0.25m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(dba200.Success, Is.True);
        Assert.That(dba200.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.FaixaPeso));
        Assert.That(dba200.Data.ValorFinalFrete, Is.EqualTo(23.45m));

        var fba200 = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 200.00m,
            PesoRealKg = 0.10m,
            Modalidade = AmazonModalidades.Fba
        });
        Assert.That(fba200.Success, Is.True);
        Assert.That(fba200.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.FaixaPeso));
        Assert.That(fba200.Data.ValorFinalFrete, Is.EqualTo(16.50m));

        var fba249KgExtra = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 249.90m,
            PesoRealKg = 11.10m,
            Modalidade = AmazonModalidades.Fba
        });
        Assert.That(fba249KgExtra.Success, Is.True);
        Assert.That(fba249KgExtra.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.KgAdicional));
        Assert.That(fba249KgExtra.Data.TarifaBase, Is.EqualTo(55.40m));
        Assert.That(fba249KgExtra.Data.KgExcedentes, Is.EqualTo(2));
        Assert.That(fba249KgExtra.Data.TarifaKgAdicional, Is.EqualTo(4.40m));
        Assert.That(fba249KgExtra.Data.ValorKgExcedente, Is.EqualTo(8.80m));
        Assert.That(fba249KgExtra.Data.ValorFinalFrete, Is.EqualTo(64.20m));
    }

    [Test]
    public async Task SkuComDimensoesGrandes_IgnoraCubagemUsaApenasPesoRealERetornaMedidas()
    {
        _db.Produtos.Add(new Produto
        {
            Sku = "AMZ-CAIXA-LEVE",
            Nome = "Caixa Grande Leve",
            PesoRealKg = 0.25m,
            ComprimentoCm = 100m,
            LarguraCm = 100m,
            AlturaCm = 100m,
            AtualizadoEm = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var resultado = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            Sku = "amz-caixa-leve",
            PrecoProduto = 100.00m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });

        Assert.That(resultado.Success, Is.True);
        Assert.That(resultado.Data!.OrigemMedidas, Is.EqualTo("SKU_CADASTRO"));
        Assert.That(resultado.Data.PesoRealKg, Is.EqualTo(0.25m));
        Assert.That(resultado.Data.ComprimentoCm, Is.EqualTo(100m));
        Assert.That(resultado.Data.LarguraCm, Is.EqualTo(100m));
        Assert.That(resultado.Data.AlturaCm, Is.EqualTo(100m));
        Assert.That(resultado.Data.DescricaoFaixaPeso, Is.EqualTo("0-250g"));
        Assert.That(resultado.Data.ValorFinalFrete, Is.EqualTo(19.45m));
    }

    [Test]
    public async Task ConfiguracaoDinamica_AlteraLimitesETaxaFixaSemHardcode()
    {
        var cfg = await _db.ConfiguracoesAmazon.FirstAsync(c => c.Id == 1);
        cfg.TaxaFixaObrigatoria = 3.50m;
        cfg.LimitePrecoTabelaPeso = 100m;
        cfg.LimitePesoTabelaKg = 5m;
        await _db.SaveChangesAsync();

        // Preço 90 (< 100 novo limite): usa FIXO se existir faixa cobrindo 90, ou se ajustarmos faixa
        var faixaFixa = await _db.TarifasAmazon.FirstAsync(t => t.Id == 3);
        faixaFixa.PrecoMax = 99.99m;
        await _db.SaveChangesAsync();

        var abaixoNovoLimite = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 90.00m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(abaixoNovoLimite.Success, Is.True);
        Assert.That(abaixoNovoLimite.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.Fixo));
        Assert.That(abaixoNovoLimite.Data.TaxaFixaObrigatoria, Is.EqualTo(3.50m));
        Assert.That(abaixoNovoLimite.Data.ValorFinalFrete, Is.EqualTo(15.45m));

        // Peso 6.2 kg (> 5 kg novo limite): 2 kg excedentes + taxa fixa 3.50
        var acimaNovoPeso = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 120.00m,
            PesoRealKg = 6.20m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(acimaNovoPeso.Success, Is.True);
        Assert.That(acimaNovoPeso.Data!.TipoRegra, Is.EqualTo(AmazonTiposRegra.KgAdicional));
        Assert.That(acimaNovoPeso.Data.KgExcedentes, Is.EqualTo(2));
        Assert.That(acimaNovoPeso.Data.TaxaFixaObrigatoria, Is.EqualTo(3.50m));
        Assert.That(acimaNovoPeso.Data.ValorFinalFrete, Is.EqualTo(58.95m + 9.00m + 3.50m));
    }

    [Test]
    public async Task EntradasInvalidas_RetornamStatusCodesApropriados()
    {
        var precoZero = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 0m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(precoZero.Success, Is.False);
        Assert.That(precoZero.StatusCode, Is.EqualTo(400));

        var modalidadeInvalida = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 50m,
            Modalidade = "OUTRA"
        });
        Assert.That(modalidadeInvalida.Success, Is.False);
        Assert.That(modalidadeInvalida.StatusCode, Is.EqualTo(400));

        var semPesoAcimaLimite = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            PrecoProduto = 79m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(semPesoAcimaLimite.Success, Is.False);
        Assert.That(semPesoAcimaLimite.StatusCode, Is.EqualTo(400));

        var skuInexistente = await _service.CalcularAsync(new CalcularFreteAmazonRequest
        {
            Sku = "NAO-EXISTE",
            PrecoProduto = 79m,
            PesoRealKg = 0m,
            Modalidade = AmazonModalidades.Dba
        });
        Assert.That(skuInexistente.Success, Is.False);
        Assert.That(skuInexistente.StatusCode, Is.EqualTo(404));
    }

    private static void SeedDefaultAmazonData(AppDbContext db)
    {
        db.ConfiguracoesAmazon.Add(new ConfiguracaoAmazon
        {
            Id = 1,
            UsaPesoCubado = false,
            LimitePrecoTabelaPeso = 79m,
            LimitePesoTabelaKg = 10m,
            RegiaoReferenciaDba = "ZONA_4_5",
            TaxaFixaObrigatoria = 0m,
            AtualizadoEm = DateTime.UtcNow
        });

        db.TarifasAmazon.AddRange(
            // DBA FIXO
            new TarifaAmazon { Id = 1, Modalidade = "DBA", TipoRegra = "FIXO", DescricaoFaixaPeso = "Qualquer peso", PrecoMin = 0m, PrecoMax = 18.99m, PesoMinKg = 0m, PesoMaxKg = 99999m, Tarifa = 4.50m, FreteCliente = "PAGO_PELO_CLIENTE" },
            new TarifaAmazon { Id = 2, Modalidade = "DBA", TipoRegra = "FIXO", DescricaoFaixaPeso = "Qualquer peso", PrecoMin = 19.00m, PrecoMax = 29.99m, PesoMinKg = 0m, PesoMaxKg = 99999m, Tarifa = 8.50m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 3, Modalidade = "DBA", TipoRegra = "FIXO", DescricaoFaixaPeso = "Qualquer peso", PrecoMin = 30.00m, PrecoMax = 78.99m, PesoMinKg = 0m, PesoMaxKg = 99999m, Tarifa = 11.95m, FreteCliente = "GRATIS" },
            // FBA FIXO
            new TarifaAmazon { Id = 4, Modalidade = "FBA", TipoRegra = "FIXO", DescricaoFaixaPeso = "Qualquer peso", PrecoMin = 0m, PrecoMax = 29.99m, PesoMinKg = 0m, PesoMaxKg = 99999m, Tarifa = 6.50m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 5, Modalidade = "FBA", TipoRegra = "FIXO", DescricaoFaixaPeso = "Qualquer peso", PrecoMin = 30.00m, PrecoMax = 78.99m, PesoMinKg = 0m, PesoMaxKg = 99999m, Tarifa = 9.75m, FreteCliente = "GRATIS" },
            // DBA 79-199.99
            new TarifaAmazon { Id = 6, Modalidade = "DBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "0-250g", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 0m, PesoMaxKg = 0.25m, Tarifa = 19.45m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 7, Modalidade = "DBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "250g-500g", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 0.25m, PesoMaxKg = 0.50m, Tarifa = 21.95m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 8, Modalidade = "DBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "9-10kg", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 9.00m, PesoMaxKg = 10.00m, Tarifa = 58.95m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 9, Modalidade = "DBA", TipoRegra = "KG_ADICIONAL", DescricaoFaixaPeso = "Kg adicional", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 10.00m, PesoMaxKg = 99999m, Tarifa = 4.50m, FreteCliente = "GRATIS" },
            // DBA >= 200
            new TarifaAmazon { Id = 10, Modalidade = "DBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "0-250g", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 0m, PesoMaxKg = 0.25m, Tarifa = 23.45m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 11, Modalidade = "DBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "9-10kg", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 9.00m, PesoMaxKg = 10.00m, Tarifa = 72.95m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 12, Modalidade = "DBA", TipoRegra = "KG_ADICIONAL", DescricaoFaixaPeso = "Kg adicional", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 10.00m, PesoMaxKg = 99999m, Tarifa = 5.20m, FreteCliente = "GRATIS" },
            // FBA 79-199.99
            new TarifaAmazon { Id = 13, Modalidade = "FBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "0 a 100g", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 0m, PesoMaxKg = 0.10m, Tarifa = 14.20m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 14, Modalidade = "FBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "100g a 250g", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 0.10m, PesoMaxKg = 0.25m, Tarifa = 15.90m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 15, Modalidade = "FBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "9 a 10kg", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 9.00m, PesoMaxKg = 10.00m, Tarifa = 47.90m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 16, Modalidade = "FBA", TipoRegra = "KG_ADICIONAL", DescricaoFaixaPeso = "Kg adicional", PrecoMin = 79.00m, PrecoMax = 199.99m, PesoMinKg = 10.00m, PesoMaxKg = 99999m, Tarifa = 3.90m, FreteCliente = "GRATIS" },
            // FBA >= 200
            new TarifaAmazon { Id = 17, Modalidade = "FBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "0 a 100g", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 0m, PesoMaxKg = 0.10m, Tarifa = 16.50m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 18, Modalidade = "FBA", TipoRegra = "FAIXA_PESO", DescricaoFaixaPeso = "9 a 10kg", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 9.00m, PesoMaxKg = 10.00m, Tarifa = 55.40m, FreteCliente = "GRATIS" },
            new TarifaAmazon { Id = 19, Modalidade = "FBA", TipoRegra = "KG_ADICIONAL", DescricaoFaixaPeso = "Kg adicional", PrecoMin = 200.00m, PrecoMax = 999999.99m, PesoMinKg = 10.00m, PesoMaxKg = 99999m, Tarifa = 4.40m, FreteCliente = "GRATIS" });
    }
}

[TestFixture]
public sealed class AmazonEndpointsIntegrationTests
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
    public async Task AmazonEndpoints_ExigemAutenticacaoEAplicamEscopoDeApiKey()
    {
        var configNoAuth = await _client.GetAsync("/api/v1/tarifas/amazon/config");
        Assert.That(configNoAuth.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var syncNoAuth = await _client.PostAsJsonAsync("/api/v1/tarifas/amazon/sync", new { });
        Assert.That(syncNoAuth.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var calcNoAuth = await _client.PostAsJsonAsync("/api/v1/frete/amazon/calcular", new { });
        Assert.That(calcNoAuth.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Chave de sync não autoriza cálculo externo
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");
        var calcWithSyncKey = await _client.PostAsJsonAsync("/api/v1/frete/amazon/calcular", new
        {
            precoProduto = 100m,
            pesoRealKg = 0.25m,
            modalidade = "DBA"
        });
        Assert.That(calcWithSyncKey.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SyncPushAppsScript_PersisteAtomicamente_ConfigECalculoFuncionam()
    {
        _client.DefaultRequestHeaders.Add("X-Api-Key", "sync-test-key");

        var syncPayload = new
        {
            origem = "AUTOMACAO_SHEETS_TARIFAS_AMAZON",
            configuracao = new
            {
                usaPesoCubado = false,
                limitePrecoTabelaPeso = 79,
                limitePesoTabelaKg = 10,
                regiaoReferenciaDba = "ZONA_4_5",
                taxaFixaObrigatoria = 0
            },
            tarifas = new object[]
            {
                new
                {
                    id = 1,
                    modalidade = "DBA",
                    tipoRegra = "FIXO",
                    descricaoFaixaPeso = "Qualquer peso",
                    precoMin = 0,
                    precoMax = 18.99,
                    pesoMinKg = 0,
                    pesoMaxKg = 99999,
                    tarifa = 4.5,
                    freteCliente = "PAGO_PELO_CLIENTE"
                },
                new
                {
                    id = 2,
                    modalidade = "DBA",
                    tipoRegra = "FAIXA_PESO",
                    descricaoFaixaPeso = "0-250g",
                    precoMin = 79,
                    precoMax = 199.99,
                    pesoMinKg = 0,
                    pesoMaxKg = 0.25,
                    tarifa = 19.45,
                    freteCliente = "GRATIS"
                }
            }
        };

        var syncRes = await _client.PostAsJsonAsync("/api/v1/tarifas/amazon/sync", syncPayload);
        Assert.That(syncRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var syncJson = JsonDocument.Parse(await syncRes.Content.ReadAsStringAsync()))
        {
            Assert.That(syncJson.RootElement.GetProperty("origem").GetString(),
                Is.EqualTo("AUTOMACAO_SHEETS_TARIFAS_AMAZON"));
            Assert.That(syncJson.RootElement.GetProperty("inseridos").GetInt32(), Is.EqualTo(2));
            Assert.That(syncJson.RootElement.GetProperty("atualizados").GetInt32(), Is.EqualTo(0));
            Assert.That(syncJson.RootElement.GetProperty("removidos").GetInt32(), Is.EqualTo(0));
        }

        // Lote com erro em um item deve retornar 400 e manter os 2 registros intactos
        var invalidSync = await _client.PostAsJsonAsync("/api/v1/tarifas/amazon/sync", new
        {
            origem = "AUTOMACAO_SHEETS_TARIFAS_AMAZON",
            configuracao = syncPayload.configuracao,
            tarifas = new object[]
            {
                new
                {
                    id = 1,
                    modalidade = "INVALIDA",
                    tipoRegra = "FIXO",
                    descricaoFaixaPeso = "Qualquer peso",
                    precoMin = 0,
                    precoMax = 18.99,
                    pesoMinKg = 0,
                    pesoMaxKg = 99999,
                    tarifa = 4.5,
                    freteCliente = "PAGO_PELO_CLIENTE"
                }
            }
        });
        Assert.That(invalidSync.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // Login via cookie HttpOnly para consultar config e calcular frete
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        var cookie = await LoginAdminAsync();
        AuthCookieTestHelper.UseAuthCookie(_client, cookie);

        var configRes = await _client.GetAsync("/api/v1/tarifas/amazon/config");
        Assert.That(configRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var cfgJson = JsonDocument.Parse(await configRes.Content.ReadAsStringAsync()))
        {
            Assert.That(cfgJson.RootElement.GetProperty("usaPesoCubado").GetBoolean(), Is.False);
            Assert.That(cfgJson.RootElement.GetProperty("limitePrecoTabelaPeso").GetDecimal(), Is.EqualTo(79m));
            Assert.That(cfgJson.RootElement.GetProperty("limitePesoTabelaKg").GetDecimal(), Is.EqualTo(10m));
            Assert.That(cfgJson.RootElement.GetProperty("regiaoReferenciaDba").GetString(), Is.EqualTo("ZONA_4_5"));
            Assert.That(cfgJson.RootElement.GetProperty("taxaFixaObrigatoria").GetDecimal(), Is.EqualTo(0m));
        }

        var calcRes = await _client.PostAsJsonAsync("/api/v1/frete/amazon/calcular", new
        {
            sku = (string?)null,
            precoProduto = 18.99m,
            pesoRealKg = 0m,
            modalidade = "DBA"
        });
        Assert.That(calcRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var calcJson = JsonDocument.Parse(await calcRes.Content.ReadAsStringAsync()))
        {
            Assert.That(calcJson.RootElement.GetProperty("modalidade").GetString(), Is.EqualTo("DBA"));
            Assert.That(calcJson.RootElement.GetProperty("tipoRegra").GetString(), Is.EqualTo("FIXO"));
            Assert.That(calcJson.RootElement.GetProperty("freteCliente").GetString(), Is.EqualTo("PAGO_PELO_CLIENTE"));
            Assert.That(calcJson.RootElement.GetProperty("valorFinalFrete").GetDecimal(), Is.EqualTo(4.50m));
        }
    }

    private async Task<string> LoginAdminAsync()
    {
        AuthCookieTestHelper.UseAuthCookie(_client, null);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@easyfrete.test",
            password = "AdminTeste123!"
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.TryGetProperty("token", out _), Is.False);
        return AuthCookieTestHelper.ExtractAuthCookie(login);
    }
}
