import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';

/**
 * Catch-all BFF proxy for CareConnect API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/careconnect/api/referrals
 *   → This handler (takes priority over next.config rewrites)
 *   → ${GATEWAY_URL}/careconnect/api/referrals  +  Authorization: Bearer <cookie>
 *   → CareConnect service at :5003
 *
 * Why this exists:
 *   The gateway validates JWT from the Authorization header only.
 *   The platform_session token lives in an HttpOnly cookie — JS can't read it.
 *   This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Server Components: use lib/server-api-client.ts directly (no extra hop).
 * Client Components: use apiClient → this proxy → gateway.
 */
type RouteContext = { params: { path: string[] } };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const token = request.cookies.get('platform_session')?.value;

  // Reconstruct the gateway path: /api/careconnect/api/providers → /careconnect/api/providers
  const gatewayPath = `/careconnect/${params.path.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

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

  return new NextResponse(responseBody, {
    status:  gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET    = proxy;
export const POST   = proxy;
export const PUT    = proxy;
export const PATCH  = proxy;
export const DELETE = proxy;
