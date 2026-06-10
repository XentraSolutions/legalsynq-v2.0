import { describe, expect, test, vi, beforeEach, afterEach } from 'vitest';
import { NextRequest } from 'next/server';

describe('POST /api/auth/login', () => {
  const originalEnv = process.env.CC_COMMON_PORTAL_HOSTNAME;

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    process.env.CC_COMMON_PORTAL_HOSTNAME = originalEnv;
    vi.restoreAllMocks();
  });

  test('passes through the CareConnect portal restriction message on the common portal host', async () => {
    process.env.CC_COMMON_PORTAL_HOSTNAME = 'careconnect.localhost';
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({
        title: 'CareConnectPortalRoleRestricted',
        detail: 'This account is not eligible to access the CareConnect portal.',
      }),
    }));

    const { POST } = await import('./route');

    const request = new NextRequest('http://careconnect.localhost/api/auth/login', {
      method: 'POST',
      headers: {
        host: 'careconnect.localhost',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        email: 'provider@example.com',
        password: 'Password123!',
      }),
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(403);
    expect(body).toEqual({
      message: 'This account is not eligible to access the CareConnect portal.',
    });
    expect(response.cookies.get('platform_session')).toBeUndefined();
  });
});
