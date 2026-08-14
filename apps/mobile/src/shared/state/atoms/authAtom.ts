import { atom } from 'jotai';

import type { AuthState } from '@/shared/types/auth';

export const authAtom = atom<AuthState>({
  user: null,
  token: null,
  isAuthenticated: false,
});
