import { useState, useEffect } from "react";
import { X, Copy, Check } from "lucide-react";
import type { UserProfile } from "../types";
import { labelRole, statusTextClass } from "../utils/formatters";

interface AccountModalProps {
  user: UserProfile;
  onClose: () => void;
}

export function AccountModal({ user, onClose }: AccountModalProps) {
  const [copiadoId, setCopiadoId] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    if (!copiadoId) return;
    const timer = window.setTimeout(() => setCopiadoId(false), 1800);
    return () => window.clearTimeout(timer);
  }, [copiadoId]);

  const copiarIdUsuario = async () => {
    if (!user.id) return;
    try {
      await navigator.clipboard.writeText(user.id);
      setCopiadoId(true);
    } catch {
      setCopiadoId(false);
    }
  };

  return (
    <div
      onClick={onClose}
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
            onClick={onClose}
            aria-label="Fechar"
            className="text-zinc-400 hover:text-zinc-200 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex items-center gap-3">
          <span className="w-12 h-12 rounded-xl bg-yellow-400/15 border border-yellow-400/40 text-yellow-300 flex items-center justify-center font-black text-lg">
            {(user.nome || "U").charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="font-bold text-zinc-100 truncate">{user.nome}</p>
            <p className="text-xs text-zinc-400 truncate">{user.email}</p>
          </div>
        </div>

        <dl className="text-xs divide-y divide-zinc-800 border-y border-zinc-800">
          <div className="py-2.5 flex justify-between">
            <dt className="text-zinc-400">Perfil</dt>
            <dd className="font-semibold text-zinc-100">
              {labelRole(user.role)}
            </dd>
          </div>
          <div className="py-2.5 flex justify-between">
            <dt className="text-zinc-400">Status</dt>
            <dd className={`font-semibold ${statusTextClass[user.status]}`}>
              {user.status}
            </dd>
          </div>
          <div className="py-2.5 flex justify-between items-center gap-3">
            <dt className="text-zinc-400 shrink-0">ID</dt>
            <dd className="flex items-center gap-2 min-w-0">
              <code className="font-mono text-[11px] text-zinc-500 truncate">
                {user.id}
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
          onClick={onClose}
          className="w-full py-2.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 font-bold text-xs cursor-pointer"
        >
          Fechar
        </button>
      </div>
    </div>
  );
}
