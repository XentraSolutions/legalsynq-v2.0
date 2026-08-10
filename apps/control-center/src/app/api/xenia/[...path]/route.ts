import { NextRequest, NextResponse } from 'next/server';

const XENIA_BASE = process.env.XENIA_API_BASE ?? 'http://127.0.0.1:5035';

async function proxyToXenia(req: NextRequest, path: string[]): Promise<NextResponse> {
  const targetPath = '/' + path.join('/');
  const url = new URL(req.url);
  const targetUrl = `${XENIA_BASE}${targetPath}${url.search}`;

  const headers = new Headers();
  const auth = req.headers.get('authorization');
  if (auth) headers.set('authorization', auth);
  headers.set('content-type', req.headers.get('content-type') ?? 'application/json');

  const correlationId = req.headers.get('x-correlation-id') ?? crypto.randomUUID();
  headers.set('x-correlation-id', correlationId);

  try {
    const body = req.method !== 'GET' && req.method !== 'HEAD'
      ? await req.text()
      : undefined;

    const upstream = await fetch(targetUrl, {
      method: req.method,
      headers,
      body,
    });

    const text = await upstream.text();
    return new NextResponse(text, {
      status: upstream.status,
      headers: {
        'content-type': upstream.headers.get('content-type') ?? 'application/json',
        'x-correlation-id': correlationId,
      },
    });
  } catch {
    return NextResponse.json(
      { error: 'Xenia service is unavailable', service: 'xenia' },
      { status: 503 },
    );
  }
}

export async function GET(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  return proxyToXenia(req, path);
}

export async function POST(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  return proxyToXenia(req, path);
}

export async function PUT(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  return proxyToXenia(req, path);
}

export async function DELETE(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  return proxyToXenia(req, path);
}
