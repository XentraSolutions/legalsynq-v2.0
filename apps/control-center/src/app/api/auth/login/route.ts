import { type NextRequest, NextResponse } from 'next/server';
import { SESSION_COOKIE_NAME, CONTROL_CENTER_ORIGIN } from '@/lib/app-config';

// TODO: integrate with Identity service session validation
// TODO: move to HttpOnly secure cookies in production
// TODO: support cross-subdomain auth (scope cookie to .legalsynq.com)

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';
const IS_PROD     = process.env.NODE_ENV === 'production';

void CONTROL_CENTER_ORIGIN; // reserved for future CORS / redirect use

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
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/login`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ tenantCode, email, password }),
    });
  } catch {
    return NextResponse.json({ message: 'Identity service unavailable' }, { status: 503 });
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    return NextResponse.json(
      { message: errBody.detail ?? errBody.title ?? 'Invalid credentials' },
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
