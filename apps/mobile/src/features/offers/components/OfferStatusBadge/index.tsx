import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import type { OfferStatus } from '@/shared/api/endpoints/Liens';

const VARIANTS: Record<OfferStatus, BadgeVariant> = {
  PENDING: 'warning',
  ACCEPTED: 'success',
  DECLINED: 'error',
  WITHDRAWN: 'neutral',
  EXPIRED: 'neutral',
};

export function OfferStatusBadge({ status }: { status: OfferStatus }) {
  return <Badge label={status} variant={VARIANTS[status]} />;
}
