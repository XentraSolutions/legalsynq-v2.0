import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';

export const dashboardTotalLienReportKeys = {
  all: ['dashboard', 'total-lien-report'] as const,
  filtered: (filter: ReportFilterRequest) => [...dashboardTotalLienReportKeys.all, filter] as const,
};

export function useDashboardTotalLienReport(filter: ReportFilterRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardTotalLienReportKeys.filtered(filter),
    queryFn: () => CasesApi.getDashboardTotalLienReportV3(filter),
    enabled,
  });
}
