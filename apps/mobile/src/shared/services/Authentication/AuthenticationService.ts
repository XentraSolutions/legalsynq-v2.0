import { getDefaultStore } from 'jotai';

import { AuthenticationApi } from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { ConfigService } from '@/shared/services/Config';
import { LoggerService } from '@/shared/services/Logger';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import type { SessionEnvelope, UserSession } from '@/shared/types/auth';

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

async function persistSession(accessToken: string, sessionEnvelope: SessionEnvelope): Promise<UserSession> {
  const authState = AuthenticationAdapter.toAuthState(accessToken, sessionEnvelope);

  await Promise.all([
    SecureStorageService.setItem(STORAGE_KEYS.ACCESS_TOKEN, accessToken),
    SecureStorageService.setItem(STORAGE_KEYS.USER_SESSION, JSON.stringify(sessionEnvelope.user)),
  ]);

  store.set(authAtom, authState);
  return sessionEnvelope.user;
}

function createDemoSession(email: string): { accessToken: string; sessionEnvelope: SessionEnvelope } {
  const now = new Date();
  const expires = new Date(now.getTime() + 8 * 60 * 60 * 1000);

  return {
    accessToken: 'demo-access-token',
    sessionEnvelope: {
      issuedAt: now.toISOString(),
      expiresAt: expires.toISOString(),
      tenantId: 'tenant-demo',
      user: {
        id: 'usr-demo-1',
        email,
        firstName: 'Avery',
        lastName: 'Mendoza',
        roles: ['TenantAdmin', 'SYNQLIEN_SELLER', 'SYNQLIEN_BUYER'],
        permissions: ['liens.read', 'liens.write', 'offers.write'],
        organization: {
          id: 'org-smith-law',
          name: 'Smith Law Firm',
          tenantId: 'tenant-demo',
        },
        tenantId: 'tenant-demo',
      },
    },
  };
}

export const AuthenticationService = {
  async login(email: string, password: string, tenantCode?: string): Promise<UserSession> {
    try {
      const response = await AuthenticationApi.login({ email, password, tenantCode });
      return persistSession(response.accessToken, response.sessionEnvelope);
    } catch (error) {
      if (ConfigService.getEnvironment() !== 'development') {
        throw error;
      }

      LoggerService.warn('Using development demo session after authentication API failure');
      const response = createDemoSession(email);
      return persistSession(response.accessToken, response.sessionEnvelope);
    }
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
