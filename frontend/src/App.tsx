import { useState, useCallback, useMemo, useEffect } from "react";
import { AccountModal } from "./components/AccountModal";
import { AdminPanel } from "./components/AdminPanel";
import { AuthScreen } from "./components/AuthScreen";
import { MagaluCalculator } from "./components/MagaluCalculator";
import { Navbar } from "./components/Navbar";
import { useAuth } from "./hooks/useAuth";
import { createApiClient } from "./services/api";
import type { ActiveView, AdminTab, NotificacaoItem } from "./types";
import { isAdminOrMaster } from "./utils/formatters";

const readResetTokenFromUrl = (): string | null => {
  try {
    const params = new URLSearchParams(window.location.search);
    const raw = params.get("reset")?.trim();
    return raw && raw.length > 0 ? raw : null;
  } catch {
    return null;
  }
};

const clearResetTokenFromUrl = () => {
  try {
    const url = new URL(window.location.href);
    if (url.searchParams.has("reset")) {
      url.searchParams.delete("reset");
      window.history.replaceState({}, "", url.pathname + url.search + url.hash);
    }
  } catch {
    // Ignora caso history.replaceState não esteja disponível
  }
};

export default function App() {
  const { token, user, login, logout, updateUserDisplay } = useAuth();
  const [activeView, setActiveView] = useState<ActiveView>("frete");
  const [adminTab, setAdminTab] = useState<AdminTab>("users");
  const [accountModalOpen, setAccountModalOpen] = useState(false);
  const [resetToken, setResetToken] = useState<string | null>(
    readResetTokenFromUrl,
  );
  const [notificacoes, setNotificacoes] = useState<NotificacaoItem[]>([]);
  const [naoLidas, setNaoLidas] = useState(0);

  const handleLogout = useCallback(() => {
    logout();
    setActiveView("frete");
    setAdminTab("users");
    setAccountModalOpen(false);
    setNotificacoes([]);
    setNaoLidas(0);
  }, [logout]);

  const api = useMemo(
    () => createApiClient(token, handleLogout),
    [token, handleLogout],
  );

  const canAccessAdmin = Boolean(token && user && isAdminOrMaster(user.role));

  const refreshNotifications = useCallback(async () => {
    if (!canAccessAdmin) return;
    const result = await api.listNotificacoes();
    if (result.ok) {
      setNotificacoes(Array.isArray(result.data.itens) ? result.data.itens : []);
      setNaoLidas(typeof result.data.naoLidas === "number" ? result.data.naoLidas : 0);
    }
  }, [api, canAccessAdmin]);

  useEffect(() => {
    if (!canAccessAdmin) return;
    let cancelled = false;

    const carregar = async () => {
      const result = await api.listNotificacoes();
      if (cancelled || !result.ok) return;
      setNotificacoes(
        Array.isArray(result.data.itens) ? result.data.itens : [],
      );
      setNaoLidas(
        typeof result.data.naoLidas === "number" ? result.data.naoLidas : 0,
      );
    };

    void carregar();

    const interval = window.setInterval(() => {
      void carregar();
    }, 60_000);
    const onFocus = () => {
      void carregar();
    };
    window.addEventListener("focus", onFocus);
    return () => {
      cancelled = true;
      window.clearInterval(interval);
      window.removeEventListener("focus", onFocus);
    };
  }, [api, canAccessAdmin]);

  const handleOpenNotification = useCallback(
    (notif: NotificacaoItem) => {
      if (!notif.lida) {
        setNotificacoes((prev) =>
          prev.map((item) =>
            item.id === notif.id ? { ...item, lida: true } : item,
          ),
        );
        setNaoLidas((prev) => Math.max(0, prev - 1));
        void api.marcarNotificacaoLida(notif.id);
      }

      const targetTab: AdminTab =
        notif.tipo === "CadastroPendente" ? "pending" : "deletions";
      setAdminTab(targetTab);
      setActiveView("admin");
    },
    [api],
  );

  const handleMarkAllRead = useCallback(async () => {
    setNotificacoes((prev) => prev.map((item) => ({ ...item, lida: true })));
    setNaoLidas(0);
    await api.marcarTodasNotificacoesLidas();
  }, [api]);

  const handleClearResetToken = useCallback(() => {
    clearResetTokenFromUrl();
    setResetToken(null);
  }, []);

  if (!token || !user || resetToken) {
    return (
      <AuthScreen
        key={resetToken ?? "auth"}
        api={api}
        initialResetToken={resetToken}
        onClearResetToken={handleClearResetToken}
        onAuthenticated={(jwt, displayFallback) => {
          handleClearResetToken();
          return login(jwt, displayFallback);
        }}
      />
    );
  }

  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col">
      <Navbar
        user={user}
        activeView={activeView}
        notificacoes={notificacoes}
        naoLidas={naoLidas}
        onSelectView={setActiveView}
        onOpenNotification={handleOpenNotification}
        onMarkAllRead={() => void handleMarkAllRead()}
        onOpenAccount={() => setAccountModalOpen(true)}
        onLogout={handleLogout}
      />

      <main className="max-w-6xl w-full mx-auto px-4 sm:px-6 py-6 flex-1">
        {activeView === "admin" ? (
          <AdminPanel
            api={api}
            currentUser={user}
            activeTab={adminTab}
            onChangeTab={setAdminTab}
            onBackToCalculator={() => setActiveView("frete")}
            onUpdateOwnDisplay={updateUserDisplay}
            onRefreshNotifications={() => void refreshNotifications()}
          />
        ) : (
          <MagaluCalculator api={api} />
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

      {accountModalOpen && (
        <AccountModal user={user} onClose={() => setAccountModalOpen(false)} />
      )}
    </div>
  );
}
