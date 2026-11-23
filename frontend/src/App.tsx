import React, { useState, useEffect, useRef } from "react";
import {
  Truck,
  Search,
  LogOut,
  CheckCircle2,
  AlertCircle,
  Scale,
  Box,
  ChevronDown,
  User,
  ShieldCheck,
  Copy,
  Check,
  X,
  ExternalLink,
  Pencil,
  Clock,
  Mail,
} from "lucide-react";

const API_BASE_URL = (import.meta.env.VITE_API_URL || "/api/v1").replace(
  /\/$/,
  "",
);

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$/;
const isValidEmail = (valor: string) => EMAIL_REGEX.test(valor.trim());

type RoleType = "Admin" | "User";
type StatusType = "Pendente" | "Aprovado" | "Rejeitado";

interface UserProfile {
  id: string;
  nome: string;
  email: string;
  role: RoleType;
  status: StatusType;
}

interface AdminUsuario extends UserProfile {
  criadoEm?: string;
}

interface EditandoAdmin {
  id: string;
  campo: "nome" | "email";
  valor: string;
  erro: string | null;
}

interface Resultado {
  faixa: string;
  faixaPeso: string;
  tarifaLabel: string;
  tarifaValor: number;
  taxaFixa: number;
  custoTotal: number;
}

interface SkuFeedback {
  tipo: "ok" | "warn" | "error";
  titulo?: string;
  mensagem: string;
}

const brl = (v: number) =>
  new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(
    Number.isFinite(v) ? v : 0,
  );

const num = (s: string) => {
  const n = parseFloat(String(s).replace(",", "."));
  return Number.isFinite(n) && n >= 0 ? n : 0;
};

const fmtKg = (v: number) => v.toFixed(3).replace(".", ",");

const formatarDataSolicitacao = (iso?: string) => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  const data = d.toLocaleDateString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
  const hora = d.toLocaleTimeString("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
  });
  return `${data} às ${hora}`;
};

const normalizarRole = (role: unknown): RoleType =>
  String(role).toLowerCase() === "admin" ? "Admin" : "User";

const normalizarStatus = (status: unknown): StatusType => {
  const s = String(status).toLowerCase();
  if (s === "aprovado") return "Aprovado";
  if (s === "rejeitado") return "Rejeitado";
  return "Pendente";
};

const labelRole = (role?: RoleType) =>
  role === "Admin" ? "Administrador" : "Usuário";

const statusBadgeClass: Record<StatusType, string> = {
  Aprovado: "bg-emerald-500/15 text-emerald-300 border-emerald-500/30",
  Pendente: "bg-yellow-400/15 text-yellow-300 border-yellow-400/30",
  Rejeitado: "bg-red-500/15 text-red-300 border-red-500/30",
};

const statusTextClass: Record<StatusType, string> = {
  Aprovado: "text-emerald-400",
  Pendente: "text-yellow-400",
  Rejeitado: "text-red-400",
};

const formatarFaixaPreco = (d: Record<string, unknown>): string => {
  const explicita = d.faixaPrecoAplicada ?? d.faixaPreco ?? d.faixaValor;
  if (typeof explicita === "string" && explicita.trim()) {
    return explicita.trim();
  }
  const regra = String(d.regraAplicada ?? "");
  if (regra.endsWith("_ACIMA_199")) return "Acima de R$ 199,00";
  if (regra.endsWith("_79_A_199")) return "R$ 79,00 – R$ 199,00";
  if (regra.startsWith("PERCENTUAL_")) return "Abaixo de R$ 79,00";
  return "—";
};

const formatarRotuloTarifa = (d: Record<string, unknown>): string => {
  const explicita = d.modalidadeAplicada ?? d.descricaoTarifa;
  if (typeof explicita === "string" && explicita.trim()) {
    return `Tarifa · ${explicita.trim()}`;
  }
  const regra = String(d.regraAplicada ?? "");
  if (regra.includes("FULFILLMENT")) return "Tarifa · Fulfillment";
  if (regra.includes("REPUTACAO_MAIOR_97")) return "Tarifa · Reputação > 97%";
  if (regra.includes("SEM_DESCONTO") || regra.includes("COLETA_ENTREGA")) {
    return "Tarifa · Coleta e entrega";
  }
  return "Tarifa";
};

