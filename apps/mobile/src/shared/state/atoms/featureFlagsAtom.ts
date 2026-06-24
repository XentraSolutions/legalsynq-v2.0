import { atom } from 'jotai';

import { DEFAULT_FEATURE_FLAGS } from '@/shared/constants/featureFlags';
import type { FeatureFlags } from '@/shared/types/common';

export const featureFlagsAtom = atom<FeatureFlags>(DEFAULT_FEATURE_FLAGS);
