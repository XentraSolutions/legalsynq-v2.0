import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * CC2-INT-B07 — Public CareConnect BFF proxy.
 *
 * Handles unauthenticated requests to the public network surface.
 * Unlike the authenticated /api/careconnect proxy, this handler does NOT
 * inject an Authorization header — the backend endpoints are AllowAnonymous.
 *
 * Tenant isolation: The X-Tenant-Id header is resolved server-side from the
 * incoming request's Host header by calling the Identity branding endpoint.
 * The client-supplied X-Tenant-Id header is never trusted or forwarded.
 *
 * Routing:
 *   Browser fetch → /api/public/careconnect/api/public/network
 *   → This handler
 *   → ${GATEWAY_URL}/careconnect/api/public/network  (anonymous gateway route)
 *   → CareConnect service at :5003
 */
type RouteContext = { params: Promise<{ path: string[] }> };

/**
 * Resolves the tenant GUID from the incoming request's Host header by calling
 * the Identity branding endpoint server-side. Returns null if the host cannot
 * be mapped to a known active tenant.
 *
 * This prevents callers from impersonating arbitrary tenants by supplying a
 * spoofed X-Tenant-Id header. The tenant is always derived from the subdomain,
 * which is controlled by the platform's DNS/routing layer.
 */
async function resolveTenantIdFromHost(host: string): Promise<string | null> {
  try {
    const brandingUrl = `${GATEWAY_URL}/identity/api/tenants/current/branding`;
    const res = await fetch(brandingUrl, {
      method: 'GET',
      headers: {
        'X-Forwarded-Host': host,
        'Host': host,
      },
    });
    if (!res.ok) return null;
    const body = await res.json() as { tenantId?: string };
    if (!body.tenantId || body.tenantId === '') return null;
    return body.tenantId;
  } catch {
    return null;
  }
}

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const { path: pathSegments } = await params;
  const gatewayPath = `/careconnect/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  // Resolve tenant ID server-side from the Host header — never from the client-supplied header.
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? request.nextUrl.host;
  const tenantId = await resolveTenantIdFromHost(host);

  if (!tenantId) {
    return NextResponse.json({ message: 'Tenant could not be resolved.' }, { status: 400 });
  }

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Tenant-Id': tenantId,
  };

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
