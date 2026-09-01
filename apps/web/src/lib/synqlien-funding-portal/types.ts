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
  sellerCompany?: string | null;
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

export type OfferedLiensSortKey =
  | 'lienNumber'
  | 'sellerName'
  | 'initialServiceDate'
  | 'billingAmount'
  | 'askAmount'
  | 'highestBidAmount'
  | 'status';

export type OfferedLiensSortDirection = 'asc' | 'desc';

export interface OfferedLiensQuery {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sort?: OfferedLiensSortKey;
  direction?: OfferedLiensSortDirection;
}

export interface OfferedLiensResult {
  rows: OfferedLienRow[];
  page: number;
  pageSize: number;
  total: number;
}

export interface OfferedLienPartyDetail {
  name?: string | null;
  contactName?: string | null;
  company?: string | null;
  email?: string | null;
  phone?: string | null;
}

export interface OfferedLienDocument {
  id: string;
  fileName: string;
  category?: string | null;
  sizeOrType?: string | null;
  url?: string | null;
  viewUrl?: string | null;
  downloadUrl?: string | null;
  createdAtUtc: string;
}

export interface OfferedLienMessage {
  id: string;
  senderType: string;
  senderName: string;
  senderInitials?: string | null;
  senderEmail?: string | null;
  message: string;
  createdAtUtc: string;
  isCurrentUser?: boolean;
  attachments?: OfferedLienMessageAttachment[];
}

export interface OfferedLienMessageAttachment {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  createdAtUtc: string;
  viewUrl?: string | null;
  downloadUrl?: string | null;
}

export interface OfferedLienActivityItem {
  id: string;
  label: string;
  occurredAtUtc: string;
  notes?: string | null;
}

export interface OfferedLienDetail {
  id: string;
  lienId: string;
  lienNumber: string;
  title: string;
  subtitle?: string | null;
  seller: OfferedLienPartyDetail;
  buyer?: OfferedLienPartyDetail | null;
  providerName?: string | null;
  status: string;
  submittedAtUtc: string;
  initialServiceDate?: string | null;
  endServiceDate?: string | null;
  billingAmount: number;
  askAmount?: number | null;
  highestBidAmount?: number | null;
  responseAmount?: number | null;
  notes?: string | null;
  responseDueAtUtc?: string | null;
  responseStatus?: string | null;
  responseNotes?: string | null;
  respondedAtUtc?: string | null;
  allowedActions?: OfferedLienAction[];
  documents: OfferedLienDocument[];
  messages: OfferedLienMessage[];
  activity: OfferedLienActivityItem[];
}
