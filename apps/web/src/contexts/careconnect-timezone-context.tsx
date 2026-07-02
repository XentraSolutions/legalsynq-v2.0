'use client';

import { createContext, useContext, type ReactNode } from 'react';
import { useBrowserTimezone } from '@/lib/use-timezone';

const CareConnectTimezoneContext = createContext<string>('UTC');

/**
 * Provides a browser-local IANA timezone to CareConnect common/public portal
 * pages. The initial render uses 'UTC' to keep SSR and hydration deterministic,
 * then updates after mount to the browser timezone when available.
 */
export function CareConnectTimezoneProvider({ children }: { children: ReactNode }) {
  const timezone = useBrowserTimezone();

  return (
    <CareConnectTimezoneContext.Provider value={timezone}>
      {children}
    </CareConnectTimezoneContext.Provider>
  );
}

export function useCareConnectTimezone(): string {
  return useContext(CareConnectTimezoneContext);
}
