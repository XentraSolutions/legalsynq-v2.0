/**
 * DELETE /api/identity/admin/users/[id]/roles/[roleId]
 *
 * BFF proxy — revokes a role from a user.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function DELETE(
  _request: NextRequest,
  { params }: { params: { id: string; roleId: string } },
): Promise<NextResponse> {
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.users.revokeRole(params.id, params.roleId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to revoke role.';
    const status  = message.includes('404') || message.toLowerCase().includes('not found') ? 404 : 500;
    return NextResponse.json({ message }, { status });
  }
}
