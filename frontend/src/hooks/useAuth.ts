import { useState, useCallback } from "react";
import type {
  UserDisplayMetadata,
  UserProfile,
  UsuarioApiResponse,
} from "../types";
import { normalizarRole, normalizarStatus } from "../utils/formatters";

export function parseUserProfile(
  raw: UsuarioApiResponse | undefined,
  emailFallback?: string,
): UserProfile | null {
  if (!raw || typeof raw.id !== "string" || !raw.id.trim()) {
    return null;
  }

  return {
    id: raw.id.trim(),
    nome: typeof raw.nome === "string" ? raw.nome.trim() : "",
    email:
      typeof raw.email === "string" && raw.email.trim()
        ? raw.email.trim()
        : (emailFallback ?? ""),
    role: normalizarRole(raw.role),
    status: normalizarStatus(raw.status ?? "Aprovado"),
  };
}

export function useAuth() {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);

  const clearSession = useCallback(() => {
    setUser(null);
    setCheckingSession(false);
  }, []);

  const login = useCallback((profile: UserProfile) => {
    setUser(profile);
    setCheckingSession(false);
  }, []);

  const finishSessionCheck = useCallback(() => {
    setCheckingSession(false);
  }, []);

  const updateUserDisplay = useCallback(
    (display: Partial<UserDisplayMetadata>) => {
      setUser((current) => {
        if (!current) return current;
        return {
          ...current,
          nome: display.nome ?? current.nome,
          email: display.email ?? current.email,
        };
      });
    },
    [],
  );

  return {
    user,
    checkingSession,
    login,
    clearSession,
    finishSessionCheck,
    updateUserDisplay,
  };
}
