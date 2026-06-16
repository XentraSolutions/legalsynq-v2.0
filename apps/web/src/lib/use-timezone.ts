'use client';

import { useEffect, useState } from 'react';
import { useSettings } from '@/contexts/settings-context';

/**
 * Returns the configured IANA timezone for the current tenant.
 * Use in authenticated (platform) routes where SettingsProvider is available.
 */
export function useTimezone(): string {
  return useSettings().timezone;
}

/**
 * Returns a browser-local IANA timezone for client-only/external portal UI.
 * The initial render uses 'UTC' so SSR and hydration stay deterministic,
 * then updates after mount to the browser timezone when available.
 *
 * Use in unauthenticated or external-portal routes (CareConnect common portal,
 * public network pages) where no tenant context is available.
 */
export function useBrowserTimezone(): string {
  const [tz, setTz] = useState<string>('UTC');

  useEffect(() => {
    try {
      setTz(Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC');
    } catch {
      setTz('UTC');
    }
  }, []);

  return tz;
}
