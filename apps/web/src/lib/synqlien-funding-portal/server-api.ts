import { ServerApiError, serverApi } from '@/lib/server-api-client';
import type {
  FundingDashboardQuery,
  FundingDashboardSummary,
  OfferedLiensQuery,
  OfferedLiensResult,
  SynqLienFundingDashboard,
} from './types';

const DASHBOARD_PATH = '/liens/api/liens/selling/buyer/dashboard';
const OFFERED_LIENS_PATH = '/liens/api/liens/selling/buyer/liens';

export const EMPTY_FUNDING_DASHBOARD: SynqLienFundingDashboard = {
  summary: {
    totalLienPendingCount: 0,
    totalLienPendingAmount: 0,
    totalPendingOfferCount: 0,
    totalPendingOfferedAmount: 0,
    purchasedLienCount: 0,
    capitalDeployedAmount: 0,
    trends: {},
  },
  pendingOffers: [],
  pipelineStages: [],
  providerPerformance: [],
  offerInbox: {
    pendingCount: 0,
    unreadCount: 0,
    latestReceivedAtUtc: null,
  },
};

export function emptyOfferedLiensResult(query: OfferedLiensQuery = {}): OfferedLiensResult {
  return {
    rows: [],
    page: normalizePositiveInteger(query.page, 1),
    pageSize: normalizePositiveInteger(query.pageSize, 10),
    total: 0,
  };
}

export async function getFundingDashboard(
  query: FundingDashboardQuery = {},
): Promise<SynqLienFundingDashboard> {
  try {
    const response = await serverApi.get<SynqLienFundingDashboard | { data: SynqLienFundingDashboard } | undefined>(
      `${DASHBOARD_PATH}${buildQueryString(query)}`,
    );
    return normalizeFundingDashboard(unwrapData(response));
  } catch (error) {
    if (isSemanticEmptyError(error)) return EMPTY_FUNDING_DASHBOARD;
    throw error;
  }
}

export async function getOfferedLiens(
  query: OfferedLiensQuery = {},
): Promise<OfferedLiensResult> {
  try {
    const response = await serverApi.get<OfferedLiensResult | { data: OfferedLiensResult } | undefined>(
      `${OFFERED_LIENS_PATH}${buildQueryString(query)}`,
    );
    return normalizeOfferedLiensResult(unwrapData(response), query);
  } catch (error) {
    if (isSemanticEmptyError(error)) return emptyOfferedLiensResult(query);
    throw error;
  }
}

function isSemanticEmptyError(error: unknown): boolean {
  return error instanceof ServerApiError && (error.status === 404 || error.status === 501);
}

function unwrapData<T>(response: T | { data: T } | undefined): T | undefined {
  if (response && typeof response === 'object' && 'data' in response) {
    return response.data;
  }
  return response;
}

function normalizeFundingDashboard(
  value: SynqLienFundingDashboard | undefined,
): SynqLienFundingDashboard {
  if (!value) return EMPTY_FUNDING_DASHBOARD;

  return {
    summary: normalizeFundingSummary(value.summary),
    pendingOffers: Array.isArray(value.pendingOffers) ? value.pendingOffers : [],
    pipelineStages: Array.isArray(value.pipelineStages) ? value.pipelineStages : [],
    providerPerformance: Array.isArray(value.providerPerformance) ? value.providerPerformance : [],
    offerInbox: value.offerInbox ?? EMPTY_FUNDING_DASHBOARD.offerInbox,
  };
}

function normalizeFundingSummary(
  value: FundingDashboardSummary | undefined,
): FundingDashboardSummary {
  const empty = EMPTY_FUNDING_DASHBOARD.summary;
  return {
    totalLienPendingCount: normalizeNumber(value?.totalLienPendingCount, empty.totalLienPendingCount),
    totalLienPendingAmount: normalizeNumber(value?.totalLienPendingAmount, empty.totalLienPendingAmount),
    totalPendingOfferCount: normalizeNumber(value?.totalPendingOfferCount, empty.totalPendingOfferCount),
    totalPendingOfferedAmount: normalizeNumber(value?.totalPendingOfferedAmount, empty.totalPendingOfferedAmount),
    purchasedLienCount: normalizeNumber(value?.purchasedLienCount, empty.purchasedLienCount),
    capitalDeployedAmount: normalizeNumber(value?.capitalDeployedAmount, empty.capitalDeployedAmount),
    trends: value?.trends ?? empty.trends,
  };
}

function normalizeOfferedLiensResult(
  value: OfferedLiensResult | undefined,
  query: OfferedLiensQuery,
): OfferedLiensResult {
  const empty = emptyOfferedLiensResult(query);
  if (!value) return empty;

  return {
    rows: Array.isArray(value.rows) ? value.rows : [],
    page: normalizePositiveInteger(value.page, empty.page),
    pageSize: normalizePositiveInteger(value.pageSize, empty.pageSize),
    total: normalizePositiveInteger(value.total, empty.total),
  };
}

function normalizeNumber(value: number | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function buildQueryString(query: object): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query) as Array<[string, unknown]>) {
    if ((typeof value === 'string' || typeof value === 'number') && value !== '') {
      params.set(key, String(value));
    }
  }

  const encoded = params.toString();
  return encoded ? `?${encoded}` : '';
}

function normalizePositiveInteger(value: number | undefined, fallback: number): number {
  return value && Number.isFinite(value) && value > 0 ? value : fallback;
}
