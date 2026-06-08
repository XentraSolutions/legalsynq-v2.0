'use server';

import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi } from '@/lib/tenant-api';

export interface ManageAccessCodeResult {
  success: boolean;
  configured?: boolean;
  version?: number;
  updatedAtUtc?: string | null;
  revealedCode?: string;
  error?: string;
}

export async function setMyNetworkAccessCode(code: string): Promise<ManageAccessCodeResult> {
  const session = await requireTenantAdmin();
  try {
    const result = await tenantServerApi.setCareConnectAccessCode(session.tenantId, code);
    return { success: true, ...result };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to save access code.',
    };
  }
}

export async function clearMyNetworkAccessCode(): Promise<ManageAccessCodeResult> {
  const session = await requireTenantAdmin();
  try {
    const result = await tenantServerApi.clearCareConnectAccessCode(session.tenantId);
    return { success: true, ...result };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to clear access code.',
    };
  }
}
