import { SELLING_STATS, SELLING_AGING, SELLING_STATUS } from './index';
import { StatGrid } from './StatGrid';
import { DashboardEmptyStateCard } from './DashboardEmptyStateCard';
import { DonutCard } from './DonutCard';
import { LineChartCard } from './LineChartCard';
import { TopBalanceCard } from './TopBalanceCard';
import { AgingSellerCard } from './AgingSellerCard';

export function SellingDashboard({ isDark, useDummyData }: { isDark: boolean; useDummyData: boolean }) {
  if (!useDummyData) {
    return (
      <DashboardEmptyStateCard
        isDark={isDark}
        message="Selling report data is not available from the API yet."
        title="No selling report data"
      />
    );
  }

  return (
    <>
      <StatGrid isDark={isDark} stats={SELLING_STATS} />
      <DonutCard
        centerCaption="Total A/R"
        centerValue="$3.8M"
        icon="pie-chart-outline"
        isDark={isDark}
        slices={SELLING_AGING}
        subtitle="Breakdown of outstanding accounts receivable by age and duration."
        title="A/R Aging Summary"
      />
      <DonutCard
        centerCaption="Total Liens"
        centerValue="1,248"
        icon="pie-chart-outline"
        isDark={isDark}
        slices={SELLING_STATUS}
        subtitle="Breakdown of total case liens by their current operational status."
        title="Liens by Status"
      />
      <LineChartCard isDark={isDark} />
      <TopBalanceCard isDark={isDark} />
      <AgingSellerCard isDark={isDark} />
    </>
  );
}
