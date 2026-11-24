import { useState, useEffect } from "react";
import {
  Search,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Scale,
  Box,
  Copy,
  Check,
} from "lucide-react";
import {
  FATOR_CUBAGEM_MAGALU,
  PISO_REPUTACAO_MAGALU,
} from "../config/marketplaces";
import type { ApiClient } from "../services/api";
import type { ResultadoFrete, SkuFeedback } from "../types";
import {
  brl,
  fmtDecimalBr,
  fmtKg,
  num,
  sanitizeDecimalInput,
} from "../utils/formatters";

interface MagaluCalculatorProps {
  api: ApiClient;
}

export function MagaluCalculator({ api }: MagaluCalculatorProps) {
  const [sku, setSku] = useState("");
  const [skuFeedback, setSkuFeedback] = useState<SkuFeedback | null>(null);
  const [linkDrive, setLinkDrive] = useState<string | null>(null);
  const [skuLoad, setSkuLoad] = useState(false);

  const [peso, setPeso] = useState("");
  const [c, setC] = useState("");
  const [l, setL] = useState("");
  const [a, setA] = useState("");
  const [valor, setValor] = useState("");
  const [ful, setFul] = useState(true);
  const [acima97, setAcima97] = useState(true);

  const [res, setRes] = useState<ResultadoFrete | null>(null);
  const [calc, setCalc] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);
  const [copiadoFrete, setCopiadoFrete] = useState(false);

  useEffect(() => {
    if (!copiadoFrete) return;
    const timer = window.setTimeout(() => setCopiadoFrete(false), 1900);
    return () => window.clearTimeout(timer);
  }, [copiadoFrete]);

  const copiarFreteTotal = async () => {
    if (!res) return;
    const texto = fmtDecimalBr(res.custoTotal);
    try {
      await navigator.clipboard.writeText(texto);
      setCopiadoFrete(true);
    } catch {
      const ta = document.createElement("textarea");
      ta.value = texto;
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      try {
        document.execCommand("copy");
        setCopiadoFrete(true);
      } catch {
        setCopiadoFrete(false);
      } finally {
        document.body.removeChild(ta);
      }
    }
  };

  const real = num(peso);
  const cubado = +(
    (num(c) * num(l) * num(a)) /
    FATOR_CUBAGEM_MAGALU
  ).toFixed(3);
  const realMaior = real >= cubado;
  const v = num(valor);
  const mostra97 = !ful && v >= PISO_REPUTACAO_MAGALU;

  const buscarSku = async () => {
    const code = sku.trim().toUpperCase();
    if (!code) return;
    setSkuLoad(true);
    setSkuFeedback(null);
    setPeso("");
    setC("");
    setL("");
    setA("");
    setLinkDrive(null);
    setRes(null);
    setCopiadoFrete(false);
    setCalcError(null);

    try {
      const result = await api.getProdutoBySku(code);
      if (!result.ok) {
        if (result.unauthorized) return;
        setSkuFeedback({
          tipo: "error",
          mensagem:
            result.status === 404
              ? "SKU não encontrado. Solicite o cadastro das medidas na planilha."
              : result.error,
        });
        return;
      }

      const p = result.data;
      const pesoApi = Number(p.pesoRealKg ?? p.pesoBruto ?? p.peso ?? 0);
      const comprimentoApi = Number(p.comprimentoCm ?? p.comprimento ?? 0);
      const larguraApi = Number(p.larguraCm ?? p.largura ?? 0);
      const alturaApi = Number(p.alturaCm ?? p.altura ?? 0);

      setPeso(pesoApi > 0 ? String(pesoApi).replace(".", ",") : "");
      setC(comprimentoApi > 0 ? String(comprimentoApi).replace(".", ",") : "");
      setL(larguraApi > 0 ? String(larguraApi).replace(".", ",") : "");
      setA(alturaApi > 0 ? String(alturaApi).replace(".", ",") : "");

      const drive = typeof p.linkDrive === "string" ? p.linkDrive.trim() : "";
      setLinkDrive(drive || null);

      const faltando = [
        pesoApi <= 0 ? "peso" : null,
        comprimentoApi <= 0 ? "comprimento" : null,
        larguraApi <= 0 ? "largura" : null,
        alturaApi <= 0 ? "altura" : null,
      ].filter((item): item is string => item !== null);

      const nomeProduto = String(p.nome || code).trim();

      if (faltando.length === 0) {
        setSkuFeedback({
          tipo: "ok",
          titulo: nomeProduto,
          mensagem: "medidas preenchidas",
        });
      } else if (faltando.length === 4) {
        setSkuFeedback({
          tipo: "warn",
          titulo: nomeProduto,
          mensagem:
            "Produto encontrado, mas ainda sem medidas cadastradas. Preencha manualmente abaixo.",
        });
      } else {
        setSkuFeedback({
          tipo: "warn",
          titulo: nomeProduto,
          mensagem: `Produto encontrado, mas faltam medidas. Preencha manualmente: ${faltando.join(", ")}.`,
        });
      }
    } finally {
      setSkuLoad(false);
    }
  };

  const calcular = async () => {
    setCalc(true);
    setCalcError(null);
    setRes(null);
    setCopiadoFrete(false);

    try {
      const result = await api.calcularFreteMagalu({
        sku: sku.trim() || null,
        precoProduto: v,
        pesoRealKg: real,
        comprimentoCm: num(c),
        larguraCm: num(l),
        alturaCm: num(a),
        isFulfillment: ful,
        reputacaoMaior97: acima97,
      });

      if (!result.ok) {
        if (!result.unauthorized) {
          setCalcError(result.error);
        }
        return;
      }

      const d = result.data;
      const faixaPreco =
        typeof d.faixaPrecoAplicada === "string" && d.faixaPrecoAplicada.trim()
          ? d.faixaPrecoAplicada.trim()
          : "—";
      const faixaPeso =
        typeof d.faixaPesoAplicada === "string" && d.faixaPesoAplicada.trim()
          ? d.faixaPesoAplicada.trim()
          : "—";
      const modalidade =
        typeof d.modalidadeAplicada === "string" && d.modalidadeAplicada.trim()
          ? d.modalidadeAplicada.trim()
          : "";

      const cand: ResultadoFrete = {
        faixa: faixaPreco,
        faixaPeso,
        tarifaLabel: modalidade ? `Tarifa · ${modalidade}` : "—",
        tarifaValor: Number(d.tarifaBase ?? 0),
        taxaFixa: Number(d.taxaFixaObrigatoria ?? 0),
        custoTotal: Number(d.valorFinalFrete ?? 0),
      };

      if (
        Number.isFinite(cand.tarifaValor) &&
        Number.isFinite(cand.custoTotal)
      ) {
        setRes(cand);
      } else {
        setCalcError("Não foi possível calcular o frete. Tente novamente.");
      }
    } finally {
      setCalc(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-black tracking-tight">
          Cálculo de frete <span className="text-yellow-400">· Magalu</span>
        </h1>
        <p className="text-sm text-zinc-400 mt-0.5">
          Busque um SKU ou informe as medidas do produto.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-7 space-y-5">
          {/* Produto e Medidas */}
          <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                SKU
              </label>
              <div className="flex gap-2">
                <input
                  type="text"
                  value={sku}
                  onChange={(e) => setSku(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && void buscarSku()}
                  placeholder="Digite o SKU e pressione Enter"
                  className="flex-1 px-3.5 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm uppercase placeholder:normal-case focus:outline-none focus:border-yellow-400"
                />
                <button
                  type="button"
                  onClick={() => void buscarSku()}
                  disabled={skuLoad}
                  className="px-4 py-2 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 font-bold text-xs flex items-center gap-1.5 cursor-pointer shrink-0"
                >
                  <Search className="w-3.5 h-3.5" />
                  {skuLoad ? "..." : "Buscar"}
                </button>
              </div>

              {skuFeedback?.tipo === "ok" && (
                <div
                  role="status"
                  className="mt-2.5 flex items-center justify-between gap-3 p-2.5 rounded-xl bg-emerald-500/10 border border-emerald-500/25"
                >
                  <span className="text-xs text-emerald-300 flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
                    <span>
                      <b>{skuFeedback.titulo}</b> · {skuFeedback.mensagem}
                    </span>
                  </span>
                  {linkDrive && (
                    <a
                      href={linkDrive}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="shrink-0 px-2.5 py-1 rounded-lg bg-blue-500 hover:bg-blue-400 text-white text-[11px] font-bold flex items-center gap-1"
                    >
                      Drive <ExternalLink className="w-3 h-3" />
                    </a>
                  )}
                </div>
              )}

              {skuFeedback?.tipo === "warn" && (
                <div
                  role="alert"
                  className="mt-2.5 flex items-center justify-between gap-3 p-2.5 rounded-xl bg-yellow-400/10 border border-yellow-400/25 text-xs text-yellow-200"
                >
                  <span className="flex items-start gap-2">
                    <AlertCircle className="w-4 h-4 shrink-0 text-yellow-400 mt-px" />
                    <span>
                      {skuFeedback.titulo && <b>{skuFeedback.titulo}: </b>}
                      {skuFeedback.mensagem}
                    </span>
                  </span>
                  {linkDrive && (
                    <a
                      href={linkDrive}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="shrink-0 px-2.5 py-1 rounded-lg bg-blue-500 hover:bg-blue-400 text-white text-[11px] font-bold flex items-center gap-1"
                    >
                      Drive <ExternalLink className="w-3 h-3" />
                    </a>
                  )}
                </div>
              )}

              {skuFeedback?.tipo === "error" && (
                <div
                  role="alert"
                  className="mt-2.5 flex items-center gap-2 p-2.5 rounded-xl bg-red-500/10 border border-red-500/25 text-xs text-red-300"
                >
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
                  <span>{skuFeedback.mensagem}</span>
                </div>
              )}
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                {
                  label: "Peso (kg)",
                  val: peso,
                  set: setPeso,
                  placeholder: "0,00",
                },
                {
                  label: "Comprimento (cm)",
                  val: c,
                  set: setC,
                  placeholder: "0,0",
                },
                {
                  label: "Largura (cm)",
                  val: l,
                  set: setL,
                  placeholder: "0,0",
                },
                {
                  label: "Altura (cm)",
                  val: a,
                  set: setA,
                  placeholder: "0,0",
                },
              ].map((f) => (
                <div key={f.label}>
                  <label className="block text-xs text-zinc-400 mb-1">
                    {f.label}
                  </label>
                  <input
                    type="text"
                    inputMode="decimal"
                    autoComplete="off"
                    placeholder={f.placeholder}
                    value={f.val}
                    onChange={(e) => {
                      f.set(sanitizeDecimalInput(e.target.value));
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm font-semibold tabular-nums focus:outline-none focus:border-yellow-400 transition-colors"
                  />
                </div>
              ))}
            </div>
          </section>

          {/* Preço e Modalidade */}
          <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-12 gap-3 items-end">
              <div className="sm:col-span-7">
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  Preço de venda
                </label>
                <div className="relative flex items-center">
                  <span className="absolute left-3.5 text-sm font-bold text-zinc-500 pointer-events-none select-none">
                    R$
                  </span>
                  <input
                    type="text"
                    inputMode="decimal"
                    autoComplete="off"
                    placeholder="0,00"
                    value={valor}
                    onChange={(e) => {
                      setValor(sanitizeDecimalInput(e.target.value));
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className="w-full h-11 pl-10 pr-3.5 rounded-xl bg-zinc-950 border border-zinc-800 text-base font-bold text-yellow-400 tabular-nums focus:outline-none focus:border-yellow-400 transition-colors"
                  />
                </div>
              </div>

              <div className="sm:col-span-5">
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  Fulfillment{" "}
                  <span className="text-zinc-500 font-normal">(CD Magalu)</span>
                </label>
                <div
                  role="group"
                  aria-label="Fulfillment (CD Magalu)"
                  className="grid grid-cols-2 gap-1 h-11 p-1 rounded-xl bg-zinc-950 border border-zinc-800"
                >
                  <button
                    type="button"
                    onClick={() => {
                      setFul(true);
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className={`rounded-lg text-xs transition cursor-pointer ${
                      ful
                        ? "bg-yellow-400 text-zinc-950 font-extrabold"
                        : "text-zinc-400 hover:text-zinc-200 font-bold"
                    }`}
                  >
                    Sim
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setFul(false);
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className={`rounded-lg text-xs transition cursor-pointer ${
                      !ful
                        ? "bg-zinc-800 text-zinc-100 font-extrabold"
                        : "text-zinc-400 hover:text-zinc-200 font-bold"
                    }`}
                  >
                    Não
                  </button>
                </div>
              </div>
            </div>

            {mostra97 && (
              <div className="flex items-center justify-between gap-3 px-3.5 py-2.5 rounded-xl bg-zinc-950/90 border border-yellow-400/25">
                <div className="min-w-0">
                  <span className="text-xs font-semibold text-zinc-200 block">
                    Reputação acima de 97%?
                  </span>
                  <span className="text-[11px] text-zinc-500">
                    Aplica desconto na coleta e entrega
                  </span>
                </div>
                <div
                  role="group"
                  aria-label="Reputação acima de 97%"
                  className="grid grid-cols-2 gap-1 p-1 rounded-lg bg-zinc-900 border border-zinc-800 shrink-0 w-28"
                >
                  <button
                    type="button"
                    onClick={() => {
                      setAcima97(true);
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className={`py-1 rounded-md text-xs transition cursor-pointer ${
                      acima97
                        ? "bg-yellow-400 text-zinc-950 font-extrabold"
                        : "text-zinc-400 hover:text-zinc-200 font-bold"
                    }`}
                  >
                    Sim
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setAcima97(false);
                      setRes(null);
                      setCopiadoFrete(false);
                    }}
                    className={`py-1 rounded-md text-xs transition cursor-pointer ${
                      !acima97
                        ? "bg-zinc-800 text-zinc-100 font-extrabold"
                        : "text-zinc-400 hover:text-zinc-200 font-bold"
                    }`}
                  >
                    Não
                  </button>
                </div>
              </div>
            )}

            <button
              type="button"
              onClick={() => void calcular()}
              disabled={
                calc || ![peso, c, l, a].every((campo) => num(campo) > 0)
              }
              className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 disabled:opacity-40 disabled:cursor-not-allowed text-zinc-950 font-black text-sm tracking-wide uppercase transition cursor-pointer"
            >
              {calc ? "Calculando..." : "Calcular frete"}
            </button>

            {calcError && (
              <div
                role="alert"
                className="p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2"
              >
                <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
                <span>{calcError}</span>
              </div>
            )}
          </section>
        </div>

        {/* Coluna Direita: Pesos e Resultado */}
        <div className="lg:col-span-5 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div
              className={`rounded-2xl p-3.5 border transition ${
                realMaior
                  ? "bg-emerald-950/25 border-emerald-500/70"
                  : "bg-zinc-900/80 border-zinc-800"
              }`}
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-medium text-zinc-400 flex items-center gap-1.5">
                  <Scale className="w-3.5 h-3.5 text-emerald-400" /> Peso real
                </span>
                {realMaior && (
                  <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                    USADO
                  </span>
                )}
              </div>
              <div className="text-2xl font-black">
                {fmtKg(real)}{" "}
                <span className="text-xs font-normal text-zinc-400">kg</span>
              </div>
            </div>

            <div
              className={`rounded-2xl p-3.5 border transition ${
                !realMaior
                  ? "bg-emerald-950/25 border-emerald-500/70"
                  : "bg-zinc-900/80 border-zinc-800"
              }`}
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-medium text-zinc-400 flex items-center gap-1.5">
                  <Box className="w-3.5 h-3.5 text-yellow-400" /> Peso cubado
                </span>
                {!realMaior && (
                  <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                    USADO
                  </span>
                )}
              </div>
              <div className="text-2xl font-black">
                {fmtKg(cubado)}{" "}
                <span className="text-xs font-normal text-zinc-400">kg</span>
              </div>
            </div>
          </div>

          <p className="text-[11px] text-zinc-500 -mt-1 px-1">
            O frete considera o maior valor entre peso real e cubado (C x L x A
            / {FATOR_CUBAGEM_MAGALU}).
          </p>

          {res ? (
            <section className="bg-zinc-900 border-2 border-yellow-400 rounded-2xl p-5 space-y-3">
              <h2 className="text-sm font-bold text-yellow-400 flex items-center gap-1.5">
                <CheckCircle2 className="w-4 h-4" /> Resultado
              </h2>

              <dl className="divide-y divide-zinc-800 text-sm">
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">Faixa de preço</dt>
                  <dd className="font-semibold text-right">{res.faixa}</dd>
                </div>
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">Faixa de peso</dt>
                  <dd className="font-semibold text-right">{res.faixaPeso}</dd>
                </div>
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">{res.tarifaLabel}</dt>
                  <dd className="font-semibold tabular-nums">
                    {brl(res.tarifaValor)}
                  </dd>
                </div>
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">Taxa fixa</dt>
                  <dd className="font-semibold tabular-nums">
                    + {brl(res.taxaFixa)}
                  </dd>
                </div>
              </dl>

              <button
                type="button"
                onClick={() => void copiarFreteTotal()}
                title="Clique para copiar o frete total"
                className={`w-full p-3.5 rounded-xl text-zinc-950 flex items-center justify-between gap-3 transition-all duration-200 cursor-pointer group text-left select-none active:scale-[0.985] focus:outline-none focus:ring-2 focus:ring-yellow-300/60 ${
                  copiadoFrete
                    ? "bg-emerald-400 hover:bg-emerald-300"
                    : "bg-yellow-400 hover:bg-yellow-300"
                }`}
              >
                <div className="flex flex-col">
                  <span className="text-xs font-black uppercase tracking-wider">
                    {copiadoFrete ? "Copiado!" : "Frete total"}
                  </span>
                  <span className="text-[11px] font-semibold text-zinc-950/60 group-hover:text-zinc-950/85 transition-colors">
                    {copiadoFrete
                      ? `${fmtDecimalBr(res.custoTotal)} na área de transferência`
                      : "Clique para copiar"}
                  </span>
                </div>

                <div className="flex items-center gap-2">
                  <span className="text-2xl font-black tabular-nums tracking-tight">
                    {brl(res.custoTotal)}
                  </span>
                  <span
                    className={`w-7 h-7 rounded-lg flex items-center justify-center transition-all ${
                      copiadoFrete
                        ? "bg-zinc-950/15"
                        : "bg-zinc-950/10 group-hover:bg-zinc-950/15"
                    }`}
                  >
                    {copiadoFrete ? (
                      <Check className="w-4 h-4 text-zinc-950 stroke-[2.8]" />
                    ) : (
                      <Copy className="w-4 h-4 text-zinc-950/75 group-hover:text-zinc-950 transition-transform group-hover:scale-105" />
                    )}
                  </span>
                </div>
              </button>
            </section>
          ) : (
            <div className="rounded-2xl border border-dashed border-zinc-800 bg-zinc-900/40 p-8 text-center">
              <p className="text-sm font-semibold text-zinc-300">
                Nenhum cálculo ainda
              </p>
              <p className="text-xs text-zinc-500 mt-1">
                Preencha as medidas e o preço, depois clique em{" "}
                <b className="text-yellow-400">Calcular frete</b>.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
