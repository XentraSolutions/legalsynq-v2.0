import { useAtomValue } from 'jotai';

import { authAtom } from '@/shared/state/atoms/authAtom';

export function useAuth() {
  return useAtomValue(authAtom);
}
