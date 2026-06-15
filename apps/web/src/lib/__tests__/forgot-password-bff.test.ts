import { test, describe, before } from 'node:test';
import assert from 'node:assert/strict';

// Set module-level env vars before any dynamic import so the route picks them up.
process.env.CC_COMMON_PORTAL_HOSTNAME = 'careconnect.example.com';
process.env.GATEWAY_URL               = 'http://identity-test:5000';
process.env.NODE_ENV                  = 'test'; // suppress console.warn

// ── Shared fetch stub infrastructure ─────────────────────────────────────────

type CapturedFetch = {
  url:     string;
  method:  string;
  headers: Record<string, string>;
  body:    unknown;
};

type FakeResponse = { ok: boolean; status: number; body: unknown };

function makeCapturingFetch(responses: FakeResponse[]): {
  fake: typeof fetch;
  calls: CapturedFetch[];
} {
  const calls: CapturedFetch[] = [];
  let callIndex = 0;

  const fake = (url: string | URL | Request, init?: RequestInit) => {
    const headers: Record<string, string> = {};
    if (init?.headers) {
      for (const [k, v] of Object.entries(init.headers as Record<string, string>)) {
        headers[k] = v;
      }
    }
    let body: unknown = undefined;
    if (typeof init?.body === 'string') {
      try { body = JSON.parse(init.body); } catch { body = init.body; }
    }
    calls.push({ url: url.toString(), method: init?.method ?? 'GET', headers, body });

    const res = responses[callIndex] ?? responses[responses.length - 1];
    callIndex++;

    return Promise.resolve({
      ok:     res.ok,
      status: res.status,
      json:   () => Promise.resolve(res.body),
    } as unknown as Response);
  };

  return { fake: fake as unknown as typeof fetch, calls };
}

function makeThrowingFetch(err: Error): typeof fetch {
  return () => Promise.reject(err) as unknown as ReturnType<typeof fetch>;
}

// ── Route loader ──────────────────────────────────────────────────────────────

// Import once; module-level CC_COMMON_PORTAL_HOSTNAME is already set above.
let POST: (req: Request) => Promise<Response>;

before(async () => {
  ({ POST } = await import('../../app/api/auth/forgot-password/route.js') as {
    POST: (req: Request) => Promise<Response>;
  });
});

// Build a NextRequest-compatible Request with sensible defaults.
function makeRequest(
  body:    unknown,
  options: { host?: string; ip?: string } = {},
): Request {
  return new Request('http://localhost/api/auth/forgot-password', {
    method:  'POST',
    headers: {
      'Content-Type':     'application/json',
      'x-forwarded-host': options.host ?? 'acme.legalsynq.com',
      'x-forwarded-for':  options.ip   ?? `10.0.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`,
    },
    body: JSON.stringify(body),
  });
}

// ── Validation ────────────────────────────────────────────────────────────────

