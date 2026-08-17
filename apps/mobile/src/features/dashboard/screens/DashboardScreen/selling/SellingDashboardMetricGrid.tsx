import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import type { StatCardData } from '../dashboardShared';
import { SELLING_STATS } from './sellingDashboardData';
import { StatGrid } from '../StatGrid';
import { mapSellingMetric } from './sellingDashboardFormatters';

export function SellingDashboardMetricGrid({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  const currency = data?.currency ?? 'USD';
  const stats: StatCardData[] = useDummyData
    ? SELLING_STATS
    : data
      ? [
          mapSellingMetric('Total Lien Revenue', data.metrics.totalLienRevenue, currency),
          mapSellingMetric('Total Outstanding', data.metrics.totalOutstanding, currency),
          mapSellingMetric('Past Amount Due', data.metrics.pastAmountDue, currency),
          mapSellingMetric('Payments', data.metrics.payments, currency),
        ]
      : [];

  return <StatGrid isDark={isDark} stats={stats} />;
}
