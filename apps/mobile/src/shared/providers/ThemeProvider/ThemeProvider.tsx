import type { ReactNode } from 'react';
import { useEffect } from 'react';
import { useAtomValue } from 'jotai';
import { colorScheme } from 'nativewind';

import { themeAtom } from '@/shared/state/atoms/themeAtom';

export interface ThemeProviderProps {
  children: ReactNode;
}

export function ThemeProvider({ children }: ThemeProviderProps) {
  const preferredTheme = useAtomValue(themeAtom);

  useEffect(() => {
    colorScheme.set(preferredTheme);
  }, [preferredTheme]);

  return children;
}
