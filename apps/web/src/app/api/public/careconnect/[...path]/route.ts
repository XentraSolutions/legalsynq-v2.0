import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * CC2-INT-B07 — Public CareConnect BFF proxy.
 *
 * Handles unauthenticated requests to the public network surface.
 * Unlike the authenticated /api/careconnect proxy, this handler does NOT
 * inject an Authorization header — the backend endpoints are AllowAnonymous.
 *
 * The X-Tenant-Id header is forwarded verbatim from the incoming request.
 * It is set server-side by the /network page Server Component after resolving
 * the subdomain → tenant via the Identity branding endpoint.
 *
 * Routing:
 *   Browser fetch → /api/public/careconnect/api/public/network
 *   → This handler
 *   → ${GATEWAY_URL}/careconnect/api/public/network  (anonymous gateway route)
 *   → CareConnect service at :5003
 */
type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const { path: pathSegments } = await params;
  const gatewayPath = `/careconnect/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  // Forward tenant ID resolved from subdomain — set server-side, never from user input
  const tenantId = request.headers.get('X-Tenant-Id');
  if (tenantId) reqHeaders['X-Tenant-Id'] = tenantId;

  let body: string | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    try { body = await request.text(); } catch { /* empty */ }
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
