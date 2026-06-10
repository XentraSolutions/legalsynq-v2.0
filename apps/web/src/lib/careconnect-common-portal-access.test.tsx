import { describe, expect, test } from 'vitest';
import { ProductRole } from '@/types';
import { isEligibleForCareConnectCommonPortal } from './careconnect-common-portal-access';

describe('isEligibleForCareConnectCommonPortal', () => {
  test('allows referrer', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.CareConnectReferrer],
    })).toBe(true);
  });

  test('allows receiver', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.CareConnectReceiver],
    })).toBe(true);
  });

  test('denies network manager', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.CareConnectNetworkManager],
    })).toBe(false);
  });

  test('denies tenant admin', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: false,
      isTenantAdmin: true,
      productRoles: [ProductRole.CareConnectReceiver],
    })).toBe(false);
  });

  test('denies platform admin', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: true,
      isTenantAdmin: false,
      productRoles: [ProductRole.CareConnectReferrer],
    })).toBe(false);
  });

  test('denies mixed allowed and disallowed roles', () => {
    expect(isEligibleForCareConnectCommonPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [
        ProductRole.CareConnectReferrer,
        ProductRole.CareConnectNetworkManager,
      ],
    })).toBe(false);
  });
});
