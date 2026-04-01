/**
 * POST /api/identity/admin/users/[id]/deactivate
 *
 * BFF proxy — deactivates a user account.
 * Called by the UserActions client component.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  _request: NextRequest,
  { params }: { params: { id: string } },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  try {
    await controlCenterServerApi.users.deactivate(params.id);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to deactivate user.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