export default function App() {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem("ef_jwt"),
  );
  const [user, setUser] = useState<UserProfile | null>(() => {
    try {
      const raw = localStorage.getItem("ef_user");
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      return {
        id: parsed.id,
        nome: String(parsed.nome || ""),
        email: String(parsed.email || ""),
        role: normalizarRole(parsed.role),
        status: normalizarStatus(parsed.status),
      };
    } catch {
      return null;
    }
  });

  // Autenticação (Login / Registro)
  const [modoAuth, setModoAuth] = useState<"login" | "register">("login");
  const [nomeCadastro, setNomeCadastro] = useState("");
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [authLoad, setAuthLoad] = useState(false);
  const [authErro, setAuthErro] = useState<string | null>(null);
  const [authSucesso, setAuthSucesso] = useState<string | null>(null);

  // Menu de Perfil, Copiar ID e Modal
  const [menuAberto, setMenuAberto] = useState(false);
  const [modalConta, setModalConta] = useState(false);
  const [copiadoId, setCopiadoId] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Painel Admin
  const [tela, setTela] = useState<"frete" | "admin">("frete");
  const [usuariosAdmin, setUsuariosAdmin] = useState<AdminUsuario[]>([]);
  const [editandoAdmin, setEditandoAdmin] = useState<EditandoAdmin | null>(
    null,
  );
  const [adminLoad, setAdminLoad] = useState(false);
  const [adminMsg, setAdminMsg] = useState<string | null>(null);
  const [adminSucesso, setAdminSucesso] = useState<string | null>(null);

  // Calculadora
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

  const [res, setRes] = useState<Resultado | null>(null);
  const [calc, setCalc] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuAberto(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  useEffect(() => {
    if (!modalConta) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setModalConta(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [modalConta]);

  useEffect(() => {
    if (!copiadoId) return;
    const timer = window.setTimeout(() => setCopiadoId(false), 1800);
    return () => window.clearTimeout(timer);
  }, [copiadoId]);

  const real = num(peso);
  const cubado = +((num(c) * num(l) * num(a)) / 6000).toFixed(3);
  const realMaior = real >= cubado;
  const v = num(valor);
  const mostra97 = !ful && v >= 79;

  const emailDigitado = email.trim();
  const emailAuthInvalido =
    emailDigitado.length > 0 && !isValidEmail(emailDigitado);

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

  const limparSessao = () => {
    localStorage.removeItem("ef_jwt");
    localStorage.removeItem("ef_user");
    setToken(null);
    setUser(null);
    setRes(null);
    setTela("frete");
    setMenuAberto(false);
    setModalConta(false);
    setEditandoAdmin(null);
  };

  const autenticar = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthErro(null);
    setAuthSucesso(null);

    const emailLimpo = email.trim().toLowerCase();
    if (!isValidEmail(emailLimpo)) {
      setAuthErro("Digite um e-mail válido (ex.: exemplo@mail.com).");
      return;
    }

    if (modoAuth === "register" && !nomeCadastro.trim()) {
      setAuthErro("Informe seu nome.");
      return;
    }

    setAuthLoad(true);

    const rota = modoAuth === "register" ? "register" : "login";
    const payload =
      modoAuth === "register"
        ? { nome: nomeCadastro.trim(), email: emailLimpo, password: senha }
        : { email: emailLimpo, password: senha };

    try {
      const r = await fetch(`${API_BASE_URL}/auth/${rota}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const d = await r.json().catch(() => ({}));

      if (!r.ok) {
        setAuthErro(
          d?.error ||
            d?.message ||
            "Não foi possível autenticar. Verifique os dados informados.",
        );
        return;
      }

      if (rota === "register") {
        setAuthSucesso(
          "Conta criada com status Pendente. Aguarde a aprovação do administrador para entrar.",
        );
        setNomeCadastro("");
        setSenha("");
        setModoAuth("login");
        return;
      }

      const jwt = d?.token || d?.accessToken || d?.jwt;
      const perfil = d?.usuario;
      if (jwt && perfil) {
        salvarSessao(jwt, {
          id: perfil.id,
          nome: String(perfil.nome || ""),
          email: String(perfil.email || emailLimpo),
          role: normalizarRole(perfil.role),
          status: normalizarStatus(perfil.status),
        });
        setSenha("");
      } else {
        setAuthErro("Não foi possível entrar. Tente novamente.");
      }
    } catch {
      setAuthErro("Sem conexão com o servidor.");
    } finally {
      setAuthLoad(false);
    }
  };

  const abrirPainelAdmin = async () => {
    setAdminMsg(null);
    setAdminSucesso(null);
    setEditandoAdmin(null);
    setTela("admin");

    if (user?.role !== "Admin") {
      setAdminMsg("Acesso restrito a administradores.");
      setUsuariosAdmin([]);
      return;
    }

    setAdminLoad(true);
    try {
      const r = await fetch(`${API_BASE_URL}/admin/usuarios`, {
        headers: authHeaders,
      });
      if (r.status === 401) {
        limparSessao();
        return;
      }
      if (!r.ok) {
        const d = await r.json().catch(() => ({}));
        setAdminMsg(d?.error || d?.message || "Acesso negado.");
        setUsuariosAdmin([]);
        return;
      }
      const lista = await r.json();
      setUsuariosAdmin(
        Array.isArray(lista)
          ? lista.map((u) => ({
              id: String(u.id),
              nome: String(u.nome || ""),
              email: String(u.email || ""),
              role: normalizarRole(u.role),
              status: normalizarStatus(u.status),
              criadoEm: typeof u.criadoEm === "string" ? u.criadoEm : undefined,
            }))
          : [],
      );
    } catch {
      setAdminMsg("Não foi possível carregar os usuários.");
    } finally {
      setAdminLoad(false);
    }
  };

  const atualizarUsuarioAdmin = async (
    id: string,
    dados: Partial<Pick<AdminUsuario, "nome" | "email" | "role" | "status">>,
    mensagemSucesso = "Usuário atualizado com sucesso.",
  ): Promise<boolean> => {
    if (user?.role !== "Admin") {
      setAdminMsg("Acesso restrito a administradores.");
      return false;
    }

    setAdminLoad(true);
    setAdminMsg(null);
    setAdminSucesso(null);
    try {
      const r = await fetch(
        `${API_BASE_URL}/admin/usuarios/${encodeURIComponent(String(id))}`,
        {
          method: "PATCH",
          headers: authHeaders,
          body: JSON.stringify(dados),
        },
      );
      if (r.status === 401) {
        limparSessao();
        return false;
      }
      const d = await r.json().catch(() => ({}));
      if (!r.ok) {
        setAdminMsg(
          d?.error || d?.message || "Não foi possível atualizar o usuário.",
        );
        return false;
      }

      const atual = usuariosAdmin.find((item) => item.id === id);
      const atualizado: AdminUsuario = {
        id: String(d?.id ?? id),
        nome: String(d?.nome ?? dados.nome ?? atual?.nome ?? ""),
        email: String(d?.email ?? dados.email ?? atual?.email ?? ""),
        role: normalizarRole(d?.role ?? dados.role ?? atual?.role),
        status: normalizarStatus(d?.status ?? dados.status ?? atual?.status),
        criadoEm:
          typeof d?.criadoEm === "string" ? d.criadoEm : atual?.criadoEm,
      };

      setUsuariosAdmin((atuais) =>
        atuais.map((usuario) => (usuario.id === id ? atualizado : usuario)),
      );

      if (user && user.id === atualizado.id) {
        const novoPerfil: UserProfile = {
          id: atualizado.id,
          nome: atualizado.nome,
          email: atualizado.email,
          role: atualizado.role,
          status: atualizado.status,
        };
        localStorage.setItem("ef_user", JSON.stringify(novoPerfil));
        setUser(novoPerfil);
      }

      setAdminSucesso(mensagemSucesso);
      return true;
    } catch {
      setAdminMsg("Não foi possível atualizar o usuário.");
      return false;
    } finally {
      setAdminLoad(false);
    }
  };

  const iniciarEdicaoAdmin = (
    usuario: AdminUsuario,
    campo: "nome" | "email",
  ) => {
    setAdminMsg(null);
    setAdminSucesso(null);
    setEditandoAdmin({
      id: usuario.id,
      campo,
      valor: campo === "nome" ? usuario.nome : usuario.email,
      erro: null,
    });
  };

  const confirmarEdicaoAdmin = async (usuario: AdminUsuario) => {
    if (!editandoAdmin || editandoAdmin.id !== usuario.id) return;
    const valorLimpo = editandoAdmin.valor.trim();

    if (editandoAdmin.campo === "nome") {
      if (!valorLimpo) {
        setEditandoAdmin({
          ...editandoAdmin,
          erro: "O nome não pode ficar vazio.",
        });
        return;
      }
      if (valorLimpo === usuario.nome) {
        setEditandoAdmin(null);
        return;
      }
      const ok = await atualizarUsuarioAdmin(
        usuario.id,
        { nome: valorLimpo },
        `Nome atualizado para "${valorLimpo}".`,
      );
      if (ok) setEditandoAdmin(null);
      return;
    }

    const emailMinusculo = valorLimpo.toLowerCase();
    if (!isValidEmail(emailMinusculo)) {
      setEditandoAdmin({
        ...editandoAdmin,
        erro: "Digite um e-mail válido (ex.: exemplo@mail.com).",
      });
      return;
    }
    if (emailMinusculo === usuario.email.toLowerCase()) {
      setEditandoAdmin(null);
      return;
    }
    const ok = await atualizarUsuarioAdmin(
      usuario.id,
      { email: emailMinusculo },
      `E-mail de ${usuario.nome} atualizado para "${emailMinusculo}".`,
    );
    if (ok) setEditandoAdmin(null);
  };

  const copiarIdUsuario = async () => {
    if (user?.id == null) return;
    try {
      await navigator.clipboard.writeText(String(user.id));
      setCopiadoId(true);
    } catch {
      setCopiadoId(false);
    }
  };

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
    setCalcError(null);
    try {
      const r = await fetch(
        `${API_BASE_URL}/produtos/${encodeURIComponent(code)}`,
        {
          headers: authHeaders,
        },
      );
      if (r.status === 401) {
        limparSessao();
        return;
      }
      if (r.ok) {
        const p = await r.json();
        const pesoApi = Number(p.pesoRealKg ?? p.pesoBruto ?? p.peso ?? 0);
        const comprimentoApi = Number(p.comprimentoCm ?? p.comprimento ?? 0);
        const larguraApi = Number(p.larguraCm ?? p.largura ?? 0);
        const alturaApi = Number(p.alturaCm ?? p.altura ?? 0);
        setPeso(pesoApi > 0 ? String(pesoApi) : "");
        setC(comprimentoApi > 0 ? String(comprimentoApi) : "");
        setL(larguraApi > 0 ? String(larguraApi) : "");
        setA(alturaApi > 0 ? String(alturaApi) : "");
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
        return;
      }

      setSkuFeedback({
        tipo: "error",
        mensagem:
          r.status === 404
            ? "SKU não encontrado. Solicite o cadastro das medidas na planilha."
            : "Não foi possível consultar o SKU.",
      });
    } catch {
      setSkuFeedback({
        tipo: "error",
        mensagem: "Sem conexão ao consultar o SKU.",
      });
    } finally {
      setSkuLoad(false);
    }
  };

  const calcular = async () => {
    if (!token) {
      limparSessao();
      return;
    }
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
      if (r.status === 401) {
        limparSessao();
        return;
      }
      if (r.ok) {
        const d = (await r.json()) as Record<string, unknown>;

        const cand: Resultado = {
          faixa: formatarFaixaPreco(d),
          faixaPeso: String(d.faixaPesoAplicada || d.faixaPeso || "—"),
          tarifaLabel: formatarRotuloTarifa(d),
          tarifaValor: Number(
            d.tarifaBase ?? d.tarifaValor ?? d.valorTarifa ?? 0,
          ),
          taxaFixa: Number(d.taxaFixaObrigatoria ?? d.taxaFixa ?? 0),
          custoTotal: Number(
            d.valorFinalFrete ?? d.custoTotalFrete ?? d.custoTotal ?? 0,
          ),
        };
        if (
          Number.isFinite(cand.tarifaValor) &&
          Number.isFinite(cand.custoTotal)
        ) {
          setRes(cand);
        } else {
          setCalcError("Não foi possível calcular o frete. Tente novamente.");
        }
        return;
      }

      const d = await r.json().catch(() => ({}));
      setCalcError(
        d?.error || d?.message || "Não foi possível calcular o frete.",
      );
    } catch {
      setCalcError("Sem conexão com o servidor.");
    } finally {
      setCalc(false);
    }
  };

  // ==================== TELA DE LOGIN E CADASTRO ====================
  if (!token) {
    return (
      <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col items-center justify-center p-4">
        <div className="flex-1 flex items-center justify-center w-full">
          <div className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-7 shadow-2xl">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-11 h-11 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-center">
              <Truck className="w-6 h-6 stroke-[2.3]" />
            </div>
            <div>
              <h1 className="text-xl font-black tracking-tight text-yellow-400">
                EASY FRETE
              </h1>
              <p className="text-xs text-zinc-400">
                Calculadora de frete automatizada para marketplaces
              </p>
            </div>
          </div>

          <div className="grid grid-cols-2 bg-zinc-950 p-1 rounded-xl border border-zinc-800 mb-5 text-xs font-bold">
            <button
              type="button"
              onClick={() => {
                setModoAuth("login");
                setAuthErro(null);
                setAuthSucesso(null);
              }}
              className={`py-2 rounded-lg transition cursor-pointer ${
                modoAuth === "login"
                  ? "bg-yellow-400 text-zinc-950"
                  : "text-zinc-400 hover:text-zinc-200"
              }`}
            >
              Entrar
            </button>
            <button
              type="button"
              onClick={() => {
                setModoAuth("register");
                setAuthErro(null);
                setAuthSucesso(null);
              }}
              className={`py-2 rounded-lg transition cursor-pointer ${
                modoAuth === "register"
                  ? "bg-yellow-400 text-zinc-950"
                  : "text-zinc-400 hover:text-zinc-200"
              }`}
            >
              Criar conta
            </button>
          </div>

          <form onSubmit={autenticar} noValidate className="space-y-4">
            {modoAuth === "register" && (
              <div>
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  Nome
                </label>
                <input
                  type="text"
                  required
                  maxLength={120}
                  placeholder="Seu nome completo"
                  value={nomeCadastro}
                  onChange={(e) => setNomeCadastro(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                />
              </div>
            )}
            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                E-mail
              </label>
              <input
                type="email"
                required
                autoComplete="email"
                placeholder="exemplo@mail.com"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value);
                  setAuthErro(null);
                }}
                className={`w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border text-sm focus:outline-none transition-colors ${
                  emailAuthInvalido
                    ? "border-red-500 focus:border-red-400"
                    : "border-zinc-800 focus:border-yellow-400"
                }`}
              />
              {emailAuthInvalido && (
                <p className="mt-1.5 text-[11px] text-red-400 flex items-center gap-1">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>Digite um e-mail válido (ex.: exemplo@mail.com).</span>
                </p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                Senha{" "}
                {modoAuth === "register" && (
                  <span className="text-zinc-500">(mínimo 8 caracteres)</span>
                )}
              </label>
              <input
                type="password"
                required
                minLength={modoAuth === "register" ? 8 : 1}
                value={senha}
                onChange={(e) => setSenha(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
              />
            </div>

            {authSucesso && (
              <div
                role="status"
                className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs text-emerald-300 flex items-center gap-2"
              >
                <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
                <span>{authSucesso}</span>
              </div>
            )}

            {authErro && (
              <div
                role="alert"
                className="p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2"
              >
                <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
                <span>{authErro}</span>
              </div>
            )}

            <button
              type="submit"
              disabled={authLoad || emailAuthInvalido}
              className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 disabled:opacity-40 disabled:cursor-not-allowed text-zinc-950 font-extrabold text-sm transition cursor-pointer"
            >
              {modoAuth === "register"
                ? authLoad
                  ? "Enviando cadastro..."
                  : "Solicitar cadastro"
                : authLoad
                  ? "Entrando..."
                  : "Entrar"}
            </button>
          </form>
          </div>
        </div>

        <footer className="py-3 text-center text-[11px] text-zinc-500">
          Projeto sem fins lucrativos · Desenvolvido por{" "}
          <a
            href="https://github.com/mayyzenacs"
            target="_blank"
            rel="noopener noreferrer"
            className="text-zinc-400 hover:text-yellow-400 underline underline-offset-2 decoration-zinc-700 hover:decoration-yellow-400/60 transition-colors"
          >
            Mayra Pereira
          </a>
        </footer>
      </div>
    );
  }

  // ==================== DASHBOARD PRINCIPAL ====================
  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col">
      {/* HEADER */}
      <header className="border-b border-zinc-800/80 bg-zinc-950/90 backdrop-blur sticky top-0 z-30">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 py-3 flex items-center justify-between gap-4">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-lg bg-yellow-400 text-zinc-950 flex items-center justify-center">
              <Truck className="w-5 h-5 stroke-[2.4]" />
            </div>
            <span className="font-black text-xl tracking-tight text-yellow-400">
              EASY FRETE
            </span>
          </div>

          {/* Seletor de Marketplace */}
          <div className="flex items-center gap-1 bg-zinc-900 p-1 rounded-full border border-zinc-800 text-xs">
            <button
              type="button"
              onClick={() => setTela("frete")}
              className={`px-4 py-1.5 rounded-full font-bold transition cursor-pointer ${
                tela === "frete"
                  ? "bg-yellow-400 text-zinc-950"
                  : "text-zinc-300 hover:text-white"
              }`}
            >
              Magalu
            </button>
            <span
              className="px-3 py-1.5 text-zinc-500 cursor-not-allowed"
              title="Em breve"
            >
              Amazon{" "}
              <span className="text-[10px] text-zinc-600">· em breve</span>
            </span>
          </div>

          {/* Menu do Usuário */}
          <div className="relative" ref={menuRef}>
            <button
              type="button"
              onClick={() => setMenuAberto((prev) => !prev)}
              className="flex items-center gap-2.5 pl-2 pr-3 py-1.5 rounded-xl bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 transition cursor-pointer"
            >
              <span className="w-8 h-8 rounded-lg bg-yellow-400/15 border border-yellow-400/40 text-yellow-300 flex items-center justify-center font-bold text-sm">
                {(user?.nome || "U").charAt(0).toUpperCase()}
              </span>
              <span className="text-left hidden sm:block leading-tight">
                <span className="block text-xs font-semibold text-zinc-100">
                  {user?.nome || "Usuário"}
                </span>
                <span className="block text-[11px] text-zinc-400">
                  {labelRole(user?.role)}
                </span>
              </span>
              <ChevronDown
                className={`w-4 h-4 text-zinc-400 transition-transform ${
                  menuAberto ? "rotate-180 text-yellow-400" : ""
                }`}
              />
            </button>

            {menuAberto && (
              <div className="absolute right-0 mt-2 w-56 rounded-xl bg-zinc-900 border border-zinc-800 shadow-2xl py-1.5 z-50 text-xs">
                <div className="px-4 py-2 border-b border-zinc-800">
                  <p className="font-semibold text-zinc-100 truncate">
                    {user?.nome}
                  </p>
                  <p className="text-[11px] text-zinc-400 truncate">
                    {user?.email}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => {
                    setModalConta(true);
                    setMenuAberto(false);
                  }}
                  className="w-full px-4 py-2 text-left text-zinc-200 hover:bg-zinc-800 flex items-center gap-2.5 cursor-pointer"
                >
                  <User className="w-4 h-4 text-yellow-400" />
                  Minha conta
                </button>

                {user?.role === "Admin" && (
                  <button
                    type="button"
                    onClick={() => {
                      setMenuAberto(false);
                      abrirPainelAdmin();
                    }}
                    className="w-full px-4 py-2 text-left text-zinc-200 hover:bg-zinc-800 flex items-center gap-2.5 cursor-pointer"
                  >
                    <ShieldCheck className="w-4 h-4 text-yellow-400" />
                    Painel admin
                  </button>
                )}

                <div className="border-t border-zinc-800 mt-1 pt-1">
                  <button
                    type="button"
                    onClick={limparSessao}
                    className="w-full px-4 py-2 text-left text-red-400 hover:bg-red-500/10 flex items-center gap-2.5 cursor-pointer"
                  >
                    <LogOut className="w-4 h-4" />
                    Sair
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* CONTEÚDO PRINCIPAL */}
      <main className="max-w-6xl w-full mx-auto px-4 sm:px-6 py-6 flex-1">
        {tela === "admin" ? (
          user?.role === "Admin" ? (
            <section>
              <div className="flex flex-wrap items-end justify-between gap-3 mb-6">
                <div>
                  <h1 className="text-2xl font-black tracking-tight">
                    Usuários
                  </h1>
                  <p className="text-sm text-zinc-400 mt-0.5">
                    Aprove solicitações, edite nome ou e-mail clicando no lápis
                    e ajuste permissões.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setTela("frete")}
                  className="px-3.5 py-2 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-xs font-bold cursor-pointer"
                >
                  Voltar ao cálculo
                </button>
              </div>

              {adminMsg && (
                <div
                  role="alert"
                  className="mb-4 p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2"
                >
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
                  <span>{adminMsg}</span>
                </div>
              )}

              {adminSucesso && (
                <div
                  role="status"
                  className="mb-4 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs text-emerald-300 flex items-center gap-2"
                >
                  <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
                  <span>{adminSucesso}</span>
                </div>
              )}

              {adminLoad && usuariosAdmin.length === 0 ? (
                <p className="text-xs text-zinc-400">Carregando usuários...</p>
              ) : (
                <div className="space-y-3">
                  {usuariosAdmin.map((usuario) => {
                    const editandoNome =
                      editandoAdmin?.id === usuario.id &&
                      editandoAdmin.campo === "nome";
                    const editandoEmail =
                      editandoAdmin?.id === usuario.id &&
                      editandoAdmin.campo === "email";
                    const emailEdicaoInvalido =
                      editandoEmail && !isValidEmail(editandoAdmin.valor);

                    return (
                      <div
                        key={usuario.id}
                        className="grid gap-3 lg:grid-cols-[1.4fr_185px_110px_140px_auto] items-center p-4 rounded-xl bg-zinc-900 border border-zinc-800"
                      >
                        <div className="min-w-0">
                          {editandoNome ? (
                            <div className="space-y-1">
                              <div className="flex items-center gap-1.5">
                                <input
                                  type="text"
                                  autoFocus
                                  maxLength={120}
                                  value={editandoAdmin.valor}
                                  onChange={(e) =>
                                    setEditandoAdmin({
                                      ...editandoAdmin,
                                      valor: e.target.value,
                                      erro: null,
                                    })
                                  }
                                  onKeyDown={(e) => {
                                    if (e.key === "Enter") {
                                      e.preventDefault();
                                      confirmarEdicaoAdmin(usuario);
                                    } else if (e.key === "Escape") {
                                      e.preventDefault();
                                      setEditandoAdmin(null);
                                    }
                                  }}
                                  className={`flex-1 min-w-0 px-2.5 py-1 rounded-lg bg-zinc-950 border text-sm font-semibold focus:outline-none ${
                                    editandoAdmin.erro
                                      ? "border-red-500"
                                      : "border-yellow-400"
                                  }`}
                                />
                                <button
                                  type="button"
                                  disabled={
                                    adminLoad || !editandoAdmin.valor.trim()
                                  }
                                  onClick={() => confirmarEdicaoAdmin(usuario)}
                                  title="Salvar nome"
                                  className="p-1.5 rounded-lg bg-yellow-400 hover:bg-yellow-300 disabled:opacity-30 disabled:cursor-not-allowed text-zinc-950 cursor-pointer"
                                >
                                  <Check className="w-3.5 h-3.5" />
                                </button>
                                <button
                                  type="button"
                                  onClick={() => setEditandoAdmin(null)}
                                  title="Cancelar"
                                  className="p-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-300 cursor-pointer"
                                >
                                  <X className="w-3.5 h-3.5" />
                                </button>
                              </div>
                              {editandoAdmin.erro && (
                                <p className="text-[11px] text-red-400 flex items-center gap-1">
                                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                                  <span>{editandoAdmin.erro}</span>
                                </p>
                              )}
                            </div>
                          ) : (
                            <div className="flex items-center gap-2 min-w-0">
                              <span className="text-sm font-bold text-zinc-100 truncate">
                                {usuario.nome}
                              </span>
                              <button
                                type="button"
                                disabled={adminLoad}
                                onClick={() =>
                                  iniciarEdicaoAdmin(usuario, "nome")
                                }
                                title="Editar nome"
                                className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-zinc-800/80 hover:bg-zinc-800 text-zinc-300 hover:text-yellow-400 border border-zinc-700/70 text-[11px] font-medium transition cursor-pointer shrink-0"
                              >
                                <Pencil className="w-3 h-3" />
                                <span>Editar</span>
                              </button>
                            </div>
                          )}

                          {editandoEmail ? (
                            <div className="space-y-1 mt-1">
                              <div className="flex items-center gap-1.5">
                                <input
                                  type="email"
                                  autoFocus
                                  maxLength={180}
                                  placeholder="exemplo@mail.com"
                                  value={editandoAdmin.valor}
                                  onChange={(e) =>
                                    setEditandoAdmin({
                                      ...editandoAdmin,
                                      valor: e.target.value,
                                      erro: null,
                                    })
                                  }
                                  onKeyDown={(e) => {
                                    if (e.key === "Enter") {
                                      e.preventDefault();
                                      confirmarEdicaoAdmin(usuario);
                                    } else if (e.key === "Escape") {
                                      e.preventDefault();
                                      setEditandoAdmin(null);
                                    }
                                  }}
                                  className={`flex-1 min-w-0 px-2.5 py-1 rounded-lg bg-zinc-950 border text-xs focus:outline-none ${
                                    emailEdicaoInvalido || editandoAdmin.erro
                                      ? "border-red-500"
                                      : "border-yellow-400"
                                  }`}
                                />
                                <button
                                  type="button"
                                  disabled={adminLoad || emailEdicaoInvalido}
                                  onClick={() => confirmarEdicaoAdmin(usuario)}
                                  title="Salvar e-mail"
                                  className="p-1.5 rounded-lg bg-yellow-400 hover:bg-yellow-300 disabled:opacity-30 disabled:cursor-not-allowed text-zinc-950 cursor-pointer"
                                >
                                  <Check className="w-3.5 h-3.5" />
                                </button>
                                <button
                                  type="button"
                                  onClick={() => setEditandoAdmin(null)}
                                  title="Cancelar"
                                  className="p-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-300 cursor-pointer"
                                >
                                  <X className="w-3.5 h-3.5" />
                                </button>
                              </div>
                              {(emailEdicaoInvalido || editandoAdmin.erro) && (
                                <p className="text-[11px] text-red-400 flex items-center gap-1">
                                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                                  <span>
                                    {editandoAdmin.erro ||
                                      "Digite um e-mail válido (ex.: exemplo@mail.com)."}
                                  </span>
                                </p>
                              )}
                            </div>
                          ) : (
                            <div className="flex items-center gap-2 min-w-0 mt-1">
                              <span className="text-xs text-zinc-400 truncate flex items-center gap-1.5">
                                <Mail className="w-3.5 h-3.5 shrink-0 text-zinc-500" />
                                <span className="truncate">
                                  {usuario.email}
                                </span>
                              </span>
                              <button
                                type="button"
                                disabled={adminLoad}
                                onClick={() =>
                                  iniciarEdicaoAdmin(usuario, "email")
                                }
                                title="Editar e-mail"
                                className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-zinc-800/60 hover:bg-zinc-800 text-zinc-400 hover:text-yellow-400 border border-zinc-800 text-[11px] transition cursor-pointer shrink-0"
                              >
                                <Pencil className="w-3 h-3" />
                                <span>Editar</span>
                              </button>
                            </div>
                          )}
                        </div>

                        <div className="text-xs">
                          <span className="text-[11px] text-zinc-500 flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            <span>Solicitado em</span>
                          </span>
                          <span className="font-medium text-zinc-300 block mt-0.5">
                            {formatarDataSolicitacao(usuario.criadoEm)}
                          </span>
                        </div>

                        <span
                          className={`justify-self-start text-[11px] font-semibold px-2.5 py-0.5 rounded-full border ${statusBadgeClass[usuario.status]}`}
                        >
                          {usuario.status}
                        </span>

                        <select
                          value={usuario.role}
                          disabled={adminLoad}
                          onChange={(e) =>
                            atualizarUsuarioAdmin(
                              usuario.id,
                              { role: normalizarRole(e.target.value) },
                              `Permissão de ${usuario.nome} atualizada.`,
                            )
                          }
                          className="px-2.5 py-2 rounded-lg bg-zinc-950 border border-zinc-800 text-xs cursor-pointer"
                        >
                          <option value="User">Usuário</option>
                          <option value="Admin">Administrador</option>
                        </select>

                        <div className="flex gap-2">
                          <button
                            type="button"
                            disabled={
                              adminLoad || usuario.status === "Aprovado"
                            }
                            onClick={() =>
                              atualizarUsuarioAdmin(
                                usuario.id,
                                { status: "Aprovado" },
                                `Conta de ${usuario.nome} aprovada.`,
                              )
                            }
                            className="px-3 py-2 rounded-lg bg-emerald-500 hover:bg-emerald-400 disabled:opacity-30 disabled:cursor-not-allowed text-zinc-950 text-xs font-bold cursor-pointer"
                          >
                            Aprovar
                          </button>
                          <button
                            type="button"
                            disabled={
                              adminLoad || usuario.status === "Rejeitado"
                            }
                            onClick={() =>
                              atualizarUsuarioAdmin(
                                usuario.id,
                                { status: "Rejeitado" },
                                `Conta de ${usuario.nome} rejeitada.`,
                              )
                            }
                            className="px-3 py-2 rounded-lg bg-red-500 hover:bg-red-400 disabled:opacity-30 disabled:cursor-not-allowed text-white text-xs font-bold cursor-pointer"
                          >
                            Rejeitar
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </section>
          ) : (
            <div
              role="alert"
              className="p-6 rounded-xl border border-red-500/30 bg-red-500/10 text-red-300 text-xs"
            >
              Acesso restrito a administradores.
            </div>
          )
        ) : (
          <div>
            <div className="mb-6">
              <h1 className="text-2xl font-black tracking-tight">
                Cálculo de frete{" "}
                <span className="text-yellow-400">· Magalu</span>
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
                        onKeyDown={(e) => e.key === "Enter" && buscarSku()}
                        placeholder="Digite o SKU e pressione Enter"
                        className="flex-1 px-3.5 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm uppercase placeholder:normal-case focus:outline-none focus:border-yellow-400"
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
                            {skuFeedback.titulo && (
                              <b>{skuFeedback.titulo}: </b>
                            )}
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
                        step: "0.01",
                      },
                      {
                        label: "Comprimento (cm)",
                        val: c,
                        set: setC,
                        step: "0.1",
                      },
                      { label: "Largura (cm)", val: l, set: setL, step: "0.1" },
                      { label: "Altura (cm)", val: a, set: setA, step: "0.1" },
                    ].map((f) => (
                      <div key={f.label}>
                        <label className="block text-xs text-zinc-400 mb-1">
                          {f.label}
                        </label>
                        <input
                          type="number"
                          step={f.step}
                          min="0"
                          value={f.val}
                          onChange={(e) => {
                            f.set(e.target.value);
                            setRes(null);
                          }}
                          className="w-full px-3 py-2 rounded-xl bg-zinc-950 border border-zinc-800 text-sm font-semibold focus:outline-none focus:border-yellow-400"
                        />
                      </div>
                    ))}
                  </div>
                </section>

                {/* Preço e Modalidade */}
                <section className="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-5 space-y-4">
                  <div className="grid grid-cols-1 sm:grid-cols-12 gap-3 items-end">
                    <div className="sm:col-span-5">
                      <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                        Preço de venda (R$)
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        value={valor}
                        onChange={(e) => {
                          setValor(e.target.value);
                          setRes(null);
                        }}
                        className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-base font-bold text-yellow-400 focus:outline-none focus:border-yellow-400"
                      />
                    </div>

                    <div className="sm:col-span-7">
                      <div className="flex items-center justify-between px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800">
                        <div>
                          <span className="text-xs font-semibold text-zinc-200 block">
                            Fulfillment
                          </span>
                          <span className="text-[11px] text-zinc-500">
                            Produto armazenado no CD Magalu
                          </span>
                        </div>
                        <button
                          type="button"
                          onClick={() => {
                            setFul((x) => !x);
                            setRes(null);
                          }}
                          className={`px-3 py-1 rounded-lg text-xs font-extrabold transition cursor-pointer ${
                            ful
                              ? "bg-yellow-400 text-zinc-950"
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
                        <span className="text-xs font-semibold text-zinc-200 block">
                          Reputação da loja acima de 97%?
                        </span>
                        <span className="text-[11px] text-zinc-500">
                          Afeta o desconto na coleta e entrega
                        </span>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          setAcima97((x) => !x);
                          setRes(null);
                        }}
                        className={`px-3 py-1 rounded-lg text-xs font-extrabold transition cursor-pointer ${
                          acima97
                            ? "bg-yellow-400 text-zinc-950"
                            : "bg-zinc-800 text-zinc-400"
                        }`}
                      >
                        {acima97 ? "SIM" : "NÃO"}
                      </button>
                    </div>
                  )}

                  <button
                    type="button"
                    onClick={calcular}
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
                        <Scale className="w-3.5 h-3.5 text-emerald-400" /> Peso
                        real
                      </span>
                      {realMaior && (
                        <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                          USADO
                        </span>
                      )}
                    </div>
                    <div className="text-2xl font-black">
                      {fmtKg(real)}{" "}
                      <span className="text-xs font-normal text-zinc-400">
                        kg
                      </span>
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
                        <Box className="w-3.5 h-3.5 text-yellow-400" /> Peso
                        cubado
                      </span>
                      {!realMaior && (
                        <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-500 text-zinc-950">
                          USADO
                        </span>
                      )}
                    </div>
                    <div className="text-2xl font-black">
                      {fmtKg(cubado)}{" "}
                      <span className="text-xs font-normal text-zinc-400">
                        kg
                      </span>
                    </div>
                  </div>
                </div>

                <p className="text-[11px] text-zinc-500 -mt-1 px-1">
                  O frete considera o maior entre peso real e cubado (C × L × A
                  ÷ 6000).
                </p>

                {res ? (
                  <section className="bg-zinc-900 border-2 border-yellow-400 rounded-2xl p-5 space-y-3">
                    <h2 className="text-sm font-bold text-yellow-400 flex items-center gap-1.5">
                      <CheckCircle2 className="w-4 h-4" /> Resultado
                    </h2>

                    <dl className="divide-y divide-zinc-800 text-sm">
                      <div className="py-2 flex justify-between gap-3">
                        <dt className="text-zinc-400">Faixa de preço</dt>
                        <dd className="font-semibold text-right">
                          {res.faixa}
                        </dd>
                      </div>
                      <div className="py-2 flex justify-between gap-3">
                        <dt className="text-zinc-400">Faixa de peso</dt>
                        <dd className="font-semibold text-right">
                          {res.faixaPeso}
                        </dd>
                      </div>
                      <div className="py-2 flex justify-between gap-3">
                        <dt className="text-zinc-400">{res.tarifaLabel}</dt>
                        <dd className="font-semibold">
                          {brl(res.tarifaValor)}
                        </dd>
                      </div>
                      <div className="py-2 flex justify-between gap-3">
                        <dt className="text-zinc-400">Taxa fixa</dt>
                        <dd className="font-semibold">+ {brl(res.taxaFixa)}</dd>
                      </div>
                    </dl>

                    <div className="p-3.5 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-between">
                      <span className="text-xs font-black uppercase tracking-wider">
                        Frete total
                      </span>
                      <span className="text-2xl font-black">
                        {brl(res.custoTotal)}
                      </span>
                    </div>
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
        )}
      </main>

      <footer className="border-t border-zinc-800/60 py-3.5 px-4">
        <p className="max-w-6xl mx-auto text-center text-[11px] text-zinc-500">
          Projeto sem fins lucrativos · Desenvolvido por{" "}
          <a
            href="https://github.com/mayyzenacs"
            target="_blank"
            rel="noopener noreferrer"
            className="text-zinc-400 hover:text-yellow-400 underline underline-offset-2 decoration-zinc-700 hover:decoration-yellow-400/60 transition-colors"
          >
            Mayra Pereira
          </a>
        </p>
      </footer>

      {/* MODAL MINHA CONTA */}
      {modalConta && (
        <div
          onClick={() => setModalConta(false)}
          className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-start justify-center p-4 pt-24 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-zinc-100">Minha conta</h3>
              <button
                type="button"
                onClick={() => setModalConta(false)}
                aria-label="Fechar"
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex items-center gap-3">
              <span className="w-12 h-12 rounded-xl bg-yellow-400/15 border border-yellow-400/40 text-yellow-300 flex items-center justify-center font-black text-lg">
                {(user?.nome || "U").charAt(0).toUpperCase()}
              </span>
              <div className="min-w-0">
                <p className="font-bold text-zinc-100 truncate">{user?.nome}</p>
                <p className="text-xs text-zinc-400 truncate">{user?.email}</p>
              </div>
            </div>

            <dl className="text-xs divide-y divide-zinc-800 border-y border-zinc-800">
              <div className="py-2.5 flex justify-between">
                <dt className="text-zinc-400">Perfil</dt>
                <dd className="font-semibold text-zinc-100">
                  {labelRole(user?.role)}
                </dd>
              </div>
              <div className="py-2.5 flex justify-between">
                <dt className="text-zinc-400">Status</dt>
                <dd
                  className={`font-semibold ${
                    user?.status ? statusTextClass[user.status] : ""
                  }`}
                >
                  {user?.status}
                </dd>
              </div>
              <div className="py-2.5 flex justify-between items-center gap-3">
                <dt className="text-zinc-400 shrink-0">ID</dt>
                <dd className="flex items-center gap-2 min-w-0">
                  <code className="font-mono text-[11px] text-zinc-500 truncate">
                    {user?.id}
                  </code>
                  <button
                    type="button"
                    onClick={copiarIdUsuario}
                    title="Copiar ID"
                    className="text-yellow-400 hover:text-yellow-300 shrink-0 cursor-pointer"
                  >
                    {copiadoId ? (
                      <Check className="w-3.5 h-3.5" />
                    ) : (
                      <Copy className="w-3.5 h-3.5" />
                    )}
                  </button>
                </dd>
              </div>
            </dl>

            <button
              type="button"
              onClick={() => setModalConta(false)}
              className="w-full py-2.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 font-bold text-xs cursor-pointer"
            >
              Fechar
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
