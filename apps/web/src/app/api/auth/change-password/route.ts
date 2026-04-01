import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';

/**
 * BFF change-password route — POST /api/auth/change-password
 *
 * Flow:
 *   1. Read the platform_session HttpOnly cookie.
 *   2. If absent → 401.
 *   3. Forward the request body to POST ${GATEWAY_URL}/identity/api/auth/change-password
 *      with Authorization: Bearer <token>.
 *   4. Return the identity service response to the client.
 *
 * The raw JWT never leaves the server. The browser only sends { currentPassword, newPassword }.
 */
export async function POST(request: NextRequest) {
  const token = request.cookies.get('platform_session')?.value;

  if (!token) {
    return NextResponse.json({ error: 'Not authenticated' }, { status: 401 });
  }

  let body: string;
  try { body = await request.text(); } catch { body = '{}'; }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/change-password`, {
      method:  'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type':  'application/json',
      },
      body,
    });
  } catch {
    return NextResponse.json({ error: 'Identity service unavailable' }, { status: 503 });
  }

  const data = await identityRes.json().catch(() => ({}));
  return NextResponse.json(data, { status: identityRes.status });
}
