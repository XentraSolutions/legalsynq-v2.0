'use server';

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
 * Called from ProductEntitlementsPanel (client component).
 * Uses the mock API stub; wire to real endpoint by updating
 * controlCenterServerApi.tenants.updateEntitlement.
 */
export async function updateProductEntitlement(
  tenantId:    string,
  productCode: ProductCode,
  enabled:     boolean,
): Promise<UpdateEntitlementResult> {
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
