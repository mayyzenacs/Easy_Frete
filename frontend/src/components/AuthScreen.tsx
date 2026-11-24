import React, { useState, useEffect } from "react";
import {
  Truck,
  CheckCircle2,
  AlertCircle,
  Eye,
  EyeOff,
  Check,
} from "lucide-react";
import type { ApiClient } from "../services/api";
import type { UserDisplayMetadata } from "../types";
import { isValidEmail } from "../utils/formatters";

type AuthMode = "login" | "register" | "forgot" | "reset";

interface AuthScreenProps {
  api: ApiClient;
  initialResetToken?: string | null;
  onClearResetToken?: () => void;
  onAuthenticated: (
    jwt: string,
    displayFallback?: Partial<UserDisplayMetadata>,
  ) => boolean;
}

export function AuthScreen({
  api,
  initialResetToken,
  onClearResetToken,
  onAuthenticated,
}: AuthScreenProps) {
  const [modoAuth, setModoAuth] = useState<AuthMode>(
    initialResetToken ? "reset" : "login",
  );
  const [nomeCadastro, setNomeCadastro] = useState("");
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [mostrarSenha, setMostrarSenha] = useState(false);

  // Estados de redefinição de senha
  const [novaSenha, setNovaSenha] = useState("");
  const [confirmarSenha, setConfirmarSenha] = useState("");
  const [mostrarNovaSenha, setMostrarNovaSenha] = useState(false);
  const [validandoToken, setValidandoToken] = useState(
    Boolean(initialResetToken),
  );
  const [tokenValido, setTokenValido] = useState(false);

  const [authLoad, setAuthLoad] = useState(false);
  const [authErro, setAuthErro] = useState<string | null>(null);
  const [authSucesso, setAuthSucesso] = useState<string | null>(null);

  useEffect(() => {
    if (!initialResetToken) return;
    let cancelled = false;

    const validar = async () => {
      const result = await api.authValidateResetToken(initialResetToken);
      if (cancelled) return;
      if (result.ok && result.data?.valid) {
        setTokenValido(true);
      } else {
        setTokenValido(false);
        setAuthErro(
          result.ok
            ? "Link de redefinição inválido ou expirado."
            : result.error,
        );
      }
      setValidandoToken(false);
    };

    void validar();
    return () => {
      cancelled = true;
    };
  }, [api, initialResetToken]);

  const emailDigitado = email.trim();
  const emailAuthInvalido =
    emailDigitado.length > 0 && !isValidEmail(emailDigitado);

  const trocarModo = (novoModo: AuthMode) => {
    if (modoAuth === "reset" && novoModo !== "reset") {
      onClearResetToken?.();
    }
    setModoAuth(novoModo);
    setAuthErro(null);
    setAuthSucesso(null);
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
    try {
      if (modoAuth === "register") {
        const result = await api.authRegister(
          nomeCadastro.trim(),
          emailLimpo,
          senha,
        );
        if (!result.ok) {
          setAuthErro(result.error);
          return;
        }
        setAuthSucesso(
          "Conta criada com status Pendente. Aguarde a aprovação do administrador para entrar.",
        );
        setNomeCadastro("");
        setSenha("");
        setModoAuth("login");
        return;
      }

      const result = await api.authLogin(emailLimpo, senha);
      if (!result.ok) {
        setAuthErro(result.error);
        return;
      }

      const d = result.data;
      const jwt = d?.token || d?.accessToken || d?.jwt;
      if (!jwt) {
        setAuthErro("Não foi possível entrar. Tente novamente.");
        return;
      }

      const ok = onAuthenticated(jwt, {
        nome: d?.usuario?.nome ? String(d.usuario.nome) : undefined,
        email: d?.usuario?.email ? String(d.usuario.email) : emailLimpo,
      });

      if (ok) {
        setSenha("");
      } else {
        setAuthErro("Não foi possível entrar. Tente novamente.");
      }
    } finally {
      setAuthLoad(false);
    }
  };

  const enviarRecuperacao = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthErro(null);
    setAuthSucesso(null);

    const emailLimpo = email.trim().toLowerCase();
    if (!isValidEmail(emailLimpo)) {
      setAuthErro("Digite um e-mail válido (ex.: exemplo@mail.com).");
      return;
    }

    setAuthLoad(true);
    try {
      const result = await api.authForgotPassword(emailLimpo);
      if (!result.ok) {
        setAuthErro(result.error);
        return;
      }
      setAuthSucesso(
        result.data?.message ||
          "Se o e-mail estiver cadastrado e com acesso aprovado, enviaremos um link para redefinir sua senha.",
      );
    } finally {
      setAuthLoad(false);
    }
  };

  const salvarNovaSenha = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthErro(null);
    setAuthSucesso(null);

    if (!initialResetToken) {
      setAuthErro("Link de redefinição inválido ou expirado.");
      return;
    }

    if (novaSenha.length < 8) {
      setAuthErro("A senha deve ter pelo menos 8 caracteres.");
      return;
    }

    if (novaSenha !== confirmarSenha) {
      setAuthErro("As senhas não conferem.");
      return;
    }

    setAuthLoad(true);
    try {
      const result = await api.authResetPassword(initialResetToken, novaSenha);
      if (!result.ok) {
        setAuthErro(result.error);
        return;
      }

      setNovaSenha("");
      setConfirmarSenha("");
      onClearResetToken?.();
      setModoAuth("login");
      setAuthSucesso(
        result.data?.message ||
          "Senha redefinida com sucesso. Você já pode entrar com a nova senha.",
      );
    } finally {
      setAuthLoad(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col items-center justify-center p-4">
      <div className="flex-1 flex items-center justify-center w-full">
        <div className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-7 shadow-2xl">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-11 h-11 rounded-xl bg-yellow-400 text-zinc-950 flex items-center justify-center shrink-0">
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

          {(modoAuth === "login" || modoAuth === "register") && (
            <>
              <div className="grid grid-cols-2 bg-zinc-950 p-1 rounded-xl border border-zinc-800 mb-5 text-xs font-bold">
                <button
                  type="button"
                  onClick={() => trocarModo("login")}
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
                  onClick={() => trocarModo("register")}
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
                      <span>
                        Digite um e-mail válido (ex.: exemplo@mail.com).
                      </span>
                    </p>
                  )}
                </div>

                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                    Senha{" "}
                    {modoAuth === "register" && (
                      <span className="text-zinc-500">
                        (mínimo 8 caracteres)
                      </span>
                    )}
                  </label>
                  <div className="relative">
                    <input
                      type={mostrarSenha ? "text" : "password"}
                      required
                      minLength={modoAuth === "register" ? 8 : 1}
                      autoComplete={
                        modoAuth === "register"
                          ? "new-password"
                          : "current-password"
                      }
                      value={senha}
                      onChange={(e) => setSenha(e.target.value)}
                      className="w-full px-3.5 py-2.5 pr-10 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                    />
                    <button
                      type="button"
                      onClick={() => setMostrarSenha((prev) => !prev)}
                      aria-label={
                        mostrarSenha ? "Ocultar senha" : "Mostrar senha"
                      }
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-500 hover:text-zinc-200 transition cursor-pointer"
                    >
                      {mostrarSenha ? (
                        <EyeOff className="w-4 h-4" />
                      ) : (
                        <Eye className="w-4 h-4" />
                      )}
                    </button>
                  </div>

                  {modoAuth === "login" && (
                    <div className="mt-2 flex justify-end">
                      <button
                        type="button"
                        onClick={() => trocarModo("forgot")}
                        className="text-xs font-medium text-zinc-400 hover:text-yellow-400 transition cursor-pointer"
                      >
                        Esqueci minha senha
                      </button>
                    </div>
                  )}
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
            </>
          )}

          {modoAuth === "forgot" && (
            <div>
              <h2 className="text-base font-bold text-zinc-100">
                Redefinir senha
              </h2>
              <p className="text-xs text-zinc-400 mt-1 mb-5">
                Informe seu e-mail cadastrado para receber um link temporário de
                redefinição.
              </p>

              <form onSubmit={enviarRecuperacao} noValidate className="space-y-4">
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
                      <span>
                        Digite um e-mail válido (ex.: exemplo@mail.com).
                      </span>
                    </p>
                  )}
                </div>

                {authSucesso && (
                  <div
                    role="status"
                    className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs text-emerald-300 flex items-start gap-2 leading-relaxed"
                  >
                    <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400 mt-0.5" />
                    <span>{authSucesso}</span>
                  </div>
                )}

                {authErro && (
                  <div
                    role="alert"
                    className="p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-start gap-2 leading-relaxed"
                  >
                    <AlertCircle className="w-4 h-4 shrink-0 text-red-400 mt-0.5" />
                    <span>{authErro}</span>
                  </div>
                )}

                <button
                  type="submit"
                  disabled={authLoad || emailAuthInvalido}
                  className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 disabled:opacity-40 disabled:cursor-not-allowed text-zinc-950 font-extrabold text-sm transition cursor-pointer"
                >
                  {authLoad ? "Enviando link..." : "Enviar link por e-mail"}
                </button>
              </form>

              <div className="text-center mt-5">
                <button
                  type="button"
                  onClick={() => trocarModo("login")}
                  className="text-xs font-medium text-zinc-400 hover:text-yellow-400 transition cursor-pointer"
                >
                  ← Voltar para o login
                </button>
              </div>
            </div>
          )}

          {modoAuth === "reset" && (
            <div>
              {validandoToken ? (
                <p className="text-xs text-zinc-400 py-6 text-center">
                  Validando link de redefinição...
                </p>
              ) : !tokenValido ? (
                <div className="space-y-4">
                  <div
                    role="alert"
                    className="p-3.5 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-start gap-2 leading-relaxed"
                  >
                    <AlertCircle className="w-4 h-4 shrink-0 text-red-400 mt-0.5" />
                    <span>
                      {authErro || "Link de redefinição inválido ou expirado."}
                    </span>
                  </div>
                  <div className="flex flex-col gap-2">
                    <button
                      type="button"
                      onClick={() => trocarModo("forgot")}
                      className="w-full py-2.5 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 font-extrabold text-xs transition cursor-pointer"
                    >
                      Solicitar novo link
                    </button>
                    <button
                      type="button"
                      onClick={() => trocarModo("login")}
                      className="w-full py-2.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-200 font-semibold text-xs transition cursor-pointer"
                    >
                      Voltar ao login
                    </button>
                  </div>
                </div>
              ) : (
                <>
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-300 border border-emerald-500/25 mb-2">
                    <Check className="w-3.5 h-3.5" />
                    Link válido
                  </span>
                  <h2 className="text-base font-bold text-zinc-100">
                    Criar nova senha
                  </h2>
                  <p className="text-xs text-zinc-400 mt-1 mb-5">
                    Escolha uma nova senha com pelo menos 8 caracteres.
                  </p>

                  <form
                    onSubmit={salvarNovaSenha}
                    noValidate
                    className="space-y-4"
                  >
                    <div>
                      <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                        Nova senha{" "}
                        <span className="text-zinc-500">
                          (mín. 8 caracteres)
                        </span>
                      </label>
                      <div className="relative">
                        <input
                          type={mostrarNovaSenha ? "text" : "password"}
                          required
                          minLength={8}
                          maxLength={128}
                          autoComplete="new-password"
                          value={novaSenha}
                          onChange={(e) => {
                            setNovaSenha(e.target.value);
                            setAuthErro(null);
                          }}
                          className="w-full px-3.5 py-2.5 pr-10 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                        />
                        <button
                          type="button"
                          onClick={() => setMostrarNovaSenha((prev) => !prev)}
                          aria-label={
                            mostrarNovaSenha ? "Ocultar senha" : "Mostrar senha"
                          }
                          className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-500 hover:text-zinc-200 transition cursor-pointer"
                        >
                          {mostrarNovaSenha ? (
                            <EyeOff className="w-4 h-4" />
                          ) : (
                            <Eye className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    </div>

                    <div>
                      <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                        Confirmar nova senha
                      </label>
                      <input
                        type={mostrarNovaSenha ? "text" : "password"}
                        required
                        minLength={8}
                        maxLength={128}
                        autoComplete="new-password"
                        value={confirmarSenha}
                        onChange={(e) => {
                          setConfirmarSenha(e.target.value);
                          setAuthErro(null);
                        }}
                        className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400"
                      />
                    </div>

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
                      disabled={authLoad}
                      className="w-full py-3 rounded-xl bg-yellow-400 hover:bg-yellow-300 disabled:opacity-40 disabled:cursor-not-allowed text-zinc-950 font-extrabold text-sm transition cursor-pointer"
                    >
                      {authLoad ? "Salvando..." : "Salvar nova senha"}
                    </button>
                  </form>

                  <div className="text-center mt-5">
                    <button
                      type="button"
                      onClick={() => trocarModo("login")}
                      className="text-xs text-zinc-400 hover:text-zinc-200 transition cursor-pointer"
                    >
                      Cancelar
                    </button>
                  </div>
                </>
              )}
            </div>
          )}
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
