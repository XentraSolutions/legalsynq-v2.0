import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { SELLING_TOP_BALANCES } from './sellingDashboardData';
import { TopBalanceCard } from './TopBalanceCard';
import { formatSellingCurrency } from './sellingDashboardFormatters';

const MARKS = ['pie', 'cube', 'wave', 'bars', 'v'];

export function SellingDashboardTopBuyers({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  const currency = data?.currency ?? 'USD';
  const items = useDummyData
    ? SELLING_TOP_BALANCES
    : (data?.topBuyers ?? []).slice(0, 5).map((buyer, index) => ({
        name: buyer.buyerName,
        subtitle: `Active Liens: ${buyer.activeLienCount.toLocaleString()}`,
        balance: formatSellingCurrency(buyer.totalBalance, currency),
        share: `${buyer.percentOfTotalBalance.toFixed(1)}%`,
        mark: MARKS[index % MARKS.length],
      }));

  return <TopBalanceCard isDark={isDark} items={items} />;
}
