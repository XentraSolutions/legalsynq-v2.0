import { atom } from 'jotai';

import { DEFAULT_API_MODE } from '@/shared/constants/apiMode';
import type { ApiMode } from '@/shared/constants/apiMode';

export const apiModeAtom = atom<ApiMode>(DEFAULT_API_MODE);

export const apiModeHydratedAtom = atom(false);
