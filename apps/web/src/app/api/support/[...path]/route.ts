import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * Catch-all BFF proxy for Support API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/support/api/tickets
 *   → This handler
 *   → ${GATEWAY_URL}/support/api/tickets  +  Authorization: Bearer <cookie>
 *   → Support service at :5017
 *
 * The gateway validates the JWT from the Authorization header.
 * The platform_session token lives in an HttpOnly cookie — JS cannot read it.
 * This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Server Components: use lib/support-server-api.ts directly (no extra hop).
 * Client Components: use this proxy.
 */
type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const { path: pathSegments } = await params;
  const gatewayPath = `/support/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const reqHeaders: Record<string, string> = {};
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  let body: string | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    reqHeaders['Content-Type'] = 'application/json';
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
