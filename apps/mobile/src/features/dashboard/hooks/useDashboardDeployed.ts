import { useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import type { DashboardStatRequest } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardDeployedKeys = {
  all: ['dashboard', 'deployed'] as const,
  filtered: (req: DashboardStatRequest) => [...dashboardDeployedKeys.all, req] as const,
};

export function useDashboardDeployed(req: DashboardStatRequest, enabled = true) {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardDeployedKeys.filtered(req),
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardDeployed(req).then(LegacyCasesAdapter.toDashboardStatResponse)
        : CasesApi.getDashboardDeployed(req),
    enabled,
  });
}
