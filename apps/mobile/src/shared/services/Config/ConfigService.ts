import { DEFAULT_FEATURE_FLAGS } from '@/shared/constants/featureFlags';
import type { Environment, FeatureFlags } from '@/shared/types/common';

const DEFAULT_API_URL = 'https://core-qa.legalsynq.net';
const DEFAULT_LEGACY_API_URL = 'https://synqlien-core.legalsynq.com';
const DEFAULT_LEGACY_API_KEY = 'V2D6MPPWD7Z5NPCT';

export const ConfigService = {
  getApiBaseUrl(): string {
    return process.env.EXPO_PUBLIC_API_URL ?? DEFAULT_API_URL;
  },

  getLegacyApiBaseUrl(): string {
    return process.env.EXPO_PUBLIC_LEGACY_API_URL ?? DEFAULT_LEGACY_API_URL;
  },

  getLegacyApiKey(): string {
    return process.env.EXPO_PUBLIC_LEGACY_API_KEY ?? DEFAULT_LEGACY_API_KEY;
  },

  getEnvironment(): Environment {
    const value = process.env.EXPO_PUBLIC_APP_ENV;
    if (value === 'qa' || value === 'production') {
      return value;
    }

    return 'development';
  },

  getDeepLinkHost(): string | null {
    return process.env.EXPO_PUBLIC_DEEP_LINK_HOST?.trim() || null;
  },

  isProduction(): boolean {
    return ConfigService.getEnvironment() === 'production';
  },

  getFeatureFlagDefaults(): FeatureFlags {
    return DEFAULT_FEATURE_FLAGS;
  },
};
