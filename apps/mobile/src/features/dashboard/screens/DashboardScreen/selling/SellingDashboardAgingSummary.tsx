import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { SLICE_COLORS, type DonutSlice } from '../dashboardShared';
import { SELLING_AGING } from './sellingDashboardData';
import { DashboardEmptyStateCard } from '../DashboardEmptyStateCard';
import { DonutCard } from '../DonutCard';
import {
  formatSellingCompactCurrency,
  formatSellingCurrency,
} from './sellingDashboardFormatters';

export function SellingDashboardAgingSummary({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  const currency = data?.currency ?? 'USD';
  if (!useDummyData && !data?.arAging.isAvailable) {
    return (
      <DashboardEmptyStateCard
        isDark={isDark}
        message={data?.arAging.unavailableReason ?? 'A/R aging data is unavailable.'}
        title="A/R Aging Summary"
      />
    );
  }

  const slices: DonutSlice[] = useDummyData
    ? SELLING_AGING
    : (data?.arAging.buckets ?? []).map((bucket, index) => ({
        label: bucket.label,
        value: bucket.amount,
        amount: formatSellingCurrency(bucket.amount, currency),
        percent: bucket.percent == null ? undefined : `(${bucket.percent.toFixed(1)}%)`,
        color: SLICE_COLORS[index % SLICE_COLORS.length],
      }));

  return (
    <DonutCard
      centerCaption="Total A/R"
      centerValue={
        useDummyData
          ? '$3.8M'
          : formatSellingCompactCurrency(data?.arAging.total ?? 0, currency)
      }
      icon="pie-chart-outline"
      isDark={isDark}
      slices={slices}
      subtitle="Breakdown of outstanding accounts receivable by age and duration."
      title="A/R Aging Summary"
    />
  );
}
