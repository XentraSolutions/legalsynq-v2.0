import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { NextRequest } from 'next/server';

// The Fetch spec forbids constructing a Response with a body when the status
// is a null-body status (204/205/304) — even an empty string throws. This
// regression covers CareConnect's DELETE endpoints (e.g. removing a network
// provider location), which return 204 No Content on success.
vi.mock('next/headers', () => ({
  cookies: vi.fn().mockResolvedValue({
    get: () => ({ value: 'test-session-token' }),
  }),
}));

describe('DELETE /api/careconnect/[...path]', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test('relays a 204 No Content upstream response without throwing', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      status: 204,
      text: async () => '',
      headers: new Headers(),
    }));

    const { DELETE } = await import('./route');
    const request = new NextRequest(
      'http://tenant.localhost/api/careconnect/api/networks/net-1/providers/prov-1',
      { method: 'DELETE' },
    );

    const response = await DELETE(request, {
      params: Promise.resolve({ path: ['api', 'networks', 'net-1', 'providers', 'prov-1'] }),
    });

    expect(response.status).toBe(204);
  });

  test('still relays a JSON body for a normal 200 response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      status: 200,
      text: async () => JSON.stringify({ ok: true }),
      headers: new Headers({ 'Content-Type': 'application/json' }),
    }));

    const { GET } = await import('./route');
    const request = new NextRequest(
      'http://tenant.localhost/api/careconnect/api/networks/net-1',
      { method: 'GET' },
    );

    const response = await GET(request, {
      params: Promise.resolve({ path: ['api', 'networks', 'net-1'] }),
    });
    const body = await response.json();

    expect(response.status).toBe(200);
    expect(body).toEqual({ ok: true });
  });
});
