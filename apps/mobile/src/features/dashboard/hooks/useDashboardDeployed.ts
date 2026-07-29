import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { DashboardStatRequest } from '@/shared/api/endpoints/Cases';

export const dashboardDeployedKeys = {
  all: ['dashboard', 'deployed'] as const,
  filtered: (req: DashboardStatRequest) => [...dashboardDeployedKeys.all, req] as const,
};

export function useDashboardDeployed(req: DashboardStatRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardDeployedKeys.filtered(req),
    queryFn: () => CasesApi.getDashboardDeployed(req),
    enabled,
  });
}
