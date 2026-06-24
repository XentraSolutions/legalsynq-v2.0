import type { ReactNode } from 'react';

import { ErrorBoundary } from '@/shared/components/ErrorBoundary';

export interface ErrorBoundaryProviderProps {
  children: ReactNode;
}

export function ErrorBoundaryProvider({ children }: ErrorBoundaryProviderProps) {
  return <ErrorBoundary>{children}</ErrorBoundary>;
}
