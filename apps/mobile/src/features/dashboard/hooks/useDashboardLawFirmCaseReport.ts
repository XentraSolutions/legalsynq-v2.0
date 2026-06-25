import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';

export const dashboardLawFirmCaseReportKeys = {
  all: ['dashboard', 'law-firm-case-report'] as const,
};

export function useDashboardLawFirmCaseReport() {
  return useQuery({
    queryKey: dashboardLawFirmCaseReportKeys.all,
    queryFn: CasesApi.getDashboardLawFirmCaseReport,
  });
}
