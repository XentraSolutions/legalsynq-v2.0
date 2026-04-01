/**
 * DELETE /api/identity/admin/groups/[id]/members/[userId]
 *
 * BFF proxy — removes a user from a group.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function DELETE(
  _request: NextRequest,
  { params }: { params: { id: string; userId: string } },
): Promise<NextResponse> {
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.groups.removeMember(params.id, params.userId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to remove group member.';
    const status  = message.includes('404') || message.toLowerCase().includes('not found') ? 404 : 500;
    return NextResponse.json({ message }, { status });
  }
}
