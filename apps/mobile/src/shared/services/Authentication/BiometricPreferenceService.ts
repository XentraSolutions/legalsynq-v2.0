import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { StorageService } from '@/shared/services/Storage';

import type { BiometricPreference } from './biometricTypes';

function isBiometricPreference(value: unknown): value is BiometricPreference {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<BiometricPreference>;
  return (
    candidate.enabled === true &&
    typeof candidate.accountLabel === 'string' &&
    typeof candidate.deviceSessionId === 'string' &&
    typeof candidate.tenantId === 'string' &&
    typeof candidate.userId === 'string' &&
    Boolean(candidate.user) &&
    candidate.user?.id === candidate.userId
  );
}

export const BiometricPreferenceService = {
  async get(): Promise<BiometricPreference | null> {
    const serialized = await StorageService.getItem(STORAGE_KEYS.BIOMETRICS_ENABLED);
    if (!serialized) return null;

    try {
      const preference: unknown = JSON.parse(serialized);
      if (isBiometricPreference(preference)) return preference;
    } catch {
      // Invalid or legacy values are removed below.
    }

    await BiometricPreferenceService.clear();
    return null;
  },

  async set(preference: BiometricPreference): Promise<void> {
    await StorageService.setItem(STORAGE_KEYS.BIOMETRICS_ENABLED, JSON.stringify(preference));
  },

  async clear(): Promise<void> {
    await StorageService.removeItem(STORAGE_KEYS.BIOMETRICS_ENABLED);
  },
};
