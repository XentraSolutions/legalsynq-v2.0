import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const DOCS_URL = 'http://127.0.0.1:5006';

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ docId: string }> },
) {
  const { docId } = await params;

  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;

  if (token) {
    try {
      const res = await fetch(`${DOCS_URL}/documents/${docId}/content`, {
        headers: { Authorization: `Bearer ${token}` },
        redirect: 'follow',
      });

      if (res.ok) {
        const contentType = res.headers.get('content-type') ?? 'application/octet-stream';
        const buffer      = await res.arrayBuffer();
        return new NextResponse(buffer, {
          status:  200,
          headers: {
            'Content-Type':  contentType,
            'Cache-Control': 'private, max-age=3600',
          },
        });
      }

      if (res.status === 401 || res.status === 403) {
        return new NextResponse(null, { status: res.status });
      }
    } catch {
      // Network/redirect failure — fall through to public endpoint
    }
  }

  try {
    const pubRes = await fetch(`${DOCS_URL}/public/logo/${docId}`);
    if (pubRes.ok) {
      const contentType = pubRes.headers.get('content-type') ?? 'image/png';
      const buffer      = await pubRes.arrayBuffer();
      return new NextResponse(buffer, {
        status:  200,
        headers: {
          'Content-Type':  contentType,
          'Cache-Control': 'public, max-age=3600, s-maxage=3600',
        },
      });
    }
  } catch {
    // ignore
  }

  return new NextResponse(null, { status: 404 });
}
