import { DEFAULT_FEATURE_FLAGS } from '@/shared/constants/featureFlags';
import type { Environment, FeatureFlags } from '@/shared/types/common';

const DEFAULT_API_URL = 'https://core-qa.legalsynq.net';

function readEnv(name: string): string | undefined {
  return process.env[name];
}

export const ConfigService = {
  getApiBaseUrl(): string {
    return readEnv('EXPO_PUBLIC_API_URL') ?? DEFAULT_API_URL;
  },

  getEnvironment(): Environment {
    const value = readEnv('EXPO_PUBLIC_APP_ENV');
    if (value === 'qa' || value === 'production') {
      return value;
    }

    return 'development';
  },

  getFeatureFlagDefaults(): FeatureFlags {
    return DEFAULT_FEATURE_FLAGS;
  },
};
