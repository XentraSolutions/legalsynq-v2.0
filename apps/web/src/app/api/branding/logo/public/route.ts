import { NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

export async function GET(req: NextRequest) {
  const host = req.headers.get('x-forwarded-host') ?? req.headers.get('host') ?? '';

  let tenantCode = req.nextUrl.searchParams.get('tenantCode') ?? '';

  if (!tenantCode) {
    const parts = host.split('.');
    if (parts.length >= 3) {
      tenantCode = parts[0].toUpperCase();
    }
  }

  const env = process.env.NEXT_PUBLIC_ENV;
  if (!tenantCode && env === 'development') {
    tenantCode = process.env.NEXT_PUBLIC_TENANT_CODE ?? '';
  }

  if (!tenantCode) {
    return new NextResponse(null, { status: 404 });
  }

  try {
    const brandingRes = await fetch(`${GATEWAY_URL}/identity/api/tenants/current/branding`, {
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

    const logoRes = await fetch(`${GATEWAY_URL}/documents/public/logo/${docId}`);

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
