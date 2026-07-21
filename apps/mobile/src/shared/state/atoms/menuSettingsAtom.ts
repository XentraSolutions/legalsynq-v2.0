import { atom } from 'jotai';

import {
  DEFAULT_MENU_VISIBILITY,
  type MenuVisibilitySettings,
} from '@/shared/constants/menuSettings';

export const menuVisibilityAtom = atom<MenuVisibilitySettings>(DEFAULT_MENU_VISIBILITY);
export const menuVisibilityHydratedAtom = atom(false);
