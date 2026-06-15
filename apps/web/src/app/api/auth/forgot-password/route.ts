import { type NextRequest, NextResponse } from 'next/server';
import { normalizeCareConnectPortalHost } from '@/lib/careconnect-login-url';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';
// AUTH-CC01: When the request arrives on this hostname, always resolve the tenant
// from the user's email — no tenant code or subdomain lookup required.
// Matches CC_COMMON_PORTAL_HOSTNAME in middleware.ts.
const CC_COMMON_PORTAL_HOSTNAME = normalizeCareConnectPortalHost(process.env.CC_COMMON_PORTAL_HOSTNAME);

if (!CC_COMMON_PORTAL_HOSTNAME && process.env.NODE_ENV !== 'test') {
  console.warn(
    '[forgot-password] CC_COMMON_PORTAL_HOSTNAME is not set — ' +
    'CareConnect common-portal forgot-password will not work. ' +
    'Set CC_COMMON_PORTAL_HOSTNAME in the environment.',
  );
}

interface RateLimitEntry {
  count: number;
  resetAt: number;
}

const forgotPasswordRateLimit = new Map<string, RateLimitEntry>();
const FORGOT_PASSWORD_LIMIT  = 5;
const FORGOT_PASSWORD_WINDOW = 15 * 60 * 1000;

function checkForgotPasswordRateLimit(ip: string): boolean {
  const now   = Date.now();
  const entry = forgotPasswordRateLimit.get(ip);
  if (!entry || now > entry.resetAt) {
    forgotPasswordRateLimit.set(ip, { count: 1, resetAt: now + FORGOT_PASSWORD_WINDOW });
    return true;
  }
  if (entry.count >= FORGOT_PASSWORD_LIMIT) return false;
  entry.count++;
  return true;
}

function extractRawSubdomain(req: NextRequest): string | null {
  const host =
    req.headers.get('x-forwarded-host') ??
    req.headers.get('host') ??
    '';
  const hostClean = host.split(',')[0].trim();
  const hostWithoutPort = hostClean.includes(':') ? hostClean.split(':')[0] : hostClean;
  const lower = hostWithoutPort.toLowerCase();
  const parts = lower.split('.');
  if (parts.length < 3 || parts[0] === 'www') return null;
  return parts[0];
}

export async function POST(request: NextRequest) {
  const ip =
    request.headers.get('x-forwarded-for')?.split(',')[0].trim() ??
    request.headers.get('x-real-ip') ??
    'unknown';

  if (!checkForgotPasswordRateLimit(ip)) {
    return NextResponse.json(
      { message: 'Too many requests. Please wait before trying again.' },
      { status: 429 },
    );
  }

  let body: Record<string, string>;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: 'Invalid request body' }, { status: 400 });
  }

  const { email, tenantCode: explicitTenantCode } = body;

  if (!email) {
    return NextResponse.json({ message: 'Email is required' }, { status: 400 });
  }

  const rawHost = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? '';
  const incomingHost = rawHost.split(':')[0].toLowerCase();
  const rawSubdomain = extractRawSubdomain(request);

  // AUTH-CC01: If the request is arriving on the configured common portal hostname,
  // skip all tenant-code resolution and tell Identity to resolve by email.
  // SECURITY: isCommonPortalHost is derived from x-forwarded-host / host headers.
  // This is safe only if the reverse proxy strips or overwrites these headers before
  // forwarding — do not deploy without proxy-level header enforcement.
  const isCommonPortalHost =
    !!CC_COMMON_PORTAL_HOSTNAME && incomingHost === CC_COMMON_PORTAL_HOSTNAME;

  const tenantCode = isCommonPortalHost
    ? ''                                       // no tenant code on common portal path
    : (explicitTenantCode?.trim() || rawSubdomain);

  if (!isCommonPortalHost && !tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved.' },
      { status: 400 },
    );
  }

  // AUTH-B01: Resolve tenant from the Tenant service so Identity can use
  // tenantId as a fallback when code/subdomain lookup misses. Skipped on the
  // common portal path (resolveByEmail=true) since Identity handles the lookup.
  let resolvedTenantId: string | null = null;
  let resolvedTenantCode: string = tenantCode ?? '';
  const resolveByEmail = isCommonPortalHost;

  if (!isCommonPortalHost && rawSubdomain) {
    try {
      const tenantRes = await fetch(
        `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(rawSubdomain)}`,
        { headers: { 'Content-Type': 'application/json' } },
      );
      if (tenantRes.ok) {
        const tenantData = await tenantRes.json();
        if (tenantData?.tenantId) {
          resolvedTenantId = tenantData.tenantId as string;
          if (tenantData?.code) resolvedTenantCode = tenantData.code as string;
        }
      }
    } catch {
      // Non-fatal — Identity will fall back to code+subdomain lookup as before.
    }
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/forgot-password`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // Identifies this as a trusted BFF call — Identity requires this header
        // to accept ResolveByEmail=true. Must be stripped from external traffic
        // by the reverse proxy before forwarding.
        'X-Ls-Internal-Source': 'bff-forgot-password',
      },
      body: JSON.stringify({
        tenantCode: resolveByEmail ? '' : resolvedTenantCode,
        email,
        subdomain: rawSubdomain,
        tenantId: resolvedTenantId,
        resolveByEmail,
      }),
    });
  } catch (err) {
    console.error(`[forgot-password] Identity service fetch error:`, err);
    return NextResponse.json(
      { message: 'Password reset is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    const upstreamMessage = errBody.error ?? errBody.detail ?? errBody.title ?? null;
    console.log(`[forgot-password] Identity returned ${identityRes.status}: ${JSON.stringify(errBody)}`);

    if (identityRes.status >= 500) {
      console.error(`[forgot-password] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
      return NextResponse.json(
        { message: 'Password reset is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }

    if (identityRes.status === 429) {
      return NextResponse.json(
        { message: 'Too many requests. Please wait before trying again.' },
        { status: 429 },
      );
    }

    return NextResponse.json(
      { message: upstreamMessage ?? 'Unable to start password reset. Please check your details and try again.' },
      { status: identityRes.status },
    );
  }

  const data = await identityRes.json();

  return NextResponse.json({ message: data.message });
}
