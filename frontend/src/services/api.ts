import type {
  AdminUsuarioApiItem,
  AdminUsuarioPatchPayload,
  ApiErrorResponse,
  AuthLoginResponse,
  CalcularFreteApiRequest,
  CalcularFreteApiResponse,
  ConfiguracaoMagaluApiResponse,
  MessageApiResponse,
  NotificacoesApiResponse,
  ProdutoApiResponse,
  ResetTokenValidationResponse,
  SolicitacaoExclusao,
} from "../types";

const API_BASE_URL = (import.meta.env.VITE_API_URL || "/api/v1").replace(
  /\/$/,
  "",
);

export type ApiResult<T> =
  | { ok: true; status: number; data: T }
  | {
      ok: false;
      status: number;
      error: string;
      unauthorized?: boolean;
      bloqueadoAte?: string;
    };

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
  authForgotPassword: (
    email: string,
  ) => Promise<ApiResult<MessageApiResponse>>;
  authValidateResetToken: (
    token: string,
  ) => Promise<ApiResult<ResetTokenValidationResponse>>;
  authResetPassword: (
    token: string,
    newPassword: string,
  ) => Promise<ApiResult<MessageApiResponse>>;
  getProdutoBySku: (sku: string) => Promise<ApiResult<ProdutoApiResponse>>;
  getConfigMagalu: () => Promise<ApiResult<ConfiguracaoMagaluApiResponse>>;
  calcularFreteMagalu: (
    payload: CalcularFreteApiRequest,
  ) => Promise<ApiResult<CalcularFreteApiResponse>>;
  listAdminUsuarios: () => Promise<ApiResult<AdminUsuarioApiItem[]>>;
  patchAdminUsuario: (
    id: string,
    payload: AdminUsuarioPatchPayload,
  ) => Promise<ApiResult<AdminUsuarioApiItem>>;
  deleteAdminUsuario: (id: string) => Promise<ApiResult<MessageApiResponse>>;
  listSolicitacoesExclusao: () => Promise<ApiResult<SolicitacaoExclusao[]>>;
  criarSolicitacaoExclusao: (
    usuarioAlvoId: string,
    motivo: string,
  ) => Promise<ApiResult<SolicitacaoExclusao>>;
  aprovarSolicitacaoExclusao: (
    id: string,
    observacao?: string,
  ) => Promise<ApiResult<SolicitacaoExclusao>>;
  recusarSolicitacaoExclusao: (
    id: string,
    observacao?: string,
  ) => Promise<ApiResult<SolicitacaoExclusao>>;
  listNotificacoes: () => Promise<ApiResult<NotificacoesApiResponse>>;
  marcarNotificacaoLida: (id: string) => Promise<ApiResult<MessageApiResponse>>;
  marcarTodasNotificacoesLidas: () => Promise<ApiResult<MessageApiResponse>>;
}

const extractErrorDetails = (
  payload: unknown,
  fallback: string,
): { error: string; bloqueadoAte?: string } => {
  if (payload && typeof payload === "object") {
    const err = payload as ApiErrorResponse;
    const bloqueadoAte =
      typeof err.bloqueadoAte === "string" ? err.bloqueadoAte : undefined;
    if (typeof err.error === "string" && err.error.trim()) {
      return { error: err.error, bloqueadoAte };
    }
    if (typeof err.message === "string" && err.message.trim()) {
      return { error: err.message, bloqueadoAte };
    }
    return { error: fallback, bloqueadoAte };
  }
  return { error: fallback };
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
        body:
          options.body !== undefined ? JSON.stringify(options.body) : undefined,
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
        const { error, bloqueadoAte } = extractErrorDetails(
          payload,
          options.fallbackError,
        );
        return {
          ok: false,
          status: response.status,
          error,
          bloqueadoAte,
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

    authForgotPassword: (email) =>
      request<MessageApiResponse>("/auth/forgot-password", {
        method: "POST",
        auth: false,
        body: { email },
        fallbackError: "Não foi possível enviar o link de redefinição.",
      }),

    authValidateResetToken: (resetToken) =>
      request<ResetTokenValidationResponse>(
        `/auth/reset-password/validate?token=${encodeURIComponent(resetToken)}`,
        {
          method: "GET",
          auth: false,
          fallbackError: "Link de redefinição inválido ou expirado.",
        },
      ),

    authResetPassword: (resetToken, newPassword) =>
      request<MessageApiResponse>("/auth/reset-password", {
        method: "POST",
        auth: false,
        body: { token: resetToken, newPassword },
        fallbackError: "Não foi possível redefinir a senha.",
      }),

    getProdutoBySku: (sku) =>
      request<ProdutoApiResponse>(`/produtos/${encodeURIComponent(sku)}`, {
        method: "GET",
        fallbackError: "Não foi possível consultar o SKU.",
        networkError: "Sem conexão ao consultar o SKU.",
      }),

    getConfigMagalu: () =>
      request<ConfiguracaoMagaluApiResponse>("/tarifas/magalu/config", {
        method: "GET",
        fallbackError:
          "Configuração de tarifas Magalu não carregada. Sincronize a planilha.",
        networkError: "Sem conexão ao carregar as regras de frete.",
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

    deleteAdminUsuario: (id) =>
      request<MessageApiResponse>(`/admin/usuarios/${encodeURIComponent(id)}`, {
        method: "DELETE",
        fallbackError: "Não foi possível excluir o usuário.",
        networkError: "Não foi possível excluir o usuário.",
      }),

    listSolicitacoesExclusao: () =>
      request<SolicitacaoExclusao[]>("/admin/exclusoes", {
        method: "GET",
        fallbackError: "Não foi possível carregar as solicitações de exclusão.",
        networkError: "Não foi possível carregar as solicitações de exclusão.",
      }),

    criarSolicitacaoExclusao: (usuarioAlvoId, motivo) =>
      request<SolicitacaoExclusao>("/admin/exclusoes", {
        method: "POST",
        body: { usuarioAlvoId, motivo },
        fallbackError: "Não foi possível enviar a solicitação de exclusão.",
      }),

    aprovarSolicitacaoExclusao: (id, observacao) =>
      request<SolicitacaoExclusao>(
        `/admin/exclusoes/${encodeURIComponent(id)}/aprovar`,
        {
          method: "POST",
          body: observacao ? { observacao } : {},
          fallbackError: "Não foi possível aprovar a solicitação.",
        },
      ),

    recusarSolicitacaoExclusao: (id, observacao) =>
      request<SolicitacaoExclusao>(
        `/admin/exclusoes/${encodeURIComponent(id)}/recusar`,
        {
          method: "POST",
          body: observacao ? { observacao } : {},
          fallbackError: "Não foi possível recusar a solicitação.",
        },
      ),

    listNotificacoes: () =>
      request<NotificacoesApiResponse>("/admin/notificacoes", {
        method: "GET",
        fallbackError: "Não foi possível carregar as notificações.",
      }),

    marcarNotificacaoLida: (id) =>
      request<MessageApiResponse>(
        `/admin/notificacoes/${encodeURIComponent(id)}/lida`,
        {
          method: "PATCH",
          fallbackError: "Não foi possível marcar a notificação como lida.",
        },
      ),

    marcarTodasNotificacoesLidas: () =>
      request<MessageApiResponse>("/admin/notificacoes/marcar-todas-lidas", {
        method: "POST",
        fallbackError: "Não foi possível marcar as notificações como lidas.",
      }),
  };
}
