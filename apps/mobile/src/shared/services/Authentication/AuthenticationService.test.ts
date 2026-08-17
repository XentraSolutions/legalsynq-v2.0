import * as SecureStore from 'expo-secure-store';
import { getDefaultStore } from 'jotai';

import { AuthenticationApi } from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { TenantSelectionService } from '@/shared/services/TenantSelection';
import { StorageService } from '@/shared/services/Storage';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';
import { authAtom } from '@/shared/state/atoms/authAtom';

import { AuthenticationService } from './AuthenticationService';
import { BiometricCredentialService } from './BiometricCredentialService';
import { BiometricPreferenceService } from './BiometricPreferenceService';

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
const jotaiStore = getDefaultStore();

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
    jotaiStore.set(apiModeAtom, 'current');
    jotaiStore.set(authAtom, {
      user: null,
      token: null,
      isAuthenticated: false,
      status: 'hydrating',
      sessionVersion: 0,
    });
  });

  it('resolves hydration unauthenticated when no complete stored session exists', async () => {
    await AuthenticationService.hydrateSession();

    expect(jotaiStore.get(authAtom)).toEqual({
      user: null,
      token: null,
      isAuthenticated: false,
      status: 'unauthenticated',
      sessionVersion: 0,
    });
  });

  it('hydrates a complete legacy stored session into the existing auth atom', async () => {
    jotaiStore.set(apiModeAtom, 'legacy');
    await secureStore.setItemAsync(STORAGE_KEYS.LEGACY_ACCESS_TOKEN, 'legacy-token');
    await StorageService.setItem(
      STORAGE_KEYS.USER_SESSION,
      JSON.stringify({
        id: 'usr-1',
        email: 'avery.mendoza@smithlaw.example',
        firstName: 'Avery',
        lastName: 'Mendoza',
        roles: [],
        permissions: [],
        organization: { id: 'org-1', name: 'Smith Law', tenantId: 'tenant-1' },
        tenantId: 'tenant-1',
      })
    );

    await AuthenticationService.hydrateSession();

    expect(jotaiStore.get(authAtom)).toMatchObject({
      isAuthenticated: true,
      status: 'authenticated',
      token: 'legacy-token',
      user: { id: 'usr-1', tenantId: 'tenant-1' },
    });
  });

  it('fails closed and removes a malformed stored user session', async () => {
    jotaiStore.set(apiModeAtom, 'legacy');
    await secureStore.setItemAsync(STORAGE_KEYS.LEGACY_ACCESS_TOKEN, 'legacy-token');
    await StorageService.setItem(STORAGE_KEYS.USER_SESSION, '{malformed');

    await AuthenticationService.hydrateSession();

    expect(jotaiStore.get(authAtom).status).toBe('unauthenticated');
    await expect(secureStore.getItemAsync(STORAGE_KEYS.LEGACY_ACCESS_TOKEN)).resolves.toBeNull();
    await expect(StorageService.getItem(STORAGE_KEYS.USER_SESSION)).resolves.toBeNull();
  });

  it('increments the session version whenever access session state is cleared', async () => {
    await AuthenticationService.clearAccessSession();
    await AuthenticationService.clearAccessSession();

    expect(jotaiStore.get(authAtom).sessionVersion).toBe(2);
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
      deviceInfo: {
        platform: 'ios',
        appVersion: '3.0.0',
        osVersion: 'unknown',
        deviceDisplayName: 'iOS device',
      },
    });
    await expect(TenantSelectionService.getActiveTenant()).resolves.toMatchObject({
      id: pendingTenant.id,
      tenantId: 'tenant-1',
      tenantName: 'Smith Law Firm',
      isConfirmed: true,
    });
  });

  it('prioritizes the selected tenant over a stale hidden login-form tenant code', async () => {
    const selectedTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    authenticationApi.login.mockResolvedValue(loginResponse);

    await AuthenticationService.login({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      tenantCode: 'stale-default-tenant',
      activeTenant: selectedTenant,
    });

    expect(authenticationApi.login).toHaveBeenCalledWith({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      tenantCode: 'smith-law',
      deviceInfo: {
        platform: 'ios',
        appVersion: '3.0.0',
        osVersion: 'unknown',
        deviceDisplayName: 'iOS device',
      },
    });
  });

  it('keeps the current access token in memory and returns masked biometric enrollment data', async () => {
    const selectedTenant = await TenantSelectionService.addLocalTenantCode('smith-law');
    authenticationApi.login.mockResolvedValue({
      ...loginResponse,
      refreshToken: 'refresh-token',
      deviceSessionId: 'device-session-1',
    });

    const outcome = await AuthenticationService.login({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      activeTenant: selectedTenant,
    });

    expect(outcome.biometricEnrollment).toMatchObject({
      accountLabel: 'a***@smithlaw.example',
      deviceSessionId: 'device-session-1',
      refreshToken: 'refresh-token',
      tenantId: 'tenant-1',
    });
    await expect(secureStore.getItemAsync(STORAGE_KEYS.ACCESS_TOKEN)).resolves.toBeNull();
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

  it('keeps enabled biometric login available after logging out', async () => {
    authenticationApi.logout.mockResolvedValue(undefined);
    await BiometricCredentialService.save('refresh-token');
    await BiometricPreferenceService.set({
      enabled: true,
      accountLabel: 'a***@smithlaw.example',
      deviceSessionId: 'device-session-1',
      tenantId: 'tenant-1',
      userId: 'usr-1',
      user: {
        id: 'usr-1',
        email: 'avery.mendoza@smithlaw.example',
        firstName: 'Avery',
        lastName: 'Mendoza',
        roles: ['TenantAdmin'],
        permissions: [],
        organization: {
          id: 'org-1',
          name: 'Smith Law Firm',
          tenantId: 'tenant-1',
        },
        tenantId: 'tenant-1',
      },
    });

    await AuthenticationService.logout();

    await expect(BiometricCredentialService.get()).resolves.toBe('refresh-token');
    await expect(BiometricPreferenceService.get()).resolves.toMatchObject({
      enabled: true,
      deviceSessionId: 'device-session-1',
    });
  });
});
