import * as SecureStore from 'expo-secure-store';

import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { SecureStorageService } from '@/shared/services/SecureStorage';

const PROTECTED_OPTIONS: SecureStore.SecureStoreOptions = {
  authenticationPrompt: 'Authenticate to access your saved LegalSynq login.',
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  requireAuthentication: true,
};

export const BiometricCredentialService = {
  async save(refreshToken: string): Promise<void> {
    await SecureStorageService.setItem(
      STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN,
      refreshToken,
      PROTECTED_OPTIONS
    );
  },

  async get(): Promise<string | null> {
    return SecureStorageService.getItem(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN, PROTECTED_OPTIONS);
  },

  async rotate(refreshToken: string): Promise<void> {
    // Updating an existing authenticated Keychain item triggers another Face ID
    // prompt on iOS. Recreate it instead so the read remains the only prompt in
    // a biometric login attempt while the rotated token stays biometric-bound.
    await SecureStorageService.deleteItem(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN);
    await SecureStorageService.setItem(
      STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN,
      refreshToken,
      PROTECTED_OPTIONS
    );
  },

  async remove(): Promise<void> {
    await Promise.all([
      SecureStorageService.deleteItem(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN),
      SecureStorageService.deleteItem(STORAGE_KEYS.BIOMETRICS_ENABLED),
    ]);
  },
};
