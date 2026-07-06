import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { DashboardStatRequest } from '@/shared/api/endpoints/Cases';

export const dashboardCashReceivedKeys = {
  all: ['dashboard', 'cash-received'] as const,
  filtered: (req: DashboardStatRequest) => [...dashboardCashReceivedKeys.all, req] as const,
};

export function useDashboardCashReceived(req: DashboardStatRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardCashReceivedKeys.filtered(req),
    queryFn: () => CasesApi.getDashboardCashReceived(req),
    enabled,
  });
}
