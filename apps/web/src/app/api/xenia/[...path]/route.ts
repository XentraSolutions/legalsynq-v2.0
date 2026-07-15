import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<Response> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;
  if (!token) {
    return NextResponse.json({ message: 'Authentication required' }, { status: 401 });
  }

  const { path } = await params;
  const joinedPath = path.join('/');
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}/xenia/${joinedPath}${qs ? `?${qs}` : ''}`;

  const headers = new Headers();
  headers.set('Authorization', `Bearer ${token}`);
  headers.set('Accept', request.headers.get('Accept') ?? 'application/json');

  const contentType = request.headers.get('Content-Type');
  if (contentType) headers.set('Content-Type', contentType);

  const correlationId = request.headers.get('X-Correlation-Id');
  if (correlationId) headers.set('X-Correlation-Id', correlationId);

  let body: BodyInit | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    body = await request.text();
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method: request.method,
      headers,
      body,
      cache: 'no-store',
    });
  } catch {
    return NextResponse.json({ message: 'Xenia service unavailable' }, { status: 503 });
  }

  const responseHeaders = new Headers();
  const responseContentType = gatewayRes.headers.get('Content-Type') ?? 'application/json';
  const isEventStream = responseContentType.startsWith('text/event-stream');

  responseHeaders.set('Content-Type', responseContentType);
  responseHeaders.set(
    'Cache-Control',
    isEventStream
      ? 'no-cache, no-transform'
      : (gatewayRes.headers.get('Cache-Control') ?? 'no-store'),
  );

  if (isEventStream) {
    responseHeaders.set('Connection', 'keep-alive');
    responseHeaders.set('X-Accel-Buffering', 'no');
  }

  const upstreamCorrelationId = gatewayRes.headers.get('X-Correlation-Id');
  if (upstreamCorrelationId) responseHeaders.set('X-Correlation-Id', upstreamCorrelationId);

  return new Response(gatewayRes.body, {
    status: gatewayRes.status,
    headers: responseHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
