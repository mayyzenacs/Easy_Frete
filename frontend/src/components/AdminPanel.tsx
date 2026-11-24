import { useState, useEffect, useCallback, useMemo } from "react";
import {
  AlertCircle,
  CheckCircle2,
  X,
  Pencil,
  Mail,
  Clock,
  Search,
  Crown,
  Shield,
  Lock,
  Trash2,
  Send,
} from "lucide-react";
import type { ApiClient } from "../services/api";
import type {
  AdminTab,
  AdminUsuario,
  AdminUsuarioPatchPayload,
  DeletionRequestStatus,
  RoleType,
  SolicitacaoExclusao,
  StatusType,
  UserDisplayMetadata,
  UserProfile,
} from "../types";
import {
  formatarDataSolicitacao,
  isAdminOrMaster,
  isValidEmail,
  normalizarDeletionStatus,
  normalizarRole,
  normalizarStatus,
  statusBadgeClass,
} from "../utils/formatters";

interface AdminPanelProps {
  api: ApiClient;
  currentUser: UserProfile;
  activeTab: AdminTab;
  onChangeTab: (tab: AdminTab) => void;
  onBackToCalculator: () => void;
  onUpdateOwnDisplay: (display: Partial<UserDisplayMetadata>) => void;
  onRefreshNotifications: () => void;
}

interface UserRowPermissions {
  canEditName: boolean;
  canEditEmail: boolean;
  canChangeRole: boolean;
  canChangeStatus: boolean;
  canDirectDelete: boolean;
  canRequestDelete: boolean;
  lockReason: string | null;
}

function getRowPermissions(
  actor: UserProfile,
  target: AdminUsuario,
): UserRowPermissions {
  const isSelf = actor.id === target.id;
  const actorIsMaster = actor.role === "Master";
  const targetIsMaster = target.role === "Master";
  const targetIsAdmin = target.role === "Admin";

  if (targetIsMaster) {
    return {
      canEditName: actorIsMaster && isSelf,
      canEditEmail: false,
      canChangeRole: false,
      canChangeStatus: false,
      canDirectDelete: false,
      canRequestDelete: false,
      lockReason: actorIsMaster ? "Conta protegida" : "Somente leitura",
    };
  }

  if (actorIsMaster) {
    return {
      canEditName: true,
      canEditEmail: true,
      canChangeRole: true,
      canChangeStatus: true,
      canDirectDelete: true,
      canRequestDelete: false,
      lockReason: null,
    };
  }

  if (targetIsAdmin) {
    return {
      canEditName: false,
      canEditEmail: false,
      canChangeRole: false,
      canChangeStatus: false,
      canDirectDelete: false,
      canRequestDelete: false,
      lockReason: isSelf ? "Sua conta Admin" : "Somente Master",
    };
  }

  return {
    canEditName: true,
    canEditEmail: true,
    canChangeRole: false,
    canChangeStatus: true,
    canDirectDelete: false,
    canRequestDelete: true,
    lockReason: null,
  };
}

const deletionBadgeClass: Record<DeletionRequestStatus, string> = {
  Pendente: "bg-yellow-400/12 text-yellow-300 border-yellow-400/30",
  Aprovado: "bg-emerald-500/12 text-emerald-300 border-emerald-500/30",
  Recusado: "bg-red-500/12 text-red-300 border-red-500/30",
};

