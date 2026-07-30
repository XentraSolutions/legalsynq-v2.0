import * as SecureStore from 'expo-secure-store';

import { STORAGE_KEYS, type StorageKey } from '@/shared/constants/storageKeys';

export const SecureStorageService = {
  async setItem(
    key: StorageKey,
    value: string,
    options?: SecureStore.SecureStoreOptions
  ): Promise<void> {
    await SecureStore.setItemAsync(key, value, options);
  },

  async getItem(key: StorageKey, options?: SecureStore.SecureStoreOptions): Promise<string | null> {
    return SecureStore.getItemAsync(key, options);
  },

  async deleteItem(key: StorageKey): Promise<void> {
    await SecureStore.deleteItemAsync(key);
  },

  async clearAll(): Promise<void> {
    await Promise.all([
      SecureStore.deleteItemAsync(STORAGE_KEYS.ACCESS_TOKEN),
      SecureStore.deleteItemAsync(STORAGE_KEYS.LEGACY_ACCESS_TOKEN),
      SecureStore.deleteItemAsync(STORAGE_KEYS.BIOMETRICS_ENABLED),
      SecureStore.deleteItemAsync(STORAGE_KEYS.BIOMETRIC_REFRESH_TOKEN),
    ]);
  },
};
