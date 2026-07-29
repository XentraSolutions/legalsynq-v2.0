import { getDefaultStore } from 'jotai';

import { AuthenticationApi, type LoginResponse } from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import { StorageService } from '@/shared/services/Storage';
import { TenantSelectionService } from '@/shared/services/TenantSelection';
import type { UserSession } from '@/shared/types/auth';
import type { RememberedTenant } from '@/shared/types/tenant';

import { AuthenticationAdapter } from './AuthenticationAdapter';

const store = getDefaultStore();

interface LoginInput {
  email: string;
  password: string;
  tenantCode?: string;
  activeTenant?: RememberedTenant | null;
}

async function clearSession(): Promise<void> {
  await Promise.all([
    SecureStorageService.clearAll(),
    StorageService.removeItem(STORAGE_KEYS.USER_SESSION),
  ]);
  store.set(authAtom, {
    user: null,
    token: null,
    isAuthenticated: false,
  });
}

async function persistSession(response: LoginResponse): Promise<UserSession> {
  const user = AuthenticationAdapter.toUserSession(response);
  const authState = AuthenticationAdapter.toAuthState(response.accessToken, user);

  await Promise.all([
    SecureStorageService.setItem(STORAGE_KEYS.ACCESS_TOKEN, response.accessToken),
    StorageService.setItem(STORAGE_KEYS.USER_SESSION, JSON.stringify(user)),
  ]);

  store.set(authAtom, authState);
  return user;
}

function responseTenantName(
  response: LoginResponse,
  tenantCode: string,
  activeTenant?: RememberedTenant | null
): string {
  const responseTenant =
    response.tenants?.find((item) => item.tenantId === response.user.tenantId) ??
    response.tenants?.[0];
  return (
    responseTenant?.tenantName?.trim() ||
    responseTenant?.name?.trim() ||
    response.user.orgType?.trim() ||
    activeTenant?.tenantName?.trim() ||
    tenantCode
  );
}

async function persistRememberedTenant(
  response: LoginResponse,
  tenantCode: string,
  activeTenant?: RememberedTenant | null
): Promise<void> {
  const responseTenant =
    response.tenants?.find((item) => item.tenantId === response.user.tenantId) ??
    response.tenants?.[0];

  await TenantSelectionService.upsertRememberedTenant({
    id: activeTenant?.id,
    tenantId: response.user.tenantId || responseTenant?.tenantId,
    tenantCode: responseTenant?.tenantCode || activeTenant?.tenantCode || tenantCode,
    tenantName: responseTenantName(response, tenantCode, activeTenant),
    apiEndpoint: responseTenant?.apiEndpoint ?? activeTenant?.apiEndpoint ?? null,
    isConfirmed: true,
  });
}

export const AuthenticationService = {
  async login({ email, password, tenantCode, activeTenant }: LoginInput): Promise<UserSession> {
    const effectiveTenantCode = tenantCode?.trim() || activeTenant?.tenantCode;
    if (!effectiveTenantCode) {
      throw new Error('Select or enter a tenant code before signing in.');
    }

    const response = await AuthenticationApi.login({
      email,
      password,
      tenantCode: effectiveTenantCode,
    });
    const user = await persistSession(response);
    await persistRememberedTenant(response, effectiveTenantCode, activeTenant);
    return user;
  },

  async logout(): Promise<void> {
    try {
      await AuthenticationApi.logout();
    } finally {
      await clearSession();
    }
  },

  async getSession(): Promise<UserSession | null> {
    const serializedSession = await StorageService.getItem(STORAGE_KEYS.USER_SESSION);
    if (!serializedSession) {
      return null;
    }

    try {
      return JSON.parse(serializedSession) as UserSession;
    } catch {
      await clearSession();
      return null;
    }
  },

  async isAuthenticated(): Promise<boolean> {
    return Boolean(await SecureStorageService.getItem(STORAGE_KEYS.ACCESS_TOKEN));
  },

  async clearSession(): Promise<void> {
    await clearSession();
  },
};
