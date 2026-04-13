import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

export async function POST(request: NextRequest) {
  let body: Record<string, string>;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: 'Invalid request body' }, { status: 400 });
  }

  const { token, newPassword } = body;

  if (!token) {
    return NextResponse.json({ message: 'Reset token is required' }, { status: 400 });
  }
  if (!newPassword || newPassword.length < 8) {
    return NextResponse.json({ message: 'Password must be at least 8 characters' }, { status: 400 });
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/password-reset/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, newPassword }),
    });
  } catch {
    return NextResponse.json({ message: 'Identity service unavailable' }, { status: 503 });
  }

  const data = await identityRes.json().catch(() => ({}));

  if (!identityRes.ok) {
    return NextResponse.json(
      { message: data.error ?? 'Failed to reset password. The link may have expired.' },
      { status: identityRes.status },
    );
  }

  return NextResponse.json({ message: data.message ?? 'Password updated successfully.' });
}
