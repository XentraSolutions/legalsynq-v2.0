import { type NextRequest, NextResponse } from 'next/server';

/**
 * Catch-all BFF proxy for all SynqLien client-side API calls.
 *
 * Client Components call  /api/lien/api/liens/...
 * This handler forwards    → GATEWAY_URL/lien/api/liens/...
 * with the session cookie forwarded as Authorization: Bearer.
 *
 * Pattern mirrors /api/fund/[...path]/route.ts exactly.
 */
const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';

async function proxy(req: NextRequest, segments: string[]): Promise<NextResponse> {
  const path   = segments.join('/');
  const search = req.nextUrl.search;
  const url    = `${GATEWAY_URL}/lien/${path}${search}`;

  const token   = req.cookies.get('platform_session')?.value;
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  let body: string | undefined;
  if (req.method !== 'GET' && req.method !== 'HEAD') {
    try { body = await req.text(); } catch { /* no body */ }
  }

  const res = await fetch(url, {
    method:  req.method,
    headers,
    body,
  });

  const responseHeaders: Record<string, string> = {};
  const correlationId = res.headers.get('X-Correlation-Id');
  if (correlationId) responseHeaders['X-Correlation-Id'] = correlationId;
  responseHeaders['Content-Type'] = res.headers.get('Content-Type') ?? 'application/json';

  if (res.status === 204) {
    return new NextResponse(null, { status: 204, headers: responseHeaders });
  }

  const data = await res.text();
  return new NextResponse(data, { status: res.status, headers: responseHeaders });
}

export async function GET(req: NextRequest, { params }: { params: { path: string[] } }) {
  return proxy(req, params.path);
}
export async function POST(req: NextRequest, { params }: { params: { path: string[] } }) {
  return proxy(req, params.path);
}
export async function PUT(req: NextRequest, { params }: { params: { path: string[] } }) {
  return proxy(req, params.path);
}
export async function PATCH(req: NextRequest, { params }: { params: { path: string[] } }) {
  return proxy(req, params.path);
}
export async function DELETE(req: NextRequest, { params }: { params: { path: string[] } }) {
  return proxy(req, params.path);
}
