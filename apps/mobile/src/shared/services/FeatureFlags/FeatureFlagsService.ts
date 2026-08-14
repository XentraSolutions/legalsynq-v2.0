import { getDefaultStore } from 'jotai';

import { DEFAULT_FEATURE_FLAGS } from '@/shared/constants/featureFlags';
import { featureFlagsAtom } from '@/shared/state/atoms/featureFlagsAtom';
import type { FeatureFlags } from '@/shared/types/common';

const store = getDefaultStore();

export const FeatureFlagsService = {
  getDefaults(): FeatureFlags {
    return DEFAULT_FEATURE_FLAGS;
  },

  setFlags(flags: Partial<FeatureFlags>): void {
    store.set(featureFlagsAtom, {
      ...store.get(featureFlagsAtom),
      ...flags,
    });
  },
};
