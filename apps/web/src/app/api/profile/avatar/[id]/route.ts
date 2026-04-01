import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const DOCS_URL = 'http://localhost:5006';

// GET /api/profile/avatar/[id]
// Proxies the document content from the documents service back to the browser.
// The docs service redirects GET /documents/{id}/content to a signed internal
// URL (/internal/files?token=...); fetch follows the redirect automatically.
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { id } = await params;

  const res = await fetch(`${DOCS_URL}/documents/${id}/content`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!res.ok) return new NextResponse(null, { status: res.status });

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
