import { useQuery } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';

export const dashboardKeys = {
  summary: ['dashboard', 'summary'] as const,
};

export function useDashboard() {
  return useQuery({
    queryKey: dashboardKeys.summary,
    queryFn: MockStore.getDashboard,
  });
}
