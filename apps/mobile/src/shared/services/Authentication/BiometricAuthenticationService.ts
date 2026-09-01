import { ANALYTICS_EVENTS } from '@/shared/constants/analyticsEvents';
import { AnalyticsService } from '@/shared/services/Analytics';
import { DeviceSecurityService, type BiometricLabel } from '@/shared/services/DeviceSecurity';
import type { ApiError } from '@/shared/types/api';

import { BiometricCredentialService } from './BiometricCredentialService';
import { BiometricPreferenceService } from './BiometricPreferenceService';
import { unavailableBiometricSessionClient } from './BiometricSessionClient';
import {
  BiometricAuthenticationCancelledError,
  BiometricCredentialInvalidError,
  BiometricSessionUnavailableError,
  type BiometricEnrollmentCredentials,
  type BiometricRefreshResult,
  type BiometricSessionClient,
  type BiometricStatus,
} from './biometricTypes';

const INVALID_SESSION_CODES = new Set([
  'REFRESH_TOKEN_INVALID',
  'REFRESH_TOKEN_EXPIRED',
  'REFRESH_TOKEN_REVOKED',
  'REFRESH_TOKEN_REUSED',
  'DEVICE_SESSION_REVOKED',
  'DEVICE_SESSION_NOT_FOUND',
  'ACCOUNT_DISABLED',
  'ACCOUNT_LOCKED',
]);

let sessionClient: BiometricSessionClient = unavailableBiometricSessionClient;
let pendingEnrollment: BiometricEnrollmentCredentials | null = null;
let refreshPromise: Promise<BiometricRefreshResult> | null = null;

async function clearStoredEnrollment(): Promise<void> {
  await Promise.all([BiometricCredentialService.remove(), BiometricPreferenceService.clear()]);
}

async function clearLocalEnrollment(): Promise<void> {
  pendingEnrollment = null;
  await clearStoredEnrollment();
}

function isAuthenticationCancellation(error: unknown): boolean {
  if (!(error instanceof Error)) return false;
  const message = error.message.toLowerCase();
  return message.includes('cancel') || message.includes('user interaction is not allowed');
}

function statusReason(
  backendAvailable: boolean,
  hasHardware: boolean,
  isEnrolled: boolean,
  enabled: boolean
): BiometricStatus['reason'] {
  if (!backendAvailable) return 'backend_unavailable';
  if (!hasHardware) return 'no_hardware';
  if (!isEnrolled) return 'not_enrolled';
  if (!enabled) return 'not_enabled';
  return undefined;
}

