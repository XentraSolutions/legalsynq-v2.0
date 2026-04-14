'use client';

import { useMemo } from 'react';
import { useSession } from '@/hooks/use-session';
import { deriveProviderMode } from '@/lib/provider-mode';
import type { ProviderModeInfo } from '@/lib/provider-mode';

const DEFAULT_MODE: ProviderModeInfo = {
  mode: 'manage',
  isSellMode: false,
  isManageMode: true,
  hasSellerRole: false,
  hasBuyerRole: false,
  hasHolderRole: false,
  hasAnyMarketplaceRole: false,
};

export function useProviderMode(): ProviderModeInfo & { isReady: boolean } {
  const { session, isLoading } = useSession();

  return useMemo(() => {
    const modeInfo = session ? deriveProviderMode(session.productRoles) : DEFAULT_MODE;
    return { ...modeInfo, isReady: !isLoading && !!session };
  }, [session, isLoading]);
}
