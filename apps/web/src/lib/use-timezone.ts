'use client';

import { useState } from 'react';
import { useSettings } from '@/contexts/settings-context';

/**
 * Returns the configured IANA timezone for the current tenant.
 * Use in authenticated (platform) routes where SettingsProvider is available.
 */
export function useTimezone(): string {
  return useSettings().timezone;
}

/**
 * Returns the browser's local IANA timezone, detected once on mount.
 * Falls back to 'UTC' if the Intl API is unavailable.
 *
 * Use in unauthenticated or external-portal routes (CareConnect common portal,
 * public network pages) where no tenant context is available.
 *
 * Initialized lazily via useState initializer so it runs only once and never
 * causes a hydration mismatch — the value is stable after the first render.
 */
export function useBrowserTimezone(): string {
  const [tz] = useState<string>(() => {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
    } catch {
      return 'UTC';
    }
  });
  return tz;
}
