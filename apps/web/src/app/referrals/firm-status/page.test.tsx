import { beforeEach, describe, expect, test, vi } from 'vitest';
import { ReferrerPortalAccessStatuses } from '@/types/careconnect';

const {
  redirectMock,
  firmStatusClientMock,
  fetchPublicCareConnectMock,
  createEnrollmentTokenMock,
} = vi.hoisted(() => ({
  redirectMock: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  }),
  firmStatusClientMock: vi.fn(() => null),
  fetchPublicCareConnectMock: vi.fn(),
  createEnrollmentTokenMock: vi.fn(async () => 'enroll-token'),
}));

vi.mock('next/navigation', () => ({
  redirect: redirectMock,
}));

vi.mock('./firm-status-client', () => ({
  FirmStatusClient: firmStatusClientMock,
}));

vi.mock('../lib/public-referral-proxy', () => ({
  fetchPublicCareConnect: fetchPublicCareConnectMock,
}));

vi.mock('@/app/enroll/actions', () => ({
  createEnrollmentToken: createEnrollmentTokenMock,
}));

import FirmStatusPage from './page';

describe('FirmStatusPage', () => {
  beforeEach(() => {
    redirectMock.mockClear();
    firmStatusClientMock.mockClear();
    fetchPublicCareConnectMock.mockReset();
    createEnrollmentTokenMock.mockClear();
  });

  test('uses the public referrer-status endpoint and passes through existing-user-other-tenant', async () => {
    const threadData = {
      referralId: 'ref-123',
      tenantId: 'tenant-1',
      referrerEmail: 'lawyer@example.com',
      referrerName: 'Lawyer User',
      notes: null,
      status: 'New',
      clientName: 'Jane Doe',
      service: 'General Referral',
      providerName: 'Demo Provider',
      createdAt: '2026-06-11T00:00:00Z',
      comments: [],
    };

    fetchPublicCareConnectMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => threadData,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ status: ReferrerPortalAccessStatuses.ExistingUserOtherTenant }),
      });

    const result = await FirmStatusPage({ searchParams: Promise.resolve({ token: 'abc123' }) });

    expect(fetchPublicCareConnectMock).toHaveBeenNthCalledWith(
      1,
      '/api/public/referrals/thread?token=abc123',
    );
    expect(fetchPublicCareConnectMock).toHaveBeenNthCalledWith(
      2,
      '/api/public/referrer-status?email=lawyer%40example.com',
    );
    expect(result).toMatchObject({
      type: firmStatusClientMock,
      props: expect.objectContaining({
        token: 'abc123',
        data: threadData,
        portalAccessStatus: ReferrerPortalAccessStatuses.ExistingUserOtherTenant,
        enrollToken: 'enroll-token',
      }),
    });
  });

  test('includes the firm name and phone in the enrollment token claims from thread data fields', async () => {
    const threadData = {
      referralId: 'ref-123',
      tenantId: 'tenant-1',
      referrerEmail: 'lawyer@example.com',
      referrerName: 'Lawyer User',
      referrerFirmName: 'Demo Law Group',
      referrerPhone: '555-0102',
      notes: null,
      status: 'New',
      clientName: 'Jane Doe',
      service: 'General Referral',
      providerName: 'Demo Provider',
      createdAt: '2026-06-11T00:00:00Z',
      comments: [],
    };

    fetchPublicCareConnectMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => threadData,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ status: ReferrerPortalAccessStatuses.NoAccount }),
      });

    await FirmStatusPage({ searchParams: Promise.resolve({ token: 'abc123' }) });

    expect(createEnrollmentTokenMock).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-1',
      email: 'lawyer@example.com',
      firm: 'Demo Law Group',
      contact: 'Lawyer User',
      phone: '555-0102',
    }));
  });
});
