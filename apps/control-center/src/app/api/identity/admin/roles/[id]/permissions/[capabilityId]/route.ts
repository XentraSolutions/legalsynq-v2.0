/**
 * DELETE /api/identity/admin/roles/[id]/permissions/[capabilityId]
 *
 * BFF proxy — revoke a capability permission from a role.
 *
 * Access: PlatformAdmin or TenantAdmin (boundary enforced by Identity service).
 * UIX-005-01: Widened from requirePlatformAdmin → requireAdmin.
 *             The Identity service enforces system-role and cross-tenant guards.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

type Ctx = { params: { id: string; capabilityId: string } };

export async function DELETE(
  _request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.roles.revokePermission(params.id, params.capabilityId);
    return new NextResponse(null, { status: 204 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to revoke permission.';
    const status  = message.includes('404') ? 404
                  : message.includes('403') ? 403
                  : 500;
    return NextResponse.json({ message }, { status });
  }
}
