'use client';

import { useState, useEffect, useCallback } from 'react';
import { useSession } from '@/hooks/use-session';
import { careConnectApi } from '@/lib/careconnect-api';
import { OrgType, ProductRole } from '@/types';

const POLL_INTERVAL_MS = 30_000;

export function useNavBadges(): Record<string, number> {
  const { session } = useSession();
  const [badges, setBadges] = useState<Record<string, number>>({});

  const isProvider =
    session?.orgType === OrgType.Provider &&
    session.productRoles?.includes(ProductRole.CareConnectReceiver);

  const fetchBadges = useCallback(async () => {
    if (!isProvider) return;

    try {
      const { data } = await careConnectApi.referrals.search({
        status: 'New',
        page: 1,
        pageSize: 1,
      });
      setBadges(prev => {
        const count = data.totalCount ?? 0;
        if (prev.newReferrals === count) return prev;
        return { ...prev, newReferrals: count };
      });
    } catch {
      // silently ignore — badge is non-critical
    }
  }, [isProvider]);

  useEffect(() => {
    if (!isProvider) {
      setBadges({});
      return;
    }

    fetchBadges();
    const id = setInterval(fetchBadges, POLL_INTERVAL_MS);
    return () => clearInterval(id);
  }, [fetchBadges, isProvider]);

  return badges;
}
