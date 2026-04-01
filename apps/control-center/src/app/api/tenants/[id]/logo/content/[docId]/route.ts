import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const DOCS_URL = 'http://localhost:5006';

/**
 * GET /api/tenants/[id]/logo/content/[docId]
 *
 * Proxies the tenant logo image from the Documents service.
 * Passes X-Admin-Target-Tenant so PlatformAdmins can fetch cross-tenant logos.
 */
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string; docId: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { id: targetTenantId, docId } = await params;

  const res = await fetch(`${DOCS_URL}/documents/${docId}/content`, {
    headers: {
      Authorization:           `Bearer ${token}`,
      'X-Admin-Target-Tenant': targetTenantId,
    },
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
