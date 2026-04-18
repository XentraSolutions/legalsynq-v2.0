import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

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

  const rawSubdomain = extractRawSubdomain(request);
  const tenantCode = explicitTenantCode?.trim() || rawSubdomain;

  if (!tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved.' },
      { status: 400 },
    );
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tenantCode, email, subdomain: rawSubdomain }),
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

    // Upstream service failure (5xx) — do NOT blame the user; the identity service is broken.
    if (identityRes.status >= 500) {
      console.error(`[forgot-password] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
      return NextResponse.json(
        { message: 'Password reset is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }

    // 4xx — pass through upstream message if available, else a neutral fallback.
    return NextResponse.json(
      { message: upstreamMessage ?? 'Unable to start password reset. Please check your details and try again.' },
      { status: identityRes.status },
    );
  }

  const data = await identityRes.json();

  const host =
    request.headers.get('x-forwarded-host') ??
    request.headers.get('host') ??
    'localhost:3000';
  const protocol = request.headers.get('x-forwarded-proto') ?? 'http';
  const origin = `${protocol}://${host}`;

  const result: Record<string, string> = {
    message: data.message,
  };

  if (data.resetToken) {
    result.resetLink = `${origin}/reset-password?token=${encodeURIComponent(data.resetToken)}`;
  }

  return NextResponse.json(result);
}
