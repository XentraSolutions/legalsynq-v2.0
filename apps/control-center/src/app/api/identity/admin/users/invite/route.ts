/**
 * POST /api/identity/admin/users/invite
 *
 * BFF proxy — sends a new user invitation.
 * Called by the InviteUserForm client component.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

interface InviteUserBody {
  email:           string;
  firstName:       string;
  lastName:        string;
  tenantId:        string;
  organizationId?: string;
  memberRole?:     string;
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  let body: InviteUserBody;
  try {
    body = await request.json() as InviteUserBody;
  } catch {
    return NextResponse.json({ message: 'Invalid JSON body' }, { status: 400 });
  }

  if (!body.email || !body.firstName || !body.lastName || !body.tenantId) {
    return NextResponse.json(
      { message: 'email, firstName, lastName, and tenantId are required.' },
      { status: 400 },
    );
  }

  try {
    await controlCenterServerApi.users.invite(body);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to send invitation.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
