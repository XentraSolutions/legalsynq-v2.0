import { NextRequest, NextResponse } from 'next/server';
import { resolveTenantIdFromPublicHost, verifyPublicAccessCode } from '@/lib/careconnect-access-code';

interface RateLimitEntry {
  count: number;
  resetAt: number;
}

const accessCodeVerifyRateLimit = new Map<string, RateLimitEntry>();
const ACCESS_CODE_VERIFY_LIMIT = 5;
const ACCESS_CODE_VERIFY_WINDOW_MS = 10 * 60 * 1000;

function getClientIp(request: NextRequest): string {
  return (
    request.headers.get('x-forwarded-for')?.split(',')[0].trim() ??
    request.headers.get('x-real-ip') ??
    'unknown'
  );
}

function checkAccessCodeVerifyRateLimit(key: string): { allowed: boolean; retryAfterSeconds: number } {
  const now = Date.now();
  const entry = accessCodeVerifyRateLimit.get(key);

  if (!entry || now > entry.resetAt) {
    accessCodeVerifyRateLimit.set(key, { count: 1, resetAt: now + ACCESS_CODE_VERIFY_WINDOW_MS });
    return { allowed: true, retryAfterSeconds: 0 };
  }

  if (entry.count >= ACCESS_CODE_VERIFY_LIMIT) {
    return {
      allowed: false,
      retryAfterSeconds: Math.max(1, Math.ceil((entry.resetAt - now) / 1000)),
    };
  }

  entry.count++;
  return { allowed: true, retryAfterSeconds: 0 };
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? request.nextUrl.host;
  const explicitTenantId = request.nextUrl.searchParams.get('tenantId')?.trim();
  const tenantId = explicitTenantId || await resolveTenantIdFromPublicHost(host);

  if (!tenantId) {
    return NextResponse.json({ message: 'Tenant could not be resolved.' }, { status: 400 });
  }

  const ip = getClientIp(request);
  const rateLimitKey = `${tenantId}:${ip}`;
  const rateLimit = checkAccessCodeVerifyRateLimit(rateLimitKey);
  if (!rateLimit.allowed) {
    return NextResponse.json(
      { message: 'Too many access code attempts. Try again later.' },
      {
        status: 429,
        headers: {
          'Retry-After': String(rateLimit.retryAfterSeconds),
        },
      },
    );
  }

  let body: { code?: string };
  try {
    body = await request.json() as { code?: string };
  } catch {
    return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 });
  }

  try {
    const result = await verifyPublicAccessCode(tenantId, body.code ?? '');
    return NextResponse.json(result);
  } catch {
    return NextResponse.json({ message: 'Access-code verification unavailable.' }, { status: 503 });
  }
}
