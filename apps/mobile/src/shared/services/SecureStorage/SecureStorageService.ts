import * as SecureStore from 'expo-secure-store';

import { STORAGE_KEYS, type StorageKey } from '@/shared/constants/storageKeys';

export const SecureStorageService = {
  async setItem(key: StorageKey, value: string): Promise<void> {
    await SecureStore.setItemAsync(key, value);
  },

  async getItem(key: StorageKey): Promise<string | null> {
    return SecureStore.getItemAsync(key);
  },

  async deleteItem(key: StorageKey): Promise<void> {
    await SecureStore.deleteItemAsync(key);
  },

  async clearAll(): Promise<void> {
    await Promise.all([
      SecureStore.deleteItemAsync(STORAGE_KEYS.ACCESS_TOKEN),
      SecureStore.deleteItemAsync(STORAGE_KEYS.USER_SESSION),
      SecureStore.deleteItemAsync(STORAGE_KEYS.BIOMETRICS_ENABLED),
    ]);
  },
};
