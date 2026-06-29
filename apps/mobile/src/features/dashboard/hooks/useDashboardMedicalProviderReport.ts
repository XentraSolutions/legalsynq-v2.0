import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { ReportFilterRequest } from '@/shared/api/endpoints/Cases';

export const dashboardMedicalProviderReportKeys = {
  all: ['dashboard', 'medical-provider-report'] as const,
  filtered: (filter: ReportFilterRequest) =>
    [...dashboardMedicalProviderReportKeys.all, filter] as const,
};

export function useDashboardMedicalProviderReport(filter: ReportFilterRequest, enabled = true) {
  return useQuery({
    queryKey: dashboardMedicalProviderReportKeys.filtered(filter),
    queryFn: () => CasesApi.getDashboardMedicalProviderReportV3(filter),
    enabled,
  });
}
