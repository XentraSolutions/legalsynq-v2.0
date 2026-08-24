import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardMedicalProviderReportKeys = {
  all: ['dashboard', 'medical-provider-report'] as const,
  filtered: (filter: ReportFilterRequest) =>
    [...dashboardMedicalProviderReportKeys.all, filter] as const,
};

export function useDashboardMedicalProviderReport(filter: ReportFilterRequest, enabled = true) {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardMedicalProviderReportKeys.filtered(filter),
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardMedicalProviderReportV3(filter).then(
            LegacyCasesAdapter.toMedicalProviderReportPage
          )
        : CasesApi.getDashboardMedicalProviderReportV3(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}
