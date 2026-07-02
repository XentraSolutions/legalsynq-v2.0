import { describe, expect, test, vi, beforeEach } from 'vitest';
import { NextRequest } from 'next/server';

vi.mock('next/headers', () => ({
  cookies: vi.fn().mockResolvedValue({
    get: vi.fn().mockReturnValue(undefined),
  }),
}));

type RouteContext = { params: Promise<{ path: string[] }> };

function makeRequest(path: string, method = 'POST', body?: object): [NextRequest, RouteContext] {
  const request = new NextRequest(`http://localhost/api/identity/${path}`, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const ctx: RouteContext = { params: Promise.resolve({ path: path.split('/') }) };
  return [request, ctx];
}

describe('Identity catch-all proxy — BLOCKED_AUTH_PATHS', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  const blockedPaths = [
    'api/auth/login',
    'api/auth/logout',
    'api/auth/forgot-password',
    'api/auth/reset-password',
    'api/auth/accept-invite',
    'api/auth/change-password',
    'api/auth/password-reset/confirm',
  ];

  test.each(blockedPaths)('blocks POST to %s with 404', async (path) => {
    const { POST } = await import('./route');
    const [req, ctx] = makeRequest(path, 'POST', { email: 'user@example.com', password: 'pw' });
    const res = await POST(req, ctx);
    expect(res.status).toBe(404);
  });

  test('whitespace-padded login via catch-all proxy is blocked (404) — not forwarded to Identity', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);

    const { POST } = await import('./route');
    const [req, ctx] = makeRequest('api/auth/login', 'POST', {
      email: ' user@example.com ',
      password: 'pw',
    });
    const res = await POST(req, ctx);

    expect(res.status).toBe(404);
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
