import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const DOCS_URL = 'http://localhost:5006';

/**
 * GET /api/branding/logo/[docId]
 *
 * Proxies the tenant's logo image from the Documents service using
 * the authenticated user's session token.
 *
 * The logo belongs to the same tenant as the logged-in user, so no
 * X-Admin-Target-Tenant header is needed — the JWT's tenant_id claim
 * already scopes the request correctly.
 */
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ docId: string }> },
) {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { docId } = await params;

  const res = await fetch(`${DOCS_URL}/documents/${docId}/content`, {
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
