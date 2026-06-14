import { beforeEach, describe, expect, test, vi } from 'vitest';
import { NextRequest } from 'next/server';

describe('GET /api/geocode/address', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllGlobals();
    process.env.PublicTrustBoundary__InternalRequestSecret = 'zip-token-test-secret';
  });

  test('returns signed address selection tokens in strict mode', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ([{
        lat: '33.749',
        lon: '-84.388',
        address: {
          house_number: '885',
          road: 'Sample Rd',
          city: 'Atlanta',
          state: 'Georgia',
          postcode: '30316-9999',
        },
      }]),
    }));

    const { GET } = await import('./route');
    const response = await GET(new NextRequest('http://localhost/api/geocode/address?q=885%20Sample'));
    const data = await response.json() as Array<{ postalCode: string; addressSelectionToken: string }>;

    expect(response.status).toBe(200);
    expect(data).toHaveLength(1);
    expect(data[0]?.postalCode).toBe('30316');
    expect(data[0]?.addressSelectionToken).toMatch(/\./);
  });
});
