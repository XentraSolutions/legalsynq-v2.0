'use client';

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

const CareConnectTimezoneContext = createContext<string>('UTC');

/**
 * Provides a browser-local IANA timezone to CareConnect common/public portal
 * pages. The initial render uses 'UTC' to keep SSR and hydration deterministic,
 * then updates after mount to the browser timezone when available.
 */
export function CareConnectTimezoneProvider({ children }: { children: ReactNode }) {
  const [timezone, setTimezone] = useState<string>('UTC');

  useEffect(() => {
    try {
      setTimezone(Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC');
    } catch {
      setTimezone('UTC');
    }
  }, []);

  return (
    <CareConnectTimezoneContext.Provider value={timezone}>
      {children}
    </CareConnectTimezoneContext.Provider>
  );
}

export function useCareConnectTimezone(): string {
  return useContext(CareConnectTimezoneContext);
}
