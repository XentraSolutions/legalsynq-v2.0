import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { PublicNetworkView } from '../public-network-view';
import type { PublicNetworkDetail } from '@/lib/public-network-api';
import type { PrefillLawFirm } from '../public-network-view';

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
  specialties: [
    {
      id: 'specialty-1',
      name: 'Physical Therapy',
      code: 'PHYSICAL_THERAPY',
      description: null,
      isActive: true,
    },
  ],
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
      specialties: [
        {
          id: 'specialty-1',
          name: 'Physical Therapy',
          code: 'PHYSICAL_THERAPY',
          description: null,
          isActive: true,
        },
      ],
      primarySpecialtyId: 'specialty-1',
      primarySpecialty: 'Physical Therapy',
      distanceMiles: null,
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
      specialties: [
        {
          id: 'specialty-1',
          name: 'Physical Therapy',
          code: 'PHYSICAL_THERAPY',
          description: null,
          isActive: true,
        },
      ],
      primarySpecialtyId: 'specialty-1',
      primarySpecialty: 'Physical Therapy',
      distanceMiles: null,
    },
  ],
};

const CHIRO_SPECIALTY = {
  id: 'specialty-2',
  name: 'Chiropractors',
  code: 'CHIROPRACTORS',
  description: null,
  isActive: true,
};

