import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';

export const dashboardLawFirmCaseReportKeys = {
  all: ['dashboard', 'law-firm-case-report'] as const,
  filtered: (filter: ReportFilterRequest) =>
    [...dashboardLawFirmCaseReportKeys.all, filter] as const,
};

export function useDashboardLawFirmCaseReport(filter: ReportFilterRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardLawFirmCaseReportKeys.filtered(filter),
    queryFn: () => CasesApi.getDashboardLawFirmCaseReportV3(filter),
    enabled,
  });
}
