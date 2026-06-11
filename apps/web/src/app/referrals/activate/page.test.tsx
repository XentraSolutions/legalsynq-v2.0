import { describe, expect, test, vi, beforeEach } from 'vitest';

const { redirectMock, enrollmentFormMock, headersMock } = vi.hoisted(() => ({
  redirectMock: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  }),
  enrollmentFormMock: vi.fn(() => null),
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

vi.mock('@/app/enroll/enrollment-form', () => ({
  EnrollmentForm: enrollmentFormMock,
}));

import ActivatePage from './page';

function expectRedirectTo(action: () => Promise<unknown>, expectedUrl: string) {
  return expect(action()).rejects.toThrow(`REDIRECT:${expectedUrl}`);
}

function findElementByType(node: unknown, type: unknown): { props?: Record<string, unknown> } | null {
  if (!node || typeof node !== 'object') return null;
  const element = node as { type?: unknown; props?: { children?: unknown } };
  if (element.type === type) return element as { props?: Record<string, unknown> };

  const children = element.props?.children;
  if (Array.isArray(children)) {
    for (const child of children) {
      const found = findElementByType(child, type);
      if (found) return found;
    }
  } else {
    return findElementByType(children, type);
  }

  return null;
}

describe('ActivatePage', () => {
  beforeEach(() => {
    redirectMock.mockClear();
    enrollmentFormMock.mockClear();
    headersMock.mockClear();
    vi.unstubAllGlobals();
  });

  test('validates activation through the public referral thread endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        referralId: 'ref-123',
        tenantId: 'tenant-123',
        providerId: 'provider-123',
        status: 'New',
        clientName: 'Jane Doe',
        service: 'Physical Therapy',
        providerName: 'Demo Provider',
        providerEmail: 'provider@example.com',
        providerPhone: '555-0101',
        providerAddressLine1: '123 Main',
        providerCity: 'Las Vegas',
        providerState: 'NV',
        providerPostalCode: '89101',
        referrerName: 'Demo Firm',
      }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const result = await ActivatePage({
      searchParams: Promise.resolve({ referralId: 'ref-123', token: 'abc123' }),
    });

    expect(redirectMock).not.toHaveBeenCalled();
    expect(String(fetchMock.mock.calls[0][0])).toContain(
      '/api/public/careconnect/api/public/referrals/thread?token=abc123',
    );
    const enrollmentForm = findElementByType(result, enrollmentFormMock);
    expect(enrollmentForm?.props).toEqual(
      expect.objectContaining({
        providerId: 'provider-123',
        tenantId: 'tenant-123',
        prefill: {
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '555-0101',
          addressLine1: '123 Main',
          city: 'Las Vegas',
          state: 'NV',
          postalCode: '89101',
        },
      }),
    );
  });

  test('rejects activation when the token resolves to a different referral', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        referralId: 'different-referral',
        tenantId: 'tenant-123',
        providerId: 'provider-123',
        status: 'New',
        clientName: 'Jane Doe',
        service: 'Physical Therapy',
        providerName: 'Demo Provider',
        referrerName: 'Demo Firm',
      }),
    }));

    await expectRedirectTo(
      () => ActivatePage({
        searchParams: Promise.resolve({ referralId: 'ref-123', token: 'abc123' }),
      }),
      '/referrals/accept/invalid?reason=expired-or-invalid',
    );
  });
});
