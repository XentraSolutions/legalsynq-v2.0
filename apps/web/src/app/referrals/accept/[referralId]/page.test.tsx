import { beforeEach, describe, expect, test, vi } from 'vitest';

const { redirectMock, activationLandingMock, headersMock } = vi.hoisted(() => ({
  redirectMock: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  }),
  activationLandingMock: vi.fn(() => null),
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

vi.mock('./activation-landing', () => ({
  ActivationLanding: activationLandingMock,
}));

import ReferralAcceptPage from './page';

describe('ReferralAcceptPage', () => {
  beforeEach(() => {
    redirectMock.mockClear();
    activationLandingMock.mockClear();
    headersMock.mockClear();
    vi.unstubAllGlobals();
  });

  test('passes providerHasAccount through to the activation landing', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        referralId: 'ref-123',
        clientFirstName: 'Jane',
        clientLastName: 'Doe',
        referrerName: 'Demo Firm',
        providerName: 'Demo Provider',
        providerPhone: '555-0101',
        providerEmail: 'provider@example.com',
        providerAddressLine1: '123 Main',
        providerCity: 'Las Vegas',
        providerState: 'NV',
        providerPostalCode: '89101',
        requestedService: 'Physical Therapy',
        status: 'New',
        isAlreadyAccepted: false,
        providerHasAccount: false,
        attachments: [],
      }),
    }));

    const result = await ReferralAcceptPage({
      params: Promise.resolve({ referralId: 'ref-123' }),
      searchParams: Promise.resolve({ token: 'abc123' }),
    });

    expect(redirectMock).not.toHaveBeenCalled();
    expect(result).toMatchObject({
      type: activationLandingMock,
      props: expect.objectContaining({
        referralId: 'ref-123',
        token: 'abc123',
        summary: expect.objectContaining({
          providerHasAccount: false,
        }),
      }),
    });
  });
});
