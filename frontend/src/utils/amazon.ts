import type {
  AmazonFreteCliente,
  AmazonModalidade,
  AmazonTipoRegra,
  CalcularFreteAmazonApiResponse,
  ConfiguracaoAmazonApiResponse,
  ResultadoFreteAmazon,
} from "../types";
import { fmtDecimalBr } from "./formatters";

const VALID_MODALIDADES: ReadonlySet<AmazonModalidade> = new Set(["DBA", "FBA"]);
const VALID_REGRAS: ReadonlySet<AmazonTipoRegra> = new Set([
  "FIXO",
  "FAIXA_PESO",
  "KG_ADICIONAL",
]);
const VALID_FRETE_CLIENTE: ReadonlySet<AmazonFreteCliente> = new Set([
  "GRATIS",
  "PAGO_PELO_CLIENTE",
]);

export function canCalculateAmazon(
  config: ConfiguracaoAmazonApiResponse | null,
  preco: number,
  pesoRealKg: number,
): boolean {
  if (!config || !Number.isFinite(preco) || preco <= 0) {
    return false;
  }

  if (preco < config.limitePrecoTabelaPeso) {
    return true;
  }

  return Number.isFinite(pesoRealKg) && pesoRealKg > 0;
}

export function formatDimensionDisplay(
  value: number | null | undefined,
): string {
  if (typeof value !== "number" || !Number.isFinite(value) || value <= 0) {
    return "—";
  }

  return fmtDecimalBr(value, 1);
}

export function formatModalidadeDescricao(
  modalidade: AmazonModalidade,
): string {
  return modalidade === "DBA"
    ? "DBA · Delivery by Amazon"
    : "FBA · Fulfillment by Amazon";
}

export function mapAmazonResponseToResult(
  data: CalcularFreteAmazonApiResponse,
): ResultadoFreteAmazon | null {
  if (!data.modalidade || !VALID_MODALIDADES.has(data.modalidade)) {
    return null;
  }

  if (!data.tipoRegra || !VALID_REGRAS.has(data.tipoRegra)) {
    return null;
  }

  const freteCliente: AmazonFreteCliente = data.freteCliente
    ? VALID_FRETE_CLIENTE.has(data.freteCliente)
      ? data.freteCliente
      : "GRATIS"
    : "GRATIS";

  const tarifaBase = Number(data.tarifaBase);
  const taxaFixa = Number(data.taxaFixaObrigatoria ?? 0);
  const custoTotal = Number(data.valorFinalFrete);
  const kgExcedentes = Number(data.kgExcedentes ?? 0);
  const tarifaKgAdicional = Number(data.tarifaKgAdicional ?? 0);
  const valorKgExcedente = Number(data.valorKgExcedente ?? 0);

  if (
    !Number.isFinite(tarifaBase) ||
    !Number.isFinite(taxaFixa) ||
    !Number.isFinite(custoTotal) ||
    !Number.isFinite(kgExcedentes) ||
    !Number.isFinite(tarifaKgAdicional) ||
    !Number.isFinite(valorKgExcedente)
  ) {
    return null;
  }

  const baseDesc =
    typeof data.descricaoFaixaPeso === "string" &&
    data.descricaoFaixaPeso.trim().length > 0
      ? data.descricaoFaixaPeso.trim()
      : "—";

  const faixaPeso =
    data.tipoRegra === "KG_ADICIONAL" && kgExcedentes > 0
      ? `${baseDesc} + ${kgExcedentes} kg excedente${kgExcedentes > 1 ? "s" : ""}`
      : baseDesc;

  return {
    modalidade: data.modalidade,
    tipoRegra: data.tipoRegra,
    faixaPeso,
    freteCliente,
    tarifaBase,
    kgExcedentes,
    tarifaKgAdicional,
    valorKgExcedente,
    taxaFixa,
    custoTotal,
  };
}
