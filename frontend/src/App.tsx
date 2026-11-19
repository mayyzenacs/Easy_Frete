import React, { useState, useEffect, useRef } from "react";
import {
  Truck,
  Package,
  Search,
  Lock,
  LogOut,
  CheckCircle2,
  AlertCircle,
  Sparkles,
  Scale,
  Box,
  ChevronDown,
  User,
  UserPlus,
  KeyRound,
  X,
  ShieldCheck,
  Copy,
  Check,
} from "lucide-react";

const API_BASE_URL = (import.meta.env.VITE_API_URL || "/api/v1").replace(
  /\/$/,
  "",
);

interface UserProfile {
  id: string;
  nome: string;
  email: string;
}

interface Resultado {
  faixa: string;
  faixaPeso: string;
  tarifaLabel: string;
  tarifaValor: number;
  taxaFixa: number;
  custoTotal: number;
  origem: "API .NET 10";
}

const brl = (v: number) =>
  new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(
    Number.isFinite(v) ? v : 0,
  );

const num = (s: string) => {
  const n = parseFloat(String(s).replace(",", "."));
  return Number.isFinite(n) && n >= 0 ? n : 0;
};

export default function App() {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem("ef_jwt"),
  );
  const [user, setUser] = useState<UserProfile | null>(() => {
    try {
      const raw = localStorage.getItem("ef_user");
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  });

  // Estados de Autenticação (Login / Registro)
  const [modoAuth, setModoAuth] = useState<"login" | "register">("login");
  const [nomeCadastro, setNomeCadastro] = useState("");
  const [email, setEmail] = useState("mayra@easyfrete.com");
  const [senha, setSenha] = useState("");
  const [authLoad, setAuthLoad] = useState(false);
  const [authMsg, setAuthMsg] = useState<string | null>(null);

  // Menu de Perfil e Modais
  const [menuAberto, setMenuAberto] = useState(false);
  const [modalConta, setModalConta] = useState(false);
  const [copiadoId, setCopiadoId] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Estados da Calculadora
  const [sku, setSku] = useState("");
  const [skuInfo, setSkuInfo] = useState<string | null>(null);
  const [linkDrive, setLinkDrive] = useState<string | null>(null);
  const [skuLoad, setSkuLoad] = useState(false);

  const [peso, setPeso] = useState("");
  const [c, setC] = useState("");
  const [l, setL] = useState("");
  const [a, setA] = useState("");
  const [valor, setValor] = useState("");
  const [ful, setFul] = useState(true);
  const [acima97, setAcima97] = useState(true);

  const [res, setRes] = useState<Resultado | null>(null);
  const [calc, setCalc] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);

  // Fecha o dropdown ao clicar fora
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuAberto(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const real = num(peso);
  const cubado = +((num(c) * num(l) * num(a)) / 6000).toFixed(3);
  const realMaior = real >= cubado;
  const v = num(valor);

  const authHeaders: Record<string, string> = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };

  const salvarSessao = (jwt: string, perfil: UserProfile) => {
    localStorage.setItem("ef_jwt", jwt);
    localStorage.setItem("ef_user", JSON.stringify(perfil));
    setToken(jwt);
    setUser(perfil);
  };

  const autenticar = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthLoad(true);
    setAuthMsg(null);

    const rota = modoAuth === "register" ? "register" : "login";
    const payload =
      modoAuth === "register"
        ? { nome: nomeCadastro.trim(), email: email.trim(), password: senha }
        : { email: email.trim(), password: senha };

    try {
      const r = await fetch(`${API_BASE_URL}/auth/${rota}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const d = await r.json().catch(() => ({}));

      if (!r.ok) {
        setAuthMsg(
          d?.error || "Erro ao autenticar. Verifique os dados informados.",
        );
        setAuthLoad(false);
        return;
      }

      const jwt = d?.token || d?.accessToken || d?.jwt;
      if (jwt) {
        const nomeFallback = email.split("@")[0] || "Usuário";
        const nomeFormatado =
          d?.nome ||
          nomeCadastro.trim() ||
          nomeFallback.charAt(0).toUpperCase() + nomeFallback.slice(1);

        salvarSessao(jwt, {
          id: d?.id || crypto.randomUUID(),
          nome: nomeFormatado,
          email: d?.email || email.trim(),
        });
        setSenha("");
      }
    } catch {
      setAuthMsg("Não foi possível conectar à API C# na porta 5000.");
    } finally {
      setAuthLoad(false);
    }
  };

  const sair = () => {
    localStorage.removeItem("ef_jwt");
    localStorage.removeItem("ef_user");
    setToken(null);
    setUser(null);
    setRes(null);
    setMenuAberto(false);
  };

  const buscarSku = async () => {
    const code = sku.trim().toUpperCase();
    if (!code) return;
    setSkuLoad(true);
    setSkuInfo(null);
    setPeso("");
    setC("");
    setL("");
    setA("");
    setLinkDrive(null);
    setRes(null);
    setCalcError(null);
    try {
      const r = await fetch(
        `${API_BASE_URL}/produtos/${encodeURIComponent(code)}`,
        {
          headers: authHeaders,
        },
      );
      if (r.ok) {
        const p = await r.json();
        const pesoApi = Number(p.pesoRealKg ?? p.pesoBruto ?? p.peso ?? 0);
        const comprimentoApi = Number(
          p.comprimentoCm ?? p.comprimento ?? 0,
        );
        const larguraApi = Number(p.larguraCm ?? p.largura ?? 0);
        const alturaApi = Number(p.alturaCm ?? p.altura ?? 0);
        setPeso(pesoApi > 0 ? String(pesoApi) : "");
        setC(comprimentoApi > 0 ? String(comprimentoApi) : "");
        setL(larguraApi > 0 ? String(larguraApi) : "");
        setA(alturaApi > 0 ? String(alturaApi) : "");
        const drive = typeof p.linkDrive === "string" ? p.linkDrive.trim() : "";
        setLinkDrive(drive || null);

        const camposFaltantes = [
          pesoApi <= 0 ? "PESO NÃO ENCONTRADO - PREENCHER MANUAL" : null,
          alturaApi <= 0 ? "ALTURA NÃO ENCONTRADA - PREENCHER MANUAL" : null,
          larguraApi <= 0 ? "LARGURA NÃO ENCONTRADA - PREENCHER MANUAL" : null,
          comprimentoApi <= 0
            ? "COMPRIMENTO NÃO ENCONTRADO - PREENCHER MANUAL"
            : null,
        ].filter((mensagem): mensagem is string => mensagem !== null);
        setSkuInfo(
          camposFaltantes.length === 4
            ? "SKU encontrado, mas não possui marcação de medidas ainda (aguardando Centro de Distribuição)."
            : camposFaltantes.length > 0
              ? camposFaltantes.join(" | ")
              : `SKU carregado do SQLite: ${p.nome || code}`,
        );
        setSkuLoad(false);
        return;
      }
      setPeso("");
      setC("");
      setL("");
      setA("");
      setLinkDrive(null);
      setSkuInfo(
        r.status === 404
          ? "SKU não encontrado. Favor solicitar peso na planilha."
          : "Erro ao consultar SKU.",
      );
    } catch {
      setPeso("");
      setC("");
      setL("");
      setA("");
      setLinkDrive(null);
      setSkuInfo("Erro de conexão ao consultar SKU.");
    }
    setSkuLoad(false);
  };

  const calcular = async () => {
    setCalc(true);
    setCalcError(null);
    setRes(null);
    try {
      const r = await fetch(`${API_BASE_URL}/frete/magalu/calcular`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          sku: sku.trim() || null,
          precoProduto: v,
          pesoRealKg: real,
          comprimentoCm: num(c),
          larguraCm: num(l),
          alturaCm: num(a),
          isFulfillment: ful,
          reputacaoMaior97: acima97,
        }),
      });
      if (r.ok) {
        const d = await r.json();
        const rawModalidade = String(
          d.modalidadeAplicada ?? d.descricaoTarifa ?? d.regraAplicada ?? "",
        );
        const tarifaLabel =
          rawModalidade ||
          "Tarifa calculada pela API";

        const cand: Resultado = {
          faixa: String(
            d.faixaPrecoAplicada ??
              d.faixaPreco ??
              d.faixaValor ??
              "Faixa informada pela API",
          ),
          faixaPeso: String(
            d.faixaPesoAplicada ||
              d.faixaPeso ||
              "Faixa informada pela API",
          ),
          tarifaLabel,
          tarifaValor: Number(
            d.tarifaBase ?? d.tarifaValor ?? d.valorTarifa ?? 0,
          ),
          taxaFixa: Number(d.taxaFixaObrigatoria ?? d.taxaFixa ?? 5),
          custoTotal: Number(
            d.valorFinalFrete ?? d.custoTotalFrete ?? d.custoTotal ?? 0,
          ),
          origem: "API .NET 10",
        };
        if (
          Number.isFinite(cand.tarifaValor) &&
          Number.isFinite(cand.custoTotal)
        )
          setRes(cand);
        else
          setCalcError("A API retornou dados de frete inválidos.");
        return;
      }

      const d = await r.json().catch(() => ({}));
      setCalcError(
        d?.error ||
          d?.message ||
          `Erro ao calcular o frete (${r.status}).`,
      );
    } catch {
      setCalcError("Não foi possível consultar a API de cálculo de frete.");
    } finally {
      setCalc(false);
    }
  };

  const mostra97 = !ful && v >= 79;

  // ==================== TELA DE LOGIN E CADASTRO (BCRYPT + GUID) ====================
  if (!token) {
    return (
      <div className="min-h-screen bg-[#09090b] text-zinc-100 flex items-center justify-center p-4">
        <div className="w-full max-w-md bg-zinc-900/90 border border-zinc-800 rounded-2xl p-7 shadow-2xl">
          <div className="flex items-center gap-3.5 mb-6">
            <div className="w-12 h-12 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-center shadow-lg shadow-yellow-400/30">
              <Truck className="w-7 h-7 stroke-[2.4]" />
            </div>
            <div>
              <h1 className="text-2xl font-black tracking-tight text-yellow-400 drop-shadow-[0_0_12px_rgba(250,204,21,0.35)]">
                EASY FRETE
              </h1>
              <p className="text-xs text-zinc-400">
                Motor de Cálculo Logístico · .NET 10 & SQLite
              </p>
            </div>
          </div>

          {/* Seletor Entrar / Criar Conta */}
          <div className="grid grid-cols-2 bg-zinc-950 p-1 rounded-xl border border-zinc-800 mb-5">
            <button
              type="button"
              onClick={() => {
                setModoAuth("login");
                setAuthMsg(null);
              }}
              className={`py-2 text-xs font-bold rounded-lg transition cursor-pointer flex items-center justify-center gap-1.5 ${
                modoAuth === "login"
                  ? "bg-yellow-400 text-zinc-950 shadow"
                  : "text-zinc-400 hover:text-zinc-200"
              }`}
            >
              <KeyRound className="w-3.5 h-3.5" /> Entrar
            </button>
            <button
              type="button"
              onClick={() => {
                setModoAuth("register");
                setAuthMsg(null);
              }}
              className={`py-2 text-xs font-bold rounded-lg transition cursor-pointer flex items-center justify-center gap-1.5 ${
                modoAuth === "register"
                  ? "bg-yellow-400 text-zinc-950 shadow"
                  : "text-zinc-400 hover:text-zinc-200"
              }`}
            >
              <UserPlus className="w-3.5 h-3.5" /> Criar Conta
            </button>
          </div>

          <form onSubmit={autenticar} className="space-y-4">
            {modoAuth === "register" && (
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400 mb-1.5">
                  Seu Nome
                </label>
                <input
                  type="text"
                  required
                  placeholder="Ex.: Mayra Pereira"
                  value={nomeCadastro}
                  onChange={(e) => setNomeCadastro(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                />
              </div>
            )}
            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400 mb-1.5">
                E-mail Corporativo
              </label>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400 mb-1.5">
                Senha{" "}
                {modoAuth === "register" &&
                  "(Mín. 6 caracteres · Criptografia BCrypt)"}
              </label>
              <input
                type="password"
                required
                minLength={modoAuth === "register" ? 6 : 1}
                value={senha}
                onChange={(e) => setSenha(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
              />
            </div>

            {authMsg && (
              <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
                <span>{authMsg}</span>
              </div>
            )}

            <button
              type="submit"
              disabled={authLoad}
              className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 font-extrabold text-sm flex items-center justify-center gap-2 transition cursor-pointer shadow-lg shadow-yellow-400/20"
            >
              {modoAuth === "register" ? (
                <>
                  <UserPlus className="w-4 h-4" />
                  {authLoad
                    ? "Cadastrando com Guid + BCrypt..."
                    : "Criar Conta e Acessar"}
                </>
              ) : (
                <>
                  <Lock className="w-4 h-4" />
                  {authLoad ? "Autenticando..." : "Acessar Painel"}
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    );
  }

  // ==================== DASHBOARD PRINCIPAL ====================
  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col">
      {/* HEADER */}
      <header className="border-b border-zinc-800/80 bg-zinc-950/90 backdrop-blur sticky top-0 z-30">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 h-18 flex items-center justify-between gap-4 py-3">
          {/* Logo EASY FRETE Aumentada */}
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-center shadow-lg shadow-yellow-400/25">
              <Truck className="w-6 h-6 stroke-[2.5]" />
            </div>
            <div>
              <span className="font-black text-2xl sm:text-3xl tracking-tight text-yellow-400 drop-shadow-[0_0_14px_rgba(250,204,21,0.35)]">
                EASY FRETE
              </span>
              <span className="hidden md:inline-block ml-2.5 text-[11px] font-semibold uppercase tracking-widest px-2 py-0.5 rounded bg-zinc-900 text-zinc-400 border border-zinc-800">
                Hub Logístico
              </span>
            </div>
          </div>

          {/* Seletor Central de Marketplaces */}
          <div className="hidden sm:flex items-center gap-1.5 bg-zinc-900 p-1 rounded-full border border-zinc-800">
            <button
              type="button"
              className="px-4 py-1.5 rounded-full text-xs font-extrabold bg-yellow-400 text-zinc-950 shadow-sm cursor-pointer"
            >
              Magalu
            </button>
            <button
              type="button"
              disabled
              className="px-3.5 py-1.5 rounded-full text-xs font-semibold text-zinc-500 flex items-center gap-1.5 cursor-not-allowed"
            >
              Amazon
              <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400">
                EM BREVE
              </span>
            </button>
          </div>

          {/* Canto Direito: Bem-vindo, Mayra + Dropdown */}
          <div className="relative" ref={menuRef}>
            <button
              type="button"
              onClick={() => setMenuAberto((prev) => !prev)}
              className="flex items-center gap-2.5 pl-3 pr-3.5 py-2 rounded-xl bg-zinc-900 hover:bg-zinc-800/90 border border-zinc-800 hover:border-yellow-400/50 transition cursor-pointer"
            >
              <div className="w-8 h-8 rounded-lg bg-yellow-400/15 border border-yellow-400/40 text-yellow-300 flex items-center justify-center font-bold text-sm">
                {(user?.nome || "M").charAt(0).toUpperCase()}
              </div>
              <div className="text-left">
                <div className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
                  <span className="text-xs font-bold text-zinc-100">
                    Bem-vindo,{" "}
                    <span className="text-yellow-400">
                      {user?.nome || "Mayra"}
                    </span>
                  </span>
                </div>
                <p className="text-[10px] text-zinc-400 hidden sm:block">
                  Conta Conectada · JWT
                </p>
              </div>
              <ChevronDown
                className={`w-4 h-4 text-zinc-400 transition-transform ${
                  menuAberto ? "rotate-180 text-yellow-400" : ""
                }`}
              />
            </button>

            {/* MENU DROPDOWN */}
            {menuAberto && (
              <div className="absolute right-0 mt-2 w-64 rounded-2xl bg-zinc-900 border border-zinc-800 shadow-2xl py-2 z-50">
                <div className="px-4 py-2.5 border-b border-zinc-800/80">
                  <p className="text-xs font-bold text-zinc-100 truncate">
                    {user?.nome}
                  </p>
                  <p className="text-[11px] text-zinc-400 truncate">
                    {user?.email}
                  </p>
                </div>

                <div className="py-1">
                  <button
                    type="button"
                    onClick={() => {
                      setModalConta(true);
                      setMenuAberto(false);
                    }}
                    className="w-full px-4 py-2 text-left text-xs font-medium text-zinc-200 hover:bg-zinc-800 flex items-center gap-2.5 cursor-pointer"
                  >
                    <User className="w-4 h-4 text-yellow-400" />
                    Minha Conta (Ver GUID / Perfil)
                  </button>

                </div>

                <div className="border-t border-zinc-800/80 py-1">
                  <p className="px-4 py-1 text-[10px] font-bold uppercase tracking-wider text-zinc-500">
                    Marketplaces
                  </p>
                  <button
                    type="button"
                    onClick={() => setMenuAberto(false)}
                    className="w-full px-4 py-2 text-left text-xs font-semibold text-yellow-400 bg-yellow-400/10 flex items-center justify-between cursor-pointer"
                  >
                    <span>Magalu</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-yellow-400 text-zinc-950 font-bold">
                      ATIVO
                    </span>
                  </button>
                  <button
                    type="button"
                    disabled
                    className="w-full px-4 py-2 text-left text-xs font-medium text-zinc-500 flex items-center justify-between cursor-not-allowed"
                  >
                    <span>Amazon</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400">
                      EM BREVE
                    </span>
                  </button>
                </div>

                <div className="border-t border-zinc-800/80 pt-1 mt-1">
                  <button
                    type="button"
                    onClick={sair}
                    className="w-full px-4 py-2 text-left text-xs font-semibold text-red-400 hover:bg-red-500/10 flex items-center gap-2.5 cursor-pointer"
                  >
                    <LogOut className="w-4 h-4" />
                    Sair da Conta
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* CONTEÚDO PRINCIPAL COMPACTO EM 2 COLUNAS */}
      <main className="max-w-7xl w-full mx-auto px-4 sm:px-6 py-6 flex-1">
        {/* Cabeçalho da Página com Efeito Brilhante (Glow/Shimmer) */}
        <div className="flex flex-wrap items-center justify-between gap-4 mb-6">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-yellow-400/10 border border-yellow-400/30 shadow-[0_0_20px_rgba(250,204,21,0.2)]">
              <Package className="w-6 h-6 text-yellow-400" />
            </div>
            <div>
              <h1 className="text-2xl sm:text-3xl font-black tracking-tight flex items-center gap-2">
                <span className="bg-gradient-to-r from-white via-yellow-100 to-yellow-400 bg-clip-text text-transparent drop-shadow-[0_0_18px_rgba(250,204,21,0.45)]">
                  Cálculo de Frete — Magalu
                </span>
                <Sparkles className="w-5 h-5 text-yellow-400 animate-pulse inline" />
              </h1>
              <p className="text-xs text-zinc-400">
                Simulador oficial de cubagem, reputação e Fulfillment conectado
                ao SQLite
              </p>
            </div>
          </div>

        </div>

        {/* GRID LADO A LADO (7 COLUNAS ESQUERDA / 5 COLUNAS DIREITA) */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          {/* COLUNA ESQUERDA: PARÂMETROS E MEDIDAS (Compacto) */}
          <div className="lg:col-span-7 space-y-5">
            {/* Bloco 1: Busca por SKU + Peso e Dimensões */}
            <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
              <div>
                <label className="block text-[11px] font-bold uppercase tracking-wider text-zinc-400 mb-1.5">
                  Buscar Produto por SKU
                </label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={sku}
                    onChange={(e) => setSku(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && buscarSku()}
                    placeholder="Ex.: SKU-001, SKU-002, SKU-003"
                    className="flex-1 px-3.5 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                  />
                  <button
                    type="button"
                    onClick={buscarSku}
                    disabled={skuLoad}
                    className="px-4 py-2 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 font-bold text-xs flex items-center gap-1.5 cursor-pointer shrink-0"
                  >
                    <Search className="w-3.5 h-3.5" />
                    {skuLoad ? "..." : "Buscar"}
                  </button>
                </div>
                {skuInfo && (
                  <p
                    role="alert"
                    className="text-xs text-yellow-300 mt-1.5"
                  >
                    {skuInfo}
                  </p>
                )}
                {linkDrive && (
                  <a
                    href={linkDrive}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-block mt-2 px-3 py-1.5 rounded-lg bg-blue-500 hover:bg-blue-400 text-white text-xs font-bold"
                  >
                    DRIVE
                  </a>
                )}
              </div>

              {/* 4 Campos Compactos Lado a Lado */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {[
                  {
                    label: "Peso Real (kg)",
                    val: peso,
                    set: setPeso,
                    step: "0.01",
                  },
                  { label: "Comp. (cm)", val: c, set: setC, step: "0.1" },
                  { label: "Largura (cm)", val: l, set: setL, step: "0.1" },
                  { label: "Altura (cm)", val: a, set: setA, step: "0.1" },
                ].map((f) => (
                  <div key={f.label}>
                    <label className="block text-[11px] font-semibold uppercase tracking-wider text-zinc-400 mb-1">
                      {f.label}
                    </label>
                    <input
                      type="number"
                      step={f.step}
                      min="0"
                      value={f.val}
                      onChange={(e) => f.set(e.target.value)}
                      className="w-full px-3 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm font-semibold focus:outline-none focus:border-yellow-400"
                    />
                  </div>
                ))}
              </div>
            </section>

            {/* Bloco 2: Preço + Switches Lado a Lado + Botão Calcular */}
            <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-12 gap-3 items-end">
                {/* Preço ocupa 5 colunas */}
                <div className="sm:col-span-5">
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-zinc-400 mb-1.5">
                    Preço de Venda (R$)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={valor}
                    onChange={(e) => setValor(e.target.value)}
                    className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-base font-bold text-yellow-400 focus:outline-none focus:border-yellow-400"
                  />
                </div>

                {/* Switch Fulfillment ocupa 7 colunas */}
                <div className="sm:col-span-7">
                  <div className="flex items-center justify-between px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800">
                    <div>
                      <span className="text-xs font-bold text-zinc-200 block">
                        Modalidade Fulfillment?
                      </span>
                      <span className="text-[10px] text-zinc-400">
                        Estoque no CD Magalu (Desconto {">"}70%)
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => setFul((x) => !x)}
                      className={`px-3 py-1 rounded-lg text-xs font-extrabold transition cursor-pointer ${
                        ful
                          ? "bg-yellow-400 text-zinc-950 shadow-sm"
                          : "bg-zinc-800 text-zinc-400"
                      }`}
                    >
                      {ful ? "SIM" : "NÃO"}
                    </button>
                  </div>
                </div>
              </div>

              {mostra97 && (
                <div className="flex items-center justify-between px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-yellow-400/30">
                  <div>
                    <span className="text-xs font-bold text-zinc-200 block">
                      Reputação da Loja Acima de 97%?
                    </span>
                    <span className="text-[10px] text-zinc-400">
                      Concede 50% de desconto na Coleta e Entrega (produtos ≥ R$
                      79)
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => setAcima97((x) => !x)}
                    className={`px-3 py-1 rounded-lg text-xs font-extrabold transition cursor-pointer ${
                      acima97
                        ? "bg-yellow-400 text-zinc-950 shadow-sm"
                        : "bg-zinc-800 text-zinc-400"
                    }`}
                  >
                    {acima97 ? "SIM (>97%)" : "NÃO (<97%)"}
                  </button>
                </div>
              )}

              <button
                type="button"
                onClick={calcular}
                disabled={calc || ![peso, c, l, a].every((campo) => num(campo) > 0)}
                className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 disabled:opacity-50 disabled:cursor-not-allowed text-zinc-950 font-black text-sm tracking-wide uppercase transition cursor-pointer shadow-lg shadow-yellow-400/20"
              >
                {calc ? "Calculando..." : "Calcular Frete"}
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

          {/* COLUNA DIREITA: CARDS DE PESO COMPACTOS + RESULTADO */}
          <div className="lg:col-span-5 space-y-4">
            {/* Cards de Peso Real vs Cubado Compactos */}
            <div className="grid grid-cols-2 gap-3">
              <div
                className={`rounded-2xl p-3.5 border transition ${
                  realMaior
                    ? "bg-emerald-950/25 border-emerald-500/70 shadow-[0_0_20px_rgba(16,185,129,0.12)]"
                    : "bg-zinc-900/80 border-zinc-800"
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-zinc-400 flex items-center gap-1">
                    <Scale className="w-3.5 h-3.5 text-emerald-400" /> Peso Real
                  </span>
                  {realMaior && (
                    <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                      USADO
                    </span>
                  )}
                </div>
                <div
                  className={`text-2xl font-black ${
                    realMaior ? "text-emerald-400" : "text-zinc-200"
                  }`}
                >
                  {real.toFixed(3)}{" "}
                  <span className="text-xs font-normal">kg</span>
                </div>
                <p className="text-[10px] text-zinc-500 mt-0.5">
                  Peso físico na balança
                </p>
              </div>

              <div
                className={`rounded-2xl p-3.5 border transition ${
                  !realMaior
                    ? "bg-emerald-950/25 border-emerald-500/70 shadow-[0_0_20px_rgba(16,185,129,0.12)]"
                    : "bg-zinc-900/80 border-zinc-800"
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-zinc-400 flex items-center gap-1">
                    <Box className="w-3.5 h-3.5 text-yellow-400" /> Peso Cubado
                  </span>
                  {!realMaior && (
                    <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                      USADO
                    </span>
                  )}
                </div>
                <div
                  className={`text-2xl font-black ${
                    !realMaior ? "text-emerald-400" : "text-zinc-200"
                  }`}
                >
                  {cubado.toFixed(3)}{" "}
                  <span className="text-xs font-normal">kg</span>
                </div>
                <p className="text-[10px] text-zinc-500 mt-0.5">
                  (C × L × A) / 6000
                </p>
              </div>
            </div>

            {/* Card de Resultado */}
            {res ? (
              <section className="bg-zinc-900 border-2 border-yellow-400 rounded-2xl p-5 space-y-3.5 shadow-2xl">
                <div className="flex items-center justify-between">
                  <h2 className="text-sm font-extrabold uppercase tracking-wider text-yellow-400 flex items-center gap-1.5">
                    <CheckCircle2 className="w-4 h-4" /> Resultado do Frete
                  </h2>
                  <span className="text-[10px] px-2 py-0.5 rounded bg-zinc-800 text-zinc-300 border border-zinc-700 font-semibold">
                    Fonte: {res.origem}
                  </span>
                </div>

                <div className="divide-y divide-zinc-800 text-xs">
                  <div className="py-2 flex justify-between gap-2">
                    <span className="text-zinc-400">Faixa de Preço</span>
                    <span className="font-bold text-right">{res.faixa}</span>
                  </div>
                  <div className="py-2 flex justify-between gap-2">
                    <span className="text-zinc-400">Faixa de Peso</span>
                    <span className="font-bold text-right">
                      {res.faixaPeso}
                    </span>
                  </div>
                  <div className="py-2 flex justify-between gap-2">
                    <span className="text-zinc-400">Regra / Tarifa</span>
                    <span className="font-bold text-right">
                      {res.tarifaLabel} → {brl(res.tarifaValor)}
                    </span>
                  </div>
                  <div className="py-2 flex justify-between gap-2">
                    <span className="text-zinc-400">Taxa Fixa Magalu</span>
                    <span className="font-bold">+ {brl(res.taxaFixa)}</span>
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-between">
                  <span className="text-xs font-black uppercase tracking-wider">
                    Valor Final do Frete
                  </span>
                  <span className="text-2xl font-black">
                    {brl(res.custoTotal)}
                  </span>
                </div>
              </section>
            ) : (
              <div className="rounded-2xl border border-dashed border-zinc-800 bg-zinc-900/40 p-6 text-center">
                <Sparkles className="w-6 h-6 text-yellow-400/60 mx-auto mb-2" />
                <p className="text-xs font-semibold text-zinc-300">
                  Pronto para calcular o frete
                </p>
                <p className="text-[11px] text-zinc-500 mt-1">
                  Informe as dimensões ou busque um SKU à esquerda e clique em{" "}
                  <strong className="text-yellow-400">Calcular Frete</strong>.
                </p>
              </div>
            )}
          </div>
        </div>
      </main>

      {/* ==================== MODAL 1: MINHA CONTA (GUID + PERFIL) ==================== */}
      {modalConta && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between border-b border-zinc-800 pb-3">
              <div className="flex items-center gap-2.5">
                <ShieldCheck className="w-5 h-5 text-yellow-400" />
                <h3 className="text-base font-extrabold text-zinc-100">
                  Minha Conta
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setModalConta(false)}
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="p-3 rounded-xl bg-zinc-950 border border-zinc-800">
                <span className="text-zinc-400 block mb-0.5">
                  Nome Conectado
                </span>
                <span className="text-sm font-bold text-yellow-400">
                  {user?.nome}
                </span>
              </div>

              <div className="p-3 rounded-xl bg-zinc-950 border border-zinc-800">
                <span className="text-zinc-400 block mb-0.5">E-mail</span>
                <span className="text-sm font-semibold text-zinc-100">
                  {user?.email}
                </span>
              </div>

              <div className="p-3 rounded-xl bg-zinc-950 border border-zinc-800">
                <div className="flex items-center justify-between mb-1">
                  <span className="text-zinc-400">
                    ID Único do Usuário (GUID)
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      if (user?.id) {
                        navigator.clipboard.writeText(user.id);
                        setCopiadoId(true);
                        setTimeout(() => setCopiadoId(false), 2000);
                      }
                    }}
                    className="text-[11px] text-yellow-400 hover:underline flex items-center gap-1 cursor-pointer"
                  >
                    {copiadoId ? (
                      <>
                        <Check className="w-3 h-3" /> Copiado
                      </>
                    ) : (
                      <>
                        <Copy className="w-3 h-3" /> Copiar GUID
                      </>
                    )}
                  </button>
                </div>
                <code className="text-[11px] font-mono text-emerald-400 break-all">
                  {user?.id}
                </code>
              </div>

              <div className="p-3 rounded-xl bg-emerald-950/20 border border-emerald-500/30 text-emerald-300 flex items-center justify-between">
                <span>Segurança da Senha</span>
                <span className="font-bold">Hash BCrypt (Work Factor 12)</span>
              </div>
            </div>

            <button
              type="button"
              onClick={() => setModalConta(false)}
              className="w-full py-2.5 rounded-xl bg-yellow-400 text-zinc-950 font-bold text-xs cursor-pointer"
            >
              Fechar
            </button>
          </div>
        </div>
      )}

    </div>
  );
}
