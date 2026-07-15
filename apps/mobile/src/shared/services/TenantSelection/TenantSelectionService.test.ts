import * as SecureStore from 'expo-secure-store';

import { STORAGE_KEYS } from '@/shared/constants/storageKeys';

import { TenantSelectionService } from './TenantSelectionService';

jest.mock('expo-secure-store', () => {
  const store = new Map<string, string>();

  return {
    deleteItemAsync: jest.fn(async (key: string) => {
      store.delete(key);
    }),
    getItemAsync: jest.fn(async (key: string) => store.get(key) ?? null),
    setItemAsync: jest.fn(async (key: string, value: string) => {
      store.set(key, value);
    }),
    __store: store,
  };
});

const secureStore = SecureStore as typeof SecureStore & { __store: Map<string, string> };

describe('TenantSelectionService', () => {
  beforeEach(() => {
    secureStore.__store.clear();
    jest.clearAllMocks();
  });

  it('returns empty defaults when no tenant is stored', async () => {
    await expect(TenantSelectionService.getRememberedTenants()).resolves.toEqual([]);
    await expect(TenantSelectionService.getActiveTenant()).resolves.toBeNull();
  });

  it('adds a local tenant code as the active pending tenant', async () => {
    const tenant = await TenantSelectionService.addLocalTenantCode(' smith-law ');

    expect(tenant).toMatchObject({
      tenantCode: 'smith-law',
      tenantName: 'smith-law',
      isConfirmed: false,
    });
    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      id: tenant.id,
      tenantCode: 'smith-law',
    });
  });

  it('does not duplicate tenant codes', async () => {
    await TenantSelectionService.addLocalTenantCode('smith-law');
    await TenantSelectionService.addLocalTenantCode('SMITH-LAW');

    await expect(TenantSelectionService.getRememberedTenants()).resolves.toHaveLength(1);
  });

  it('confirms and enriches an existing local tenant after login', async () => {
    const pendingTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    const confirmedTenant = await TenantSelectionService.upsertRememberedTenant({
      tenantId: 'tenant-1',
      tenantCode: 'SMITH-LAW',
      tenantName: 'Smith Law Firm',
      isConfirmed: true,
    });

    expect(confirmedTenant).toMatchObject({
      id: pendingTenant.id,
      tenantId: 'tenant-1',
      tenantCode: 'SMITH-LAW',
      tenantName: 'Smith Law Firm',
      isConfirmed: true,
    });
    await expect(TenantSelectionService.getRememberedTenants()).resolves.toHaveLength(1);
  });

  it('clears malformed tenant storage safely', async () => {
    secureStore.__store.set(STORAGE_KEYS.REMEMBERED_TENANTS, '{bad json');
    secureStore.__store.set(STORAGE_KEYS.ACTIVE_TENANT_ID, 'tenant-code:bad');

    await expect(TenantSelectionService.getRememberedTenants()).resolves.toEqual([]);
    expect(secureStore.__store.has(STORAGE_KEYS.REMEMBERED_TENANTS)).toBe(false);
    expect(secureStore.__store.has(STORAGE_KEYS.ACTIVE_TENANT_ID)).toBe(false);
  });

  it('does not remove the only remembered tenant', async () => {
    const tenant = await TenantSelectionService.addLocalTenantCode('smith-law');

    await expect(TenantSelectionService.removeRememberedTenant(tenant.id)).resolves.toBe(false);
    await expect(TenantSelectionService.getRememberedTenants()).resolves.toHaveLength(1);
  });

  it('does not remove the active tenant when multiple tenants exist', async () => {
    const firstTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    await TenantSelectionService.addLocalTenantCode('nova-care');
    await TenantSelectionService.setActiveTenant(firstTenant.id);

    await expect(TenantSelectionService.removeRememberedTenant(firstTenant.id)).resolves.toBe(
      false
    );
    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      id: firstTenant.id,
    });
    await expect(TenantSelectionService.getRememberedTenants()).resolves.toHaveLength(2);
  });

  it('removes a non-active tenant when multiple tenants exist', async () => {
    const firstTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    const secondTenant = await TenantSelectionService.addLocalTenantCode('nova-care');
    await TenantSelectionService.setActiveTenant(firstTenant.id);

    await expect(TenantSelectionService.removeRememberedTenant(secondTenant.id)).resolves.toBe(
      true
    );
    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      id: firstTenant.id,
    });
    const tenants = await TenantSelectionService.getRememberedTenants();
    expect(tenants).toHaveLength(1);
    expect(tenants[0]).toMatchObject({ id: firstTenant.id });
  });
});
