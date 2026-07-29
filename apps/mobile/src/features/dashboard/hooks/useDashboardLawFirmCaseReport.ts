import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { CasesApi, LegacyCasesAdapter, LegacyCasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const dashboardLawFirmCaseReportKeys = {
  all: ['dashboard', 'law-firm-case-report'] as const,
  filtered: (filter: ReportFilterRequest) =>
    [...dashboardLawFirmCaseReportKeys.all, filter] as const,
};

export function useDashboardLawFirmCaseReport(filter: ReportFilterRequest, enabled = true) {
  const mode = useAtomValue(apiModeAtom);

  return useQuery({
    queryKey: dashboardLawFirmCaseReportKeys.filtered(filter),
    queryFn: () =>
      mode === 'legacy'
        ? LegacyCasesApi.getDashboardLawFirmCaseReportV3(filter).then(
            LegacyCasesAdapter.toLawFirmCaseReportPage
          )
        : CasesApi.getDashboardLawFirmCaseReportV3(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}
