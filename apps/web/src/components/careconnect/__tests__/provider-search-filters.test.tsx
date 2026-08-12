import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import { ProviderSearchFilters } from '../provider-search-filters';
import { careConnectApi } from '@/lib/careconnect-api';
import type { SpecialtyOption } from '@/types/careconnect';

const pushMock = vi.fn();
const replaceMock = vi.fn();
let currentSearch = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock, replace: replaceMock }),
  usePathname: () => '/careconnect/providers',
  useSearchParams: () => new URLSearchParams(currentSearch),
}));

vi.mock('@/lib/careconnect-api', () => ({
  careConnectApi: {
    specialties: {
      list: vi.fn(),
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

function ok<T>(data: T) {
  return { data, status: 200, correlationId: 'test-correlation' } as const;
}

describe('ProviderSearchFilters', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentSearch = '';
    vi.mocked(careConnectApi.specialties.list).mockResolvedValue(ok(SPECIALTIES));
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/api/geocode/address')) {
        return new Response(JSON.stringify([{ latitude: 34.0522, longitude: -118.2437 }]), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      throw new Error(`Unhandled fetch in test: ${url}`);
    }) as typeof fetch;
  });

  test('writes specialtyCode and ZIP-derived geo filters to the URL', async () => {
    const user = userEvent.setup();
    render(<ProviderSearchFilters />);

    await waitFor(() => expect(screen.getByRole('option', { name: 'Chiropractors' })).toBeInTheDocument());

    await user.selectOptions(screen.getByRole('combobox'), 'CHIROPRACTORS');
    await user.type(screen.getByPlaceholderText('e.g. 60601'), '90012');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledTimes(1));
    const pushed = String(pushMock.mock.calls[0][0]);

    expect(pushed).toContain('/careconnect/providers?');
    expect(pushed).toContain('specialtyCode=CHIROPRACTORS');
    expect(pushed).toContain('zip=90012');
    expect(pushed).toContain('lat=34.052200');
    expect(pushed).toContain('lng=-118.243700');
    expect(pushed).toContain('radius=25');
    expect(pushed).not.toContain('categoryCode');
  });

  test('clears ZIP-derived lat/lng/radius while preserving other filters when ZIP is blanked', async () => {
    const user = userEvent.setup();
    currentSearch = 'specialtyCode=CHIROPRACTORS&zip=90012&lat=34.052200&lng=-118.243700&radius=25';

    render(<ProviderSearchFilters />);
    await waitFor(() => expect(screen.getByRole('option', { name: 'Chiropractors' })).toBeInTheDocument());

    await user.clear(screen.getByPlaceholderText('e.g. 60601'));
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledTimes(1));
    const pushed = String(pushMock.mock.calls[0][0]);

    expect(pushed).toContain('specialtyCode=CHIROPRACTORS');
    expect(pushed).not.toContain('zip=');
    expect(pushed).not.toContain('lat=');
    expect(pushed).not.toContain('lng=');
    expect(pushed).not.toContain('radius=');
  });
});
