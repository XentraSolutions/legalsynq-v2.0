import { requireOrg } from '@/lib/auth-guards';
import { IntakeQueue } from './intake-queue';

export const dynamic = 'force-dynamic';

export default async function IntakePage() {
  const session = await requireOrg();
  const permissions = session.permissions ?? [];
  const canManage = session.isTenantAdmin || session.isPlatformAdmin ||
    permissions.some((permission) => permission === 'intake.review.manage');
  const canAssign = canManage || permissions.includes('intake.review.assign');
  const canComplete = canManage || permissions.includes('intake.review.complete');
  return (
    <IntakeQueue
      currentUserId={session.userId}
      canManage={canManage}
      canAssign={canAssign}
      canComplete={canComplete}
    />
  );
}