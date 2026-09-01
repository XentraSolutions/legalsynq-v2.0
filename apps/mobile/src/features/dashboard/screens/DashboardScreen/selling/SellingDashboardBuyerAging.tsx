import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { AgingSellerCard } from './AgingSellerCard';
import { DashboardEmptyStateCard } from '../DashboardEmptyStateCard';
import {
  formatSellingAgingPeriod,
  formatSellingCurrency,
  visibleSellingAgingBuckets,
} from './sellingDashboardFormatters';
import type { SellerRisk } from './sellingDashboardData';

export function SellingDashboardBuyerAging({
  data,
  isDark,
  useDummyData,
}: {
  data?: SellingDashboardAnalyticsResponse;
  isDark: boolean;
  useDummyData: boolean;
}) {
  if (useDummyData) return <AgingSellerCard isDark={isDark} />;

  if (data?.buyerAging.isAvailable && data.buyerAging.items.length > 0) {
    const currency = data.currency ?? 'USD';
    const sellers: SellerRisk[] = data.buyerAging.items.map((buyer) => ({
      name: buyer.buyerName,
      balance: formatSellingCurrency(buyer.total, currency),
      share: buyer.pastDuePercent == null ? '—' : `${buyer.pastDuePercent.toFixed(1)}% past due`,
      risk: (buyer.pastDuePercent ?? 0) >= 50 ? 'High' : 'Medium',
      rows: visibleSellingAgingBuckets(buyer.buckets).map((bucket) => ({
        label: `${formatSellingAgingPeriod(bucket.bucket)}:`,
        value: `${formatSellingCurrency(bucket.amount, currency)} · ${bucket.lienCount.toLocaleString()} liens`,
      })),
    }));

    return <AgingSellerCard isDark={isDark} sellers={sellers} />;
  }

  return (
    <DashboardEmptyStateCard
      isDark={isDark}
      message={data?.buyerAging.unavailableReason ?? 'Buyer aging data is unavailable.'}
      title="Aging By Lien Buyer"
    />
  );
}
