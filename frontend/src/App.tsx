import { useState, useCallback, useMemo } from "react";
import { AccountModal } from "./components/AccountModal";
import { AdminPanel } from "./components/AdminPanel";
import { AuthScreen } from "./components/AuthScreen";
import { MagaluCalculator } from "./components/MagaluCalculator";
import { Navbar } from "./components/Navbar";
import { useAuth } from "./hooks/useAuth";
import { createApiClient } from "./services/api";
import type { ActiveView } from "./types";

export default function App() {
  const { token, user, login, logout, updateUserDisplay } = useAuth();
  const [activeView, setActiveView] = useState<ActiveView>("frete");
  const [accountModalOpen, setAccountModalOpen] = useState(false);

  const handleLogout = useCallback(() => {
    logout();
    setActiveView("frete");
    setAccountModalOpen(false);
  }, [logout]);

  const api = useMemo(
    () => createApiClient(token, handleLogout),
    [token, handleLogout],
  );

  if (!token || !user) {
    return <AuthScreen api={api} onAuthenticated={login} />;
  }

  return (
    <div className="min-h-screen bg-[#09090b] text-zinc-100 flex flex-col">
      <Navbar
        user={user}
        activeView={activeView}
        onSelectView={setActiveView}
        onOpenAccount={() => setAccountModalOpen(true)}
        onLogout={handleLogout}
      />

      <main className="max-w-6xl w-full mx-auto px-4 sm:px-6 py-6 flex-1">
        {activeView === "admin" ? (
          <AdminPanel
            api={api}
            currentUser={user}
            onBackToCalculator={() => setActiveView("frete")}
            onUpdateOwnDisplay={updateUserDisplay}
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