export const BiometricAuthenticationService = {
  configureSessionClient(client: BiometricSessionClient): void {
    sessionClient = client;
  },

  resetSessionClient(): void {
    sessionClient = unavailableBiometricSessionClient;
  },

  isBackendAvailable(): boolean {
    return sessionClient.isAvailable();
  },

  async getStatus(): Promise<BiometricStatus> {
    const [capability, preference] = await Promise.all([
      DeviceSecurityService.getBiometricCapability(),
      BiometricPreferenceService.get(),
    ]);
    const backendAvailable = sessionClient.isAvailable();
    const enabled = Boolean(preference);

    return {
      available: backendAvailable && capability.canUseBiometrics && enabled,
      backendAvailable,
      capability,
      enabled,
      preference,
      reason: statusReason(
        backendAvailable,
        capability.hasHardware,
        capability.isEnrolled,
        enabled
      ),
    };
  },

  async prepareEnrollment(
    credentials?: BiometricEnrollmentCredentials
  ): Promise<{ label: BiometricLabel; shouldOffer: boolean }> {
    pendingEnrollment = credentials ?? null;
    if (!credentials || !sessionClient.isAvailable()) {
      return { label: 'Biometrics', shouldOffer: false };
    }

    const [capability, preference] = await Promise.all([
      DeviceSecurityService.getBiometricCapability(),
      BiometricPreferenceService.get(),
    ]);
    const alreadyEnabled =
      preference?.userId === credentials.user.id && preference.tenantId === credentials.tenantId;
    if (preference && !alreadyEnabled) {
      await clearStoredEnrollment();
    }

    AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_OFFERED, {
      eligible: capability.canUseBiometrics && !alreadyEnabled,
    });

    return {
      label: capability.label,
      shouldOffer: capability.canUseBiometrics && !alreadyEnabled,
    };
  },

  hasPendingEnrollment(): boolean {
    return Boolean(pendingEnrollment);
  },

  discardPendingEnrollment(): void {
    pendingEnrollment = null;
  },

  async enable(): Promise<void> {
    const credentials = pendingEnrollment;
    if (!credentials || !sessionClient.isAvailable()) {
      throw new BiometricSessionUnavailableError();
    }

    AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_ACCEPTED);
    const authentication = await DeviceSecurityService.authenticate(
      'Authenticate to enable biometric login'
    );
    if (authentication.status === 'cancelled') {
      AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_CANCELLED);
      throw new BiometricAuthenticationCancelledError();
    }
    if (authentication.status !== 'success') {
      AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_FAILED, {
        reason: authentication.status,
      });
      throw new Error(authentication.message ?? 'Unable to verify your identity.');
    }

    let remoteEnabled = false;
    try {
      await BiometricCredentialService.save(credentials.refreshToken);
      await sessionClient.enableBiometrics(credentials.deviceSessionId);
      remoteEnabled = true;
      await BiometricPreferenceService.set({
        enabled: true,
        accountLabel: credentials.accountLabel,
        deviceSessionId: credentials.deviceSessionId,
        tenantId: credentials.tenantId,
        userId: credentials.user.id,
        user: credentials.user,
      });
      AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_COMPLETED);
    } catch (error) {
      if (remoteEnabled) {
        try {
          await sessionClient.disableBiometrics(credentials.deviceSessionId);
        } catch {
          // Best-effort rollback; local cleanup below still prevents use on this device.
        }
      }
      await clearLocalEnrollment();
      AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_ENROLLMENT_FAILED);
      throw error;
    }
  },

  async login(): Promise<BiometricRefreshResult> {
    if (refreshPromise) return refreshPromise;

    refreshPromise = (async () => {
      const status = await BiometricAuthenticationService.getStatus();
      if (!status.available || !status.preference) {
        throw new BiometricCredentialInvalidError();
      }

      AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_INITIATED);
      let refreshToken: string | null;
      try {
        refreshToken = await BiometricCredentialService.get();
      } catch (error) {
        if (isAuthenticationCancellation(error)) {
          AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_CANCELLED);
          throw new BiometricAuthenticationCancelledError();
        }
        AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_FAILED, {
          code: 'SECURE_STORAGE_ERROR',
        });
        throw error;
      }
      if (!refreshToken) {
        await clearLocalEnrollment();
        AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_CREDENTIAL_INVALIDATED);
        throw new BiometricCredentialInvalidError(
          'Your device security settings have changed. Please sign in again to re-enable biometric login.'
        );
      }

      try {
        const result = await sessionClient.refreshSession({
          deviceSessionId: status.preference.deviceSessionId,
          refreshToken,
        });
        try {
          await BiometricCredentialService.rotate(result.refreshToken);
        } catch {
          await clearLocalEnrollment();
          AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_FAILED, {
            code: 'ROTATED_TOKEN_STORAGE_FAILED',
          });
          throw new BiometricCredentialInvalidError(
            'Your saved login could not be updated. Please sign in again to re-enable biometric login.'
          );
        }
        AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_SUCCEEDED);
        return { ...result, user: status.preference.user };
      } catch (error) {
        if (error instanceof BiometricCredentialInvalidError) {
          throw error;
        }
        const code = (error as ApiError | undefined)?.code;
        if (code && INVALID_SESSION_CODES.has(code)) {
          await clearLocalEnrollment();
          AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_CREDENTIAL_INVALIDATED, {
            code,
          });
          throw new BiometricCredentialInvalidError();
        }

        AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_FAILED, {
          code: code ?? 'UNKNOWN',
        });
        throw error;
      }
    })().finally(() => {
      refreshPromise = null;
    });

    return refreshPromise;
  },

  async disable(): Promise<void> {
    const preference = await BiometricPreferenceService.get();
    if (preference && sessionClient.isAvailable()) {
      await sessionClient.disableBiometrics(preference.deviceSessionId);
    } else if (preference) {
      throw new BiometricSessionUnavailableError();
    }

    await clearLocalEnrollment();
    AnalyticsService.track(ANALYTICS_EVENTS.BIOMETRIC_LOGIN_DISABLED);
  },

  async clearLocalEnrollment(): Promise<void> {
    await clearLocalEnrollment();
  },
};
