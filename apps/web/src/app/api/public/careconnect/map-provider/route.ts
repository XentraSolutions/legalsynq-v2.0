import { NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

export async function GET(request: NextRequest) {
  const tenantId = request.nextUrl.searchParams.get('tenantId')?.trim();

  if (!tenantId) {
    return NextResponse.json({ message: 'tenantId is required' }, { status: 400 });
  }

  const url = `${GATEWAY_URL}/tenant/api/v1/public/tenants/${encodeURIComponent(tenantId)}/settings/map-provider`;

  try {
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) {
      return NextResponse.json({ value: 'google' }, { status: 200 });
    }

    const data = await response.json() as { value?: string };
    return NextResponse.json({
      value: data.value === 'osm' ? 'osm' : 'google',
    });
  } catch {
    return NextResponse.json({ value: 'google' }, { status: 200 });
  }
}
