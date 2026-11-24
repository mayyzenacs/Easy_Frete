import type { MarketplaceId } from "../types";

export const FATOR_CUBAGEM_MAGALU = 6000;
export const PISO_REPUTACAO_MAGALU = 79;

export interface MarketplaceConfig {
  id: MarketplaceId;
  label: string;
  available: boolean;
  fatorCubagem: number;
  pisoReputacao?: number;
}

export const MARKETPLACES: Record<MarketplaceId, MarketplaceConfig> = {
  magalu: {
    id: "magalu",
    label: "Magalu",
    available: true,
    fatorCubagem: FATOR_CUBAGEM_MAGALU,
    pisoReputacao: PISO_REPUTACAO_MAGALU,
  },
  amazon: {
    id: "amazon",
    label: "Amazon",
    available: false,
    fatorCubagem: 6000,
  },
};
