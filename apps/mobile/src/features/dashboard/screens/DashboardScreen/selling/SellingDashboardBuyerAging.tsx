import type { SellingDashboardAnalyticsResponse } from '@/shared/api/endpoints/Liens';
import { AgingSellerCard } from './AgingSellerCard';
import { DashboardEmptyStateCard } from '../DashboardEmptyStateCard';

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

  return (
    <DashboardEmptyStateCard
      isDark={isDark}
      message={data?.buyerAging.unavailableReason ?? 'Buyer aging data is unavailable.'}
      title="Aging By Lien Buyer"
    />
  );
}