export function AdminPanel({
  api,
  currentUser,
  activeTab,
  onChangeTab,
  onBackToCalculator,
  onUpdateOwnDisplay,
  onRefreshNotifications,
}: AdminPanelProps) {
  const isMaster = currentUser.role === "Master";
  const canAccess = isAdminOrMaster(currentUser.role);

  const [usuarios, setUsuarios] = useState<AdminUsuario[]>([]);
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoExclusao[]>([]);
  const [loading, setLoading] = useState(canAccess);
  const [erroMsg, setErroMsg] = useState<string | null>(null);
  const [sucessoMsg, setSucessoMsg] = useState<string | null>(null);

  // Filtros
  const [busca, setBusca] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | StatusType>("all");
  const [roleFilter, setRoleFilter] = useState<"all" | RoleType>("all");

  // Modais
  const [editTarget, setEditTarget] = useState<AdminUsuario | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editErro, setEditErro] = useState<string | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<AdminUsuario | null>(null);
  const [deleteConfirmWord, setDeleteConfirmWord] = useState("");
  const [deleteErro, setDeleteErro] = useState<string | null>(null);

  const [requestDeleteTarget, setRequestDeleteTarget] =
    useState<AdminUsuario | null>(null);
  const [requestDeleteMotivo, setRequestDeleteMotivo] = useState("");
  const [requestDeleteErro, setRequestDeleteErro] = useState<string | null>(
    null,
  );

  const [rejectRequestTarget, setRejectRequestTarget] =
    useState<SolicitacaoExclusao | null>(null);
  const [rejectObservacao, setRejectObservacao] = useState("");

  const carregarDados = useCallback(async () => {
    if (!canAccess) return;
    setLoading(true);
    setErroMsg(null);

    const [resUsers, resDel] = await Promise.all([
      api.listAdminUsuarios(),
      api.listSolicitacoesExclusao(),
    ]);

    if (!resUsers.ok) {
      if (!resUsers.unauthorized) {
        setErroMsg(resUsers.error);
        setUsuarios([]);
      }
    } else {
      const lista = Array.isArray(resUsers.data)
        ? resUsers.data.map((u) => ({
            id: String(u.id),
            nome: String(u.nome || ""),
            email: String(u.email || ""),
            role: normalizarRole(u.role),
            status: normalizarStatus(u.status),
            criadoEm: typeof u.criadoEm === "string" ? u.criadoEm : undefined,
            possuiPedidoExclusaoPendente: Boolean(
              u.possuiPedidoExclusaoPendente,
            ),
          }))
        : [];
      setUsuarios(lista);
    }

    if (resDel.ok && Array.isArray(resDel.data)) {
      setSolicitacoes(
        resDel.data.map((s) => ({
          ...s,
          status: normalizarDeletionStatus(s.status),
        })),
      );
    }

    setLoading(false);
  }, [api, canAccess]);

  useEffect(() => {
    if (!canAccess) return;
    let cancelled = false;

    const carregarInicial = async () => {
      const [resUsers, resDel] = await Promise.all([
        api.listAdminUsuarios(),
        api.listSolicitacoesExclusao(),
      ]);
      if (cancelled) return;

      if (!resUsers.ok) {
        if (!resUsers.unauthorized) {
          setErroMsg(resUsers.error);
          setUsuarios([]);
        }
      } else {
        const lista = Array.isArray(resUsers.data)
          ? resUsers.data.map((u) => ({
              id: String(u.id),
              nome: String(u.nome || ""),
              email: String(u.email || ""),
              role: normalizarRole(u.role),
              status: normalizarStatus(u.status),
              criadoEm: typeof u.criadoEm === "string" ? u.criadoEm : undefined,
              possuiPedidoExclusaoPendente: Boolean(
                u.possuiPedidoExclusaoPendente,
              ),
            }))
          : [];
        setUsuarios(lista);
      }

      if (resDel.ok && Array.isArray(resDel.data)) {
        setSolicitacoes(
          resDel.data.map((s) => ({
            ...s,
            status: normalizarDeletionStatus(s.status),
          })),
        );
      }

      setLoading(false);
    };

    void carregarInicial();
    return () => {
      cancelled = true;
    };
  }, [api, canAccess]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setEditTarget(null);
        setDeleteTarget(null);
        setRequestDeleteTarget(null);
        setRejectRequestTarget(null);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const counts = useMemo(() => {
    const total = usuarios.length;
    const equipe = usuarios.filter((u) => u.role !== "User").length;
    const comuns = usuarios.filter((u) => u.role === "User").length;
    const aprovados = usuarios.filter((u) => u.status === "Aprovado").length;
    const pendentes = usuarios.filter((u) => u.status === "Pendente").length;
    const pedidosPendentes = solicitacoes.filter(
      (s) => s.status === "Pendente",
    ).length;
    return {
      total,
      equipe,
      comuns,
      aprovados,
      pendentes,
      pedidosPendentes,
    };
  }, [usuarios, solicitacoes]);

  const usuariosFiltrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return usuarios
      .filter((u) => {
        if (activeTab === "approved" && u.status !== "Aprovado") return false;
        if (activeTab === "pending" && u.status !== "Pendente") return false;
        if (
          activeTab === "users" &&
          statusFilter !== "all" &&
          u.status !== statusFilter
        ) {
          return false;
        }
        if (roleFilter !== "all" && u.role !== roleFilter) return false;
        if (
          termo &&
          !u.nome.toLowerCase().includes(termo) &&
          !u.email.toLowerCase().includes(termo)
        ) {
          return false;
        }
        return true;
      })
      .sort((a, b) => {
        const rank = (u: AdminUsuario) =>
          u.status === "Pendente"
            ? 0
            : u.role === "Master"
              ? 1
              : u.role === "Admin"
                ? 2
                : 3;
        return rank(a) - rank(b) || a.nome.localeCompare(b.nome, "pt-BR");
      });
  }, [usuarios, activeTab, statusFilter, roleFilter, busca]);

  if (!canAccess) {
    return (
      <div
        role="alert"
        className="p-6 rounded-xl border border-red-500/30 bg-red-500/10 text-red-300 text-xs"
      >
        Acesso restrito a administradores.
      </div>
    );
  }

  const atualizarUsuario = async (
    id: string,
    dados: AdminUsuarioPatchPayload,
    mensagemSucesso: string,
  ): Promise<boolean> => {
    setLoading(true);
    setErroMsg(null);
    setSucessoMsg(null);

    try {
      const result = await api.patchAdminUsuario(id, dados);
      if (!result.ok) {
        if (!result.unauthorized) {
          setErroMsg(result.error);
        }
        return false;
      }

      const d = result.data;
      setUsuarios((atuais) =>
        atuais.map((item) =>
          item.id === id
            ? {
                id: String(d?.id ?? id),
                nome: String(d?.nome ?? dados.nome ?? item.nome),
                email: String(d?.email ?? dados.email ?? item.email),
                role: normalizarRole(d?.role ?? dados.role ?? item.role),
                status: normalizarStatus(d?.status ?? dados.status ?? item.status),
                criadoEm:
                  typeof d?.criadoEm === "string" ? d.criadoEm : item.criadoEm,
                possuiPedidoExclusaoPendente:
                  typeof d?.possuiPedidoExclusaoPendente === "boolean"
                    ? d.possuiPedidoExclusaoPendente
                    : item.possuiPedidoExclusaoPendente,
              }
            : item,
        ),
      );

      if (currentUser.id === id) {
        onUpdateOwnDisplay({
          nome: String(d?.nome ?? dados.nome ?? currentUser.nome),
          email: String(d?.email ?? dados.email ?? currentUser.email),
        });
      }

      setSucessoMsg(mensagemSucesso);
      onRefreshNotifications();
      return true;
    } finally {
      setLoading(false);
    }
  };

  const abrirModalEdicao = (usuario: AdminUsuario) => {
    setEditTarget(usuario);
    setEditNome(usuario.nome);
    setEditEmail(usuario.email);
    setEditErro(null);
  };

  const salvarEdicao = async () => {
    if (!editTarget) return;
    const perms = getRowPermissions(currentUser, editTarget);
    const payload: AdminUsuarioPatchPayload = {};

    if (perms.canEditName) {
      const nomeLimpo = editNome.trim();
      if (!nomeLimpo) {
        setEditErro("O nome não pode ficar vazio.");
        return;
      }
      if (nomeLimpo !== editTarget.nome) {
        payload.nome = nomeLimpo;
      }
    }

    if (perms.canEditEmail) {
      const emailLimpo = editEmail.trim().toLowerCase();
      if (!isValidEmail(emailLimpo)) {
        setEditErro("Digite um e-mail válido (ex.: exemplo@mail.com).");
        return;
      }
      if (emailLimpo !== editTarget.email.toLowerCase()) {
        payload.email = emailLimpo;
      }
    }

    if (!payload.nome && !payload.email) {
      setEditTarget(null);
      return;
    }

    const ok = await atualizarUsuario(
      editTarget.id,
      payload,
      `Dados de ${payload.nome ?? editTarget.nome} atualizados.`,
    );
    if (ok) {
      setEditTarget(null);
    }
  };

  const confirmarExclusaoDireta = async () => {
    if (!deleteTarget) return;
    if (deleteConfirmWord.trim().toUpperCase() !== "EXCLUIR") {
      setDeleteErro("Digite EXCLUIR para confirmar.");
      return;
    }

    setLoading(true);
    setErroMsg(null);
    setSucessoMsg(null);
    try {
      const result = await api.deleteAdminUsuario(deleteTarget.id);
      if (!result.ok) {
        if (!result.unauthorized) {
          setDeleteErro(result.error);
        }
        return;
      }

      const nomeRemovido = deleteTarget.nome;
      setDeleteTarget(null);
      setDeleteConfirmWord("");
      setDeleteErro(null);
      setSucessoMsg(`Conta de ${nomeRemovido} excluída permanentemente.`);
      await carregarDados();
      onRefreshNotifications();
    } finally {
      setLoading(false);
    }
  };

  const enviarPedidoExclusao = async () => {
    if (!requestDeleteTarget) return;
    const motivo = requestDeleteMotivo.trim();
    if (motivo.length < 5) {
      setRequestDeleteErro("Informe um motivo com pelo menos 5 caracteres.");
      return;
    }

    setLoading(true);
    setErroMsg(null);
    setSucessoMsg(null);
    try {
      const result = await api.criarSolicitacaoExclusao(
        requestDeleteTarget.id,
        motivo,
      );
      if (!result.ok) {
        if (!result.unauthorized) {
          setRequestDeleteErro(result.error);
        }
        return;
      }

      const nomeAlvo = requestDeleteTarget.nome;
      setRequestDeleteTarget(null);
      setRequestDeleteMotivo("");
      setRequestDeleteErro(null);
      setSucessoMsg(
        `Solicitação de exclusão de ${nomeAlvo} enviada para avaliação da Master.`,
      );
      await carregarDados();
      onRefreshNotifications();
    } finally {
      setLoading(false);
    }
  };

  const aprovarPedidoExclusao = async (req: SolicitacaoExclusao) => {
    setLoading(true);
    setErroMsg(null);
    setSucessoMsg(null);
    try {
      const result = await api.aprovarSolicitacaoExclusao(req.id);
      if (!result.ok) {
        if (!result.unauthorized) {
          setErroMsg(result.error);
        }
        return;
      }
      setSucessoMsg(
        `Exclusão de ${req.usuarioAlvoNome} aprovada e executada.`,
      );
      await carregarDados();
      onRefreshNotifications();
    } finally {
      setLoading(false);
    }
  };

  const confirmarRecusaPedido = async () => {
    if (!rejectRequestTarget) return;
    setLoading(true);
    setErroMsg(null);
    setSucessoMsg(null);
    try {
      const result = await api.recusarSolicitacaoExclusao(
        rejectRequestTarget.id,
        rejectObservacao.trim() || undefined,
      );
      if (!result.ok) {
        if (!result.unauthorized) {
          setErroMsg(result.error);
        }
        return;
      }
      const nomeAlvo = rejectRequestTarget.usuarioAlvoNome;
      setRejectRequestTarget(null);
      setRejectObservacao("");
      setSucessoMsg(`Pedido de exclusão de ${nomeAlvo} recusado.`);
      await carregarDados();
      onRefreshNotifications();
    } finally {
      setLoading(false);
    }
  };

  const kpiCardCls = (active: boolean) =>
    active
      ? "text-left p-4 rounded-xl bg-zinc-900 border border-yellow-400/60 transition cursor-pointer"
      : "text-left p-4 rounded-xl bg-zinc-900/70 border border-zinc-800/80 hover:border-zinc-700 transition cursor-pointer";

  const tabCls = (active: boolean) =>
    active
      ? "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold bg-yellow-400 text-zinc-950 transition cursor-pointer"
      : "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold text-zinc-400 hover:text-zinc-100 transition cursor-pointer";

  const pillCountCls = (
    active: boolean,
    val: number,
    highlight?: "yellow" | "red",
  ) => {
    if (active) return "px-1.5 py-0.5 rounded-full text-xs bg-zinc-950/15 text-zinc-950";
    if (highlight === "yellow" && val > 0) {
      return "px-1.5 py-0.5 rounded-full text-xs bg-yellow-400/20 text-yellow-300";
    }
    if (highlight === "red" && val > 0) {
      return "px-1.5 py-0.5 rounded-full text-xs bg-red-500/20 text-red-300";
    }
    return "px-1.5 py-0.5 rounded-full text-xs bg-zinc-800 text-zinc-400";
  };

  const deletionLabel = (status: DeletionRequestStatus) => {
    if (status === "Pendente") {
      return isMaster ? "Pendente" : "Pendente · Aguardando Master";
    }
    if (status === "Aprovado") return "Aceita · Conta excluída";
    return "Recusada pela Master";
  };

  const editPerms = editTarget
    ? getRowPermissions(currentUser, editTarget)
    : null;

  return (
    <section className="space-y-6">
      {/* Cabeçalho */}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black tracking-tight text-zinc-100">
            Usuários
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Gerencie contas, aprovações de acesso e solicitações da equipe.
          </p>
        </div>
        <button
          type="button"
          onClick={onBackToCalculator}
          className="h-9 px-3.5 rounded-xl bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-xs font-semibold text-zinc-300 transition cursor-pointer"
        >
          Voltar ao cálculo
        </button>
      </div>

      {/* Feedbacks */}
      {erroMsg && (
        <div
          role="alert"
          className="p-3 rounded-xl bg-red-500/10 border border-red-500/30 text-xs text-red-300 flex items-center gap-2"
        >
          <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
          <span>{erroMsg}</span>
        </div>
      )}

      {sucessoMsg && (
        <div
          role="status"
          className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs text-emerald-300 flex items-center gap-2"
        >
          <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-400" />
          <span>{sucessoMsg}</span>
        </div>
      )}

      {/* KPIs sincronizados com as 4 abas */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <button
          type="button"
          onClick={() => onChangeTab("users")}
          className={kpiCardCls(activeTab === "users")}
        >
          <div className="text-xs font-medium text-zinc-400">
            Todos os usuários
          </div>
          <div className="text-2xl font-black text-zinc-100 mt-1">
            {counts.total}
          </div>
          <div className="text-xs text-zinc-500 mt-0.5">
            {counts.equipe} equipe · {counts.comuns} usuários
          </div>
        </button>

        <button
          type="button"
          onClick={() => onChangeTab("approved")}
          className={kpiCardCls(activeTab === "approved")}
        >
          <div className="text-xs font-medium text-zinc-400">Aprovados</div>
          <div className="text-2xl font-black text-emerald-400 mt-1">
            {counts.aprovados}
          </div>
          <div className="text-xs text-zinc-500 mt-0.5">
            Contas com acesso ativo
          </div>
        </button>

        <button
          type="button"
          onClick={() => onChangeTab("pending")}
          className={kpiCardCls(activeTab === "pending")}
        >
          <div className="text-xs font-medium text-zinc-400">
            Cadastros pendentes
          </div>
          <div
            className={`text-2xl font-black mt-1 ${
              counts.pendentes > 0 ? "text-yellow-400" : "text-zinc-100"
            }`}
          >
            {counts.pendentes}
          </div>
          <div className="text-xs text-zinc-500 mt-0.5">
            {counts.pendentes > 0 ? "Aguardando avaliação" : "Nenhum pendente"}
          </div>
        </button>

        <button
          type="button"
          onClick={() => onChangeTab("deletions")}
          className={kpiCardCls(activeTab === "deletions")}
        >
          <div className="text-xs font-medium text-zinc-400">
            {isMaster ? "Pedidos de exclusão" : "Minhas solicitações"}
          </div>
          <div
            className={`text-2xl font-black mt-1 ${
              counts.pedidosPendentes > 0 ? "text-amber-300" : "text-zinc-100"
            }`}
          >
            {counts.pedidosPendentes}
          </div>
          <div className="text-xs text-zinc-500 mt-0.5">
            {isMaster
              ? counts.pedidosPendentes > 0
                ? "Aguardando sua decisão"
                : "Nenhum pedido pendente"
              : `${solicitacoes.length} no histórico`}
          </div>
        </button>
      </div>

      {/* Abas + Busca e Filtros */}
      <div className="flex flex-wrap items-center justify-between gap-3 pb-3.5 border-b border-zinc-800/80">
        <div className="flex flex-wrap gap-1 bg-zinc-900/90 p-1 rounded-xl border border-zinc-800/90">
          <button
            type="button"
            onClick={() => onChangeTab("users")}
            className={tabCls(activeTab === "users")}
          >
            <span>Todos</span>
            <span className={pillCountCls(activeTab === "users", counts.total)}>
              {counts.total}
            </span>
          </button>

          <button
            type="button"
            onClick={() => onChangeTab("approved")}
            className={tabCls(activeTab === "approved")}
          >
            <span>Aprovados</span>
            <span
              className={pillCountCls(
                activeTab === "approved",
                counts.aprovados,
              )}
            >
              {counts.aprovados}
            </span>
          </button>

          <button
            type="button"
            onClick={() => onChangeTab("pending")}
            className={tabCls(activeTab === "pending")}
          >
            <span>Pendentes</span>
            <span
              className={pillCountCls(
                activeTab === "pending",
                counts.pendentes,
                "yellow",
              )}
            >
              {counts.pendentes}
            </span>
          </button>

          <button
            type="button"
            onClick={() => onChangeTab("deletions")}
            className={tabCls(activeTab === "deletions")}
          >
            <span>
              {isMaster ? "Pedidos de exclusão" : "Minhas solicitações"}
            </span>
            <span
              className={pillCountCls(
                activeTab === "deletions",
                counts.pedidosPendentes,
                "red",
              )}
            >
              {counts.pedidosPendentes}
            </span>
          </button>
        </div>

        {activeTab !== "deletions" && (
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative min-w-[215px]">
              <Search className="w-3.5 h-3.5 text-zinc-500 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input
                type="text"
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                placeholder="Buscar nome ou e-mail..."
                className="w-full h-9 pl-8 pr-3 rounded-xl bg-zinc-950 border border-zinc-800 text-xs text-zinc-100 placeholder-zinc-500 focus:outline-none focus:border-yellow-400/70 transition"
              />
            </div>

            {activeTab === "users" && (
              <select
                value={statusFilter}
                onChange={(e) =>
                  setStatusFilter(e.target.value as "all" | StatusType)
                }
                className="h-9 px-3 rounded-xl bg-zinc-950 border border-zinc-800 text-xs text-zinc-300 focus:outline-none focus:border-yellow-400/70 cursor-pointer"
              >
                <option value="all">Todos os status</option>
                <option value="Aprovado">Aprovados</option>
                <option value="Pendente">Pendentes</option>
                <option value="Rejeitado">Rejeitados</option>
              </select>
            )}

            <select
              value={roleFilter}
              onChange={(e) =>
                setRoleFilter(e.target.value as "all" | RoleType)
              }
              className="h-9 px-3 rounded-xl bg-zinc-950 border border-zinc-800 text-xs text-zinc-300 focus:outline-none focus:border-yellow-400/70 cursor-pointer"
            >
              <option value="all">Todos os perfis</option>
              <option value="Master">Master</option>
              <option value="Admin">Administrador</option>
              <option value="User">Usuário</option>
            </select>
          </div>
        )}
      </div>

      {/* Conteúdo */}
      {loading && usuarios.length === 0 && solicitacoes.length === 0 ? (
        <p className="text-xs text-zinc-400">Carregando dados...</p>
      ) : activeTab === "deletions" ? (
        solicitacoes.length === 0 ? (
          <div className="p-10 rounded-xl bg-zinc-900/60 border border-dashed border-zinc-800 text-center text-xs text-zinc-500">
            {isMaster
              ? "Nenhum pedido de exclusão registrado."
              : "Você ainda não enviou nenhuma solicitação de exclusão."}
          </div>
        ) : (
          <div className="space-y-3">
            {solicitacoes.map((req) => (
              <div
                key={req.id}
                className={`p-4 rounded-xl bg-zinc-900/90 border flex flex-wrap items-center justify-between gap-4 ${
                  req.status === "Pendente"
                    ? "border-zinc-700"
                    : "border-zinc-800/70"
                }`}
              >
                <div className="flex-1 min-w-[240px]">
                  <div className="flex flex-wrap items-center gap-2">
                    <strong className="text-sm text-zinc-100">
                      {req.usuarioAlvoNome}
                    </strong>
                    <span className="text-xs text-zinc-400">
                      {req.usuarioAlvoEmail}
                    </span>
                    <span
                      className={`px-2.5 py-0.5 rounded-full text-xs font-semibold border ${deletionBadgeClass[req.status]}`}
                    >
                      {deletionLabel(req.status)}
                    </span>
                  </div>
                  <div className="text-xs text-zinc-500 mt-1">
                    Solicitado por{" "}
                    <span className="text-zinc-300 font-medium">
                      {req.solicitadoPorNome}
                    </span>{" "}
                    · {formatarDataSolicitacao(req.criadoEm)}
                    {req.decididoEm && (
                      <> · Decidido em {formatarDataSolicitacao(req.decididoEm)}</>
                    )}
                  </div>
                  <div className="mt-2.5 p-2.5 rounded-lg bg-zinc-950/90 border border-zinc-800/80 text-xs text-zinc-300">
                    <span className="text-zinc-500 block mb-0.5">
                      Motivo da solicitação:
                    </span>
                    {req.motivo}
                  </div>
                  {req.observacaoDecisao && (
                    <div className="mt-2 p-2.5 rounded-lg bg-zinc-950/70 border border-zinc-800/80 text-xs text-zinc-400">
                      <span className="text-zinc-500 font-medium">
                        Observação da Master:
                      </span>{" "}
                      {req.observacaoDecisao}
                    </div>
                  )}
                </div>

                {isMaster && req.status === "Pendente" && (
                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      disabled={loading}
                      onClick={() => void aprovarPedidoExclusao(req)}
                      className="h-9 inline-flex items-center gap-1.5 px-3.5 rounded-xl bg-red-500/15 hover:bg-red-500/25 border border-red-500/35 text-red-300 text-xs font-semibold transition disabled:opacity-40 cursor-pointer"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                      Aprovar exclusão
                    </button>
                    <button
                      type="button"
                      disabled={loading}
                      onClick={() => {
                        setRejectRequestTarget(req);
                        setRejectObservacao("");
                      }}
                      className="h-9 px-3.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-semibold transition disabled:opacity-40 cursor-pointer"
                    >
                      Recusar pedido
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        )
      ) : usuariosFiltrados.length === 0 ? (
        <div className="p-10 rounded-xl bg-zinc-900/60 border border-dashed border-zinc-800 text-center text-xs text-zinc-500">
          Nenhum usuário encontrado nesta visualização.
        </div>
      ) : (
        <div className="space-y-2.5">
          {usuariosFiltrados.map((usuario) => {
            const perms = getRowPermissions(currentUser, usuario);
            const isSelf = currentUser.id === usuario.id;
            const temPedido = Boolean(usuario.possuiPedidoExclusaoPendente);

            const avatarCls =
              usuario.role === "Master"
                ? "bg-yellow-400/15 border-yellow-400/35 text-yellow-300"
                : usuario.role === "Admin"
                  ? "bg-sky-400/10 border-sky-400/30 text-sky-300"
                  : "bg-zinc-800/90 border-zinc-700/80 text-zinc-300";

            return (
              <div
                key={usuario.id}
                className={`p-4 rounded-xl bg-zinc-900/90 border transition grid gap-3 lg:grid-cols-[minmax(240px,1.4fr)_150px_100px_auto] items-center ${
                  usuario.role === "Master"
                    ? "border-yellow-400/25"
                    : temPedido
                      ? "border-amber-400/30"
                      : "border-zinc-800/80 hover:border-zinc-700/80"
                }`}
              >
                <div className="flex items-start gap-3 min-w-0">
                  <span
                    className={`w-9 h-9 rounded-lg border flex items-center justify-center font-bold text-sm shrink-0 ${avatarCls}`}
                  >
                    {(usuario.nome || "U").charAt(0).toUpperCase()}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-sm font-bold text-zinc-100 truncate">
                        {usuario.nome}
                      </span>
                      {isSelf && (
                        <span className="px-1.5 py-0.5 rounded-md text-[11px] bg-zinc-800 text-zinc-400">
                          Você
                        </span>
                      )}
                      {usuario.role === "Master" ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-yellow-400/12 text-yellow-300 border border-yellow-400/30">
                          <Crown className="w-3 h-3" />
                          Master
                        </span>
                      ) : usuario.role === "Admin" ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-sky-400/10 text-sky-300 border border-sky-400/25">
                          <Shield className="w-3 h-3" />
                          Admin
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium bg-zinc-800/80 text-zinc-400 border border-zinc-700/70">
                          Usuário
                        </span>
                      )}
                    </div>

                    <div className="text-xs text-zinc-400 mt-0.5 flex items-center gap-1.5 truncate">
                      <Mail className="w-3.5 h-3.5 shrink-0 text-zinc-500" />
                      <span className="truncate">{usuario.email}</span>
                    </div>

                    {temPedido && (
                      <div className="text-[11px] text-amber-300/90 mt-1">
                        Exclusão solicitada (aguardando Master)
                      </div>
                    )}
                  </div>
                </div>

                <div className="text-xs">
                  <span className="text-[11px] text-zinc-500 flex items-center gap-1">
                    <Clock className="w-3 h-3" />
                    Solicitado em
                  </span>
                  <span className="font-medium text-zinc-300 block mt-0.5">
                    {formatarDataSolicitacao(usuario.criadoEm)}
                  </span>
                </div>

                <div>
                  <span
                    className={`inline-flex text-[11px] font-semibold px-2.5 py-0.5 rounded-full border ${statusBadgeClass[usuario.status]}`}
                  >
                    {usuario.status}
                  </span>
                </div>

                <div className="flex flex-wrap items-center lg:justify-end gap-2">
                  {perms.lockReason && !perms.canEditName ? (
                    <span className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-zinc-950/60 border border-zinc-800/90 text-xs text-zinc-500">
                      <Lock className="w-3.5 h-3.5" />
                      {perms.lockReason}
                    </span>
                  ) : (
                    <>
                      {perms.canChangeRole && (
                        <select
                          value={usuario.role}
                          disabled={loading}
                          onChange={(e) =>
                            void atualizarUsuario(
                              usuario.id,
                              { role: normalizarRole(e.target.value) },
                              `Perfil de ${usuario.nome} atualizado.`,
                            )
                          }
                          className="h-8 px-2.5 rounded-lg bg-zinc-950 border border-zinc-800 text-xs text-zinc-200 focus:outline-none focus:border-yellow-400/70 cursor-pointer"
                        >
                          <option value="User">Usuário</option>
                          <option value="Admin">Administrador</option>
                        </select>
                      )}

                      {(perms.canEditName || perms.canEditEmail) && (
                        <button
                          type="button"
                          disabled={loading}
                          onClick={() => abrirModalEdicao(usuario)}
                          className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-zinc-800/80 hover:bg-zinc-800 border border-zinc-700/70 text-xs font-medium text-zinc-200 transition disabled:opacity-40 cursor-pointer"
                        >
                          <Pencil className="w-3.5 h-3.5" />
                          Editar
                        </button>
                      )}

                      {perms.canChangeStatus && usuario.status !== "Aprovado" && (
                        <button
                          type="button"
                          disabled={loading}
                          onClick={() =>
                            void atualizarUsuario(
                              usuario.id,
                              { status: "Aprovado" },
                              `Conta de ${usuario.nome} aprovada.`,
                            )
                          }
                          className="h-8 inline-flex items-center px-3 rounded-lg bg-emerald-500/15 hover:bg-emerald-500/25 border border-emerald-500/30 text-emerald-300 text-xs font-semibold transition disabled:opacity-40 cursor-pointer"
                        >
                          Aprovar
                        </button>
                      )}

                      {perms.canChangeStatus &&
                        usuario.status !== "Rejeitado" && (
                          <button
                            type="button"
                            disabled={loading}
                            onClick={() =>
                              void atualizarUsuario(
                                usuario.id,
                                { status: "Rejeitado" },
                                `Conta de ${usuario.nome} rejeitada.`,
                              )
                            }
                            className="h-8 inline-flex items-center px-3 rounded-lg bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-zinc-400 hover:text-zinc-200 text-xs font-medium transition disabled:opacity-40 cursor-pointer"
                          >
                            Rejeitar
                          </button>
                        )}

                      {perms.canDirectDelete && (
                        <button
                          type="button"
                          disabled={loading}
                          onClick={() => {
                            setDeleteTarget(usuario);
                            setDeleteConfirmWord("");
                            setDeleteErro(null);
                          }}
                          className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-zinc-950/70 hover:bg-red-500/12 border border-zinc-800 hover:border-red-500/35 text-zinc-400 hover:text-red-300 text-xs font-medium transition disabled:opacity-40 cursor-pointer"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                          Excluir
                        </button>
                      )}

                      {perms.canRequestDelete &&
                        (temPedido ? (
                          <span className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-amber-400/10 border border-amber-400/25 text-amber-300/90 text-xs font-medium">
                            <Clock className="w-3.5 h-3.5" />
                            Exclusão pendente
                          </span>
                        ) : (
                          <button
                            type="button"
                            disabled={loading}
                            onClick={() => {
                              setRequestDeleteTarget(usuario);
                              setRequestDeleteMotivo("");
                              setRequestDeleteErro(null);
                            }}
                            className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-zinc-950/70 hover:bg-red-500/12 border border-zinc-800 hover:border-red-500/35 text-zinc-400 hover:text-red-300 text-xs font-medium transition disabled:opacity-40 cursor-pointer"
                          >
                            <Send className="w-3.5 h-3.5" />
                            Pedir exclusão
                          </button>
                        ))}

                      {usuario.role === "Master" && isSelf && (
                        <span className="h-8 inline-flex items-center gap-1.5 px-3 rounded-lg bg-zinc-950/60 border border-zinc-800/90 text-xs text-zinc-500">
                          <Lock className="w-3.5 h-3.5" />
                          Conta protegida
                        </span>
                      )}
                    </>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Modal: Editar Usuário */}
      {editTarget && editPerms && (
        <div
          onClick={() => setEditTarget(null)}
          className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl"
          >
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-base font-bold text-zinc-100">
                Editar usuário
              </h3>
              <button
                type="button"
                onClick={() => setEditTarget(null)}
                aria-label="Fechar"
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-3.5">
              <div>
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  Nome
                </label>
                <input
                  type="text"
                  maxLength={120}
                  value={editNome}
                  disabled={!editPerms.canEditName}
                  onChange={(e) => {
                    setEditNome(e.target.value);
                    setEditErro(null);
                  }}
                  className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400 disabled:opacity-45"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                  E-mail
                </label>
                <input
                  type="email"
                  maxLength={180}
                  value={editEmail}
                  disabled={!editPerms.canEditEmail}
                  onChange={(e) => {
                    setEditEmail(e.target.value);
                    setEditErro(null);
                  }}
                  className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-yellow-400 disabled:opacity-45"
                />
                {!editPerms.canEditEmail && (
                  <p className="mt-1.5 text-xs text-zinc-500 flex items-center gap-1.5">
                    <Lock className="w-3.5 h-3.5 shrink-0" />
                    E-mail gerenciado pela configuração do servidor.
                  </p>
                )}
              </div>

              {editErro && (
                <p className="text-xs text-red-400 flex items-center gap-1.5">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{editErro}</span>
                </p>
              )}
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button
                type="button"
                onClick={() => setEditTarget(null)}
                className="px-4 py-2 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-xs font-semibold cursor-pointer"
              >
                Cancelar
              </button>
              <button
                type="button"
                disabled={loading}
                onClick={() => void salvarEdicao()}
                className="px-4 py-2 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 text-xs font-bold disabled:opacity-40 cursor-pointer"
              >
                Salvar
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Excluir Diretamente (Master) */}
      {deleteTarget && (
        <div
          onClick={() => setDeleteTarget(null)}
          className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl"
          >
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-base font-bold text-zinc-100">
                Excluir conta
              </h3>
              <button
                type="button"
                onClick={() => setDeleteTarget(null)}
                aria-label="Fechar"
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-zinc-400 leading-relaxed mb-4">
              Tem certeza que deseja excluir permanentemente{" "}
              <strong className="text-zinc-100">{deleteTarget.nome}</strong> (
              {deleteTarget.email})? Esta ação não pode ser desfeita e revoga o
              acesso imediatamente.
            </p>

            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                Digite <strong className="text-zinc-200">EXCLUIR</strong> para
                confirmar
              </label>
              <input
                type="text"
                autoComplete="off"
                placeholder="EXCLUIR"
                value={deleteConfirmWord}
                onChange={(e) => {
                  setDeleteConfirmWord(e.target.value);
                  setDeleteErro(null);
                }}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-sm focus:outline-none focus:border-red-400/70"
              />
              {deleteErro && (
                <p className="mt-1.5 text-xs text-red-400 flex items-center gap-1.5">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{deleteErro}</span>
                </p>
              )}
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button
                type="button"
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-xs font-semibold cursor-pointer"
              >
                Cancelar
              </button>
              <button
                type="button"
                disabled={loading}
                onClick={() => void confirmarExclusaoDireta()}
                className="px-4 py-2 rounded-xl bg-red-500/85 hover:bg-red-500 text-white text-xs font-bold disabled:opacity-40 cursor-pointer"
              >
                Confirmar exclusão
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Solicitar Exclusão (Admin -> Master) */}
      {requestDeleteTarget && (
        <div
          onClick={() => setRequestDeleteTarget(null)}
          className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl"
          >
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-base font-bold text-zinc-100">
                Solicitar exclusão
              </h3>
              <button
                type="button"
                onClick={() => setRequestDeleteTarget(null)}
                aria-label="Fechar"
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-zinc-400 leading-relaxed mb-3.5">
              A solicitação para remover{" "}
              <strong className="text-zinc-100">
                {requestDeleteTarget.nome}
              </strong>{" "}
              será enviada para avaliação da Master. Você será notificado quando
              houver uma decisão.
            </p>

            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                Motivo <span className="text-red-400">*</span>
              </label>
              <textarea
                rows={3}
                maxLength={500}
                placeholder="Ex.: Usuário saiu da equipe ou cadastro duplicado..."
                value={requestDeleteMotivo}
                onChange={(e) => {
                  setRequestDeleteMotivo(e.target.value);
                  setRequestDeleteErro(null);
                }}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-xs focus:outline-none focus:border-yellow-400/70"
              />
              {requestDeleteErro && (
                <p className="mt-1.5 text-xs text-red-400 flex items-center gap-1.5">
                  <AlertCircle className="w-3.5 h-3.5 shrink-0" />
                  <span>{requestDeleteErro}</span>
                </p>
              )}
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button
                type="button"
                onClick={() => setRequestDeleteTarget(null)}
                className="px-4 py-2 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-xs font-semibold cursor-pointer"
              >
                Cancelar
              </button>
              <button
                type="button"
                disabled={loading}
                onClick={() => void enviarPedidoExclusao()}
                className="px-4 py-2 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 text-xs font-bold disabled:opacity-40 cursor-pointer"
              >
                Enviar solicitação
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Recusar Solicitação de Exclusão (Master) */}
      {rejectRequestTarget && (
        <div
          onClick={() => setRejectRequestTarget(null)}
          className="fixed inset-0 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl"
          >
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-base font-bold text-zinc-100">
                Recusar pedido de exclusão
              </h3>
              <button
                type="button"
                onClick={() => setRejectRequestTarget(null)}
                aria-label="Fechar"
                className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-zinc-400 leading-relaxed mb-3.5">
              A conta de{" "}
              <strong className="text-zinc-100">
                {rejectRequestTarget.usuarioAlvoNome}
              </strong>{" "}
              será mantida e{" "}
              <strong className="text-zinc-200">
                {rejectRequestTarget.solicitadoPorNome}
              </strong>{" "}
              será notificado.
            </p>

            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5">
                Observação para o Admin{" "}
                <span className="text-zinc-500">(opcional)</span>
              </label>
              <input
                type="text"
                maxLength={500}
                placeholder="Ex.: Conta ainda utilizada para conferência mensal."
                value={rejectObservacao}
                onChange={(e) => setRejectObservacao(e.target.value)}
                className="w-full px-3.5 py-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-xs focus:outline-none focus:border-yellow-400/70"
              />
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button
                type="button"
                onClick={() => setRejectRequestTarget(null)}
                className="px-4 py-2 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-xs font-semibold cursor-pointer"
              >
                Cancelar
              </button>
              <button
                type="button"
                disabled={loading}
                onClick={() => void confirmarRecusaPedido()}
                className="px-4 py-2 rounded-xl bg-yellow-400 hover:bg-yellow-300 text-zinc-950 text-xs font-bold disabled:opacity-40 cursor-pointer"
              >
                Confirmar recusa
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
