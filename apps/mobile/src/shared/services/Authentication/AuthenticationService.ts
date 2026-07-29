import { getDefaultStore } from 'jotai';

import {
  AuthenticationApi,
  LegacyAuthenticationAdapter,
  LegacyAuthenticationApi,
  type ForgotPasswordRequest,
  type LoginResponse,
} from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS, type StorageKey } from '@/shared/constants/storageKeys';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';
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

async function persistSession(
  accessToken: string,
  user: UserSession,
  tokenStorageKey: StorageKey
): Promise<UserSession> {
  const authState = AuthenticationAdapter.toAuthState(accessToken, user);

  await Promise.all([
    SecureStorageService.setItem(tokenStorageKey, accessToken),
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

async function loginCurrent({
  email,
  password,
  tenantCode,
  activeTenant,
}: LoginInput): Promise<UserSession> {
  const effectiveTenantCode = activeTenant?.tenantCode.trim() || tenantCode?.trim();
  if (!effectiveTenantCode) {
    throw new Error('Select or enter a tenant code before signing in.');
  }

  const response = await AuthenticationApi.login({
    email,
    password,
    tenantCode: effectiveTenantCode,
  });
  const user = AuthenticationAdapter.toUserSession(response);
  await persistSession(response.accessToken, user, STORAGE_KEYS.ACCESS_TOKEN);
  await persistRememberedTenant(response, effectiveTenantCode, activeTenant);
  return user;
}

async function loginLegacy({ email, password }: LoginInput): Promise<UserSession> {
  const response = await LegacyAuthenticationApi.login({ username: email, password });
  if (!response.isSuccess || !response.sessionId) {
    // TEMP DIAGNOSTIC: remove once the parsing question is resolved.
    let raw = '';
    try {
      raw = JSON.stringify(response).slice(0, 300);
    } catch {
      raw = String(response);
    }
    throw new Error(
      response?.message ||
        `Unable to sign in. [diag v3: typeof=${typeof response} isSuccess=${String(response?.isSuccess)} sessionId=${String(response?.sessionId)} raw=${raw}]`
    );
  }

  const user = LegacyAuthenticationAdapter.toUserSession(response);
  return persistSession(response.sessionId, user, STORAGE_KEYS.LEGACY_ACCESS_TOKEN);
}

export const AuthenticationService = {
  async login(input: LoginInput): Promise<UserSession> {
    const mode = store.get(apiModeAtom);
    return mode === 'legacy' ? loginLegacy(input) : loginCurrent(input);
  },

  async logout(): Promise<void> {
    const mode = store.get(apiModeAtom);
    try {
      if (mode === 'legacy') {
        await LegacyAuthenticationApi.logout();
      } else {
        await AuthenticationApi.logout();
      }
    } finally {
      await clearSession();
    }
  },

  async forgotPassword(body: ForgotPasswordRequest): Promise<void> {
    const mode = store.get(apiModeAtom);
    if (mode === 'legacy') {
      await LegacyAuthenticationApi.forgotPassword();
      return;
    }

    await AuthenticationApi.forgotPassword(body);
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
