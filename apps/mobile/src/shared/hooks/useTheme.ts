import { useAtom } from 'jotai';

import { themeAtom } from '@/shared/state/atoms/themeAtom';

export function useTheme() {
  const [theme, setTheme] = useAtom(themeAtom);

  return {
    theme,
    setTheme,
  };
}
