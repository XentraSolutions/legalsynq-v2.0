import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { SLICE_COLORS, type DonutSlice } from '../dashboardShared';
import { SELLING_STATUS } from './sellingDashboardData';
import { DonutCard } from '../DonutCard';
import { formatSellingCurrency } from './sellingDashboardFormatters';

export function SellingDashboardStatusSummary({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  const currency = data?.currency ?? 'USD';
  const slices: DonutSlice[] = useDummyData
    ? SELLING_STATUS
    : (data?.lienStatuses ?? []).map((status, index) => ({
        label: status.status,
        value: status.lienCount,
        amount: status.lienCount.toLocaleString(),
        percent: `(${status.percentOfLiens.toFixed(1)}%)`,
        color: SLICE_COLORS[index % SLICE_COLORS.length],
        details: [
          { label: 'Original', value: formatSellingCurrency(status.originalAmount, currency) },
          {
            label: 'Outstanding',
            value: formatSellingCurrency(status.outstandingAmount, currency),
          },
        ],
      }));
  const totalLiens = (data?.lienStatuses ?? []).reduce((sum, item) => sum + item.lienCount, 0);

  return (
    <DonutCard
      centerCaption="Total Liens"
      centerValue={useDummyData ? '1,248' : totalLiens.toLocaleString()}
      icon="pie-chart-outline"
      isDark={isDark}
      slices={slices}
      subtitle="Breakdown of total case liens by their current operational status."
      title="Liens by Status"
    />
  );
}
