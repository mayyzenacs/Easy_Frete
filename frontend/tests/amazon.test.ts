import { describe, expect, test } from "bun:test";
import { MARKETPLACES } from "../src/config/marketplaces";
import type { ConfiguracaoAmazonApiResponse } from "../src/types";
import {
  canCalculateAmazon,
  formatDimensionDisplay,
  formatModalidadeDescricao,
  mapAmazonResponseToResult,
} from "../src/utils/amazon";

const baseConfig: ConfiguracaoAmazonApiResponse = {
  usaPesoCubado: false,
  limitePrecoTabelaPeso: 79,
  limitePesoTabelaKg: 10,
  regiaoReferenciaDba: "ZONA_4_5",
  taxaFixaObrigatoria: 0,
};

describe("MARKETPLACES config", () => {
  test("habilita Amazon mantendo Magalu disponível", () => {
    expect(MARKETPLACES.magalu.available).toBe(true);
    expect(MARKETPLACES.amazon.available).toBe(true);
  });
});

describe("canCalculateAmazon", () => {
  test("bloqueia quando config não está carregado ou preço é inválido", () => {
    expect(canCalculateAmazon(null, 50, 1)).toBe(false);
    expect(canCalculateAmazon(baseConfig, 0, 1)).toBe(false);
    expect(canCalculateAmazon(baseConfig, -10, 1)).toBe(false);
    expect(canCalculateAmazon(baseConfig, Number.NaN, 1)).toBe(false);
  });

  test("permite calcular abaixo do limite de tabela mesmo sem peso", () => {
    expect(canCalculateAmazon(baseConfig, 18.99, 0)).toBe(true);
    expect(canCalculateAmazon(baseConfig, 78.99, 0)).toBe(true);
  });

  test("exige peso positivo a partir do limite configurado dinamicamente", () => {
    expect(canCalculateAmazon(baseConfig, 79, 0)).toBe(false);
    expect(canCalculateAmazon(baseConfig, 79, 0.25)).toBe(true);

    const customConfig: ConfiguracaoAmazonApiResponse = {
      ...baseConfig,
      limitePrecoTabelaPeso: 100,
    };
    expect(canCalculateAmazon(customConfig, 85, 0)).toBe(true);
    expect(canCalculateAmazon(customConfig, 100, 0)).toBe(false);
  });
});

describe("mapAmazonResponseToResult", () => {
  test("mapeia regra FIXO com PAGO_PELO_CLIENTE e GRATIS", () => {
    const pago = mapAmazonResponseToResult({
      modalidade: "DBA",
      tipoRegra: "FIXO",
      descricaoFaixaPeso: "Qualquer peso",
      freteCliente: "PAGO_PELO_CLIENTE",
      tarifaBase: 4.5,
      taxaFixaObrigatoria: 0,
      valorFinalFrete: 4.5,
    });

    expect(pago).toEqual({
      modalidade: "DBA",
      tipoRegra: "FIXO",
      faixaPeso: "Qualquer peso",
      freteCliente: "PAGO_PELO_CLIENTE",
      tarifaBase: 4.5,
      kgExcedentes: 0,
      tarifaKgAdicional: 0,
      valorKgExcedente: 0,
      taxaFixa: 0,
      custoTotal: 4.5,
    });

    const gratis = mapAmazonResponseToResult({
      modalidade: "FBA",
      tipoRegra: "FIXO",
      descricaoFaixaPeso: "Qualquer peso",
      freteCliente: "GRATIS",
      tarifaBase: 6.5,
      taxaFixaObrigatoria: 0,
      valorFinalFrete: 6.5,
    });

    expect(gratis?.freteCliente).toBe("GRATIS");
    expect(gratis?.modalidade).toBe("FBA");
  });

  test("mapeia regra FAIXA_PESO e taxa fixa dinâmica quando > 0", () => {
    const result = mapAmazonResponseToResult({
      modalidade: "DBA",
      tipoRegra: "FAIXA_PESO",
      descricaoFaixaPeso: "0-250g",
      freteCliente: "GRATIS",
      tarifaBase: 19.45,
      taxaFixaObrigatoria: 2.5,
      valorFinalFrete: 21.95,
    });

    expect(result).not.toBeNull();
    expect(result?.faixaPeso).toBe("0-250g");
    expect(result?.taxaFixa).toBe(2.5);
    expect(result?.custoTotal).toBe(21.95);
  });

  test("formata KG_ADICIONAL no singular e plural", () => {
    const umKg = mapAmazonResponseToResult({
      modalidade: "DBA",
      tipoRegra: "KG_ADICIONAL",
      descricaoFaixaPeso: "9-10kg",
      freteCliente: "GRATIS",
      tarifaBase: 58.95,
      kgExcedentes: 1,
      tarifaKgAdicional: 4.5,
      valorKgExcedente: 4.5,
      taxaFixaObrigatoria: 0,
      valorFinalFrete: 63.45,
    });

    expect(umKg?.faixaPeso).toBe("9-10kg + 1 kg excedente");
    expect(umKg?.valorKgExcedente).toBe(4.5);

    const doisKg = mapAmazonResponseToResult({
      modalidade: "FBA",
      tipoRegra: "KG_ADICIONAL",
      descricaoFaixaPeso: "9 a 10kg",
      freteCliente: "GRATIS",
      tarifaBase: 55.4,
      kgExcedentes: 2,
      tarifaKgAdicional: 4.4,
      valorKgExcedente: 8.8,
      taxaFixaObrigatoria: 0,
      valorFinalFrete: 64.2,
    });

    expect(doisKg?.faixaPeso).toBe("9 a 10kg + 2 kg excedentes");
    expect(doisKg?.custoTotal).toBe(64.2);
  });

  test("rejeita payloads inválidos ou valores não finitos", () => {
    expect(
      mapAmazonResponseToResult({
        tipoRegra: "FIXO",
        tarifaBase: 4.5,
        valorFinalFrete: 4.5,
      }),
    ).toBeNull();

    expect(
      mapAmazonResponseToResult({
        modalidade: "DBA",
        tarifaBase: 4.5,
        valorFinalFrete: 4.5,
      }),
    ).toBeNull();

    expect(
      mapAmazonResponseToResult({
        modalidade: "DBA",
        tipoRegra: "FIXO",
        tarifaBase: Number.NaN,
        valorFinalFrete: 4.5,
      }),
    ).toBeNull();
  });
});

describe("formatDimensionDisplay e formatModalidadeDescricao", () => {
  test("formata dimensões para conferência visual", () => {
    expect(formatDimensionDisplay(null)).toBe("—");
    expect(formatDimensionDisplay(0)).toBe("—");
    expect(formatDimensionDisplay(-5)).toBe("—");
    expect(formatDimensionDisplay(28)).toBe("28,0");
    expect(formatDimensionDisplay(9.5)).toBe("9,5");
  });

  test("formata descrição das modalidades Amazon", () => {
    expect(formatModalidadeDescricao("DBA")).toBe("DBA · Delivery by Amazon");
    expect(formatModalidadeDescricao("FBA")).toBe(
      "FBA · Fulfillment by Amazon",
    );
  });
});
