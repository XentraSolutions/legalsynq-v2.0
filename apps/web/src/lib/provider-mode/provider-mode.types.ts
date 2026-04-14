export type ProviderMode = 'sell' | 'manage';

export interface ProviderModeInfo {
  mode: ProviderMode;
  isSellMode: boolean;
  isManageMode: boolean;
  hasSellerRole: boolean;
  hasBuyerRole: boolean;
  hasHolderRole: boolean;
  hasAnyMarketplaceRole: boolean;
}
