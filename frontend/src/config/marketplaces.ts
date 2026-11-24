import type { MarketplaceId } from "../types";

export interface MarketplaceConfig {
  id: MarketplaceId;
  label: string;
  available: boolean;
}

export const MARKETPLACES: Record<MarketplaceId, MarketplaceConfig> = {
  magalu: {
    id: "magalu",
    label: "Magalu",
    available: true,
  },
  amazon: {
    id: "amazon",
    label: "Amazon",
    available: false,
  },
};
