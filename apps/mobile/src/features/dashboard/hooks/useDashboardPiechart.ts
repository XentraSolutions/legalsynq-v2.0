import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';

export const dashboardPiechartKeys = {
  all: ['dashboard', 'piechart'] as const,
};

export function useDashboardPiechart() {
  return useQuery({
    queryKey: dashboardPiechartKeys.all,
    queryFn: CasesApi.getDashboardPiechart,
  });
}
