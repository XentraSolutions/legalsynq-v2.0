import { type NextRequest, NextResponse } from 'next/server';
import { SESSION_COOKIE_NAME }            from '@/lib/app-config';
import { CONTROL_CENTER_API_BASE, IS_PROD } from '@/lib/env';

// All env var resolution is delegated to env.ts — no process.env reads here.
// TODO: integrate with Identity service session validation
// TODO: support cross-subdomain auth (scope cookie to .legalsynq.com)

/**
 * BFF Login route — POST /api/auth/login
 *
 * Accepts { email, password, tenantCode? } from the login form.
 * Forwards credentials to Identity.Api via the gateway.
 * Sets the platform_session HttpOnly cookie on success.
 * Returns a session envelope — the raw token never reaches browser JS.
 */
export async function POST(request: NextRequest) {
  let body: Record<string, string>;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: 'Invalid request body' }, { status: 400 });
  }

  const { email, password, tenantCode: explicitTenantCode } = body;

  if (!email || !password) {
    return NextResponse.json({ message: 'Email and password are required' }, { status: 400 });
  }

  const tenantCode = explicitTenantCode?.trim() || extractTenantCodeFromHost(request);

  if (!tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved. Please provide a tenant code.' },
      { status: 400 },
    );
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${CONTROL_CENTER_API_BASE}/identity/api/auth/login`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ tenantCode, email, password }),
    });
  } catch {
    return NextResponse.json({ message: 'Identity service unavailable' }, { status: 503 });
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    const message = errBody.detail ?? errBody.title ?? 'Invalid credentials';

    const isVerifying = typeof message === 'string' && message.includes('verifying DNS configuration');
    if (isVerifying) {
      return NextResponse.json(
        { message: 'Your workspace is verifying DNS configuration. This typically completes within a few minutes. Please try again shortly.' },
        { status: 503 },
      );
    }

    const isNotProvisioned = typeof message === 'string' && message.includes('not fully provisioned');
    if (isNotProvisioned) {
      return NextResponse.json(
        { message: 'This tenant is still being set up. Please try again shortly.' },
        { status: 503 },
      );
    }

    return NextResponse.json(
      { message },
      { status: identityRes.status === 401 ? 401 : 400 },
    );
  }

  const data = await identityRes.json();
  const { accessToken, expiresAtUtc, user } = data;

  const expiresDate   = new Date(expiresAtUtc);
  const maxAgeSeconds = Math.floor((expiresDate.getTime() - Date.now()) / 1000);

  const sessionEnvelope = {
    userId:       user.id,
    email:        user.email,
    tenantId:     user.tenantId,
    tenantCode:   user.tenantCode ?? tenantCode,
    productRoles: user.productRoles ?? [],
    systemRoles:  user.roles ?? [],
    expiresAtUtc,
  };

  const response = NextResponse.json(sessionEnvelope, { status: 200 });

  response.cookies.set(SESSION_COOKIE_NAME, accessToken, {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   maxAgeSeconds,
  });

  return response;
}

function extractTenantCodeFromHost(request: NextRequest): string | null {
  const host = request.headers.get('x-forwarded-host')
    ?? request.headers.get('host')
    ?? '';
  const hostWithoutPort = host.includes(':') ? host.split(':')[0] : host;
  const parts = hostWithoutPort.split('.');
  if (parts.length >= 3) return parts[0].toUpperCase();
  return null;
}
