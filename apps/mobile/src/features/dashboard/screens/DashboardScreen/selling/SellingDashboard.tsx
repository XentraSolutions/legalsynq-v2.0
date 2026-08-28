import type { DashboardDateRange } from '@/features/dashboard/types/types';
import { useMonthlyAgingReport, useSellingDashboardAnalytics } from '@/features/dashboard/hooks';
import { DashboardReportState } from '../DashboardReportState';
import { SellingDashboardMetricGrid } from './SellingDashboardMetricGrid';
import { SellingDashboardAgingSummary } from './SellingDashboardAgingSummary';
import { SellingDashboardStatusSummary } from './SellingDashboardStatusSummary';
import { SellingDashboardTimeSeries } from './SellingDashboardTimeSeries';
import { SellingDashboardTopBuyers } from './SellingDashboardTopBuyers';
import { SellingDashboardBuyerAging } from './SellingDashboardBuyerAging';
import { resolveSellingAgingAsOfDate } from './sellingDashboardFormatters';

export function SellingDashboard({
  dashboardSettingsHydrated,
  dateRange,
  isDark,
  useDummyData,
  onViewAgingDetails,
}: {
  dashboardSettingsHydrated: boolean;
  dateRange: DashboardDateRange;
  isDark: boolean;
  useDummyData: boolean;
  onViewAgingDetails: (asOfDate: string) => void;
}) {
  const query = useSellingDashboardAnalytics(dateRange, dashboardSettingsHydrated && !useDummyData);
  const data = query.data;
  const agingAsOfDate = resolveSellingAgingAsOfDate(dateRange.endDate);
  const monthlyAgingQuery = useMonthlyAgingReport(
    agingAsOfDate,
    1,
    10,
    dashboardSettingsHydrated && !useDummyData
  );

  if (!useDummyData && (!dashboardSettingsHydrated || (!data && query.isFetching))) {
    return (
      <DashboardReportState
        isDark={isDark}
        isError={false}
        isLoading
        legendRows={5}
        title="Selling dashboard"
        onRetry={() => undefined}
      >
        {null}
      </DashboardReportState>
    );
  }

  if (!useDummyData && !data && query.isError) {
    return (
      <DashboardReportState
        errorMessage={query.error instanceof Error ? query.error.message : undefined}
        isDark={isDark}
        isError
        isLoading={false}
        legendRows={5}
        title="Selling dashboard"
        onRetry={() => {
          void query.refetch();
        }}
      >
        {null}
      </DashboardReportState>
    );
  }

  if (!useDummyData && !data) return null;

  return (
    <>
      <SellingDashboardMetricGrid data={data} isDark={isDark} useDummyData={useDummyData} />
      <SellingDashboardAgingSummary
        isDark={isDark}
        isError={monthlyAgingQuery.isError}
        isLoading={monthlyAgingQuery.isFetching}
        monthlyAging={monthlyAgingQuery.data}
        useDummyData={useDummyData}
        onViewDetails={() => onViewAgingDetails(agingAsOfDate)}
      />
      <SellingDashboardStatusSummary data={data} isDark={isDark} useDummyData={useDummyData} />
      <SellingDashboardTimeSeries data={data} isDark={isDark} useDummyData={useDummyData} />
      <SellingDashboardTopBuyers data={data} isDark={isDark} useDummyData={useDummyData} />
      <SellingDashboardBuyerAging data={data} isDark={isDark} useDummyData={useDummyData} />
    </>
  );
}
