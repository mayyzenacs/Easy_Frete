import { useState, useCallback, useEffect } from "react";
import type { UserDisplayMetadata, UserProfile } from "../types";
import { decodeJwtPayload, parseUserFromJwt } from "../utils/jwt";

const JWT_STORAGE_KEY = "ef_jwt";
const USER_DISPLAY_STORAGE_KEY = "ef_user";

interface AuthState {
  token: string | null;
  user: UserProfile | null;
}

const readStoredDisplay = (): Partial<UserDisplayMetadata> | undefined => {
  try {
    const raw = localStorage.getItem(USER_DISPLAY_STORAGE_KEY);
    if (!raw) return undefined;
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return undefined;
    }
    const record = parsed as Record<string, unknown>;
    return {
      nome: typeof record.nome === "string" ? record.nome : undefined,
      email: typeof record.email === "string" ? record.email : undefined,
    };
  } catch {
    return undefined;
  }
};

const clearStoredSession = () => {
  localStorage.removeItem(JWT_STORAGE_KEY);
  localStorage.removeItem(USER_DISPLAY_STORAGE_KEY);
};

const readInitialAuthState = (): AuthState => {
  const storedToken = localStorage.getItem(JWT_STORAGE_KEY);
  if (!storedToken) {
    return { token: null, user: null };
  }

  const profile = parseUserFromJwt(storedToken, readStoredDisplay());
  if (!profile) {
    clearStoredSession();
    return { token: null, user: null };
  }

  return { token: storedToken, user: profile };
};

export function useAuth() {
  const [auth, setAuth] = useState<AuthState>(readInitialAuthState);

  const logout = useCallback(() => {
    clearStoredSession();
    setAuth({ token: null, user: null });
  }, []);

  const login = useCallback(
    (jwt: string, displayFallback?: Partial<UserDisplayMetadata>): boolean => {
      const profile = parseUserFromJwt(jwt, displayFallback);
      if (!profile) {
        clearStoredSession();
        return false;
      }

      const safeDisplay: UserDisplayMetadata = {
        nome: profile.nome,
        email: profile.email,
      };
      localStorage.setItem(JWT_STORAGE_KEY, jwt);
      localStorage.setItem(
        USER_DISPLAY_STORAGE_KEY,
        JSON.stringify(safeDisplay),
      );
      setAuth({ token: jwt, user: profile });
      return true;
    },
    [],
  );

  const updateUserDisplay = useCallback(
    (display: Partial<UserDisplayMetadata>) => {
      setAuth((current) => {
        if (!current.token || !current.user) return current;
        const nome = display.nome ?? current.user.nome;
        const email = display.email ?? current.user.email;
        const safeDisplay: UserDisplayMetadata = { nome, email };
        localStorage.setItem(
          USER_DISPLAY_STORAGE_KEY,
          JSON.stringify(safeDisplay),
        );
        return {
          token: current.token,
          user: {
            ...current.user,
            nome,
            email,
          },
        };
      });
    },
    [],
  );

  useEffect(() => {
    if (!auth.token) return;
    const payload = decodeJwtPayload(auth.token);
    const remainingMs =
      payload && typeof payload.exp === "number"
        ? payload.exp * 1000 - Date.now()
        : 0;

    // Limite máximo seguro para setTimeout (~24.8 dias)
    const delay = Math.min(Math.max(remainingMs, 0), 2_147_483_647);
    const timer = window.setTimeout(logout, delay);
    return () => window.clearTimeout(timer);
  }, [auth.token, logout]);

  return {
    token: auth.token,
    user: auth.user,
    login,
    logout,
    updateUserDisplay,
  };
}
