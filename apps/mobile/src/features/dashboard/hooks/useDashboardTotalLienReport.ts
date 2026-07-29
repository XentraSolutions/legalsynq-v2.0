import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardTotalLienReportKeys = {
  all: ['dashboard', 'total-lien-report'] as const,
  filtered: (filter: ReportFilterRequest) => [...dashboardTotalLienReportKeys.all, filter] as const,
};

export function useDashboardTotalLienReport(filter: ReportFilterRequest, enabled = true) {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardTotalLienReportKeys.filtered(filter),
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardTotalLienReportV3(filter).then(
            LegacyCasesAdapter.toTotalLienReportPage
          )
        : CasesApi.getDashboardTotalLienReportV3(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}
