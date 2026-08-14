import { requireOrg } from '@/lib/auth-guards';
import { ReviewWorkspace } from '../review-workspace';

export const dynamic = 'force-dynamic';

export default async function ReviewDetailPage({ params }: { params: Promise<{ reviewId: string }> }) {
  const [{ reviewId }, session] = await Promise.all([params, requireOrg()]);
  const permissions = session.permissions ?? [];
  const canManage = session.isTenantAdmin || session.isPlatformAdmin ||
    permissions.some((permission) => permission === 'intake.review.manage');
  const canAssign = canManage || permissions.includes('intake.review.assign');
  const canComplete = canManage || permissions.includes('intake.review.complete');
  return (
    <ReviewWorkspace
      reviewId={reviewId}
      currentUserId={session.userId}
      canManage={canManage}
      canAssign={canAssign}
      canComplete={canComplete}
    />
  );
}