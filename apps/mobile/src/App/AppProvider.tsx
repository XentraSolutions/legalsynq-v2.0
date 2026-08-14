import type { ReactNode } from 'react';

import { ErrorBoundaryProvider } from '@/shared/providers/ErrorBoundaryProvider';
import { QueryProvider } from '@/shared/providers/QueryProvider';
import { ThemeProvider } from '@/shared/providers/ThemeProvider';
import { ToastProvider } from '@/shared/providers/ToastProvider';

export interface AppProviderProps {
  children: ReactNode;
}

export function AppProvider({ children }: AppProviderProps) {
  return (
    <ErrorBoundaryProvider>
      <QueryProvider>
        <ThemeProvider>
          <ToastProvider>{children}</ToastProvider>
        </ThemeProvider>
      </QueryProvider>
    </ErrorBoundaryProvider>
  );
}
