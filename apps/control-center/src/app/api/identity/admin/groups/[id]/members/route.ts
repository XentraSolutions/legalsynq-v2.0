/**
 * POST /api/identity/admin/groups/[id]/members
 *
 * BFF proxy — adds a user to a group.
 * Body: { userId: string }
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  request: NextRequest,
  { params }: { params: { id: string } },
): Promise<NextResponse> {
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { userId?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.userId) {
    return NextResponse.json({ message: 'userId is required.' }, { status: 400 });
  }

  try {
    await controlCenterServerApi.groups.addMember(params.id, body.userId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to add group member.';
    const status  = message.includes('409') || message.toLowerCase().includes('conflict') ? 409 : 500;
    return NextResponse.json({ message }, { status });
  }
}
