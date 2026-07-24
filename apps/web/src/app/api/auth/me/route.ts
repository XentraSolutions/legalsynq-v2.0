import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';
const IS_PROD = process.env.NODE_ENV === 'production';

/**
 * BFF /auth/me route — GET /api/auth/me
 *
 * Flow:
 *   1. Read the `platform_session` HttpOnly cookie from the incoming request
 *   2. If absent → 401 (not authenticated)
 *   3. Forward to GET ${GATEWAY_URL}/identity/api/auth/me
 *      with Authorization: Bearer <token>
 *   4. Return the Identity service's AuthMeResponse to the client
 *
 * This keeps the JWT on the server side only.
 * The browser JS never sees the raw token — only the session envelope.
 *
 * Called by:
 *   - SessionProvider (client component) on mount
 *   - getServerSession() (server-side, reads cookie + calls identity directly)
 */
export async function GET(request: NextRequest) {
  const token = request.cookies.get('platform_session')?.value;

  if (!token) {
    return NextResponse.json({ message: 'Not authenticated' }, { status: 401 });
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/me`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type':  'application/json',
      },
      cache: 'no-store',
    });
  } catch {
    return NextResponse.json({ message: 'Identity service unavailable' }, { status: 503 });
  }

  if (!identityRes.ok) {
    if (identityRes.status === 401 || identityRes.status === 403) {
      return NextResponse.json({ message: 'Session expired' }, { status: 401 });
    }
    return NextResponse.json(
      { message: 'Identity service error' },
      { status: identityRes.status >= 500 ? 503 : identityRes.status },
    );
  }

  const data = await identityRes.json();
  const { refreshedAccessToken, ...sessionEnvelope } = data as Record<string, unknown>;

  // Forward X-Correlation-Id from the identity response if present
  const correlationId = identityRes.headers.get('X-Correlation-Id');
  const headers: Record<string, string> = {};
  if (correlationId) headers['X-Correlation-Id'] = correlationId;

  const response = NextResponse.json(sessionEnvelope, { status: 200, headers });

  if (typeof refreshedAccessToken === 'string' && refreshedAccessToken.length > 0) {
    const expiresAtUtc = typeof sessionEnvelope.expiresAtUtc === 'string'
      ? sessionEnvelope.expiresAtUtc
      : null;
    const maxAgeSeconds = expiresAtUtc
      ? Math.max(0, Math.floor((new Date(expiresAtUtc).getTime() - Date.now()) / 1000))
      : undefined;

    response.cookies.set('platform_session', refreshedAccessToken, {
      httpOnly: true,
      secure: IS_PROD,
      sameSite: IS_PROD ? 'strict' : 'lax',
      path: '/',
      ...(maxAgeSeconds !== undefined ? { maxAge: maxAgeSeconds } : {}),
    });
  }

  return response;
}
