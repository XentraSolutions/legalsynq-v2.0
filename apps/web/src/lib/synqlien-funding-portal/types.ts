export type FundingDashboardRange = 'last7Days' | 'last30Days' | 'custom';

export type FundingMetricKey =
  | 'totalLienPending'
  | 'totalPendingOffered'
  | 'purchasedLiens'
  | 'capitalDeployed';

export type FundingTrendDirection = 'up' | 'down' | 'flat';

export interface FundingMetricTrend {
  value: number;
  direction: FundingTrendDirection;
  label?: string | null;
}

export interface FundingDashboardSummary {
  totalLienPendingCount: number;
  totalLienPendingAmount: number;
  totalPendingOfferCount: number;
  totalPendingOfferedAmount: number;
  purchasedLienCount: number;
  capitalDeployedAmount: number;
  trends?: Partial<Record<FundingMetricKey, FundingMetricTrend | null>>;
}

export interface PendingFundingOfferRow {
  id: string;
  lienNumber: string;
  providerName: string;
  sellerName: string;
  offeredAmount: number;
  receivedAtUtc: string;
  responseDueAtUtc?: string | null;
  status: string;
  detailHref?: string | null;
}

export interface AcquisitionPipelineStage {
  key: string;
  label: string;
  count: number;
  totalAmount: number;
  conversionRatePercent?: number | null;
}

export interface ProviderPerformanceRow {
  providerId: string;
  providerName: string;
  lienCount: number;
  offeredAmount: number;
  acceptedAmount: number;
  averageResponseHours?: number | null;
}

export interface OfferInboxSummary {
  pendingCount: number;
  unreadCount?: number | null;
  latestReceivedAtUtc?: string | null;
}

export interface SynqLienFundingDashboard {
  summary: FundingDashboardSummary;
  pendingOffers: PendingFundingOfferRow[];
  pipelineStages: AcquisitionPipelineStage[];
  providerPerformance: ProviderPerformanceRow[];
  offerInbox?: OfferInboxSummary | null;
}

export interface FundingDashboardQuery {
  range?: FundingDashboardRange;
  from?: string;
  to?: string;
}

export type OfferedLienAction = 'view' | 'accept' | 'decline' | 'counter';

export interface OfferedLienRow {
  id: string;
  lienNumber: string;
  providerName: string;
  sellerName: string;
  initialServiceDate?: string | null;
  serviceDate?: string | null;
  billingAmount?: number | null;
  originalAmount?: number | null;
  askAmount?: number | null;
  highestBidAmount?: number | null;
  highestBid?: number | null;
  offeredAmount: number;
  receivedAtUtc: string;
  status: string;
  responseDueAtUtc?: string | null;
  allowedActions?: OfferedLienAction[];
  detailHref?: string | null;
}

export interface OfferedLiensQuery {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface OfferedLiensResult {
  rows: OfferedLienRow[];
  page: number;
  pageSize: number;
  total: number;
}
