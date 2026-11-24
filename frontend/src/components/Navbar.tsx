import { useState, useEffect, useRef } from "react";
import { Truck, ChevronDown, User, ShieldCheck, LogOut } from "lucide-react";
import { MARKETPLACES } from "../config/marketplaces";
import type { ActiveView, UserProfile } from "../types";
import { labelRole } from "../utils/formatters";

interface NavbarProps {
  user: UserProfile;
  activeView: ActiveView;
  onSelectView: (view: ActiveView) => void;
  onOpenAccount: () => void;
  onLogout: () => void;
}

export function Navbar({
  user,
  activeView,
  onSelectView,
  onOpenAccount,
  onLogout,
}: NavbarProps) {
  const [menuAberto, setMenuAberto] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuAberto(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

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

        {/* Menu do Usuário */}
        <div className="relative" ref={menuRef}>
          <button
            type="button"
            onClick={() => setMenuAberto((prev) => !prev)}
            className="flex items-center gap-2.5 pl-2 pr-3 py-1.5 rounded-xl bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 transition cursor-pointer"
          >
            <span className="w-8 h-8 rounded-lg bg-yellow-400/15 border border-yellow-400/40 text-yellow-300 flex items-center justify-center font-bold text-sm">
              {(user.nome || "U").charAt(0).toUpperCase()}
            </span>
            <span className="text-left hidden sm:block leading-tight">
              <span className="block text-xs font-semibold text-zinc-100">
                {user.nome || "Usuário"}
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

              {user.role === "Admin" && (
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
    </header>
  );
}
