'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { TenantDetail } from '@/types/control-center';

export interface CreateTenantResult {
  success: boolean;
  tenant?: {
    tenantId:            string;
    displayName:         string;
    code:                string;
    subdomain?:          string;
    provisioningStatus?: string;
    hostname?:           string;
  };
  adminUser?: {
    adminUserId:       string | null;
    adminEmail:        string;
    temporaryPassword: string | null;
  };
  error?: string;
}

export async function createTenantAction(data: {
  name:               string;
  code:               string;
  orgType:            string;
  adminEmail:         string;
  adminFirstName:     string;
  adminLastName:      string;
  addressLine1?:      string;
  city?:              string;
  state?:             string;
  postalCode?:        string;
  latitude?:          number;
  longitude?:         number;
  geoPointSource?:    string;
}): Promise<CreateTenantResult> {
  await requirePlatformAdmin();

  try {
    const result = await controlCenterServerApi.tenants.create(data);
    return {
      success: true,
      tenant: {
        tenantId:            result.tenantId,
        displayName:         result.displayName,
        code:                result.code,
        subdomain:           result.subdomain,
        provisioningStatus:  result.provisioningStatus,
        hostname:            result.hostname,
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

export interface RetryProvisioningResult {
  success:            boolean;
  provisioningStatus: string;
  hostname?:          string;
  failureStage?:      string;
  error?:             string;
  attemptNumber?:     number;
  stillRetrying?:     boolean;
  exhausted?:         boolean;
  nextRetryAtUtc?:    string;
}

export async function retryProvisioningAction(tenantId: string): Promise<RetryProvisioningResult> {
  await requirePlatformAdmin();

  try {
    const result = await controlCenterServerApi.tenants.retryProvisioning(tenantId);
    return result;
  } catch (err) {
    return {
      success: false,
      provisioningStatus: 'Failed',
      error: err instanceof Error ? err.message : 'Failed to retry provisioning.',
    };
  }
}

export interface RetryVerificationResult {
  success:            boolean;
  provisioningStatus: string;
  hostname?:          string;
  error?:             string;
  failureStage?:      string;
  attemptNumber?:     number;
  stillRetrying?:     boolean;
  exhausted?:         boolean;
  nextRetryAtUtc?:    string;
}

export async function retryVerificationAction(tenantId: string): Promise<RetryVerificationResult> {
  await requirePlatformAdmin();

  try {
    const result = await controlCenterServerApi.tenants.retryVerification(tenantId);
    return result;
  } catch (err) {
    return {
      success: false,
      provisioningStatus: 'Failed',
      error: err instanceof Error ? err.message : 'Failed to retry verification.',
    };
  }
}

export async function getTenantProvisioningAction(tenantId: string): Promise<TenantDetail | null> {
  await requirePlatformAdmin();
  return controlCenterServerApi.tenants.getById(tenantId);
}
