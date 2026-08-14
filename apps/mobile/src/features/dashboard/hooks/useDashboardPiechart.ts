import { useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardPiechartKeys = {
  all: ['dashboard', 'piechart'] as const,
};

export function useDashboardPiechart() {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardPiechartKeys.all,
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardPiechart().then(LegacyCasesAdapter.toDashboardPiechart)
        : CasesApi.getDashboardPiechart(),
  });
}
