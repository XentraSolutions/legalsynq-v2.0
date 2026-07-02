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

  // ── Whitespace guard ────────────────────────────────────────────────────────

  test.each([
    ' user@example.com',
    'user@example.com ',
    ' user@example.com ',
    '\tuser@example.com',
    'user@example.com\t',
  ])('rejects email with whitespace %j with 401 before forwarding to Identity', async (paddedEmail) => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);

    const { POST } = await import('./route');
    const request = new NextRequest('http://tenant.localhost/api/auth/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json', host: 'tenant.localhost' },
      body: JSON.stringify({ email: paddedEmail, password: 'Password123!' }),
    });

    const response = await POST(request);

    expect(response.status).toBe(401);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  test('accepts a clean email and forwards to Identity', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => ({ tenantId: 't1' }) })  // tenant resolve
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          accessToken: 'tok',
          expiresAtUtc: new Date(Date.now() + 3600_000).toISOString(),
          user: { id: 'u1', email: 'user@example.com', tenantId: 't1', tenantCode: 'TEST', roles: [], productRoles: [] },
        }),
      }));

    const { POST } = await import('./route');
    const request = new NextRequest('http://tenant.localhost/api/auth/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json', host: 'tenant.localhost' },
      body: JSON.stringify({ email: 'user@example.com', password: 'Password123!' }),
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
  });

  // ── CareConnect portal ──────────────────────────────────────────────────────

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
