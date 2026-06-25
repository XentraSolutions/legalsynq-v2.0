import { getDefaultStore } from 'jotai';

import { AuthenticationApi, type LoginResponse } from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import type { UserSession } from '@/shared/types/auth';

import { AuthenticationAdapter } from './AuthenticationAdapter';

const store = getDefaultStore();

async function clearSession(): Promise<void> {
  await SecureStorageService.clearAll();
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
    SecureStorageService.setItem(STORAGE_KEYS.USER_SESSION, JSON.stringify(user)),
  ]);

  store.set(authAtom, authState);
  return user;
}

export const AuthenticationService = {
  async login(email: string, password: string, tenantCode: string): Promise<UserSession> {
    const response = await AuthenticationApi.login({ email, password, tenantCode });
    return persistSession(response);
  },

  async logout(): Promise<void> {
    try {
      await AuthenticationApi.logout();
    } finally {
      await clearSession();
    }
  },

  async getSession(): Promise<UserSession | null> {
    const serializedSession = await SecureStorageService.getItem(STORAGE_KEYS.USER_SESSION);
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
