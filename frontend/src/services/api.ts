import type {
  AdminUsuarioApiItem,
  AdminUsuarioPatchPayload,
  ApiErrorResponse,
  AuthLoginResponse,
  CalcularFreteApiRequest,
  CalcularFreteApiResponse,
  ProdutoApiResponse,
} from "../types";

const API_BASE_URL = (import.meta.env.VITE_API_URL || "/api/v1").replace(
  /\/$/,
  "",
);

export type ApiResult<T> =
  | { ok: true; status: number; data: T }
  | { ok: false; status: number; error: string; unauthorized?: boolean };

interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  body?: unknown;
  auth?: boolean;
  fallbackError: string;
  networkError?: string;
}

export interface ApiClient {
  authLogin: (
    email: string,
    password: string,
  ) => Promise<ApiResult<AuthLoginResponse>>;
  authRegister: (
    nome: string,
    email: string,
    password: string,
  ) => Promise<ApiResult<unknown>>;
  getProdutoBySku: (sku: string) => Promise<ApiResult<ProdutoApiResponse>>;
  calcularFreteMagalu: (
    payload: CalcularFreteApiRequest,
  ) => Promise<ApiResult<CalcularFreteApiResponse>>;
  listAdminUsuarios: () => Promise<ApiResult<AdminUsuarioApiItem[]>>;
  patchAdminUsuario: (
    id: string,
    payload: AdminUsuarioPatchPayload,
  ) => Promise<ApiResult<AdminUsuarioApiItem>>;
}

const extractErrorMessage = (payload: unknown, fallback: string): string => {
  if (payload && typeof payload === "object") {
    const err = payload as ApiErrorResponse;
    if (typeof err.error === "string" && err.error.trim()) return err.error;
    if (typeof err.message === "string" && err.message.trim()) {
      return err.message;
    }
  }
  return fallback;
};

export function createApiClient(
  token: string | null,
  onUnauthorized: () => void,
): ApiClient {
  const request = async <T>(
    path: string,
    options: RequestOptions,
  ): Promise<ApiResult<T>> => {
    const useAuth = options.auth !== false;
    const activeToken = useAuth ? token : null;

    if (useAuth && !activeToken) {
      onUnauthorized();
      return {
        ok: false,
        status: 401,
        error: "Sessão expirada.",
        unauthorized: true,
      };
    }

    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };
    if (useAuth && activeToken) {
      headers.Authorization = `Bearer ${activeToken}`;
    }

    try {
      const response = await fetch(`${API_BASE_URL}${path}`, {
        method: options.method ?? "GET",
        headers,
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      });

      if (useAuth && response.status === 401) {
        onUnauthorized();
        return {
          ok: false,
          status: 401,
          error: "Sessão expirada.",
          unauthorized: true,
        };
      }

      const payload: unknown = await response.json().catch(() => null);

      if (!response.ok) {
        return {
          ok: false,
          status: response.status,
          error: extractErrorMessage(payload, options.fallbackError),
        };
      }

      return {
        ok: true,
        status: response.status,
        data: payload as T,
      };
    } catch {
      return {
        ok: false,
        status: 0,
        error: options.networkError ?? "Sem conexão com o servidor.",
      };
    }
  };

  return {
    authLogin: (email, password) =>
      request<AuthLoginResponse>("/auth/login", {
        method: "POST",
        auth: false,
        body: { email, password },
        fallbackError:
          "Não foi possível autenticar. Verifique os dados informados.",
      }),

    authRegister: (nome, email, password) =>
      request<unknown>("/auth/register", {
        method: "POST",
        auth: false,
        body: { nome, email, password },
        fallbackError:
          "Não foi possível autenticar. Verifique os dados informados.",
      }),

    getProdutoBySku: (sku) =>
      request<ProdutoApiResponse>(`/produtos/${encodeURIComponent(sku)}`, {
        method: "GET",
        fallbackError: "Não foi possível consultar o SKU.",
        networkError: "Sem conexão ao consultar o SKU.",
      }),

    calcularFreteMagalu: (payload) =>
      request<CalcularFreteApiResponse>("/frete/magalu/calcular", {
        method: "POST",
        body: payload,
        fallbackError: "Não foi possível calcular o frete.",
      }),

    listAdminUsuarios: () =>
      request<AdminUsuarioApiItem[]>("/admin/usuarios", {
        method: "GET",
        fallbackError: "Acesso negado.",
        networkError: "Não foi possível carregar os usuários.",
      }),

    patchAdminUsuario: (id, payload) =>
      request<AdminUsuarioApiItem>(
        `/admin/usuarios/${encodeURIComponent(id)}`,
        {
          method: "PATCH",
          body: payload,
          fallbackError: "Não foi possível atualizar o usuário.",
          networkError: "Não foi possível atualizar o usuário.",
        },
      ),
  };
}
