import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * Catch-all BFF proxy for Identity API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/identity/api/users
 *   → This handler (takes priority over next.config rewrites)
 *   → ${GATEWAY_URL}/identity/api/users  +  Authorization: Bearer <cookie>
 *   → Identity service at :5001
 *
 * Why this exists:
 *   The gateway validates JWT from the Authorization header only.
 *   The platform_session token lives in an HttpOnly cookie — JS can't read it.
 *   This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Server Components: use lib/server-api-client.ts directly (no extra hop).
 * Client Components: use controlCenterApi → this proxy → gateway.
 *
 * Cookie reading: uses cookies() from next/headers (server-side store) rather
 * than request.cookies — more reliable inside App Router Route Handlers.
 *
 * Authorization:
 *   Admin paths (/api/admin/**) require at minimum TenantAdmin or PlatformAdmin.
 *   Platform-admin-only paths require PlatformAdmin.
 *   This is a defense-in-depth check; the identity service enforces the same
 *   rules at the endpoint level. The JWT payload is decoded (not re-verified)
 *   here — the gateway performs full signature verification.
 */
type RouteContext = { params: Promise<{ path: string[] }> };

/**
 * Platform-admin-only path prefixes (relative to /api/admin/).
 * Any request whose joined path starts with one of these requires PlatformAdmin.
 */
const PLATFORM_ADMIN_ONLY_PREFIXES = [
  'api/admin/tenants',
  'api/admin/settings',
  'api/admin/audit',
  'api/admin/support',
  'api/admin/dns/',
  'api/admin/legacy-coverage',
  'api/admin/platform-readiness',
  'api/admin/permissions',
  'api/admin/policies',
  'api/admin/permission-policies',
  'api/admin/organization-relationships',
  'api/admin/authorization/simulate',
];

/**
 * Decode the JWT payload without verification (verification is done by the
 * gateway). Returns the parsed payload or null on any parse failure.
 */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = Buffer.from(payload, 'base64').toString('utf8');
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/** Extract the role claim from a decoded JWT payload (string or string[]). */
function extractRoles(payload: Record<string, unknown>): string[] {
  const role = payload['role'];
  if (typeof role === 'string') return [role];
  if (Array.isArray(role)) return role.filter((r): r is string => typeof r === 'string');
  return [];
}

/**
 * Returns a 403 response with a structured error body.
 * Used when the BFF detects a privilege mismatch before forwarding upstream.
 */
function forbidden(reason: string): NextResponse {
  return NextResponse.json(
    { error: 'Forbidden', reason },
    { status: 403 },
  );
}

/**
 * Auth paths that must go through their dedicated BFF routes — never through
 * this generic proxy.  The dedicated routes own rate-limiting, tenant
 * resolution, cookie issuance, whitespace guards, and session management.
 * Blocking them here prevents the proxy from being used as a side-door that
 * bypasses those controls.
 */
const BLOCKED_AUTH_PATHS = new Set([
  'api/auth/login',
  'api/auth/logout',
  'api/auth/forgot-password',
  'api/auth/reset-password',
  'api/auth/accept-invite',
  'api/auth/change-password',
  'api/auth/password-reset/confirm',
]);

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const { path: pathSegments } = await params;
  const joinedPath = pathSegments.join('/');

  if (BLOCKED_AUTH_PATHS.has(joinedPath)) {
    return NextResponse.json(
      { message: 'Use the dedicated BFF route for this operation.' },
      { status: 404 },
    );
  }

  const gatewayPath = `/identity/${joinedPath}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  // ── BFF admin path authorization (defense-in-depth) ────────────────────────
  // The identity service enforces these same rules at the endpoint level.
  // This layer prevents needless forwarding of unauthorized requests and gives
  // a consistent 403 before the request ever reaches the backend.
  if (joinedPath.startsWith('api/admin/')) {
    if (!token) {
      return forbidden('Authentication required for admin routes');
    }

    const payload = decodeJwtPayload(token);
    const roles = payload ? extractRoles(payload) : [];
    const isPlatformAdmin = roles.includes('PlatformAdmin');
    const isTenantAdmin   = roles.includes('TenantAdmin');

    const requiresPlatformAdmin = PLATFORM_ADMIN_ONLY_PREFIXES.some(prefix =>
      joinedPath === prefix || joinedPath.startsWith(prefix + '/'),
    );

    if (requiresPlatformAdmin && !isPlatformAdmin) {
      return forbidden('PlatformAdmin role required');
    }

    if (!isPlatformAdmin && !isTenantAdmin) {
      return forbidden('TenantAdmin or PlatformAdmin role required for admin routes');
    }
  }

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  const isBrandingRoute = joinedPath === 'api/tenants/current/branding'
    || joinedPath === 'tenants/current/branding';
  if (isBrandingRoute) {
    const tenantCode = request.headers.get('X-Tenant-Code');
    if (tenantCode) reqHeaders['X-Tenant-Code'] = tenantCode;

    const forwardedHost = request.headers.get('x-forwarded-host') ?? request.headers.get('host');
    if (forwardedHost) reqHeaders['X-Forwarded-Host'] = forwardedHost;
  }

  let body: string | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    try { body = await request.text(); } catch { /* empty body */ }
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method:  request.method,
      headers: reqHeaders,
      body,
    });
  } catch {
    return NextResponse.json({ message: 'Gateway unavailable' }, { status: 503 });
  }

  const responseBody = await gatewayRes.text();

  const resHeaders: Record<string, string> = {
    'Content-Type': gatewayRes.headers.get('Content-Type') ?? 'application/json',
  };
  const correlationId = gatewayRes.headers.get('X-Correlation-Id');
  if (correlationId) resHeaders['X-Correlation-Id'] = correlationId;

  // Null-body statuses (204/205/304) must not be constructed with a body — even an
  // empty string throws "Invalid response status code" per the Fetch spec.
  const isNullBodyStatus = gatewayRes.status === 204 || gatewayRes.status === 205 || gatewayRes.status === 304;

  return new NextResponse(isNullBodyStatus ? null : responseBody, {
    status:  gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET    = proxy;
export const POST   = proxy;
export const PUT    = proxy;
export const PATCH  = proxy;
export const DELETE = proxy;
