import { useState, useEffect, useRef } from "react";
import {
  Truck,
  ChevronDown,
  User,
  ShieldCheck,
  LogOut,
  Bell,
  UserPlus,
  Trash2,
  CheckCircle2,
  XCircle,
  ArrowRight,
  Crown,
} from "lucide-react";
import { MARKETPLACES } from "../config/marketplaces";
import type {
  ActiveView,
  NotificacaoItem,
  UserProfile,
} from "../types";
import {
  formatarDataSolicitacao,
  isAdminOrMaster,
  labelRole,
} from "../utils/formatters";

interface NavbarProps {
  user: UserProfile;
  activeView: ActiveView;
  notificacoes: NotificacaoItem[];
  naoLidas: number;
  onSelectView: (view: ActiveView) => void;
  onOpenNotification: (notif: NotificacaoItem) => void;
  onMarkAllRead: () => void;
  onOpenAccount: () => void;
  onLogout: () => void;
}

export function Navbar({
  user,
  activeView,
  notificacoes,
  naoLidas,
  onSelectView,
  onOpenNotification,
  onMarkAllRead,
  onOpenAccount,
  onLogout,
}: NavbarProps) {
  const [menuAberto, setMenuAberto] = useState(false);
  const [notifAberto, setNotifAberto] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const notifRef = useRef<HTMLDivElement>(null);

  const canAccessAdmin = isAdminOrMaster(user.role);
  const isMaster = user.role === "Master";

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuAberto(false);
      }
      if (notifRef.current && !notifRef.current.contains(e.target as Node)) {
        setNotifAberto(false);
      }
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setMenuAberto(false);
        setNotifAberto(false);
      }
    };
    document.addEventListener("mousedown", handler);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", handler);
      document.removeEventListener("keydown", onKey);
    };
  }, []);

  const renderNotifIcon = (tipo: NotificacaoItem["tipo"]) => {
    if (tipo === "SolicitacaoExclusao") {
      return (
        <span className="w-8 h-8 rounded-lg bg-red-500/10 border border-red-500/25 text-red-300 flex items-center justify-center shrink-0 mt-0.5">
          <Trash2 className="w-4 h-4" />
        </span>
      );
    }
    if (tipo === "ExclusaoAprovada") {
      return (
        <span className="w-8 h-8 rounded-lg bg-emerald-500/10 border border-emerald-500/25 text-emerald-300 flex items-center justify-center shrink-0 mt-0.5">
          <CheckCircle2 className="w-4 h-4" />
        </span>
      );
    }
    if (tipo === "ExclusaoRecusada") {
      return (
        <span className="w-8 h-8 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300 flex items-center justify-center shrink-0 mt-0.5">
          <XCircle className="w-4 h-4" />
        </span>
      );
    }
    return (
      <span className="w-8 h-8 rounded-lg bg-yellow-400/10 border border-yellow-400/25 text-yellow-300 flex items-center justify-center shrink-0 mt-0.5">
        <UserPlus className="w-4 h-4" />
      </span>
    );
  };

  return (
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
            onClick={() => onSelectView("frete")}
            className={`px-4 py-1.5 rounded-full font-bold transition cursor-pointer ${
              activeView === "frete"
                ? "bg-yellow-400 text-zinc-950"
                : "text-zinc-300 hover:text-white"
            }`}
          >
            {MARKETPLACES.magalu.label}
          </button>
          <span
            className="px-3 py-1.5 text-zinc-500 cursor-not-allowed"
            title="Em breve"
          >
            {MARKETPLACES.amazon.label}{" "}
            <span className="text-[10px] text-zinc-600">· em breve</span>
          </span>
        </div>

        <div className="flex items-center gap-2.5">
          {/* Sino de Notificações (Master & Admin — puramente informativo) */}
          {canAccessAdmin && (
            <div className="relative" ref={notifRef}>
              <button
                type="button"
                onClick={() => {
                  setNotifAberto((prev) => !prev);
                  setMenuAberto(false);
                }}
                aria-label="Notificações"
                title="Notificações"
                className="relative w-10 h-10 rounded-xl bg-zinc-900 hover:bg-zinc-800/90 border border-zinc-800 text-zinc-300 hover:text-yellow-400 flex items-center justify-center transition cursor-pointer"
              >
                <Bell className="w-4 h-4" />
                {naoLidas > 0 && (
                  <span className="absolute -top-1 -right-1 min-w-[18px] h-[18px] px-1 rounded-full bg-yellow-400 text-zinc-950 text-[10px] font-extrabold flex items-center justify-center border-2 border-zinc-950">
                    {naoLidas > 99 ? "99+" : naoLidas}
                  </span>
                )}
              </button>

              {notifAberto && (
                <div className="absolute right-0 mt-2.5 w-[350px] max-w-[calc(100vw-24px)] rounded-2xl bg-zinc-900 border border-zinc-800 shadow-2xl overflow-hidden z-50">
                  <div className="px-4 py-3 border-b border-zinc-800/90 flex items-center justify-between gap-2">
                    <h3 className="text-xs font-bold text-zinc-100">
                      Notificações ({notificacoes.length})
                    </h3>
                    {naoLidas > 0 && (
                      <button
                        type="button"
                        onClick={onMarkAllRead}
                        className="text-xs text-zinc-400 hover:text-yellow-400 transition cursor-pointer"
                      >
                        Marcar lidas
                      </button>
                    )}
                  </div>

                  <div className="max-h-[360px] overflow-y-auto divide-y divide-zinc-800/70">
                    {notificacoes.length === 0 ? (
                      <div className="p-7 text-center text-xs text-zinc-500">
                        Nenhuma notificação no momento.
                      </div>
                    ) : (
                      notificacoes.map((notif) => (
                        <button
                          key={notif.id}
                          type="button"
                          onClick={() => {
                            setNotifAberto(false);
                            onOpenNotification(notif);
                          }}
                          className={`w-full text-left p-3.5 flex items-start gap-3 hover:bg-zinc-800/50 transition cursor-pointer ${
                            !notif.lida ? "bg-zinc-800/25" : ""
                          }`}
                        >
                          {renderNotifIcon(notif.tipo)}
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center justify-between gap-2">
                              <span className="text-xs font-bold text-zinc-100 truncate">
                                {notif.titulo}
                              </span>
                              {!notif.lida && (
                                <span className="w-2 h-2 rounded-full bg-yellow-400 shrink-0" />
                              )}
                            </div>
                            <p className="text-xs text-zinc-400 mt-0.5 leading-relaxed">
                              {notif.mensagem}
                            </p>
                            <div className="flex items-center justify-between mt-1.5 text-[11px] text-zinc-500">
                              <span>
                                {formatarDataSolicitacao(notif.criadoEm)}
                              </span>
                              <span className="inline-flex items-center gap-1 text-zinc-400">
                                Ver no painel
                                <ArrowRight className="w-3 h-3" />
                              </span>
                            </div>
                          </div>
                        </button>
                      ))
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Menu do Usuário */}
          <div className="relative" ref={menuRef}>
            <button
              type="button"
              onClick={() => {
                setMenuAberto((prev) => !prev);
                setNotifAberto(false);
              }}
              className="flex items-center gap-2.5 pl-2 pr-3 py-1.5 rounded-xl bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 transition cursor-pointer"
            >
              <span
                className={`w-8 h-8 rounded-lg border flex items-center justify-center font-bold text-sm ${
                  isMaster
                    ? "bg-yellow-400/15 border-yellow-400/40 text-yellow-300"
                    : user.role === "Admin"
                      ? "bg-sky-400/15 border-sky-400/30 text-sky-300"
                      : "bg-yellow-400/15 border-yellow-400/40 text-yellow-300"
                }`}
              >
                {(user.nome || "U").charAt(0).toUpperCase()}
              </span>
              <span className="text-left hidden sm:block leading-tight">
                <span className="flex items-center gap-1.5 text-xs font-semibold text-zinc-100">
                  <span>{user.nome || "Usuário"}</span>
                  {isMaster && <Crown className="w-3 h-3 text-yellow-400" />}
                </span>
                <span className="block text-[11px] text-zinc-400">
                  {labelRole(user.role)}
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
                    {user.nome}
                  </p>
                  <p className="text-[11px] text-zinc-400 truncate">
                    {user.email}
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => {
                    setMenuAberto(false);
                    onOpenAccount();
                  }}
                  className="w-full px-4 py-2 text-left text-zinc-200 hover:bg-zinc-800 flex items-center gap-2.5 cursor-pointer"
                >
                  <User className="w-4 h-4 text-yellow-400" />
                  Minha conta
                </button>

                {canAccessAdmin && (
                  <button
                    type="button"
                    onClick={() => {
                      setMenuAberto(false);
                      onSelectView("admin");
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
                    onClick={() => {
                      setMenuAberto(false);
                      onLogout();
                    }}
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
      </div>
    </header>
  );
}
