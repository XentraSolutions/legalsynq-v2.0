import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { LineChartCard } from './LineChartCard';
import { formatSellingBucketLabel } from './sellingDashboardFormatters';

export function SellingDashboardTimeSeries({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  const timeSeries = data?.timeSeries ?? [];
  return (
    <LineChartCard
      currency={data?.currency ?? 'USD'}
      isDark={isDark}
      labels={
        useDummyData
          ? undefined
          : timeSeries.map((point) =>
              formatSellingBucketLabel(point.bucketStart, point.grain)
            )
      }
      points={useDummyData ? undefined : timeSeries.map((point) => point.lienRevenue)}
    />
  );
}
