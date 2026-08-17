import { useQuery } from '@tanstack/react-query';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import type { DashboardDateRange } from '@/features/dashboard/types/types';

export const sellingDashboardAnalyticsKeys = {
  all: ['dashboard', 'selling-analytics'] as const,
  filtered: (range: DashboardDateRange) =>
    [...sellingDashboardAnalyticsKeys.all, range.startDate, range.endDate] as const,
};

function toApiDate(value: string): string | undefined {
  if (!value) return undefined;
  const [month, day, year] = value.split('/');
  return month && day && year ? `${year}-${month}-${day}` : value;
}

export function useSellingDashboardAnalytics(range: DashboardDateRange, enabled = true) {
  return useQuery({
    queryKey: sellingDashboardAnalyticsKeys.filtered(range),
    queryFn: () =>
      LiensApi.getSellingDashboardAnalytics({
        startDate: toApiDate(range.startDate),
        endDate: toApiDate(range.endDate),
        compare: 'previousPeriod',
      }),
    enabled,
  });
}
