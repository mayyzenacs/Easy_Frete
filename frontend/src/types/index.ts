export type RoleType = "Admin" | "User";
export type StatusType = "Pendente" | "Aprovado" | "Rejeitado";
export type ActiveView = "frete" | "admin";
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
}

export type AdminUsuarioPatchPayload = Partial<
  Pick<AdminUsuario, "nome" | "email" | "role" | "status">
>;

export interface EditandoAdmin {
  id: string;
  campo: "nome" | "email";
  valor: string;
  erro: string | null;
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
}

export interface AuthLoginResponse {
  token?: string;
  accessToken?: string;
  jwt?: string;
  usuario?: {
    id?: string;
    nome?: string;
    email?: string;
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
}
