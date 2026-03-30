import { requireOrg } from '@/lib/auth-guards';
import { BlankPage }  from '@/components/ui/blank-page';

/**
 * /activity — Tenant portal: activity & audit log viewer.
 *
 * Access: authenticated org member (requireOrg guard).
 *
 * Phase 1: placeholder shell — renders the standard BlankPage skeleton so the
 * route is reachable and nav links resolve correctly.
 *
 * Phase 2 (next step): wire GET /audit-service/audit/events scoped to the
 * authenticated org's tenantId, render a read-only event table matching the
 * control-center CanonicalAuditTable design but with tenant-safe columns only
 * (no platform-internal fields such as ipAddress, integrityhash, source).
 */
export default async function ActivityPage() {
  await requireOrg();
  return <BlankPage />;
}
