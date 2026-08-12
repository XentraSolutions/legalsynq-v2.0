import { beforeEach, describe, expect, test, vi } from 'vitest';
import { ServerApiError, serverApi } from '@/lib/server-api-client';
import {
  EMPTY_FUNDING_DASHBOARD,
  emptyOfferedLiensResult,
  getFundingDashboard,
  getOfferedLienDetail,
  getOfferedLiens,
} from './server-api';
import { getOfferedLiensEmptyStateCopy } from './empty-state';

vi.mock('@/lib/server-api-client', async importOriginal => {
  const actual = await importOriginal<typeof import('@/lib/server-api-client')>();
  return {
    ...actual,
    serverApi: {
      get: vi.fn(),
    },
  };
});

const serverGet = vi.mocked(serverApi.get);

describe('SynqLien funding portal server API', () => {
  beforeEach(() => {
    serverGet.mockReset();
  });

  test('returns an empty dashboard when the dashboard endpoint is unavailable', async () => {
    serverGet.mockRejectedValueOnce(new ServerApiError(404, 'HTTP 404'));

    await expect(getFundingDashboard()).resolves.toEqual(EMPTY_FUNDING_DASHBOARD);
  });

  test('returns populated dashboard data unchanged', async () => {
    const dashboard = {
      summary: {
        totalLienPendingCount: 2,
        totalLienPendingAmount: 120000,
        totalPendingOfferCount: 1,
        totalPendingOfferedAmount: 50000,
        purchasedLienCount: 3,
        capitalDeployedAmount: 90000,
        trends: {
          capitalDeployed: { value: 12.5, direction: 'up' as const },
        },
      },
      pendingOffers: [{
        id: 'offer-1',
        lienNumber: 'LN-1',
        providerName: 'Provider',
        sellerName: 'Law Firm',
        offeredAmount: 50000,
        receivedAtUtc: '2026-07-20T00:00:00Z',
        responseDueAtUtc: null,
        status: 'Pending',
      }],
      pipelineStages: [{
        key: 'pending',
        label: 'Pending',
        count: 1,
        totalAmount: 50000,
      }],
      providerPerformance: [{
        providerId: 'provider-1',
        providerName: 'Provider',
        lienCount: 1,
        offeredAmount: 50000,
        acceptedAmount: 0,
      }],
      offerInbox: {
        pendingCount: 1,
        unreadCount: 1,
        latestReceivedAtUtc: '2026-07-20T00:00:00Z',
      },
    };
    serverGet.mockResolvedValueOnce(dashboard);

    await expect(getFundingDashboard({ range: 'last7Days' })).resolves.toEqual(dashboard);
    expect(serverGet).toHaveBeenCalledWith('/liens/api/liens/selling/buyer/dashboard?range=last7Days');
  });

  test('normalizes partial dashboard data without adding rows', async () => {
    serverGet.mockResolvedValueOnce({
      summary: {
        totalPendingOfferCount: 2,
        totalPendingOfferedAmount: 50000,
      },
    });

    await expect(getFundingDashboard()).resolves.toMatchObject({
      summary: {
        totalLienPendingCount: 0,
        totalLienPendingAmount: 0,
        totalPendingOfferCount: 2,
        totalPendingOfferedAmount: 50000,
        purchasedLienCount: 0,
        capitalDeployedAmount: 0,
      },
      pendingOffers: [],
      pipelineStages: [],
      providerPerformance: [],
    });
  });

  test('returns empty offered liens for no-content future endpoint', async () => {
    serverGet.mockResolvedValueOnce(undefined);

    await expect(getOfferedLiens({ status: 'Pending', page: 2, pageSize: 25 }))
      .resolves
      .toEqual(emptyOfferedLiensResult({ status: 'Pending', page: 2, pageSize: 25 }));
  });

  test('normalizes partial offered liens response without adding rows', async () => {
    serverGet.mockResolvedValueOnce({ total: 4 });

    await expect(getOfferedLiens({ page: 2, pageSize: 10 }))
      .resolves
      .toEqual({
        rows: [],
        page: 2,
        pageSize: 10,
        total: 4,
      });
  });

  test('passes offered lien list search, filters, pagination, and sort to the API', async () => {
    serverGet.mockResolvedValueOnce({ rows: [], page: 3, pageSize: 25, total: 0 });

    await getOfferedLiens({
      status: 'Accepted',
      search: 'Xentra',
      page: 3,
      pageSize: 25,
      sort: 'sellerName',
      direction: 'desc',
    });

    expect(serverGet).toHaveBeenCalledWith(
      '/liens/api/liens/selling/buyer/liens?status=Accepted&search=Xentra&page=3&pageSize=25&sort=sellerName&direction=desc',
    );
  });

  test('fetches offered lien detail by authenticated access-link id', async () => {
    const detail = {
      id: 'access-link-1',
      lienId: 'lien-1',
      lienNumber: 'LIEN-1',
      title: 'Seller Operator',
      seller: { name: 'Seller Operator', company: 'Smith & Associates LLP' },
      status: 'Pending',
      submittedAtUtc: '2026-07-28T12:00:00Z',
      billingAmount: 6300,
      documents: [{ id: 'doc-1', fileName: 'signed-lien.pdf', createdAtUtc: '2026-07-28T12:00:00Z' }],
      messages: [],
      activity: [],
    };
    serverGet.mockResolvedValueOnce({ data: detail });

    await expect(getOfferedLienDetail('access-link-1')).resolves.toEqual({
      ...detail,
      buyer: null,
      allowedActions: [],
    });
    expect(serverGet).toHaveBeenCalledWith('/liens/api/liens/selling/buyer/liens/access-link-1');
  });

  test('returns null offered lien detail for missing access-link detail', async () => {
    serverGet.mockRejectedValueOnce(new ServerApiError(404, 'HTTP 404'));

    await expect(getOfferedLienDetail('missing-link')).resolves.toBeNull();
  });

  test('normalizes partial offered lien detail arrays without adding fake rows', async () => {
    serverGet.mockResolvedValueOnce({
      id: 'access-link-2',
      lienId: 'lien-2',
      lienNumber: 'LIEN-2',
      title: 'Seller',
      status: 'Pending',
      submittedAtUtc: '2026-07-28T12:00:00Z',
      billingAmount: 100,
    });

    await expect(getOfferedLienDetail('access-link-2')).resolves.toMatchObject({
      seller: {},
      buyer: null,
      documents: [],
      messages: [],
      activity: [],
      allowedActions: [],
    });
  });

  test('keeps filtered no-results copy distinct from no-data copy', () => {
    expect(getOfferedLiensEmptyStateCopy(false).title).toBe('No offered liens yet');
    expect(getOfferedLiensEmptyStateCopy(true).title).toBe('No results match your filters');
  });
});
