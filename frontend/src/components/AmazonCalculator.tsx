import { useState, useEffect } from "react";
import {
  Search,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Copy,
  Check,
} from "lucide-react";
import type { ApiClient } from "../services/api";
import type {
  AmazonModalidade,
  AmazonTipoRegra,
  ConfiguracaoAmazonApiResponse,
  ResultadoFreteAmazon,
  SkuFeedback,
} from "../types";
import {
  canCalculateAmazon,
  formatDimensionDisplay,
  formatModalidadeDescricao,
  mapAmazonResponseToResult,
} from "../utils/amazon";
import { copyTextToClipboard } from "../utils/clipboard";
import {
  brl,
  fmtDecimalBr,
  num,
  sanitizeDecimalInput,
} from "../utils/formatters";

interface AmazonCalculatorProps {
  api: ApiClient;
}

const REGRA_BADGE_CLASS: Record<AmazonTipoRegra, string> = {
  FIXO: "bg-sky-500/15 text-sky-300 border-sky-500/30",
  KG_ADICIONAL: "bg-amber-500/15 text-amber-300 border-amber-500/30",
  FAIXA_PESO: "bg-zinc-800 text-zinc-200 border-zinc-700",
};

export function AmazonCalculator({ api }: AmazonCalculatorProps) {
  const [sku, setSku] = useState("");
  const [skuFeedback, setSkuFeedback] = useState<SkuFeedback | null>(null);
  const [linkDrive, setLinkDrive] = useState<string | null>(null);
  const [skuLoad, setSkuLoad] = useState(false);

  const [peso, setPeso] = useState("");
  const [dimensoes, setDimensoes] = useState<{
    c: number | null;
    l: number | null;
    a: number | null;
  }>({ c: null, l: null, a: null });

  const [valor, setValor] = useState("");
  const [modalidade, setModalidade] = useState<AmazonModalidade>("DBA");

  const [res, setRes] = useState<ResultadoFreteAmazon | null>(null);
  const [calc, setCalc] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);
  const [copiadoFrete, setCopiadoFrete] = useState(false);

  const [config, setConfig] = useState<ConfiguracaoAmazonApiResponse | null>(
    null,
  );
  const [configLoad, setConfigLoad] = useState(true);
  const [configError, setConfigError] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    const carregarConfig = async () => {
      setConfigLoad(true);
      setConfigError(null);
      const result = await api.getConfigAmazon();
      if (!ativo) return;
      if (result.ok) {
        setConfig(result.data);
      } else if (!result.unauthorized) {
        setConfigError(result.error);
      }
      setConfigLoad(false);
    };
    void carregarConfig();
    return () => {
      ativo = false;
    };
  }, [api]);

  useEffect(() => {
    if (!copiadoFrete) return;
    const timer = window.setTimeout(() => setCopiadoFrete(false), 1900);
    return () => window.clearTimeout(timer);
  }, [copiadoFrete]);

  const copiarFreteTotal = async () => {
    if (!res) return;
    const ok = await copyTextToClipboard(fmtDecimalBr(res.custoTotal));
    setCopiadoFrete(ok);
  };

  const pesoReal = num(peso);
  const preco = num(valor);
  const podeCalcular = canCalculateAmazon(config, preco, pesoReal);

  const buscarSku = async () => {
    const code = sku.trim().toUpperCase();
    if (!code) return;
    setSkuLoad(true);
    setSkuFeedback(null);
    setPeso("");
    setDimensoes({ c: null, l: null, a: null });
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
      setDimensoes({
        c: comprimentoApi > 0 ? comprimentoApi : null,
        l: larguraApi > 0 ? larguraApi : null,
        a: alturaApi > 0 ? alturaApi : null,
      });

      const drive = typeof p.linkDrive === "string" ? p.linkDrive.trim() : "";
      setLinkDrive(drive || null);

      const nomeProduto = String(p.nome || code).trim();

      if (pesoApi > 0) {
        setSkuFeedback({
          tipo: "ok",
          titulo: nomeProduto,
          mensagem: "dados preenchidos",
        });
      } else {
        setSkuFeedback({
          tipo: "warn",
          titulo: nomeProduto,
          mensagem:
            "Produto encontrado, mas ainda sem peso cadastrado. Informe o peso manualmente abaixo.",
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
      const result = await api.calcularFreteAmazon({
        sku: sku.trim() || null,
        precoProduto: preco,
        pesoRealKg: pesoReal,
        modalidade,
      });

      if (!result.ok) {
        if (!result.unauthorized) {
          setCalcError(result.error);
        }
        return;
      }

      const mapped = mapAmazonResponseToResult(result.data);
      if (mapped) {
        setRes(mapped);
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
          Cálculo de frete <span className="text-[#FF9900]">· Amazon</span>
        </h1>
        <p className="text-sm text-zinc-400 mt-0.5">
          Busque um SKU ou informe o peso e o preço do produto.
        </p>
      </div>

      {configError && (
        <div
          role="alert"
          className="mb-5 p-3.5 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2"
        >
          <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
          <span>{configError}</span>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <div className="lg:col-span-7 space-y-5">
          {/* Produto, Peso e Medidas para conferência */}
          <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
            <div>
              <label
                htmlFor="amazon-sku"
                className="block text-xs font-medium text-zinc-400 mb-1.5"
              >
                SKU
              </label>
              <div className="flex gap-2">
                <input
                  id="amazon-sku"
                  type="text"
                  value={sku}
                  onChange={(e) => {
                    const nextSku = e.target.value;
                    setSku(nextSku);
                    if (!nextSku.trim()) {
                      setSkuFeedback(null);
                      setLinkDrive(null);
                    }
                  }}
                  onKeyDown={(e) => e.key === "Enter" && void buscarSku()}
                  placeholder="Digite o SKU e pressione Enter"
                  className="flex-1 px-3.5 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm uppercase placeholder:normal-case focus:outline-none focus:border-[#FF9900] transition-colors"
                />
                <button
                  type="button"
                  onClick={() => void buscarSku()}
                  disabled={skuLoad}
                  className="px-4 py-2 rounded-xl bg-[#FF9900] hover:bg-[#ffaa2b] text-zinc-950 font-bold text-xs flex items-center gap-1.5 cursor-pointer shrink-0 transition"
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
                  className="mt-2.5 flex items-center justify-between gap-3 p-2.5 rounded-xl bg-amber-400/10 border border-amber-400/25 text-xs text-amber-200"
                >
                  <span className="flex items-start gap-2">
                    <AlertCircle className="w-4 h-4 shrink-0 text-amber-400 mt-px" />
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
              <div>
                <label
                  htmlFor="amazon-peso"
                  className="block text-xs font-medium text-zinc-300 mb-1"
                >
                  Peso (kg)
                </label>
                <input
                  id="amazon-peso"
                  type="text"
                  inputMode="decimal"
                  autoComplete="off"
                  placeholder="0,000"
                  value={peso}
                  onChange={(e) => {
                    setPeso(sanitizeDecimalInput(e.target.value));
                    setRes(null);
                    setCopiadoFrete(false);
                  }}
                  className="w-full px-3 py-2 rounded-xl bg-zinc-950 border border-zinc-700 text-sm font-semibold tabular-nums focus:outline-none focus:border-[#FF9900] transition-colors"
                />
              </div>

              {[
                { label: "Comprimento (cm)", val: dimensoes.c },
                { label: "Largura (cm)", val: dimensoes.l },
                { label: "Altura (cm)", val: dimensoes.a },
              ].map((d) => (
                <div key={d.label}>
                  <span className="block text-xs text-zinc-500 mb-1">
                    {d.label}
                  </span>
                  <div className="w-full px-3 py-2 rounded-xl bg-zinc-950/50 border border-zinc-800/80 text-sm font-medium text-zinc-400 tabular-nums select-all">
                    {formatDimensionDisplay(d.val)}
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Preço e Modalidade */}
          <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-12 gap-3 items-end">
              <div className="sm:col-span-7">
                <label
                  htmlFor="amazon-preco"
                  className="block text-xs font-medium text-zinc-400 mb-1.5"
                >
                  Preço de venda
                </label>
                <div className="relative flex items-center">
                  <span className="absolute left-3.5 text-sm font-bold text-zinc-500 pointer-events-none select-none">
                    R$
                  </span>
                  <input
                    id="amazon-preco"
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
                    className="w-full h-11 pl-10 pr-3.5 rounded-xl bg-zinc-950 border border-zinc-800 text-base font-bold text-[#FF9900] tabular-nums focus:outline-none focus:border-[#FF9900] transition-colors"
                  />
                </div>
              </div>

              <div className="sm:col-span-5">
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  Modalidade
                </label>
                <div
                  role="group"
                  aria-label="Modalidade Amazon"
                  className="grid grid-cols-2 gap-1 h-11 p-1 rounded-xl bg-zinc-950 border border-zinc-800"
                >
                  {(["DBA", "FBA"] as const).map((modo) => (
                    <button
                      key={modo}
                      type="button"
                      onClick={() => {
                        setModalidade(modo);
                        setRes(null);
                        setCopiadoFrete(false);
                      }}
                      className={`rounded-lg text-xs transition cursor-pointer ${
                        modalidade === modo
                          ? "bg-[#FF9900] text-zinc-950 font-extrabold"
                          : "text-zinc-400 hover:text-zinc-200 font-bold"
                      }`}
                    >
                      {modo}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <button
              type="button"
              onClick={() => void calcular()}
              disabled={calc || configLoad || !podeCalcular}
              className="w-full py-3 rounded-xl bg-[#FF9900] hover:bg-[#ffaa2b] disabled:opacity-40 disabled:cursor-not-allowed text-zinc-950 font-black text-sm tracking-wide uppercase transition cursor-pointer"
            >
              {configLoad
                ? "Carregando regras..."
                : calc
                  ? "Calculando..."
                  : "Calcular frete"}
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

        {/* Coluna Direita: Resultado */}
        <div className="lg:col-span-5">
          {res ? (
            <section className="bg-zinc-900 border-2 border-[#FF9900] rounded-2xl p-5 space-y-3">
              <div className="flex items-center justify-between gap-2">
                <h2 className="text-sm font-bold text-[#FF9900] flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4" />
                  Resultado · {res.modalidade}
                </h2>
                {res.freteCliente === "PAGO_PELO_CLIENTE" && (
                  <span className="text-[11px] font-bold px-2.5 py-0.5 rounded-full border bg-amber-500/15 text-amber-300 border-amber-500/35">
                    Pago pelo cliente
                  </span>
                )}
              </div>

              <dl className="divide-y divide-zinc-800 text-sm">
                <div className="py-2 flex justify-between items-center gap-3">
                  <dt className="text-zinc-400">Modalidade</dt>
                  <dd className="font-semibold text-right">
                    {formatModalidadeDescricao(res.modalidade)}
                  </dd>
                </div>
                <div className="py-2 flex justify-between items-center gap-3">
                  <dt className="text-zinc-400">Regra aplicada</dt>
                  <dd>
                    <span
                      className={`text-[11px] font-bold px-2 py-0.5 rounded-md border ${REGRA_BADGE_CLASS[res.tipoRegra]}`}
                    >
                      {res.tipoRegra}
                    </span>
                  </dd>
                </div>
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">Faixa de peso</dt>
                  <dd className="font-semibold text-right">{res.faixaPeso}</dd>
                </div>
                <div className="py-2 flex justify-between gap-3">
                  <dt className="text-zinc-400">
                    {res.tipoRegra === "KG_ADICIONAL"
                      ? "Valor base"
                      : `Valor · ${res.modalidade}`}
                  </dt>
                  <dd className="font-semibold tabular-nums">
                    {brl(res.tarifaBase)}
                  </dd>
                </div>
                {res.tipoRegra === "KG_ADICIONAL" && (
                  <div className="py-2 flex justify-between gap-3">
                    <dt className="text-zinc-400">
                      Excedente ({res.kgExcedentes} kg ×{" "}
                      {brl(res.tarifaKgAdicional)})
                    </dt>
                    <dd className="font-semibold tabular-nums text-amber-300">
                      + {brl(res.valorKgExcedente)}
                    </dd>
                  </div>
                )}
                {res.taxaFixa > 0 && (
                  <div className="py-2 flex justify-between gap-3">
                    <dt className="text-zinc-400">Taxa fixa</dt>
                    <dd className="font-semibold tabular-nums">
                      + {brl(res.taxaFixa)}
                    </dd>
                  </div>
                )}
              </dl>

              <button
                type="button"
                onClick={() => void copiarFreteTotal()}
                title="Clique para copiar o frete total"
                className={`w-full p-3.5 rounded-xl text-zinc-950 flex items-center justify-between gap-3 transition-all duration-200 cursor-pointer group text-left select-none active:scale-[0.985] focus:outline-none focus:ring-2 focus:ring-[#FF9900]/60 ${
                  copiadoFrete
                    ? "bg-emerald-400 hover:bg-emerald-300"
                    : "bg-[#FF9900] hover:bg-[#ffaa2b]"
                }`}
              >
                <div className="flex flex-col">
                  <span className="text-xs font-black uppercase tracking-wider">
                    {copiadoFrete ? "Copiado!" : "Frete total"}
                  </span>
                  <span className="text-[11px] font-semibold text-zinc-950/65 group-hover:text-zinc-950/90 transition-colors">
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
                Informe o preço e o peso do produto, depois clique em{" "}
                <b className="text-[#FF9900]">Calcular frete</b>.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
