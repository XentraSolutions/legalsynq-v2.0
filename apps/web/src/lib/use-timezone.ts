'use client';

import { useEffect, useState } from 'react';
import { useSettings } from '@/contexts/settings-context';

const DEFAULT_TIMEZONE = 'UTC';
const TIMEZONE_REFRESH_INTERVAL_MS = 60_000;

function resolveBrowserTimezone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIMEZONE;
  } catch {
    return DEFAULT_TIMEZONE;
  }
}

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
  const [tz, setTz] = useState<string>(DEFAULT_TIMEZONE);

  useEffect(() => {
    const updateTimezone = () => {
      setTz((current) => {
        const next = resolveBrowserTimezone();
        return current === next ? current : next;
      });
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        updateTimezone();
      }
    };

    updateTimezone();
    window.addEventListener('focus', updateTimezone);
    window.addEventListener('pageshow', updateTimezone);
    document.addEventListener('visibilitychange', handleVisibilityChange);
    const intervalId = window.setInterval(updateTimezone, TIMEZONE_REFRESH_INTERVAL_MS);

    return () => {
      window.removeEventListener('focus', updateTimezone);
      window.removeEventListener('pageshow', updateTimezone);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.clearInterval(intervalId);
    };
  }, []);

  return tz;
}
