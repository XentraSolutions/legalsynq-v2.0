import type { MonthlyAgingReportResponse } from '@/shared/api/endpoints/Liens';
import type { DonutSlice } from '../dashboardShared';
import { SELLING_AGING } from './sellingDashboardData';
import { DashboardEmptyStateCard } from '../DashboardEmptyStateCard';
import { DonutCard } from '../DonutCard';
import {
  formatSellingCompactCurrency,
  buildMonthlyAgingSlices,
} from './sellingDashboardFormatters';

export function SellingDashboardAgingSummary({
  monthlyAging,
  isError,
  isLoading,
  isDark,
  useDummyData,
  onViewDetails,
}: {
  monthlyAging?: MonthlyAgingReportResponse;
  isError: boolean;
  isLoading: boolean;
  isDark: boolean;
  useDummyData: boolean;
  onViewDetails: () => void;
}) {
  const currency = monthlyAging?.currency ?? 'USD';
  if (!useDummyData && isError) {
    return (
      <DashboardEmptyStateCard
        isDark={isDark}
        message="A/R aging data is unavailable. Pull to refresh and try again."
        title="A/R Aging Summary"
      />
    );
  }

  const slices: DonutSlice[] = useDummyData ? SELLING_AGING : buildMonthlyAgingSlices(monthlyAging);

  return (
    <DonutCard
      centerCaption="Total A/R"
      centerValue={
        useDummyData
          ? '$3.8M'
          : isLoading
            ? '…'
            : formatSellingCompactCurrency(monthlyAging?.summaryTotals?.totalAmount ?? 0, currency)
      }
      icon="pie-chart-outline"
      isDark={isDark}
      slices={slices}
      subtitle="Breakdown of outstanding accounts receivable by age and duration."
      title="A/R Aging Summary"
      onViewDetails={onViewDetails}
    />
  );
}
