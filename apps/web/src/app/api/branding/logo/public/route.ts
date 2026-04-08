import { NextRequest, NextResponse } from 'next/server';

const IDENTITY_URL = 'http://localhost:5001';
const DOCS_URL     = 'http://localhost:5006';

export async function GET(req: NextRequest) {
  const host = req.headers.get('host') ?? '';

  let tenantCode = '';
  const parts = host.split('.');
  if (parts.length >= 3) {
    tenantCode = parts[0].toUpperCase();
  }

  const env = process.env.NEXT_PUBLIC_ENV;
  if (!tenantCode && env === 'development') {
    tenantCode = process.env.NEXT_PUBLIC_TENANT_CODE ?? '';
  }

  if (!tenantCode) {
    return new NextResponse(null, { status: 404 });
  }

  try {
    const brandingRes = await fetch(`${IDENTITY_URL}/api/tenants/current/branding`, {
      headers: { 'X-Tenant-Code': tenantCode },
    });

    if (!brandingRes.ok) {
      return new NextResponse(null, { status: 404 });
    }

    const branding = await brandingRes.json();
    const docId = branding.logoDocumentId;

    if (!docId) {
      return new NextResponse(null, { status: 404 });
    }

    const logoRes = await fetch(`${DOCS_URL}/public/logo/${docId}`, {
      redirect: 'follow',
    });

    if (!logoRes.ok) {
      return new NextResponse(null, { status: logoRes.status });
    }

    const contentType = logoRes.headers.get('content-type') ?? 'image/png';
    const buffer = await logoRes.arrayBuffer();

    return new NextResponse(buffer, {
      status: 200,
      headers: {
        'Content-Type':  contentType,
        'Cache-Control': 'public, max-age=3600, s-maxage=3600',
      },
    });
  } catch {
    return new NextResponse(null, { status: 502 });
  }
}
