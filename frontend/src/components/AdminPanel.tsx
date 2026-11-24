import { useState, useEffect } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Check,
  X,
  Pencil,
  Mail,
  Clock,
} from "lucide-react";
import type { ApiClient } from "../services/api";
import type {
  AdminUsuario,
  AdminUsuarioPatchPayload,
  EditandoAdmin,
  UserDisplayMetadata,
  UserProfile,
} from "../types";
import {
  formatarDataSolicitacao,
  isValidEmail,
  normalizarRole,
  normalizarStatus,
  statusBadgeClass,
} from "../utils/formatters";

interface AdminPanelProps {
  api: ApiClient;
  currentUser: UserProfile;
  onBackToCalculator: () => void;
  onUpdateOwnDisplay: (display: Partial<UserDisplayMetadata>) => void;
}

export function AdminPanel({
  api,
  currentUser,
  onBackToCalculator,
  onUpdateOwnDisplay,
}: AdminPanelProps) {
  const [usuariosAdmin, setUsuariosAdmin] = useState<AdminUsuario[]>([]);
  const [editandoAdmin, setEditandoAdmin] = useState<EditandoAdmin | null>(
    null,
  );
  const [adminLoad, setAdminLoad] = useState(false);
  const [adminMsg, setAdminMsg] = useState<string | null>(null);
  const [adminSucesso, setAdminSucesso] = useState<string | null>(null);

  useEffect(() => {
    if (currentUser.role !== "Admin") return;

    let cancelled = false;
    const carregar = async () => {
      setAdminLoad(true);
      setAdminMsg(null);
      setAdminSucesso(null);
      const result = await api.listAdminUsuarios();
      if (cancelled) return;

      if (!result.ok) {
        if (!result.unauthorized) {
          setAdminMsg(result.error);
          setUsuariosAdmin([]);
        }
        setAdminLoad(false);
        return;
      }

      const lista = Array.isArray(result.data)
        ? result.data.map((u) => ({
            id: String(u.id),
            nome: String(u.nome || ""),
            email: String(u.email || ""),
            role: normalizarRole(u.role),
            status: normalizarStatus(u.status),
            criadoEm: typeof u.criadoEm === "string" ? u.criadoEm : undefined,
          }))
        : [];
      setUsuariosAdmin(lista);
      setAdminLoad(false);
    };

    void carregar();
    return () => {
      cancelled = true;
    };
  }, [api, currentUser.role]);

  if (currentUser.role !== "Admin") {
    return (
      <div
        role="alert"
        className="p-6 rounded-xl border border-red-500/30 bg-red-500/10 text-red-300 text-xs"
      >
        Acesso restrito a administradores.
      </div>
    );
  }

  const atualizarUsuarioAdmin = async (
    id: string,
    dados: AdminUsuarioPatchPayload,
    mensagemSucesso = "Usuário atualizado com sucesso.",
  ): Promise<boolean> => {
    setAdminLoad(true);
    setAdminMsg(null);
    setAdminSucesso(null);

    try {
      const result = await api.patchAdminUsuario(id, dados);
      if (!result.ok) {
        if (!result.unauthorized) {
          setAdminMsg(result.error);
        }
        return false;
      }

      const d = result.data;
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

      if (currentUser.id === atualizado.id) {
        onUpdateOwnDisplay({
          nome: atualizado.nome,
          email: atualizado.email,
        });
      }

      setAdminSucesso(mensagemSucesso);
      return true;
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

  return (
    <section>
      <div className="flex flex-wrap items-end justify-between gap-3 mb-6">
        <div>
          <h1 className="text-2xl font-black tracking-tight">Usuários</h1>
          <p className="text-sm text-zinc-400 mt-0.5">
            Aprove solicitações, edite nome ou e-mail clicando no lápis e ajuste
            permissões.
          </p>
        </div>
        <button
          type="button"
          onClick={onBackToCalculator}
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
                              void confirmarEdicaoAdmin(usuario);
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
                          disabled={adminLoad || !editandoAdmin.valor.trim()}
                          onClick={() => void confirmarEdicaoAdmin(usuario)}
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
                        onClick={() => iniciarEdicaoAdmin(usuario, "nome")}
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
                              void confirmarEdicaoAdmin(usuario);
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
                          onClick={() => void confirmarEdicaoAdmin(usuario)}
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
                        <span className="truncate">{usuario.email}</span>
                      </span>
                      <button
                        type="button"
                        disabled={adminLoad}
                        onClick={() => iniciarEdicaoAdmin(usuario, "email")}
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
                    void atualizarUsuarioAdmin(
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
                    disabled={adminLoad || usuario.status === "Aprovado"}
                    onClick={() =>
                      void atualizarUsuarioAdmin(
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
                    disabled={adminLoad || usuario.status === "Rejeitado"}
                    onClick={() =>
                      void atualizarUsuarioAdmin(
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
  );
}
