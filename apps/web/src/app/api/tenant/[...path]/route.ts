import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const { path: pathSegments } = await params;
  const gatewayPath = `/tenant/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const incomingContentType = request.headers.get('Content-Type') ?? '';
  const isMultipart = incomingContentType.startsWith('multipart/form-data');

  const reqHeaders: Record<string, string> = {};
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  let body: ArrayBuffer | string | undefined;

  if (!['GET', 'HEAD'].includes(request.method)) {
    if (isMultipart) {
      reqHeaders['Content-Type'] = incomingContentType;
      try { body = await request.arrayBuffer(); } catch { /* empty body */ }
    } else {
      reqHeaders['Content-Type'] = 'application/json';
      try { body = await request.text(); } catch { /* empty body */ }
    }
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method: request.method,
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
    status: gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
