import { NextRequest, NextResponse } from 'next/server';
import { fetchPublicAccessCodeStatus, resolveTenantIdFromPublicHost } from '@/lib/careconnect-access-code';

export async function GET(request: NextRequest): Promise<NextResponse> {
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? request.nextUrl.host;
  const explicitTenantId = request.nextUrl.searchParams.get('tenantId')?.trim();
  const tenantId = explicitTenantId || await resolveTenantIdFromPublicHost(host);

  if (!tenantId) {
    return NextResponse.json({ message: 'Tenant could not be resolved.' }, { status: 400 });
  }

  try {
    const result = await fetchPublicAccessCodeStatus(tenantId);
    return NextResponse.json(result);
  } catch {
    return NextResponse.json({ message: 'Access-code status unavailable.' }, { status: 503 });
  }
}
