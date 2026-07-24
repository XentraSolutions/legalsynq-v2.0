import { describe, expect, test } from 'vitest';
import { ProductRole, SystemRole } from '@/types';
import { isEligibleForSynqLienFundingPortal } from './access';

describe('isEligibleForSynqLienFundingPortal', () => {
  test('allows buyer', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.SynqLienBuyer],
    })).toBe(true);
  });

  test('denies buyer and holder', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.SynqLienBuyer, ProductRole.SynqLienHolder],
    })).toBe(false);
  });

  test('denies seller', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.SynqLienBuyer, ProductRole.SynqLienSeller],
    })).toBe(false);
  });

  test('denies tenant admin', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: true,
      productRoles: [ProductRole.SynqLienBuyer],
    })).toBe(false);
  });

  test('denies non-admin system roles', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      systemRoles: [SystemRole.StandardUser],
      productRoles: [ProductRole.SynqLienBuyer],
    })).toBe(false);
  });

  test('denies missing buyer role', () => {
    expect(isEligibleForSynqLienFundingPortal({
      isPlatformAdmin: false,
      isTenantAdmin: false,
      productRoles: [ProductRole.SynqLienHolder],
    })).toBe(false);
  });
});
