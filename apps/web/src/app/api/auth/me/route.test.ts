import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { NextRequest } from 'next/server';

describe('GET /api/auth/me', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test('rotates the session cookie and strips the refreshed token from the JSON payload', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      headers: new Headers(),
      json: async () => ({
        userId: 'u1',
        email: 'user@example.com',
        tenantId: 't1',
        tenantCode: 'tenant',
        productRoles: [],
        systemRoles: [],
        expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
        sessionTimeoutMinutes: 30,
        refreshedAccessToken: 'renewed-token',
      }),
    }));

    const { GET } = await import('./route');
    const request = new NextRequest('http://tenant.localhost/api/auth/me', {
      headers: {
        cookie: 'platform_session=old-token',
      },
    });

    const response = await GET(request);
    const body = await response.json();

    expect(response.status).toBe(200);
    expect(body.refreshedAccessToken).toBeUndefined();
    expect(response.cookies.get('platform_session')?.value).toBe('renewed-token');
  });
});
