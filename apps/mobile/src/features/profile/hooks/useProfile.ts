import { useAtomValue } from 'jotai';

import { DEMO_USER } from '@/features/mockData';
import { authAtom } from '@/shared/state/atoms/authAtom';

export function useProfile() {
  const auth = useAtomValue(authAtom);

  return {
    user: auth.user ?? DEMO_USER,
  };
}
