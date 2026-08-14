import { getDefaultStore } from 'jotai';

import { API_MODE_STORAGE_KEY, DEFAULT_API_MODE, type ApiMode } from '@/shared/constants/apiMode';
import { queryClient } from '@/shared/providers/QueryProvider';
import { AuthenticationService } from '@/shared/services/Authentication';
import { ConfigService } from '@/shared/services/Config';
import { LegacyPsaService } from '@/shared/services/LegacyPsa';
import { StorageService } from '@/shared/services/Storage';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';
import { toastAtom } from '@/shared/state/atoms/toastAtom';

const store = getDefaultStore();

function normalizeMode(value: string | null): ApiMode {
  if (ConfigService.isProduction()) {
    return 'current';
  }

  return value === 'legacy' ? 'legacy' : DEFAULT_API_MODE;
}

function modeLabel(mode: ApiMode): string {
  return mode === 'legacy' ? 'Legacy' : 'Current';
}

export const ApiModeService = {
  async getMode(): Promise<ApiMode> {
    const value = await StorageService.getItem(API_MODE_STORAGE_KEY);
    return normalizeMode(value);
  },

  async setMode(mode: ApiMode): Promise<void> {
    await StorageService.setItem(API_MODE_STORAGE_KEY, normalizeMode(mode));
  },

  async switchMode(nextMode: ApiMode): Promise<void> {
    nextMode = normalizeMode(nextMode);
    const currentMode = store.get(apiModeAtom);
    if (currentMode === nextMode) {
      return;
    }

    await AuthenticationService.clearSession();
    queryClient.clear();
    await ApiModeService.setMode(nextMode);
    store.set(apiModeAtom, nextMode);

    if (nextMode === 'legacy') {
      try {
        await LegacyPsaService.refreshToken();
      } catch {
        // Best-effort: callCaseService() will fetch/retry lazily when actually needed.
      }
    } else {
      LegacyPsaService.clearToken();
    }

    store.set(toastAtom, {
      visible: true,
      message: `Switched to ${modeLabel(nextMode)} mode. Please sign in again.`,
      type: 'info',
    });
  },
};
