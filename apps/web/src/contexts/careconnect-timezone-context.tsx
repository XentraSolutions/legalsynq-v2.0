'use client';

import { createContext, useContext, useState, type ReactNode } from 'react';

const CareConnectTimezoneContext = createContext<string>('UTC');

/**
 * Detects and provides the browser's local IANA timezone to CareConnect
 * common-portal and public-portal pages.
 *
 * Detection uses a lazy useState initializer — runs once on the client, never
 * causes a hydration mismatch (server renders UTC, client rehydrates with the
 * detected value on first mount).
 */
export function CareConnectTimezoneProvider({ children }: { children: ReactNode }) {
  const [timezone] = useState<string>(() => {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
    } catch {
      return 'UTC';
    }
  });

  return (
    <CareConnectTimezoneContext.Provider value={timezone}>
      {children}
    </CareConnectTimezoneContext.Provider>
  );
}

export function useCareConnectTimezone(): string {
  return useContext(CareConnectTimezoneContext);
}
