import { ProductRole } from '@/types';
import type { ProductRoleValue } from '@/types';
import type { ProviderMode, ProviderModeInfo } from './provider-mode.types';

const MARKETPLACE_ROLES: ProductRoleValue[] = [
  ProductRole.SynqLienSeller,
  ProductRole.SynqLienBuyer,
  ProductRole.SynqLienHolder,
];

export function deriveProviderMode(productRoles: ProductRoleValue[]): ProviderModeInfo {
  const hasSellerRole = productRoles.includes(ProductRole.SynqLienSeller);
  const hasBuyerRole = productRoles.includes(ProductRole.SynqLienBuyer);
  const hasHolderRole = productRoles.includes(ProductRole.SynqLienHolder);
  const hasAnyMarketplaceRole = MARKETPLACE_ROLES.some((r) => productRoles.includes(r));

  const mode: ProviderMode = hasAnyMarketplaceRole ? 'sell' : 'manage';

  return {
    mode,
    isSellMode: mode === 'sell',
    isManageMode: mode === 'manage',
    hasSellerRole,
    hasBuyerRole,
    hasHolderRole,
    hasAnyMarketplaceRole,
  };
}

export function getMode(productRoles: ProductRoleValue[]): ProviderMode {
  return deriveProviderMode(productRoles).mode;
}

export function isSellMode(productRoles: ProductRoleValue[]): boolean {
  return deriveProviderMode(productRoles).isSellMode;
}

export function isManageMode(productRoles: ProductRoleValue[]): boolean {
  return deriveProviderMode(productRoles).isManageMode;
}
