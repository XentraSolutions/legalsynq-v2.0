import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { PublicNetworkView } from '../public-network-view';
import type { PublicNetworkDetail } from '@/lib/public-network-api';

vi.mock('next/dynamic', () => ({
  default: () => {
    function MockDynamicComponent() {
      return <div data-testid="public-network-map" />;
    }
    return MockDynamicComponent;
  },
}));

vi.mock('@/app/enroll/actions', () => ({
  createEnrollmentToken: vi.fn().mockResolvedValue('enroll-token'),
}));

const DETAIL: PublicNetworkDetail = {
  networkId: 'network-1',
  networkName: 'CareConnect Network',
  networkDescription: 'Public provider network',
  providers: [
    {
      id: 'provider-1',
      name: 'Atlas Rehab',
      organizationName: 'Atlas Health',
      phone: '555-123-4567',
      city: 'Austin',
      state: 'TX',
      postalCode: '78701',
      isActive: true,
      acceptingReferrals: true,
      accessStage: 'PUBLIC',
      primaryCategory: 'Physical Therapy',
    },
  ],
  markers: [
    {
      id: 'provider-1',
      name: 'Atlas Rehab',
      organizationName: 'Atlas Health',
      city: 'Austin',
      state: 'TX',
      acceptingReferrals: true,
      latitude: 30.2672,
      longitude: -97.7431,
    },
  ],
};

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('PublicNetworkView', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        return jsonResponse([{ referralId: 'ref-1', providerId: 'provider-1' }]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrer-status')) {
        return jsonResponse({ hasPortalAccess: true });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    }) as typeof fetch;
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  test('uses the provided loginUrl for the existing-portal-access success CTA', async () => {
    const user = userEvent.setup();
    const loginUrl = 'https://demo.careconnect.example.com/login?redirect=%2Freferrals';

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl={loginUrl}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));

    await user.type(screen.getByPlaceholderText('Acme Injury Law'), 'Acme Injury Law');
    await user.type(screen.getByPlaceholderText('intake@firm.example'), 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Jane Doe'), 'Jane Doe');
    const phoneInputs = screen.getAllByPlaceholderText('(555) 555-5555');
    expect(phoneInputs).toHaveLength(2);

    await user.type(phoneInputs[1], '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    expect(dateInputs).toHaveLength(2);

    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    const loginCta = await screen.findByRole('link', { name: 'Login to CareConnect' });

    expect(loginCta).toHaveAttribute('href', loginUrl);
    await waitFor(() =>
      expect(screen.getByText('You already have portal access')).toBeInTheDocument(),
    );
  });
});
