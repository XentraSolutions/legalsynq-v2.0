import { getDefaultStore } from 'jotai';
import { Platform } from 'react-native';

import appPackage from '../../../../package.json';

import {
  AuthenticationApi,
  LegacyAuthenticationAdapter,
  LegacyAuthenticationApi,
  type ForgotPasswordRequest,
  type LoginResponse,
} from '@/shared/api/endpoints/Authentication';
import { STORAGE_KEYS, type StorageKey } from '@/shared/constants/storageKeys';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { biometricEnrollmentOfferAtom } from '@/shared/state/atoms/biometricAtom';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import { StorageService } from '@/shared/services/Storage';
import { TenantSelectionService } from '@/shared/services/TenantSelection';
import type { UserSession } from '@/shared/types/auth';
import type { RememberedTenant } from '@/shared/types/tenant';

import { AuthenticationAdapter } from './AuthenticationAdapter';
import { BiometricAuthenticationService } from './BiometricAuthenticationService';
import type { BiometricEnrollmentCredentials } from './biometricTypes';

const store = getDefaultStore();

interface LoginInput {
  email: string;
  password: string;
  tenantCode?: string;
  activeTenant?: RememberedTenant | null;
}

function maskedAccountLabel(email: string): string {
  const [localPart, domain] = email.split('@');
  if (!localPart || !domain) return 'your saved account';
  return `${localPart.slice(0, 1)}***@${domain}`;
}

export interface LoginOutcome {
  biometricEnrollment?: BiometricEnrollmentCredentials;
  user: UserSession;
}

function parseStoredUserSession(serializedSession: string): UserSession | null {
  try {
    const candidate = JSON.parse(serializedSession) as Partial<UserSession> | null;
    const organization = candidate?.organization;
    if (
      !candidate ||
      typeof candidate.id !== 'string' ||
      !candidate.id ||
      typeof candidate.email !== 'string' ||
      typeof candidate.firstName !== 'string' ||
      typeof candidate.lastName !== 'string' ||
      !Array.isArray(candidate.roles) ||
      !candidate.roles.every((role) => typeof role === 'string') ||
      !Array.isArray(candidate.permissions) ||
      !candidate.permissions.every((permission) => typeof permission === 'string') ||
      typeof candidate.tenantId !== 'string' ||
      !candidate.tenantId ||
      !organization ||
      typeof organization.id !== 'string' ||
      typeof organization.name !== 'string' ||
      organization.tenantId !== candidate.tenantId
    ) {
      return null;
    }

    return candidate as UserSession;
  } catch {
    return null;
  }
}

async function clearAccessSession(): Promise<void> {
  await Promise.allSettled([
    SecureStorageService.deleteItem(STORAGE_KEYS.ACCESS_TOKEN),
    SecureStorageService.deleteItem(STORAGE_KEYS.LEGACY_ACCESS_TOKEN),
    StorageService.removeItem(STORAGE_KEYS.USER_SESSION),
  ]);
  const currentAuth = store.get(authAtom);
  store.set(authAtom, {
    user: null,
    token: null,
    isAuthenticated: false,
    status: 'unauthenticated',
    sessionVersion: currentAuth.sessionVersion + 1,
  });
  store.set(biometricEnrollmentOfferAtom, {
    label: 'Biometrics',
    visible: false,
  });
  BiometricAuthenticationService.discardPendingEnrollment();
}

async function persistSession(
  accessToken: string,
  user: UserSession,
  tokenStorageKey?: StorageKey
): Promise<UserSession> {
  const authState = AuthenticationAdapter.toAuthState(accessToken, user);
  authState.sessionVersion = store.get(authAtom).sessionVersion;

  await Promise.all(
    [
      tokenStorageKey ? SecureStorageService.setItem(tokenStorageKey, accessToken) : null,
      StorageService.setItem(STORAGE_KEYS.USER_SESSION, JSON.stringify(user)),
    ].filter((operation): operation is Promise<void> => operation !== null)
  );

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
}: LoginInput): Promise<LoginOutcome> {
  const effectiveTenantCode = activeTenant?.tenantCode.trim() || tenantCode?.trim();
  if (!effectiveTenantCode) {
    throw new Error('Select or enter a tenant code before signing in.');
  }

  const response = await AuthenticationApi.login({
    email,
    password,
    tenantCode: effectiveTenantCode,
    deviceInfo: {
      platform: Platform.OS,
      appVersion: appPackage.version,
      osVersion: Platform.Version == null ? 'unknown' : String(Platform.Version),
      deviceDisplayName: `${Platform.OS === 'ios' ? 'iOS' : 'Android'} device`,
    },
  });
  const user = AuthenticationAdapter.toUserSession(response);
  await persistSession(response.accessToken, user);
  await persistRememberedTenant(response, effectiveTenantCode, activeTenant);
  const deviceSessionId = response.deviceSessionId;
  const biometricEnrollment =
    response.refreshToken && deviceSessionId
      ? {
          accountLabel: maskedAccountLabel(response.user.email),
          deviceSessionId,
          refreshToken: response.refreshToken,
          tenantId: response.user.tenantId,
          user,
        }
      : undefined;

  return { biometricEnrollment, user };
}

async function loginLegacy({ email, password }: LoginInput): Promise<LoginOutcome> {
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
  await persistSession(response.sessionId, user, STORAGE_KEYS.LEGACY_ACCESS_TOKEN);
  return { user };
}

export const AuthenticationService = {
  async hydrateSession(): Promise<void> {
    const mode = store.get(apiModeAtom);
    const tokenStorageKey =
      mode === 'legacy' ? STORAGE_KEYS.LEGACY_ACCESS_TOKEN : STORAGE_KEYS.ACCESS_TOKEN;

    let accessToken: string | null = null;
    let serializedSession: string | null = null;
    try {
      [accessToken, serializedSession] = await Promise.all([
        SecureStorageService.getItem(tokenStorageKey),
        StorageService.getItem(STORAGE_KEYS.USER_SESSION),
      ]);
    } catch {
      // Storage read failures fail closed below and leave the app usable for login.
    }

    const user = serializedSession ? parseStoredUserSession(serializedSession) : null;
    if (accessToken && user) {
      const authState = AuthenticationAdapter.toAuthState(accessToken, user);
      authState.sessionVersion = store.get(authAtom).sessionVersion;
      store.set(authAtom, authState);
      return;
    }

    await Promise.allSettled([
      SecureStorageService.deleteItem(tokenStorageKey),
      StorageService.removeItem(STORAGE_KEYS.USER_SESSION),
    ]);

    const currentAuth = store.get(authAtom);
    store.set(authAtom, {
      user: null,
      token: null,
      isAuthenticated: false,
      status: 'unauthenticated',
      sessionVersion: currentAuth.sessionVersion,
    });
  },

  async login(input: LoginInput): Promise<LoginOutcome> {
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
      await clearAccessSession();
    }
  },

  async establishSession(accessToken: string, user: UserSession): Promise<UserSession> {
    return persistSession(accessToken, user);
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
      await clearAccessSession();
      return null;
    }
  },

  async isAuthenticated(): Promise<boolean> {
    return store.get(authAtom).isAuthenticated;
  },

  async clearAccessSession(): Promise<void> {
    await clearAccessSession();
  },

  async clearSession(): Promise<void> {
    await Promise.all([
      clearAccessSession(),
      BiometricAuthenticationService.clearLocalEnrollment(),
    ]);
  },
};
