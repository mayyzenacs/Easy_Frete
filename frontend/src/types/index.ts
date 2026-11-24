export type RoleType = "Master" | "Admin" | "User";
export type StatusType = "Pendente" | "Aprovado" | "Rejeitado";
export type DeletionRequestStatus = "Pendente" | "Aprovado" | "Recusado";
export type NotificationType =
  | "CadastroPendente"
  | "SolicitacaoExclusao"
  | "ExclusaoAprovada"
  | "ExclusaoRecusada";
export type ActiveView = "frete" | "admin";
export type AdminTab = "users" | "approved" | "pending" | "deletions";
export type MarketplaceId = "magalu" | "amazon";

export interface UserDisplayMetadata {
  nome: string;
  email: string;
}

export interface UserProfile extends UserDisplayMetadata {
  id: string;
  role: RoleType;
  status: StatusType;
}

export interface AdminUsuario extends UserProfile {
  criadoEm?: string;
  possuiPedidoExclusaoPendente?: boolean;
}

export type AdminUsuarioPatchPayload = Partial<
  Pick<AdminUsuario, "nome" | "email" | "role" | "status">
>;

export interface SolicitacaoExclusao {
  id: string;
  usuarioAlvoId: string | null;
  usuarioAlvoNome: string;
  usuarioAlvoEmail: string;
  solicitadoPorId: string | null;
  solicitadoPorNome: string;
  motivo: string;
  status: DeletionRequestStatus;
  criadoEm: string;
  decididoPorId: string | null;
  decididoPorNome: string | null;
  observacaoDecisao: string | null;
  decididoEm: string | null;
}

export interface NotificacaoItem {
  id: string;
  tipo: NotificationType;
  titulo: string;
  mensagem: string;
  referenciaId: string | null;
  lida: boolean;
  criadoEm: string;
  lidaEm: string | null;
}

export interface NotificacoesApiResponse {
  naoLidas: number;
  itens: NotificacaoItem[];
}

export interface MessageApiResponse {
  message: string;
}

export interface ResetTokenValidationResponse {
  valid: boolean;
  expiraEm: string;
}

export interface ResultadoFrete {
  faixa: string;
  faixaPeso: string;
  tarifaLabel: string;
  tarifaValor: number;
  taxaFixa: number;
  custoTotal: number;
}

export interface SkuFeedback {
  tipo: "ok" | "warn" | "error";
  titulo?: string;
  mensagem: string;
}

export interface ApiErrorResponse {
  error?: string;
  message?: string;
  bloqueadoAte?: string;
}

export interface AuthLoginResponse {
  token?: string;
  accessToken?: string;
  jwt?: string;
  usuario?: {
    id?: string;
    nome?: string;
    email?: string;
    role?: string;
    status?: string;
  };
}

export interface ProdutoApiResponse {
  sku?: string;
  nome?: string;
  pesoRealKg?: number;
  pesoBruto?: number;
  peso?: number;
  comprimentoCm?: number;
  comprimento?: number;
  larguraCm?: number;
  largura?: number;
  alturaCm?: number;
  altura?: number;
  linkDrive?: string | null;
}

export interface CalcularFreteApiRequest {
  sku: string | null;
  precoProduto: number;
  pesoRealKg: number;
  comprimentoCm: number;
  larguraCm: number;
  alturaCm: number;
  isFulfillment: boolean;
  reputacaoMaior97: boolean;
}

export interface ConfiguracaoMagaluApiResponse {
  taxaFixaObrigatoria: number;
  limiteTabela: number;
  limiteAcima199: number;
  percentualAbaixoLimiteFulfillment: number;
  percentualAbaixoLimiteColeta: number;
  divisorCubagem: number;
  percentualReputacaoMinima: number;
  atualizadoEm?: string;
}

export interface CalcularFreteApiResponse {
  faixaPrecoAplicada?: string | null;
  faixaPesoAplicada?: string | null;
  modalidadeAplicada?: string | null;
  tarifaBase?: number;
  taxaFixaObrigatoria?: number;
  valorFinalFrete?: number;
}

export interface AdminUsuarioApiItem {
  id: string;
  nome?: string;
  email?: string;
  role?: unknown;
  status?: unknown;
  criadoEm?: string;
  possuiPedidoExclusaoPendente?: boolean;
}
