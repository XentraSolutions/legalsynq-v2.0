import { useQuery } from '@tanstack/react-query';
import { LiensApi } from '@/shared/api/endpoints/Liens';

export const monthlyAgingReportKeys = {
  all: ['dashboard', 'selling-monthly-aging'] as const,
  page: (asOfDate: string, page: number, pageSize: number) =>
    [...monthlyAgingReportKeys.all, asOfDate, page, pageSize] as const,
};

export function useMonthlyAgingReport(asOfDate: string, page = 1, pageSize = 10, enabled = true) {
  return useQuery({
    queryKey: monthlyAgingReportKeys.page(asOfDate, page, pageSize),
    queryFn: () => LiensApi.getMonthlyAgingReport({ asOfDate, page, pageSize }),
    enabled: enabled && Boolean(asOfDate),
  });
}
