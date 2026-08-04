import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import { MyNetworkClient } from '../my-network-client';
import { careConnectApi } from '@/lib/careconnect-api';
import type { NetworkDetail, NetworkProviderItem, ProviderSearchResult, SpecialtyOption } from '@/types/careconnect';

vi.mock('next/dynamic', () => ({
  default: () => {
    function MockDynamicComponent() {
      return <div data-testid="my-network-map" />;
    }
    return MockDynamicComponent;
  },
}));

vi.mock('@/lib/careconnect-api', () => ({
  careConnectApi: {
    networks: {
      create: vi.fn(),
      getMarkers: vi.fn(),
      searchProviders: vi.fn(),
      addProvider: vi.fn(),
      updateProvider: vi.fn(),
      removeProvider: vi.fn(),
    },
  },
}));

const SPECIALTIES: SpecialtyOption[] = [
  {
    id: 'specialty-1',
    name: 'Physical Therapy',
    code: 'PHYSICAL_THERAPY',
    description: null,
    isActive: true,
  },
  {
    id: 'specialty-2',
    name: 'Chiropractors',
    code: 'CHIROPRACTORS',
    description: null,
    isActive: true,
  },
];

const MULTI_SPECIALTIES: SpecialtyOption[] = [
  SPECIALTIES[0],
  SPECIALTIES[1],
  {
    id: 'specialty-3',
    name: 'Pain',
    code: 'PAIN',
    description: null,
    isActive: true,
  },
  {
    id: 'specialty-4',
    name: 'Spine',
    code: 'SPINE',
    description: null,
    isActive: true,
  },
];

const BASE_PROVIDER: NetworkProviderItem = {
  id: 'network-provider-1',
  networkProviderId: 'network-provider-1',
  providerId: 'provider-1',
  facilityId: 'facility-1',
  name: 'Atlas Rehab',
  title: null,
  organizationName: 'Atlas Health',
  facilityName: 'Atlas Health',
  email: 'atlas@example.com',
  phone: '5551234567',
  addressLine1: '123 Main St',
  city: 'Austin',
  state: 'TX',
  postalCode: '78701',
  isActive: true,
  acceptingReferrals: true,
  accessStage: 'PUBLIC',
  specialties: [],
  primarySpecialtyId: null,
  primarySpecialty: null,
  distanceMiles: null,
};

const BASE_SEARCH_RESULT: ProviderSearchResult = {
  id: 'provider-existing',
  facilityId: 'facility-existing',
  name: 'Dr. Jane Smith',
  title: 'Dr.',
  organizationName: 'Smith Family Practice',
  email: 'jane@example.com',
  phone: '5551234567',
  addressLine1: '123 Main St',
  city: 'Chicago',
  state: 'IL',
  postalCode: '60601',
  npi: '1234567890',
  isActive: true,
  acceptingReferrals: true,
  accessStage: 'PUBLIC',
  specialties: [SPECIALTIES[0]],
  primarySpecialtyId: SPECIALTIES[0].id,
  primarySpecialty: SPECIALTIES[0].name,
  distanceMiles: null,
};

function makeNetwork(providers: NetworkProviderItem[] = []): NetworkDetail {
  return {
    id: 'network-1',
    name: 'Preferred Providers',
    description: 'Demo network',
    providers,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z',
  };
}

function ok<T>(data: T) {
  return { data, status: 200, correlationId: 'test-correlation' } as const;
}

function inputFor(label: string): HTMLInputElement {
  const labelNode = screen.getByText(label);
  const input = labelNode.parentElement?.querySelector('input');
  if (!input) throw new Error(`Input not found for ${label}`);
  return input as HTMLInputElement;
}

