import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';
const IS_PROD     = process.env.NODE_ENV === 'production';

/**
 * BFF Login route — POST /api/auth/login
 *
 * Flow:
 *   1. Receive { email, password, tenantCode? } from the login form
 *   2. Resolve tenantCode: use the form value if present (dev mode),
 *      otherwise derive from the Host / X-Forwarded-Host header (prod)
 *   3. Forward to POST ${GATEWAY_URL}/identity/api/auth/login
 *   4. Receive { accessToken, expiresAtUtc, user } from Identity service
 *   5. Store accessToken in an HttpOnly cookie (platform_session)
 *   6. Return a session envelope to the client — raw token is NEVER sent to JS
 *
 * Cookie attributes:
 *   - HttpOnly:   browser JS cannot read the token (XSS protection)
 *   - SameSite:   Strict in production; Lax in development
 *   - Secure:     true in production only (HTTPS required)
 *   - Path:       / (sent with every request to this origin)
 *   - Domain:     NOT set — scopes cookie to the exact subdomain only
 *                 Setting Domain=.legalsynq.com would share cookies across tenants
 *   - Max-Age:    matches token expiry from Identity service
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

  const isDev = process.env.NEXT_PUBLIC_ENV === 'development';
  const tenantCode = isDev
    ? (explicitTenantCode?.trim() || extractTenantCodeFromHost(request))
    : extractTenantCodeFromHost(request);

  if (!tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved. Please provide a tenant code.' },
      { status: 400 },
    );
  }

  // Forward login to Identity service via gateway
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

  // Compute Max-Age in seconds
  const expiresDate = new Date(expiresAtUtc);
  const maxAgeSeconds = Math.floor((expiresDate.getTime() - Date.now()) / 1000);

  // Build the session envelope — accessToken is intentionally excluded
  const sessionEnvelope = {
    userId:       user.id,
    email:        user.email,
    tenantId:     user.tenantId,
    tenantCode:   user.tenantCode ?? tenantCode,
    orgId:        user.organizationId ?? null,
    orgType:      user.orgType ?? null,
    productRoles: user.productRoles ?? [],
    systemRoles:  user.roles ?? [],
    expiresAtUtc,
  };

  const response = NextResponse.json(sessionEnvelope, { status: 200 });

  // Set the HttpOnly cookie — this is the only place the raw token touches HTTP headers
  response.cookies.set('platform_session', accessToken, {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   maxAgeSeconds,
    // domain: intentionally omitted — scopes to exact request origin only
  });

  return response;
}

/**
 * Extracts a tenantCode from the request's Host header.
 *
 * Subdomain slugs use hyphens for readability (URL-friendly), but tenant
 * codes are stored without hyphens in uppercase.
 *
 * Examples:
 *   "maner-law.demo.legalsynq.com" → "MANERLAW"
 *   "lisa-medical.demo.legalsynq.com" → "LISAMEDICAL"
 *   "legalsynq.demo.legalsynq.com" → "LEGALSYNQ"
 *   "localhost:3000" → null (no subdomain)
 */
function extractTenantCodeFromHost(request: NextRequest): string | null {
  const rawHost = request.headers.get('x-forwarded-host')
    ?? request.headers.get('host')
    ?? '';

  const host = rawHost.split(',')[0].trim();
  const hostWithoutPort = host.includes(':') ? host.split(':')[0] : host;
  const lower = hostWithoutPort.toLowerCase();

  if (!/^[a-z0-9.-]+$/.test(lower)) return null;

  const parts = lower.split('.');

  if (parts.length >= 3) {
    const sub = parts[0];
    if (sub === 'www') return null;
    return sub.toUpperCase();
  }

  return null;
}
