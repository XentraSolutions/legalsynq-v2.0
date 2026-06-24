import { atom } from 'jotai';

import type { ThemePreference } from '@/shared/types/common';

export const themeAtom = atom<ThemePreference>('system');
