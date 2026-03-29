import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';
const IS_PROD     = process.env.NODE_ENV === 'production';

/**
 * BFF Logout route — POST /api/auth/logout
 * Clears the platform_session cookie and optionally notifies the backend.
 */
export async function POST(request: NextRequest) {
  const token = request.cookies.get('platform_session')?.value;

  if (token) {
    fetch(`${GATEWAY_URL}/identity/api/auth/logout`, {
      method:  'POST',
      headers: { 'Authorization': `Bearer ${token}` },
    }).catch(() => { /* best-effort — backend logout is stateless */ });
  }

  const response = NextResponse.json({ ok: true }, { status: 200 });

  response.cookies.set('platform_session', '', {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   0,
  });

  return response;
}
