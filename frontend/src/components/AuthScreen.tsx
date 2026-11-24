import React, { useState } from "react";
import { Truck, CheckCircle2, AlertCircle } from "lucide-react";
import type { ApiClient } from "../services/api";
import type { UserDisplayMetadata } from "../types";
import { isValidEmail } from "../utils/formatters";

interface AuthScreenProps {
  api: ApiClient;
  onAuthenticated: (
    jwt: string,
    displayFallback?: Partial<UserDisplayMetadata>,
  ) => boolean;
}

export function AuthScreen({ api, onAuthenticated }: AuthScreenProps) {
  const [modoAuth, setModoAuth] = useState<"login" | "register">("login");
  const [nomeCadastro, setNomeCadastro] = useState("");
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [authLoad, setAuthLoad] = useState(false);
  const [authErro, setAuthErro] = useState<string | null>(null);
  const [authSucesso, setAuthSucesso] = useState<string | null>(null);

  const emailDigitado = email.trim();
  const emailAuthInvalido =
    emailDigitado.length > 0 && !isValidEmail(emailDigitado);

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
