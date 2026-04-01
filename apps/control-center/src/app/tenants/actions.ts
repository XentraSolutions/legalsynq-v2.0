'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';

export interface CreateTenantResult {
  success: boolean;
  tenant?: {
    tenantId:    string;
    displayName: string;
    code:        string;
  };
  adminUser?: {
    adminUserId:       string;
    adminEmail:        string;
    temporaryPassword: string;
  };
  error?: string;
}

/**
 * Server Action: create a new tenant with a default admin user.
 *
 * Requires an active PlatformAdmin session.
 * Returns the new tenant info + a one-time temporary password on success.
 */
export async function createTenantAction(data: {
  name:           string;
  code:           string;
  adminEmail:     string;
  adminFirstName: string;
  adminLastName:  string;
}): Promise<CreateTenantResult> {
  await requirePlatformAdmin();

  try {
    const result = await controlCenterServerApi.tenants.create(data);
    return {
      success: true,
      tenant: {
        tenantId:    result.tenantId,
        displayName: result.displayName,
        code:        result.code,
      },
      adminUser: {
        adminUserId:       result.adminUserId,
        adminEmail:        result.adminEmail,
        temporaryPassword: result.temporaryPassword,
      },
    };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to create tenant.',
    };
  }
}
