import { describe, expect, test, vi, beforeEach } from 'vitest';

const { redirectMock } = vi.hoisted(() => ({
  redirectMock: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  }),
}));

vi.mock('next/navigation', () => ({
  redirect: redirectMock,
}));

import ReferralViewPage from './page';

function expectRedirectTo(action: () => Promise<unknown>, expectedUrl: string) {
  return expect(action()).rejects.toThrow(`REDIRECT:${expectedUrl}`);
}

describe('ReferralViewPage', () => {
  beforeEach(() => {
    redirectMock.mockClear();
    vi.unstubAllGlobals();
  });

  test('redirects missing tokens to the invalid page', async () => {
    await expectRedirectTo(
      () => ReferralViewPage({ searchParams: Promise.resolve({}) }),
      '/referrals/accept/invalid?reason=missing-token',
    );
  });

  test('routes pending providers into the CareConnect referral detail flow', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ routeType: 'pending', referralId: 'ref-123' }),
    }));

    await expectRedirectTo(
      () => ReferralViewPage({ searchParams: Promise.resolve({ token: 'abc123' }) }),
      '/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-123&reason=referral-view',
    );
  });

  test('routes active providers into the CareConnect referral detail flow', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ routeType: 'active', referralId: 'ref-456' }),
    }));

    await expectRedirectTo(
      () => ReferralViewPage({ searchParams: Promise.resolve({ token: 'xyz789' }) }),
      '/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-456&reason=referral-view',
    );
  });

  test('redirects invalid tokens to the invalid page', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ routeType: 'invalid', referralId: null }),
    }));

    await expectRedirectTo(
      () => ReferralViewPage({ searchParams: Promise.resolve({ token: 'expired' }) }),
      '/referrals/accept/invalid?reason=expired-or-invalid',
    );
  });
});
