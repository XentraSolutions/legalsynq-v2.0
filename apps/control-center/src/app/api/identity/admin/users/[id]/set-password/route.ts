import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { apiClient, ApiError }            from '@/lib/api-client';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requireAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  try {
    const body = await request.json() as { newPassword?: string };
    if (!body.newPassword || body.newPassword.length < 8) {
      return NextResponse.json(
        { message: 'Password must be at least 8 characters.' },
        { status: 400 },
      );
    }

    await apiClient.post(
      `/identity/api/admin/users/${encodeURIComponent(id)}/set-password`,
      { newPassword: body.newPassword },
    );
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to set password.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
