'use server';

/**
 * tenants/[id]/actions.ts — Server Actions for Tenant Detail management.
 *
 * ── Security guards ──────────────────────────────────────────────────────────
 *
 *   Every action calls requirePlatformAdmin() before any mutation.
 *   This performs a full server-side session + role check:
 *     - No session cookie  → redirect /login?reason=unauthenticated
 *     - Session invalid    → redirect /login?reason=unauthenticated
 *     - Not PlatformAdmin  → redirect /login?reason=unauthorized
 *
 * TODO: add RBAC enforcement middleware
 * TODO: add rate limiting
 * TODO: add security headers (CSP, HSTS)
 */

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { ProductCode, ProductEntitlementSummary } from '@/types/control-center';

export interface UpdateEntitlementResult {
  success:      boolean;
  entitlement?: ProductEntitlementSummary;
  error?:       string;
}

/**
 * Server Action: toggle a product entitlement for a tenant.
 *
 * Requires an active PlatformAdmin session. Called from ProductEntitlementsPanel
 * (client component). Uses the mock API stub; wire to real endpoint by updating
 * controlCenterServerApi.tenants.updateEntitlement.
 */
export async function updateProductEntitlement(
  tenantId:    string,
  productCode: ProductCode,
  enabled:     boolean,
): Promise<UpdateEntitlementResult> {
  await requirePlatformAdmin();
  try {
    const entitlement = await controlCenterServerApi.tenants.updateEntitlement(
      tenantId,
      productCode,
      enabled,
    );
    return { success: true, entitlement };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to update entitlement.',
    };
  }
}
