import { useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import type { DashboardStatRequest } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardCashReceivedKeys = {
  all: ['dashboard', 'cash-received'] as const,
  filtered: (req: DashboardStatRequest) => [...dashboardCashReceivedKeys.all, req] as const,
};

export function useDashboardCashReceived(req: DashboardStatRequest, enabled = true) {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardCashReceivedKeys.filtered(req),
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardCashReceived(req).then(
            LegacyCasesAdapter.toDashboardStatResponse
          )
        : CasesApi.getDashboardCashReceived(req),
    enabled,
  });
}
