import { ServerApiError, serverApi } from '@/lib/server-api-client';
import type {
  FundingDashboardQuery,
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
    return unwrapData(response) ?? EMPTY_FUNDING_DASHBOARD;
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
    return unwrapData(response) ?? emptyOfferedLiensResult(query);
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