describe('input validation', () => {
  test('returns 400 when email is missing', async () => {
    const { fake } = makeCapturingFetch([{ ok: true, status: 200, body: {} }]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest({ tenantCode: 'ACME' }));
      assert.equal(res.status, 400);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('returns 400 when not on CC host and no tenantCode/subdomain', async () => {
    const { fake } = makeCapturingFetch([{ ok: true, status: 200, body: {} }]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      // Host has no subdomain (localhost-style), so tenant cannot be resolved
      const req = new Request('http://localhost/api/auth/forgot-password', {
        method:  'POST',
        headers: {
          'Content-Type':     'application/json',
          'x-forwarded-host': 'nxdomain.invalid',
          'x-forwarded-for':  '10.0.99.1',
        },
        body: JSON.stringify({ email: 'user@example.com' }),
      });
      const res = await POST(req);
      assert.equal(res.status, 400);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });
});

// ── Standard path (non-CC host) ───────────────────────────────────────────────

describe('standard path (non-CC host)', () => {
  test('sends resolveByEmail=false to Identity and forwards success', async () => {
    const { fake, calls } = makeCapturingFetch([
      { ok: true, status: 200, body: { message: 'If an account exists...' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'user@acme.com', tenantCode: 'ACME' },
        { host: 'acme.legalsynq.com' },
      ));
      assert.equal(res.status, 200);

      // Should have made one call to the tenant-resolve endpoint and one to Identity.
      // Find the Identity call (the last POST to /identity/api/auth/forgot-password).
      const identityCall = calls.find(c => c.url.includes('/identity/api/auth/forgot-password'));
      assert.ok(identityCall, 'Identity must be called');
      assert.equal((identityCall.body as Record<string, unknown>).resolveByEmail, false);
      // X-Ls-Internal-Source must be forwarded on every call
      assert.equal(identityCall.headers['X-Ls-Internal-Source'], 'bff-forgot-password');
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });
});

// ── CareConnect common-portal path ────────────────────────────────────────────

describe('CareConnect common-portal path (CC host)', () => {
  test('sends resolveByEmail=true and X-Ls-Internal-Source header to Identity', async () => {
    const { fake, calls } = makeCapturingFetch([
      { ok: true, status: 200, body: { message: 'If an account exists...' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'provider@example.com' },
        { host: 'careconnect.example.com' },
      ));
      assert.equal(res.status, 200);

      const identityCall = calls.find(c => c.url.includes('/identity/api/auth/forgot-password'));
      assert.ok(identityCall, 'Identity must be called');

      const body = identityCall.body as Record<string, unknown>;
      assert.equal(body.resolveByEmail, true, 'resolveByEmail must be true on CC path');
      assert.equal(body.tenantCode, '', 'tenantCode must be empty on CC path');
      assert.equal(
        identityCall.headers['X-Ls-Internal-Source'],
        'bff-forgot-password',
        'X-Ls-Internal-Source header must be present',
      );
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('skips tenant-resolve fetch on CC path (no extra network calls)', async () => {
    const { fake, calls } = makeCapturingFetch([
      { ok: true, status: 200, body: { message: 'If an account exists...' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      await POST(makeRequest(
        { email: 'provider@example.com' },
        { host: 'careconnect.example.com' },
      ));
      const tenantResolveCall = calls.find(c => c.url.includes('/tenant/api/v1/public/resolve'));
      assert.equal(tenantResolveCall, undefined, 'Tenant service must not be called on CC path');
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('returns 200 with generic message when Identity returns 200', async () => {
    const { fake } = makeCapturingFetch([
      { ok: true, status: 200, body: { message: 'If an account exists with that email...' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'provider@example.com' },
        { host: 'careconnect.example.com' },
      ));
      assert.equal(res.status, 200);
      const json = await res.json() as Record<string, string>;
      assert.ok(typeof json.message === 'string' && json.message.length > 0);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });
});

// ── Error path forwarding ─────────────────────────────────────────────────────

describe('error path forwarding', () => {
  test('returns 503 when Identity service is unreachable', async () => {
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = makeThrowingFetch(new Error('ECONNREFUSED'));
      const res = await POST(makeRequest(
        { email: 'user@acme.com', tenantCode: 'ACME' },
        { host: 'acme.legalsynq.com', ip: '10.0.50.1' },
      ));
      assert.equal(res.status, 503);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('maps Identity 500 to 503', async () => {
    const { fake } = makeCapturingFetch([
      { ok: false, status: 500, body: { title: 'Internal Server Error' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'user@acme.com', tenantCode: 'ACME' },
        { host: 'acme.legalsynq.com', ip: '10.0.51.1' },
      ));
      assert.equal(res.status, 503);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('maps Identity 429 to 429', async () => {
    const { fake } = makeCapturingFetch([
      { ok: false, status: 429, body: { error: 'Too Many Requests' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'user@acme.com', tenantCode: 'ACME' },
        { host: 'acme.legalsynq.com', ip: '10.0.52.1' },
      ));
      assert.equal(res.status, 429);
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });

  test('maps Identity 400 to 400 with message', async () => {
    const { fake } = makeCapturingFetch([
      { ok: false, status: 400, body: { error: 'tenantCode is required.' } },
    ]);
    const original = globalThis.fetch;
    try {
      (globalThis as Record<string, unknown>).fetch = fake;
      const res = await POST(makeRequest(
        { email: 'user@acme.com', tenantCode: 'ACME' },
        { host: 'acme.legalsynq.com', ip: '10.0.53.1' },
      ));
      assert.equal(res.status, 400);
      const json = await res.json() as Record<string, string>;
      assert.equal(json.message, 'tenantCode is required.');
    } finally {
      (globalThis as Record<string, unknown>).fetch = original;
    }
  });
});
