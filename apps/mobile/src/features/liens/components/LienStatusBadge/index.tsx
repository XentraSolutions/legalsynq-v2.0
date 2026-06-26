import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import type { LienStatus } from '@/shared/api/endpoints/Liens';

const STATUS_VARIANTS: Record<LienStatus, BadgeVariant> = {
  AVAILABLE: 'lien-available',
  PENDING: 'lien-pending',
  SOLD: 'lien-sold',
  SETTLED: 'lien-settled',
  DRAFT: 'lien-draft',
  DISPUTED: 'error',
};

export function LienStatusBadge({ status }: { status: LienStatus }) {
  return <Badge label={status} variant={STATUS_VARIANTS[status]} />;
}
