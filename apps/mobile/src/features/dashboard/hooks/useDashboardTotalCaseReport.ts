import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';

export const dashboardTotalCaseReportKeys = {
  all: ['dashboard', 'total-case-report'] as const,
  filtered: (filter: ReportFilterRequest) => [...dashboardTotalCaseReportKeys.all, filter] as const,
};

export function useDashboardTotalCaseReport(filter: ReportFilterRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardTotalCaseReportKeys.filtered(filter),
    queryFn: () => CasesApi.getDashboardTotalCaseReportV3(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}
