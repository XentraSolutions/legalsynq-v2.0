import { describe, expect, test, vi, beforeEach } from 'vitest';

const { redirectMock, headersMock } = vi.hoisted(() => ({
  redirectMock: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  }),
  headersMock: vi.fn(async () => new Headers([
    ['host', 'rl-liens1.legalsynq.net'],
    ['x-forwarded-proto', 'https'],
  ])),
}));

vi.mock('next/navigation', () => ({
  redirect: redirectMock,
}));

vi.mock('next/headers', () => ({
  headers: headersMock,
}));

import ReferralAcceptPage from './page';

function expectRedirectTo(action: () => Promise<unknown>, expectedUrl: string) {
  return expect(action()).rejects.toThrow(`REDIRECT:${expectedUrl}`);
}

describe('ReferralAcceptPage', () => {
  beforeEach(() => {
    redirectMock.mockClear();
    headersMock.mockClear();
    vi.unstubAllGlobals();
  });

  test('does not forward invalid legacy accept links to the referral thread page', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({ reason: 'malformed' }),
    }));

    await expectRedirectTo(
      () => ReferralAcceptPage({
        params: Promise.resolve({ referralId: 'ref-123' }),
        searchParams: Promise.resolve({ token: 'abc123' }),
      }),
      '/referrals/accept/invalid?reason=expired-or-invalid',
    );
  });
});