describe('MyNetworkClient', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  test('renders every assigned specialty in the provider list', () => {
    render(
      <MyNetworkClient
        initialNetwork={makeNetwork([{
          ...BASE_PROVIDER,
          specialties: MULTI_SPECIALTIES,
          primarySpecialtyId: MULTI_SPECIALTIES[0].id,
          primarySpecialty: MULTI_SPECIALTIES[0].name,
        }])}
        fetchError={null}
        specialtyOptions={MULTI_SPECIALTIES}
      />,
    );

    expect(screen.getByText('Physical Therapy')).toBeInTheDocument();
    expect(screen.getByText('Chiropractors')).toBeInTheDocument();
    expect(screen.getByText('Pain')).toBeInTheDocument();
    expect(screen.getByText('Spine')).toBeInTheDocument();
    expect(screen.queryByText(/\+\d+/)).not.toBeInTheDocument();
  });

  test('requires a specialty before creating a provider and submits selected specialty codes', async () => {
    const user = userEvent.setup();
    vi.mocked(careConnectApi.networks.addProvider).mockResolvedValue(ok({
      ...BASE_PROVIDER,
      id: 'network-provider-new',
      networkProviderId: 'network-provider-new',
      providerId: 'provider-new',
      facilityId: 'facility-new',
      name: 'Jane Smith',
      specialties: [SPECIALTIES[0]],
      primarySpecialtyId: SPECIALTIES[0].id,
      primarySpecialty: SPECIALTIES[0].name,
    }));

    render(
      <MyNetworkClient
        initialNetwork={makeNetwork()}
        fetchError={null}
        specialtyOptions={SPECIALTIES}
      />,
    );

    await user.click(screen.getByRole('button', { name: /Add Provider/i }));
    await user.click(screen.getByRole('button', { name: /Not found\? Add new instead/i }));

    await user.type(inputFor('Title'), 'Dr.');
    await user.type(screen.getByPlaceholderText('Jane'), 'Jane');
    await user.type(screen.getByPlaceholderText('Smith'), 'Smith');
    await user.type(screen.getByPlaceholderText('jane@example.com'), 'jane@example.com');
    await user.type(screen.getByPlaceholderText('(555) 555-5555'), '5555555555');
    await user.type(screen.getByPlaceholderText('123 Main St'), '123 Main St');
    await user.type(inputFor('City *'), 'Austin');
    await user.type(screen.getByPlaceholderText('IL'), 'TX');
    await user.type(screen.getByPlaceholderText('60601'), '78701');

    await user.click(screen.getByRole('button', { name: /Add to Registry & My Network/i }));

    expect(await screen.findAllByText('Select at least one specialty.')).not.toHaveLength(0);
    expect(careConnectApi.networks.addProvider).not.toHaveBeenCalled();

    await user.click(screen.getByRole('checkbox', { name: 'Physical Therapy' }));
    await user.click(screen.getByRole('button', { name: /Add to Registry & My Network/i }));

    await waitFor(() => expect(careConnectApi.networks.addProvider).toHaveBeenCalledTimes(1));
    expect(careConnectApi.networks.addProvider).toHaveBeenCalledWith(
      'network-1',
      expect.objectContaining({
        newProvider: expect.objectContaining({
          title: 'Dr.',
          firstName: 'Jane',
          lastName: 'Smith',
          specialtyCodes: ['PHYSICAL_THERAPY'],
          primarySpecialtyCode: 'PHYSICAL_THERAPY',
        }),
      }),
    );
  });

  test('adds a new location for an existing provider through the explicit search flow', async () => {
    const user = userEvent.setup();
    vi.mocked(careConnectApi.networks.searchProviders).mockResolvedValue(ok([BASE_SEARCH_RESULT]));
    vi.mocked(careConnectApi.networks.addProvider).mockResolvedValue(ok({
      ...BASE_PROVIDER,
      id: 'network-provider-location',
      networkProviderId: 'network-provider-location',
      providerId: BASE_SEARCH_RESULT.id,
      facilityId: 'facility-north',
      name: BASE_SEARCH_RESULT.name,
      title: BASE_SEARCH_RESULT.title,
      organizationName: BASE_SEARCH_RESULT.organizationName,
      facilityName: 'Smith Family Practice - North',
      email: 'north@example.com',
      phone: '5552223333',
      addressLine1: '456 Oak Ave',
      city: 'Naperville',
      state: 'IL',
      postalCode: '60540',
      specialties: BASE_SEARCH_RESULT.specialties,
      primarySpecialtyId: BASE_SEARCH_RESULT.primarySpecialtyId,
      primarySpecialty: BASE_SEARCH_RESULT.primarySpecialty,
    }));

    render(
      <MyNetworkClient
        initialNetwork={makeNetwork()}
        fetchError={null}
        specialtyOptions={SPECIALTIES}
      />,
    );

    await user.click(screen.getByRole('button', { name: /Add Provider/i }));
    await user.type(inputFor('Name or organization'), 'Jane Smith');
    await user.click(screen.getByRole('button', { name: /Search Registry/i }));

    expect(await screen.findByText('Dr. Jane Smith')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Add new location/i }));

    const locationInput = inputFor('Location / Facility name *');
    await user.clear(locationInput);
    await user.type(locationInput, 'Smith Family Practice - North');
    await user.clear(screen.getByPlaceholderText('jane@example.com'));
    await user.type(screen.getByPlaceholderText('jane@example.com'), 'north@example.com');
    await user.clear(screen.getByPlaceholderText('(555) 555-5555'));
    await user.type(screen.getByPlaceholderText('(555) 555-5555'), '5552223333');
    await user.type(screen.getByPlaceholderText('123 Main St'), '456 Oak Ave');
    await user.type(inputFor('City *'), 'Naperville');
    await user.type(screen.getByPlaceholderText('IL'), 'IL');
    await user.type(screen.getByPlaceholderText('60601'), '60540');
    await user.click(screen.getByRole('button', { name: /Add Location to My Network/i }));

    await waitFor(() => expect(careConnectApi.networks.addProvider).toHaveBeenCalledTimes(1));
    expect(careConnectApi.networks.addProvider).toHaveBeenCalledWith(
      'network-1',
      expect.objectContaining({
        existingProviderId: BASE_SEARCH_RESULT.id,
        newProvider: expect.objectContaining({
          organizationName: 'Smith Family Practice - North',
          email: 'north@example.com',
          phone: '5552223333',
          addressLine1: '456 Oak Ave',
          city: 'Naperville',
          state: 'IL',
          postalCode: '60540',
        }),
      }),
    );
    const request = vi.mocked(careConnectApi.networks.addProvider).mock.calls[0]?.[1];
    expect(request).toBeDefined();
    expect(request?.existingFacilityId).toBeUndefined();
    expect(request?.newProvider?.specialtyCodes).toBeUndefined();
    expect(request?.newProvider?.npi).toBeUndefined();
  });

  test('requires a specialty before editing a provider and submits selected specialty ids', async () => {
    const user = userEvent.setup();
    vi.mocked(careConnectApi.networks.updateProvider).mockResolvedValue(ok({
      ...BASE_PROVIDER,
      specialties: [SPECIALTIES[1]],
      primarySpecialtyId: SPECIALTIES[1].id,
      primarySpecialty: SPECIALTIES[1].name,
    }));

    render(
      <MyNetworkClient
        initialNetwork={makeNetwork([{ ...BASE_PROVIDER, name: 'Dr. Atlas Rehab', title: 'Dr.' }])}
        fetchError={null}
        specialtyOptions={SPECIALTIES}
      />,
    );

    await user.click(screen.getByTitle('Edit provider'));

    expect(inputFor('Title')).toHaveValue('Dr.');

    await user.click(screen.getByRole('button', { name: /Save Provider/i }));

    expect(await screen.findAllByText('Select at least one specialty.')).not.toHaveLength(0);
    expect(careConnectApi.networks.updateProvider).not.toHaveBeenCalled();

    await user.click(screen.getByRole('checkbox', { name: 'Chiropractors' }));
    await user.click(screen.getByRole('button', { name: /Save Provider/i }));

    await waitFor(() => expect(careConnectApi.networks.updateProvider).toHaveBeenCalledTimes(1));
    expect(careConnectApi.networks.updateProvider).toHaveBeenCalledWith(
      'network-1',
      'network-provider-1',
      expect.objectContaining({
        title: 'Dr.',
        specialtyIds: ['specialty-2'],
      }),
    );
  });

  test('submits backend geo point source after selecting a geocoded address', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [
        {
          displayName: '123 North Usa Drive, Greenland, AR 72701',
          addressLine1: '123 North Usa Drive',
          city: 'Greenland',
          state: 'AR',
          postalCode: '72701',
          latitude: 35.9948,
          longitude: -94.1741,
        },
      ],
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.mocked(careConnectApi.networks.addProvider).mockResolvedValue(ok({
      ...BASE_PROVIDER,
      id: 'network-provider-new',
      networkProviderId: 'network-provider-new',
      providerId: 'provider-new',
      facilityId: 'facility-new',
      name: 'Dr. Test Test',
      specialties: [SPECIALTIES[0]],
      primarySpecialtyId: SPECIALTIES[0].id,
      primarySpecialty: SPECIALTIES[0].name,
    }));

    render(
      <MyNetworkClient
        initialNetwork={makeNetwork()}
        fetchError={null}
        specialtyOptions={SPECIALTIES}
      />,
    );

    await user.click(screen.getByRole('button', { name: /Add Provider/i }));
    await user.click(screen.getByRole('button', { name: /Not found\? Add new instead/i }));

    await user.type(inputFor('Title'), 'Dr.');
    await user.type(screen.getByPlaceholderText('Jane'), 'Test');
    await user.type(screen.getByPlaceholderText('Smith'), 'Test');
    await user.type(screen.getByPlaceholderText('jane@example.com'), 'test@example.com');
    await user.type(screen.getByPlaceholderText('(555) 555-5555'), '5123513513');
    await user.type(screen.getByPlaceholderText('123 Main St'), '123 North Usa Drive');
    await user.click(await screen.findByText('123 North Usa Drive, Greenland, AR 72701'));
    await user.click(screen.getByRole('checkbox', { name: 'Physical Therapy' }));
    await user.click(screen.getByRole('button', { name: /Add to Registry & My Network/i }));

    await waitFor(() => expect(careConnectApi.networks.addProvider).toHaveBeenCalledTimes(1));
    expect(careConnectApi.networks.addProvider).toHaveBeenCalledWith(
      'network-1',
      expect.objectContaining({
        newProvider: expect.objectContaining({
          title: 'Dr.',
          firstName: 'Test',
          addressLine1: '123 North Usa Drive',
          city: 'Greenland',
          state: 'AR',
          postalCode: '72701',
          latitude: 35.9948,
          longitude: -94.1741,
          geoPointSource: 'Geocoded',
        }),
      }),
    );
  });
});
