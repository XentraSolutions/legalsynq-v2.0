import * as SecureStore from 'expo-secure-store';

import { AuthenticationApi } from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { TenantSelectionService } from '@/shared/services/TenantSelection';

import { AuthenticationService } from './AuthenticationService';

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

jest.mock('@/shared/api/endpoints/Authentication', () => ({
  AuthenticationApi: {
    login: jest.fn(),
    logout: jest.fn(),
  },
}));

const secureStore = SecureStore as typeof SecureStore & { __store: Map<string, string> };
const authenticationApi = AuthenticationApi as unknown as {
  login: any;
  logout: any;
};

const loginResponse = {
  accessToken: 'token',
  expiresAtUtc: '2026-07-15T00:00:00Z',
  user: {
    id: 'usr-1',
    tenantId: 'tenant-1',
    email: 'avery.mendoza@smithlaw.example',
    firstName: 'Avery',
    lastName: 'Mendoza',
    isActive: true,
    roles: ['TenantAdmin'],
    organizationId: 'org-1',
    orgType: 'Smith Law Firm',
    productRoles: ['SYNQLIEN_SELLER'],
  },
  tenants: [{ tenantId: 'tenant-1', tenantCode: 'smith-law', tenantName: 'Smith Law Firm' }],
};

describe('AuthenticationService', () => {
  beforeEach(async () => {
    secureStore.__store.clear();
    jest.clearAllMocks();
    await TenantSelectionService.clearRememberedTenants();
  });

  it('uses the active local tenant for returning login and confirms it after success', async () => {
    const pendingTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    authenticationApi.login.mockResolvedValue(loginResponse);

    await AuthenticationService.login({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      activeTenant: pendingTenant,
    });

    expect(authenticationApi.login).toHaveBeenCalledWith({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      tenantCode: 'smith-law',
    });
    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      id: pendingTenant.id,
      tenantId: 'tenant-1',
      tenantName: 'Smith Law Firm',
      isConfirmed: true,
    });
  });

  it('keeps remembered tenant storage when logging out', async () => {
    await TenantSelectionService.addLocalTenantCode('smith-law');
    await secureStore.setItemAsync(STORAGE_KEYS.ACCESS_TOKEN, 'token');
    authenticationApi.logout.mockResolvedValue(undefined);

    await AuthenticationService.logout();

    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      tenantCode: 'smith-law',
    });
    await expect(secureStore.getItemAsync(STORAGE_KEYS.ACCESS_TOKEN)).resolves.toBeNull();
  });
});
