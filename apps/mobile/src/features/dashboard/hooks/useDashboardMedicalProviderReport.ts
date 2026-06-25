import { useQuery } from '@tanstack/react-query';

import { CasesApi } from '@/shared/api/endpoints/Cases';

export const dashboardMedicalProviderReportKeys = {
  all: ['dashboard', 'medical-provider-report'] as const,
};

export function useDashboardMedicalProviderReport() {
  return useQuery({
    queryKey: dashboardMedicalProviderReportKeys.all,
    queryFn: CasesApi.getDashboardMedicalProviderReport,
  });
}