const MULTI_PROVIDER_DETAIL: PublicNetworkDetail = {
  ...DETAIL,
  specialties: [...DETAIL.specialties, CHIRO_SPECIALTY],
  providers: [
    ...DETAIL.providers,
    {
      id: 'provider-2',
      name: 'Bright Spine',
      organizationName: 'Bright Spine Clinic',
      phone: '555-987-6543',
      city: 'Los Angeles',
      state: 'CA',
      postalCode: '90012',
      isActive: true,
      acceptingReferrals: true,
      accessStage: 'PUBLIC',
      primaryCategory: null,
      specialties: [CHIRO_SPECIALTY],
      primarySpecialtyId: CHIRO_SPECIALTY.id,
      primarySpecialty: CHIRO_SPECIALTY.name,
      distanceMiles: null,
    },
  ],
  markers: [
    ...DETAIL.markers,
    {
      id: 'provider-2',
      name: 'Bright Spine',
      organizationName: 'Bright Spine Clinic',
      city: 'Los Angeles',
      state: 'CA',
      acceptingReferrals: true,
      latitude: 34.0522,
      longitude: -118.2437,
      specialties: [CHIRO_SPECIALTY],
      primarySpecialtyId: CHIRO_SPECIALTY.id,
      primarySpecialty: CHIRO_SPECIALTY.name,
      distanceMiles: null,
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
  const authenticatedLawFirm: PrefillLawFirm = {
    firmName: 'Acme Injury Law',
    email: 'intake@firm.example',
    contactName: 'Jane Intake',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        return jsonResponse({ referralId: 'ref-1', providerId: 'provider-1' });
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

  test('shows the organization name above the provider name in provider cards', () => {
    render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    const organizationName = screen.getByText('Atlas Health');
    const providerName = screen.getByText('Atlas Rehab');

    expect(
      organizationName.compareDocumentPosition(providerName) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
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

    await user.type(screen.getByPlaceholderText('Enter firm name'), 'Acme Injury Law');
    await user.type(screen.getAllByPlaceholderText('Enter email address')[0], 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    const phoneInputs = screen.getAllByPlaceholderText('Enter 10-digit phone number');
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

  test('public referral flow sends General Referral when no specific service is selected', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        const body = JSON.parse(String(init?.body ?? '{}')) as { serviceType?: string };
        expect(body.serviceType).toBe('General Referral');
        return jsonResponse({ referralId: 'ref-1', providerId: 'provider-1' });
      }

      if (url.includes('/api/public/careconnect/api/public/referrer-status')) {
        return jsonResponse({ hasPortalAccess: false });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    });
    global.fetch = fetchMock as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));

    await user.type(screen.getByPlaceholderText('Enter firm name'), 'Acme Injury Law');
    await user.type(screen.getAllByPlaceholderText('Enter email address')[0], 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    await user.type(screen.getAllByPlaceholderText('Enter 10-digit phone number')[1], '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    expect(dateInputs).toHaveLength(2);
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/public/careconnect/api/public/referrals',
        expect.objectContaining({ method: 'POST' }),
      ),
    );
  });

  test('public referral flow sends split contact first/last name as senderFirstName/senderLastName', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        const body = JSON.parse(String(init?.body ?? '{}')) as {
          senderFirstName?: string;
          senderLastName?: string;
        };
        expect(body.senderFirstName).toBe('Pat');
        expect(body.senderLastName).toBe('Paralegal');
        return jsonResponse({ referralId: 'ref-1', providerId: 'provider-1' });
      }

      if (url.includes('/api/public/careconnect/api/public/referrer-status')) {
        return jsonResponse({ hasPortalAccess: false });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    });
    global.fetch = fetchMock as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));

    await user.type(screen.getByPlaceholderText('Enter firm name'), 'Acme Injury Law');
    await user.type(screen.getByPlaceholderText('Enter first name'), 'Pat');
    await user.type(screen.getByPlaceholderText('Enter last name'), 'Paralegal');
    await user.type(screen.getAllByPlaceholderText('Enter email address')[0], 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    await user.type(screen.getAllByPlaceholderText('Enter 10-digit phone number')[1], '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/public/careconnect/api/public/referrals',
        expect.objectContaining({ method: 'POST' }),
      ),
    );
  });

  test('public referral flow falls back senderFirstName to the firm name when contact name is left blank', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        const body = JSON.parse(String(init?.body ?? '{}')) as {
          senderFirstName?: string;
          senderLastName?: string;
        };
        expect(body.senderFirstName).toBe('Acme Injury Law');
        expect(body.senderLastName).toBeUndefined();
        return jsonResponse({ referralId: 'ref-1', providerId: 'provider-1' });
      }

      if (url.includes('/api/public/careconnect/api/public/referrer-status')) {
        return jsonResponse({ hasPortalAccess: false });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    });
    global.fetch = fetchMock as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));

    // Contact first/last name left blank — senderFirstName should fall back to the firm name.
    await user.type(screen.getByPlaceholderText('Enter firm name'), 'Acme Injury Law');
    await user.type(screen.getAllByPlaceholderText('Enter email address')[0], 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    await user.type(screen.getAllByPlaceholderText('Enter 10-digit phone number')[1], '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/public/careconnect/api/public/referrals',
        expect.objectContaining({ method: 'POST' }),
      ),
    );
  });

  test('public referral flow sends patient first/last name as separate fields', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/public/careconnect/api/public/referrals')) {
        const body = JSON.parse(String(init?.body ?? '{}')) as {
          patientFirstName?: string;
          patientLastName?: string;
        };
        expect(body.patientFirstName).toBe('Prince');
        expect(body.patientLastName).toBe('Rogers');
        return jsonResponse({ referralId: 'ref-1', providerId: 'provider-1' });
      }

      if (url.includes('/api/public/careconnect/api/public/referrer-status')) {
        return jsonResponse({ hasPortalAccess: false });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    });
    global.fetch = fetchMock as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));

    await user.type(screen.getByPlaceholderText('Enter firm name'), 'Acme Injury Law');
    await user.type(screen.getAllByPlaceholderText('Enter email address')[0], 'intake@firm.example');
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Prince');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Rogers');
    await user.type(screen.getAllByPlaceholderText('Enter 10-digit phone number')[1], '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    expect(dateInputs).toHaveLength(2);
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/public/careconnect/api/public/referrals',
        expect.objectContaining({ method: 'POST' }),
      ),
    );
  });

  test('authenticated referral flow shows an error when document upload fails after referral creation', async () => {
    const user = userEvent.setup();

    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);

      if (url.includes('/api/auth/me')) {
        return jsonResponse({ userId: 'user-1' });
      }

      if (url.includes('/api/careconnect/api/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/careconnect/api/referrals') && !url.includes('/attachments/upload')) {
        return jsonResponse({ id: 'ref-1', providerId: 'provider-1' });
      }

      if (url.includes('/api/careconnect/api/referrals/ref-1/attachments/upload')) {
        return new Response(JSON.stringify({ detail: 'Forbidden upload for test.' }), {
          status: 403,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    }) as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
        prefillLawFirm={authenticatedLawFirm}
        referrerScopeSignature="signed-scope"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    await user.type(screen.getByPlaceholderText('Enter 10-digit phone number'), '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    expect(dateInputs).toHaveLength(2);
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement | null;
    expect(fileInput).not.toBeNull();
    await user.upload(fileInput!, new File(['test'], 'records.pdf', { type: 'application/pdf' }));

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(screen.getByText('Submission failed')).toBeInTheDocument(),
    );
    expect(screen.getByText(/Referral created, but the document upload failed:/)).toBeInTheDocument();
    expect(screen.queryByText('Referral Sent!')).not.toBeInTheDocument();
  });

  test('authenticated referral flow uses the referral dto id when building the upload URL', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);

      if (url.includes('/api/auth/me')) {
        return jsonResponse({ userId: 'user-1' });
      }

      if (url.includes('/api/careconnect/api/treatment-types')) {
        return jsonResponse([]);
      }

      if (url.includes('/api/careconnect/api/referrals') && !url.includes('/attachments/upload')) {
        return jsonResponse({ id: 'ref-42', providerId: 'provider-1' });
      }

      if (url.includes('/api/careconnect/api/referrals/ref-42/attachments/upload')) {
        return jsonResponse({ id: 'att-1' });
      }

      throw new Error(`Unhandled fetch in test: ${url}`);
    });
    global.fetch = fetchMock as typeof fetch;

    const { container } = render(
      <PublicNetworkView
        detail={DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
        prefillLawFirm={authenticatedLawFirm}
        referrerScopeSignature="signed-scope"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Select provider' }));
    await user.type(screen.getByPlaceholderText('Enter patient first name'), 'Jane');
    await user.type(screen.getByPlaceholderText('Enter patient last name'), 'Doe');
    await user.type(screen.getByPlaceholderText('Enter 10-digit phone number'), '5555555555');

    const dateInputs = container.querySelectorAll('input[type="date"]');
    fireEvent.change(dateInputs[0], { target: { value: '1990-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2024-01-15' } });

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement | null;
    expect(fileInput).not.toBeNull();
    await user.upload(fileInput!, new File(['test'], 'records.pdf', { type: 'application/pdf' }));

    await user.click(screen.getByRole('button', { name: 'Send Referral' }));
    await user.click(await screen.findByRole('button', { name: 'Confirm & Send' }));

    await waitFor(() =>
      expect(screen.getByText('Referral Sent!')).toBeInTheDocument(),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/careconnect/api/referrals/ref-42/attachments/upload'),
      expect.objectContaining({ method: 'POST' }),
    );
  });

  test('filters provider cards by specialty without reloading the page', async () => {
    const user = userEvent.setup();

    render(
      <PublicNetworkView
        detail={MULTI_PROVIDER_DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    expect(screen.getByText('Atlas Rehab')).toBeInTheDocument();
    expect(screen.getByText('Bright Spine')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Specialty'), 'CHIROPRACTORS');

    expect(screen.queryByText('Atlas Rehab')).not.toBeInTheDocument();
    expect(screen.getByText('Bright Spine')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Specialty'), '');

    expect(screen.getByText('Atlas Rehab')).toBeInTheDocument();
    expect(screen.getByText('Bright Spine')).toBeInTheDocument();
  });

  test('geocodes ZIP, sorts by distance, displays miles, and clears filters', async () => {
    const user = userEvent.setup();
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/api/geocode/address')) {
        return jsonResponse([{ latitude: 34.0522, longitude: -118.2437 }]);
      }
      if (url.includes('/api/public/careconnect/api/public/treatment-types')) {
        return jsonResponse([]);
      }
      throw new Error(`Unhandled fetch in test: ${url}`);
    }) as typeof fetch;

    render(
      <PublicNetworkView
        detail={MULTI_PROVIDER_DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.type(screen.getByLabelText('ZIP code'), '90012');
    await user.click(screen.getByRole('button', { name: 'Apply ZIP' }));

    await waitFor(() => expect(screen.getByText('0.0 mi away')).toBeInTheDocument());

    const bright = screen.getByText('Bright Spine');
    const atlas = screen.getByText('Atlas Rehab');
    expect(bright.compareDocumentPosition(atlas) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await user.selectOptions(screen.getByLabelText('Specialty'), 'CHIROPRACTORS');
    expect(screen.queryByText('Atlas Rehab')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Clear' }));
    expect(screen.getByLabelText('ZIP code')).toHaveValue('');
    expect(screen.getByLabelText('Specialty')).toHaveValue('');
    expect(screen.getByText('Atlas Rehab')).toBeInTheDocument();
  });

  test('shows the no-results state when no provider matches specialty and text filters', async () => {
    const user = userEvent.setup();

    render(
      <PublicNetworkView
        detail={MULTI_PROVIDER_DETAIL}
        tenantCode="demo"
        tenantId="tenant-1"
        loginUrl="https://demo.careconnect.example.com/login"
      />,
    );

    await user.selectOptions(screen.getByLabelText('Specialty'), 'CHIROPRACTORS');
    await user.type(screen.getByPlaceholderText('Search by name, location, or specialty…'), 'atlas');

    expect(screen.getByText('No providers found.')).toBeInTheDocument();
  });
});
